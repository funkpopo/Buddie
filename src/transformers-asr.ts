import { pipeline, AutomaticSpeechRecognitionPipeline, env } from '@xenova/transformers';

// 获取模型根目录
async function getModelsRootDir(): Promise<string> {
  // 检查是否在Electron渲染进程中
  if (typeof window !== 'undefined' && window.electronAPI) {
    try {
      return await window.electronAPI.system.getModelsRootDir();
    } catch (error) {
      console.warn('Failed to get models root dir from main process:', error);
      return './models';
    }
  } else if (typeof process !== 'undefined' && process.versions && process.versions.electron) {
    // 在Electron主进程或Node.js环境中
    const path = require('path');
    const { app } = require('electron');
    
    if (process.env.NODE_ENV === 'development') {
      // 开发环境：项目根目录下的models文件夹
      return path.join(process.cwd(), 'models');
    } else {
      // 生产环境：exe同级的models文件夹
      const exePath = app.getPath('exe');
      const exeDir = path.dirname(exePath);
      return path.join(exeDir, 'models');
    }
  } else {
    // 浏览器环境，使用相对路径
    return './models';
  }
}

// 获取特定模型的目录
function getModelDir(modelsRootDir: string, modelName: string): string {
  const path = require('path');
  return path.join(modelsRootDir, modelName);
}

// 获取特定模型的ONNX目录
function getModelONNXDir(modelsRootDir: string, modelName: string): string {
  const path = require('path');
  return path.join(modelsRootDir, modelName, 'onnx');
}

// Browser-compatible EventEmitter
class EventEmitter {
  private events: Record<string, Function[]> = {};

  on(event: string, listener: Function): this {
    if (!this.events[event]) {
      this.events[event] = [];
    }
    this.events[event].push(listener);
    return this;
  }

  off(event: string, listener: Function): this {
    if (!this.events[event]) {
      return this;
    }
    const index = this.events[event].indexOf(listener);
    if (index > -1) {
      this.events[event].splice(index, 1);
    }
    return this;
  }

  emit(event: string, ...args: any[]): boolean {
    if (!this.events[event]) {
      return false;
    }
    this.events[event].forEach(listener => {
      listener.apply(this, args);
    });
    return true;
  }

  removeAllListeners(event?: string): this {
    if (event) {
      delete this.events[event];
    } else {
      this.events = {};
    }
    return this;
  }
}

export interface TransformersASRResult {
  text: string;
  confidence?: number;
  isFinal: boolean;
  timestamp?: number;
}

export interface TransformersASROptions {
  model?: string;
  language?: string;
  chunk_length_s?: number;
  stride_length_s?: number;
  return_timestamps?: boolean;
}

export interface ModelInfo {
  id: string;
  name: string;
  size: string;
  languages: string[];
  description: string;
  downloaded: boolean;
  downloading: boolean;
  downloadProgress: number;
}

export interface ModelDownloadProgress {
  modelId: string;
  progress: number;
  status: 'downloading' | 'completed' | 'error';
  error?: string;
}

export class TransformersASR extends EventEmitter {
  private pipeline: AutomaticSpeechRecognitionPipeline | null = null;
  private isInitialized = false;
  private isListening = false;
  private audioContext: AudioContext | null = null;
  private mediaStream: MediaStream | null = null;
  private processor: ScriptProcessorNode | null = null;
  private audioBuffer: Float32Array[] = [];
  private options: TransformersASROptions;
  private bufferDuration = 2; // 2秒缓冲
  private sampleRate = 16000;
  private modelsRootDir: string;
  
  // 可用的轻量化模型列表，优化用于中英文识别和CPU运行
  private availableModels: ModelInfo[] = [
    {
      id: 'Xenova/whisper-tiny',
      name: 'Whisper Tiny (多语言)',
      size: '~40MB',
      languages: ['中文', '英文', '其他99种语言'],
      description: '最轻量的多语言模型，支持中英文识别，CPU运行流畅',
      downloaded: false,
      downloading: false,
      downloadProgress: 0
    },
    {
      id: 'Xenova/whisper-tiny.en',
      name: 'Whisper Tiny English',
      size: '~40MB',
      languages: ['英文'],
      description: '英文专用轻量模型，CPU运行，英文识别准确度更高',
      downloaded: false,
      downloading: false,
      downloadProgress: 0
    },
    {
      id: 'Xenova/whisper-base',
      name: 'Whisper Base (多语言)',
      size: '~140MB',
      languages: ['中文', '英文', '其他99种语言'],
      description: '平衡的多语言模型，识别准确度较高，适合CPU运行',
      downloaded: false,
      downloading: false,
      downloadProgress: 0
    },
    {
      id: 'Xenova/whisper-base.en',
      name: 'Whisper Base English',
      size: '~140MB',
      languages: ['英文'],
      description: '英文专用平衡模型，CPU运行，英文识别准确度很高',
      downloaded: false,
      downloading: false,
      downloadProgress: 0
    }
  ];
  
  private currentModelId: string = 'Xenova/whisper-tiny';
  
  constructor(options: TransformersASROptions = {}) {
    super();
    this.options = {
      model: 'Xenova/whisper-tiny',
      language: 'chinese',
      chunk_length_s: 30,
      stride_length_s: 5,
      return_timestamps: false,
      ...options
    };
    this.currentModelId = this.options.model!;
    this.modelsRootDir = './models'; // 初始默认值
    this.loadModelStatus();
    this.initializeModelsRootDir(); // 异步初始化模型根目录
  }

  // 异步初始化模型根目录
  private async initializeModelsRootDir(): Promise<void> {
    try {
      this.modelsRootDir = await getModelsRootDir();
      console.log('Models root directory set to:', this.modelsRootDir);
    } catch (error) {
      console.warn('Failed to initialize models root directory:', error);
      this.modelsRootDir = './models'; // 回退到默认值
    }
  }

  async initialize(): Promise<void> {
    if (this.isInitialized) {
      return;
    }

    try {
      console.log('Initializing Transformers ASR...');
      
      // 确保模型根目录已初始化
      if (this.modelsRootDir === './models') {
        await this.initializeModelsRootDir();
      }
      
      // 在初始化前先验证模型文件状态
      console.log('ASR: 初始化前验证模型文件状态');
      await this.verifyModelFiles();
      
      // 检查当前模型是否已下载
      const currentModel = this.availableModels.find(m => m.id === this.currentModelId);
      console.log('ASR: 当前模型信息:', currentModel);
      if (!currentModel || !currentModel.downloaded) {
        throw new Error(`当前模型 ${this.currentModelId} 尚未下载，请先在设置中下载模型`);
      }
      
      // 配置环境变量
      env.allowRemoteModels = true;
      env.allowLocalModels = true;
      env.useBrowserCache = true;
      
      console.log(`Loading model: ${this.currentModelId}`);
      const modelName = this.getModelFolderName(this.currentModelId);
      const modelDir = getModelDir(this.modelsRootDir, modelName);
      console.log(`Model directory: ${modelDir}`);
      
      this.pipeline = await pipeline(
        'automatic-speech-recognition',
        this.currentModelId,
        {
          quantized: true,
          revision: 'main',
          cache_dir: modelDir,
          local_files_only: true, // 使用本地模型
          progress_callback: (progress: { status: string; progress?: number; file?: string }) => {
            if (progress.status === 'downloading' || progress.status === 'loading') {
              console.log(`${progress.status} ${progress.file || this.currentModelId}: ${progress.progress || 0}%`);
              this.emit('modelProgress', { ...progress, model: this.currentModelId });
            }
          }
        }
      );
      
      console.log(`Successfully loaded model: ${this.currentModelId}`);

      this.isInitialized = true;
      console.log('Transformers ASR initialized successfully');
      this.emit('initialized');
      
    } catch (error) {
      console.error('Failed to initialize Transformers ASR:', error);
      throw error;
    }
  }

  async startListening(): Promise<void> {
    if (this.isListening) {
      throw new Error('Already listening');
    }

    if (!this.isInitialized) {
      await this.initialize();
    }

    try {
      // 请求麦克风权限
      this.mediaStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: this.sampleRate,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true
        }
      });

      // 创建音频上下文
      this.audioContext = new AudioContext({ sampleRate: this.sampleRate });
      const source = this.audioContext.createMediaStreamSource(this.mediaStream);
      
      // 创建音频处理器
      this.processor = this.audioContext.createScriptProcessor(4096, 1, 1);
      
      // 重置音频缓冲区
      this.audioBuffer = [];
      
      // 音频处理回调
      this.processor.onaudioprocess = (event) => {
        if (this.isListening) {
          const inputData = event.inputBuffer.getChannelData(0);
          this.collectAudioData(inputData);
        }
      };

      source.connect(this.processor);
      this.processor.connect(this.audioContext.destination);
      
      this.isListening = true;
      this.emit('start');
      console.log('Transformers ASR started listening');
      
      // 定期处理累积的音频数据
      this.startPeriodicProcessing();
      
    } catch (error) {
      console.error('Error starting Transformers ASR:', error);
      throw error;
    }
  }

  async stopListening(): Promise<void> {
    if (!this.isListening) {
      return;
    }

    try {
      this.isListening = false;

      // 处理剩余的音频数据
      if (this.audioBuffer.length > 0) {
        await this.processAccumulatedAudio();
      }

      // 清理音频资源
      if (this.processor) {
        this.processor.disconnect();
        this.processor = null;
      }

      if (this.audioContext) {
        await this.audioContext.close();
        this.audioContext = null;
      }

      if (this.mediaStream) {
        this.mediaStream.getTracks().forEach(track => track.stop());
        this.mediaStream = null;
      }

      this.audioBuffer = [];
      this.emit('stop');
      console.log('Transformers ASR stopped listening');
      
    } catch (error) {
      console.error('Error stopping Transformers ASR:', error);
      throw error;
    }
  }

  private collectAudioData(audioData: Float32Array): void {
    // 添加新的音频数据到缓冲区
    this.audioBuffer.push(new Float32Array(audioData));
    
    // 检查缓冲区是否达到处理阈值
    const totalSamples = this.audioBuffer.reduce((sum, chunk) => sum + chunk.length, 0);
    const bufferDurationSec = totalSamples / this.sampleRate;
    
    if (bufferDurationSec >= this.bufferDuration) {
      this.processAccumulatedAudio();
    }
  }

  private async processAccumulatedAudio(): Promise<void> {
    if (!this.pipeline || this.audioBuffer.length === 0) {
      return;
    }

    try {
      // 合并所有音频数据
      const totalLength = this.audioBuffer.reduce((acc, chunk) => acc + chunk.length, 0);
      const combinedAudio = new Float32Array(totalLength);
      let offset = 0;

      for (const chunk of this.audioBuffer) {
        combinedAudio.set(chunk, offset);
        offset += chunk.length;
      }

      // 清空缓冲区
      this.audioBuffer = [];

      // 使用transformers.js进行语音识别
      const result = await this.pipeline(combinedAudio, {
        language: this.options.language,
        chunk_length_s: this.options.chunk_length_s,
        stride_length_s: this.options.stride_length_s,
        return_timestamps: this.options.return_timestamps
      });

      // 处理结果
      if (result) {
        // 处理可能的数组或单个对象结果
        let text = '';
        if (Array.isArray(result)) {
          // 如果是数组，取第一个元素或合并所有文本
          text = result.length > 0 ? result[0].text || '' : '';
        } else {
          // 如果是单个对象
          text = result.text || '';
        }
        
        if (text && text.trim()) {
          const asrResult: TransformersASRResult = {
            text: text.trim(),
            confidence: 0.9, // transformers.js doesn't provide confidence scores
            isFinal: true,
            timestamp: Date.now()
          };

          this.emit('result', asrResult);
          console.log('ASR Result:', asrResult.text);
        }
      }

    } catch (error) {
      console.error('Error processing accumulated audio:', error);
      this.emit('error', error);
    }
  }

  private startPeriodicProcessing(): void {
    const processInterval = setInterval(() => {
      if (!this.isListening) {
        clearInterval(processInterval);
        return;
      }

      // 每秒检查并处理音频缓冲区
      if (this.audioBuffer.length > 0) {
        const totalSamples = this.audioBuffer.reduce((sum, chunk) => sum + chunk.length, 0);
        const bufferDurationSec = totalSamples / this.sampleRate;
        
        // 如果缓冲区超过1秒，就处理一次
        if (bufferDurationSec >= 1) {
          this.processAccumulatedAudio();
        }
      }
    }, 1000);
  }

  // 测试麦克风功能 - 旧版本
  async testMicrophone(): Promise<{ success: boolean; error?: string; audioLevel?: number }> {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: this.sampleRate,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true
        }
      });

      const audioContext = new AudioContext({ sampleRate: this.sampleRate });
      const source = audioContext.createMediaStreamSource(stream);
      const analyser = audioContext.createAnalyser();
      
      analyser.fftSize = 256;
      const bufferLength = analyser.frequencyBinCount;
      const dataArray = new Uint8Array(bufferLength);
      
      source.connect(analyser);
      
      return new Promise((resolve) => {
        let maxLevel = 0;
        let sampleCount = 0;
        const maxSamples = 10;
        
        const checkLevel = () => {
          analyser.getByteFrequencyData(dataArray);
          const average = dataArray.reduce((a, b) => a + b) / bufferLength;
          maxLevel = Math.max(maxLevel, average);
          sampleCount++;
          
          if (sampleCount >= maxSamples) {
            // 清理资源
            source.disconnect();
            audioContext.close();
            stream.getTracks().forEach(track => track.stop());
            
            resolve({
              success: true,
              audioLevel: maxLevel
            });
          } else {
            requestAnimationFrame(checkLevel);
          }
        };
        
        checkLevel();
      });
      
    } catch (error) {
      return {
        success: false,
        error: (error as Error).message
      };
    }
  }

  // 手动麦克风测试相关属性
  private micTestStream: MediaStream | null = null;
  private micTestContext: AudioContext | null = null;
  private micTestAnalyser: AnalyserNode | null = null;
  private micTestSource: MediaStreamAudioSourceNode | null = null;
  private micTestAnimationId: number | null = null;
  private isMicTesting = false;

  // 开始手动麦克风测试
  async startMicrophoneTest(): Promise<{ success: boolean; error?: string }> {
    if (this.isMicTesting) {
      return { success: false, error: '已经在进行麦克风测试' };
    }

    try {
      this.micTestStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: this.sampleRate,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true
        }
      });

      this.micTestContext = new AudioContext({ sampleRate: this.sampleRate });
      this.micTestSource = this.micTestContext.createMediaStreamSource(this.micTestStream);
      this.micTestAnalyser = this.micTestContext.createAnalyser();
      
      this.micTestAnalyser.fftSize = 256;
      this.micTestSource.connect(this.micTestAnalyser);
      
      this.isMicTesting = true;
      this.startMicTestLoop();

      return { success: true };
    } catch (error) {
      this.cleanupMicTest();
      return {
        success: false,
        error: (error as Error).message
      };
    }
  }

  // 停止手动麦克风测试
  async stopMicrophoneTest(): Promise<{ success: boolean; error?: string }> {
    if (!this.isMicTesting) {
      return { success: false, error: '当前没有进行麦克风测试' };
    }

    try {
      this.cleanupMicTest();
      return { success: true };
    } catch (error) {
      return {
        success: false,
        error: (error as Error).message
      };
    }
  }

  // 清理麦克风测试资源
  private cleanupMicTest(): void {
    this.isMicTesting = false;

    if (this.micTestAnimationId) {
      cancelAnimationFrame(this.micTestAnimationId);
      this.micTestAnimationId = null;
    }

    if (this.micTestSource) {
      this.micTestSource.disconnect();
      this.micTestSource = null;
    }

    if (this.micTestContext) {
      this.micTestContext.close();
      this.micTestContext = null;
    }

    if (this.micTestStream) {
      this.micTestStream.getTracks().forEach(track => track.stop());
      this.micTestStream = null;
    }

    this.micTestAnalyser = null;
    this.emit('micTestStopped');
  }

  // 麦克风测试循环
  private startMicTestLoop(): void {
    if (!this.micTestAnalyser || !this.isMicTesting) {
      return;
    }

    const bufferLength = this.micTestAnalyser.frequencyBinCount;
    const dataArray = new Uint8Array(bufferLength);

    const updateLevel = () => {
      if (!this.isMicTesting || !this.micTestAnalyser) {
        return;
      }

      this.micTestAnalyser.getByteFrequencyData(dataArray);
      const average = dataArray.reduce((a, b) => a + b) / bufferLength;
      const decibels = 20 * Math.log10(average / 255 + 0.001); // 转换为分贝，避免log(0)
      const normalizedLevel = Math.max(0, Math.min(100, (decibels + 60) * 100 / 60)); // 将-60dB到0dB映射到0-100

      this.emit('micTestLevel', { level: normalizedLevel, decibels });

      this.micTestAnimationId = requestAnimationFrame(updateLevel);
    };

    updateLevel();
  }

  // 获取当前是否在进行麦克风测试
  get microphoneTesting(): boolean {
    return this.isMicTesting;
  }

  // 获取模型文件夹名称
  private getModelFolderName(modelId: string): string {
    const modelNameMap: { [key: string]: string } = {
      'Xenova/whisper-tiny': 'whisper-tiny',
      'Xenova/whisper-tiny.en': 'whisper-tiny-en',
      'Xenova/whisper-base': 'whisper-base',
      'Xenova/whisper-base.en': 'whisper-base-en'
    };
    return modelNameMap[modelId] || modelId.replace(/\//g, '-');
  }

  // 获取模型所需的配置文件列表
  private getModelConfigFiles(modelId: string): Array<{url: string, filename: string}> {
    const baseUrl = `https://huggingface.co/${modelId}/resolve/main`;
    const configFiles = [
      { url: `${baseUrl}/added_tokens.json`, filename: 'added_tokens.json' },
      { url: `${baseUrl}/config.json`, filename: 'config.json' },
      { url: `${baseUrl}/generation_config.json`, filename: 'generation_config.json' },
      { url: `${baseUrl}/merges.txt`, filename: 'merges.txt' },
      { url: `${baseUrl}/normalizer.json`, filename: 'normalizer.json' },
      { url: `${baseUrl}/preprocessor_config.json`, filename: 'preprocessor_config.json' },
      { url: `${baseUrl}/quant_config.json`, filename: 'quant_config.json' },
      { url: `${baseUrl}/quantize_config.json`, filename: 'quantize_config.json' },
      { url: `${baseUrl}/special_tokens_map.json`, filename: 'special_tokens_map.json' },
      { url: `${baseUrl}/tokenizer.json`, filename: 'tokenizer.json' },
      { url: `${baseUrl}/tokenizer_config.json`, filename: 'tokenizer_config.json' },
      { url: `${baseUrl}/vocab.json`, filename: 'vocab.json' }
    ];
    return configFiles;
  }

  // 获取模型ONNX文件列表
  private getModelONNXFiles(modelId: string): Array<{url: string, filename: string}> {
    const baseUrl = `https://huggingface.co/${modelId}/resolve/main/onnx`;
    const onnxFiles = [
      { url: `${baseUrl}/encoder_model_q4.onnx`, filename: 'encoder_model_q4.onnx' },
      { url: `${baseUrl}/decoder_model_q4.onnx`, filename: 'decoder_model_q4.onnx' }
    ];
    return onnxFiles;
  }

  // 模型管理方法
  
  // 检测网络连接
  private async checkNetworkConnection(): Promise<void> {
    try {
      // 尝试访问 Hugging Face 来检测连接
      const response = await fetch('https://huggingface.co/Xenova/whisper-tiny/resolve/main/config.json', {
        method: 'HEAD',
        cache: 'no-cache'
      });
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
    } catch (error) {
      console.error('网络连接检测失败:', error);
      throw new Error(`无法连接到 Hugging Face CDN: ${error instanceof Error ? error.message : '未知错误'}`);
    }
  }
  
  // 获取可用模型列表
  getAvailableModels(): ModelInfo[] {
    return [...this.availableModels];
  }
  
  // 获取当前使用的模型ID
  getCurrentModelId(): string {
    return this.currentModelId;
  }
  
  // 切换到指定模型
  async switchToModel(modelId: string): Promise<void> {
    console.log(`ASR: 开始切换到模型 ${modelId}`);
    const model = this.availableModels.find(m => m.id === modelId);
    if (!model) {
      throw new Error(`模型 ${modelId} 不存在`);
    }
    
    console.log(`ASR: 找到模型 ${model.name}, 已下载: ${model.downloaded}`);
    
    if (!model.downloaded) {
      throw new Error(`模型 ${model.name} 尚未下载，请先下载模型`);
    }
    
    // 如果当前正在使用相同模型，无需切换
    if (this.currentModelId === modelId) {
      console.log(`ASR: 模型 ${modelId} 已经是当前使用的模型`);
      return;
    }
    
    console.log(`ASR: 当前模型 ${this.currentModelId}, 目标模型 ${modelId}`);
    
    // 停止当前的识别
    if (this.isListening) {
      console.log(`ASR: 停止当前语音识别`);
      await this.stopListening();
    }
    
    // 重置初始化状态
    this.isInitialized = false;
    this.pipeline = null;
    
    // 更新当前模型
    this.currentModelId = modelId;
    this.options.model = modelId;
    
    console.log(`ASR: 更新模型配置完成`);
    
    // 保存模型状态
    console.log(`ASR: 开始保存模型状态`);
    await this.saveModelStatus();
    console.log(`ASR: 模型状态保存完成`);
    
    console.log(`已切换到模型: ${model.name}`);
  }
  
  // 直接下载Whisper Tiny ONNX模型文件
  async downloadWhisperTinyONNX(): Promise<void> {
    const url = 'https://huggingface.co/Xenova/whisper-tiny/resolve/main/onnx/decoder_model_q4.onnx';
    const modelId = 'Xenova/whisper-tiny';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex === -1) {
      throw new Error(`模型 ${modelId} 不存在`);
    }
    
    const model = this.availableModels[modelIndex];
    if (model.downloaded) {
      console.log(`模型 ${model.name} 已经下载完成`);
      return;
    }
    
    if (model.downloading) {
      throw new Error(`模型 ${model.name} 正在下载中`);
    }
    
    // 标记为下载中
    this.availableModels[modelIndex] = {
      ...model,
      downloading: true,
      downloadProgress: 0
    };
    
    this.emit('modelListUpdated', this.getAvailableModels());
    
    try {
      console.log(`开始下载Whisper Tiny ONNX模型: ${url}`);
      
      // 检查是否在Electron环境中
      if (typeof window !== 'undefined' && window.electronAPI) {
        // 在Electron渲染进程中，使用IPC调用主进程
        const result = await window.electronAPI.speechRecognition.downloadWhisperTinyONNX();
        if (!result.success) {
          throw new Error(result.error || '下载失败');
        }
        
        // 下载成功，但不在这里设置完成状态，由updateWhisperTinyONNXProgress处理
        console.log(`Whisper Tiny ONNX模型下载完成`);
        return;
      }
      
      // 浏览器环境的fallback实现
      throw new Error('浏览器环境下暂不支持直接下载模型文件，请在Electron应用中使用此功能');
      
    } catch (error) {
      // 下载失败
      this.availableModels[modelIndex] = {
        ...model,
        downloading: false,
        downloadProgress: 0
      };
      
      const errorMessage = (error as Error).message;
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress: 0,
        status: 'error',
        error: errorMessage
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      console.error(`Whisper Tiny ONNX模型下载失败:`, error);
      throw new Error(errorMessage);
    }
  }

  // 更新 Whisper Tiny ONNX 模型下载进度
  updateWhisperTinyONNXProgress(progress: number): void {
    const modelId = 'Xenova/whisper-tiny';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex !== -1) {
      const isCompleted = progress >= 100;
      const wasCompleted = this.availableModels[modelIndex].downloaded;
      
      this.availableModels[modelIndex] = {
        ...this.availableModels[modelIndex],
        downloading: progress < 100,
        downloadProgress: progress,
        downloaded: isCompleted
      };
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress,
        status: isCompleted ? 'completed' : 'downloading'
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      // 仅在下载完成时保存模型状态
      if (isCompleted && !wasCompleted) {
        this.saveModelStatus();
      }
    }
  }

  // 下载Whisper Tiny EN ONNX模型文件
  async downloadWhisperTinyENONNX(): Promise<void> {
    const url = 'https://huggingface.co/Xenova/whisper-tiny.en/resolve/main/onnx/decoder_model_q4.onnx';
    const modelId = 'Xenova/whisper-tiny.en';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex === -1) {
      throw new Error(`模型 ${modelId} 不存在`);
    }
    
    const model = this.availableModels[modelIndex];
    if (model.downloaded) {
      console.log(`模型 ${model.name} 已经下载完成`);
      return;
    }
    
    if (model.downloading) {
      throw new Error(`模型 ${model.name} 正在下载中`);
    }
    
    // 标记为下载中
    this.availableModels[modelIndex] = {
      ...model,
      downloading: true,
      downloadProgress: 0
    };
    
    this.emit('modelListUpdated', this.getAvailableModels());
    
    try {
      console.log(`开始下载Whisper Tiny EN ONNX模型: ${url}`);
      
      // 检查是否在Electron环境中
      if (typeof window !== 'undefined' && window.electronAPI) {
        // 在Electron渲染进程中，使用IPC调用主进程
        const result = await window.electronAPI.speechRecognition.downloadWhisperTinyENONNX();
        if (!result.success) {
          throw new Error(result.error || '下载失败');
        }

        // 下载成功，但不在这里设置完成状态，由updateWhisperTinyENONNXProgress处理
        console.log(`Whisper Tiny EN ONNX模型下载完成`);
        return;
      }
      
      // 浏览器环境的fallback实现
      throw new Error('浏览器环境下暂不支持直接下载模型文件，请在Electron应用中使用此功能');
      
    } catch (error) {
      // 下载失败
      this.availableModels[modelIndex] = {
        ...model,
        downloading: false,
        downloadProgress: 0
      };
      
      const errorMessage = (error as Error).message;
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress: 0,
        status: 'error',
        error: errorMessage
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      console.error(`Whisper Tiny EN ONNX模型下载失败:`, error);
      throw new Error(errorMessage);
    }
  }
  
  // 更新 Whisper Tiny EN ONNX 模型下载进度
  updateWhisperTinyENONNXProgress(progress: number): void {
    const modelId = 'Xenova/whisper-tiny.en';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex !== -1) {
      const isCompleted = progress >= 100;
      const wasCompleted = this.availableModels[modelIndex].downloaded;
      
      this.availableModels[modelIndex] = {
        ...this.availableModels[modelIndex],
        downloading: progress < 100,
        downloadProgress: progress,
        downloaded: isCompleted
      };
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress,
        status: isCompleted ? 'completed' : 'downloading'
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      // 仅在下载完成时保存模型状态
      if (isCompleted && !wasCompleted) {
        this.saveModelStatus();
      }
    }
  }
  
  // 下载Whisper Base EN ONNX模型文件
  async downloadWhisperBaseENONNX(): Promise<void> {
    const url = 'https://huggingface.co/Xenova/whisper-base.en/resolve/main/onnx/decoder_model_q4.onnx';
    const modelId = 'Xenova/whisper-base.en';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex === -1) {
      throw new Error(`模型 ${modelId} 不存在`);
    }
    
    const model = this.availableModels[modelIndex];
    if (model.downloaded) {
      console.log(`模型 ${model.name} 已经下载完成`);
      return;
    }
    
    if (model.downloading) {
      throw new Error(`模型 ${model.name} 正在下载中`);
    }
    
    // 标记为下载中
    this.availableModels[modelIndex] = {
      ...model,
      downloading: true,
      downloadProgress: 0
    };
    
    this.emit('modelListUpdated', this.getAvailableModels());
    
    try {
      console.log(`开始下载Whisper Base EN ONNX模型: ${url}`);
      
      // 检查是否在Electron环境中
      if (typeof window !== 'undefined' && window.electronAPI) {
        // 在Electron渲染进程中，使用IPC调用主进程
        const result = await window.electronAPI.speechRecognition.downloadWhisperBaseENONNX();
        if (!result.success) {
          throw new Error(result.error || '下载失败');
        }
        
        // 下载成功，但不在这里设置完成状态，由updateWhisperBaseENONNXProgress处理
        console.log(`Whisper Base EN ONNX模型下载完成`);
        return;
      }
      
      // 浏览器环境的fallback实现
      throw new Error('浏览器环境下暂不支持直接下载模型文件，请在Electron应用中使用此功能');
      
    } catch (error) {
      // 下载失败
      this.availableModels[modelIndex] = {
        ...model,
        downloading: false,
        downloadProgress: 0
      };
      
      const errorMessage = (error as Error).message;
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress: 0,
        status: 'error',
        error: errorMessage
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      console.error(`Whisper Base EN ONNX模型下载失败:`, error);
      throw new Error(errorMessage);
    }
  }
  
  // 更新 Whisper Base EN ONNX 模型下载进度
  updateWhisperBaseENONNXProgress(progress: number): void {
    const modelId = 'Xenova/whisper-base.en';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex !== -1) {
      const isCompleted = progress >= 100;
      const wasCompleted = this.availableModels[modelIndex].downloaded;
      
      this.availableModels[modelIndex] = {
        ...this.availableModels[modelIndex],
        downloading: progress < 100,
        downloadProgress: progress,
        downloaded: isCompleted
      };
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress,
        status: isCompleted ? 'completed' : 'downloading'
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      // 仅在下载完成时保存模型状态
      if (isCompleted && !wasCompleted) {
        this.saveModelStatus();
      }
    }
  }
  
  // 下载Whisper Base ONNX模型文件
  async downloadWhisperBaseONNX(): Promise<void> {
    const url = 'https://huggingface.co/Xenova/whisper-base/resolve/main/onnx/decoder_model_q4.onnx';
    const modelId = 'Xenova/whisper-base';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex === -1) {
      throw new Error(`模型 ${modelId} 不存在`);
    }
    
    const model = this.availableModels[modelIndex];
    if (model.downloaded) {
      console.log(`模型 ${model.name} 已经下载完成`);
      return;
    }
    
    if (model.downloading) {
      throw new Error(`模型 ${model.name} 正在下载中`);
    }
    
    // 标记为下载中
    this.availableModels[modelIndex] = {
      ...model,
      downloading: true,
      downloadProgress: 0
    };
    
    this.emit('modelListUpdated', this.getAvailableModels());
    
    try {
      console.log(`开始下载Whisper Base ONNX模型: ${url}`);
      
      // 检查是否在Electron环境中
      if (typeof window !== 'undefined' && window.electronAPI) {
        // 在Electron渲染进程中，使用IPC调用主进程
        const result = await window.electronAPI.speechRecognition.downloadWhisperBaseONNX();
        if (!result.success) {
          throw new Error(result.error || '下载失败');
        }
        
        // 下载成功，但不在这里设置完成状态，由updateWhisperBaseONNXProgress处理
        console.log(`Whisper Base ONNX模型下载完成`);
        return;
      }
      
      // 浏览器环境的fallback实现
      throw new Error('浏览器环境下暂不支持直接下载模型文件，请在Electron应用中使用此功能');
      
    } catch (error) {
      // 下载失败
  
      const errorMessage = (error as Error).message;
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress: 0,
        status: 'error',
        error: errorMessage
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      console.error(`Whisper Base ONNX模型下载失败:`, error);
      throw new Error(errorMessage);
    }
  }
  
  // 更新 Whisper Base ONNX 模型下载进度
  updateWhisperBaseONNXProgress(progress: number): void {
    const modelId = 'Xenova/whisper-base';
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    
    if (modelIndex !== -1) {
      const isCompleted = progress >= 100;
      const wasCompleted = this.availableModels[modelIndex].downloaded;
      
      this.availableModels[modelIndex] = {
        ...this.availableModels[modelIndex],
        downloading: progress < 100,
        downloadProgress: progress,
        downloaded: isCompleted
      };
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress,
        status: isCompleted ? 'completed' : 'downloading'
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      // 仅在下载完成时保存模型状态
      if (isCompleted && !wasCompleted) {
        this.saveModelStatus();
      }
    }
  }

  // 保存模型状态到本地存储
  private async saveModelStatus(): Promise<void> {
    try {
      console.log('ASR: 开始保存模型状态');
      const statusData = {
        models: this.availableModels.map(model => ({
          id: model.id,
          downloaded: model.downloaded
        })),
        currentModelId: this.currentModelId
      };
      
      console.log('ASR: 状态数据:', statusData);
      
      // 检查是否在Electron环境中
      if (typeof window !== 'undefined' && window.electronAPI) {
        console.log('ASR: 在Electron渲染进程中，使用IPC调用主进程');
        // 在Electron渲染进程中，使用IPC调用主进程保存模型状态
        try {
          const result = await window.electronAPI.speechRecognition.saveModelStatus(statusData);
          if (!result.success) {
            console.warn('保存模型状态失败:', result.error);
          } else {
            console.log('ASR: 模型状态保存成功');
          }
        } catch (error) {
          console.warn('保存模型状态失败:', error);
        }
      } else if (typeof process !== 'undefined' && process.versions && process.versions.electron) {
        // 在 Electron 主进程中，保存到文件系统
        try {
          const fs = require('fs');
          const path = require('path');
          const { app } = require('electron');
          
          const configPath = path.join(app.getPath('userData'), 'transformers-asr-models.json');
          const statusJson = JSON.stringify(statusData);
          fs.writeFileSync(configPath, statusJson, 'utf8');
        } catch (error) {
          console.warn('保存模型状态到文件失败:', error);
        }
      } else if (typeof window !== 'undefined' && window.localStorage) {
        // 浏览器环境
        const statusJson = JSON.stringify(statusData);
        localStorage.setItem('transformers-asr-models', statusJson);
      }
    } catch (error) {
      console.warn('保存模型状态失败:', error);
    }
  }

  // 验证模型文件是否存在
  private async verifyModelFiles(): Promise<void> {
    try {
      console.log('ASR: 开始验证模型文件');
      // 检查是否在Electron环境中
      if (typeof window !== 'undefined' && window.electronAPI) {
        console.log('ASR: 在渲染进程中，使用IPC调用主进程');
        // 在渲染进程中，使用IPC调用主进程检查文件
        try {
          const result = await window.electronAPI.speechRecognition.verifyModelFiles();
          console.log('ASR: IPC调用结果:', result);
          if (result.success) {
            console.log('ASR: 已下载的模型:', result.downloadedModels);
            // 更新模型状态
            this.availableModels.forEach((model, index) => {
              const isDownloaded = result.downloadedModels?.includes(model.id) || false;
              console.log(`ASR: 模型 ${model.id} 下载状态: ${isDownloaded}`);
              this.availableModels[index].downloaded = isDownloaded;
            });
            this.emit('modelListUpdated', this.getAvailableModels());
          }
        } catch (error) {
          console.warn('验证模型文件失败:', error);
        }
      } else if (typeof process !== 'undefined' && process.versions && process.versions.electron) {
        // 在主进程中直接检查文件系统
        const fs = require('fs');
        const path = require('path');
        
        this.availableModels.forEach((model, index) => {
          const modelName = this.getModelFolderName(model.id);
          const modelDir = getModelDir(this.modelsRootDir, modelName);
          const onnxDir = getModelONNXDir(this.modelsRootDir, modelName);
          
          // 检查ONNX文件
          const onnxFiles = this.getModelONNXFiles(model.id);
          const onnxFilesExist = onnxFiles.every(file => {
            const filePath = path.join(onnxDir, file.filename);
            return fs.existsSync(filePath);
          });
          
          // 检查配置文件
          const configFiles = this.getModelConfigFiles(model.id);
          const configFilesExist = configFiles.every(file => {
            const filePath = path.join(modelDir, file.filename);
            return fs.existsSync(filePath);
          });
          
          // 只有在所有文件都存在时才认为模型已下载
          this.availableModels[index].downloaded = onnxFilesExist && configFilesExist;
        });
        
        this.emit('modelListUpdated', this.getAvailableModels());
      }
    } catch (error) {
      console.warn('验证模型文件失败:', error);
    }
  }

  // 手动刷新模型状态
  async refreshModelStatus(): Promise<void> {
    try {
      await this.verifyModelFiles();
      this.saveModelStatus();
    } catch (error) {
      console.warn('刷新模型状态失败:', error);
    }
  }

  // 从本地存储加载模型状态
  private loadModelStatus(): void {
    try {
      console.log('ASR: 开始加载模型状态');
      let savedData: string | null = null;
      
      // 检查是否在浏览器环境中
      if (typeof window !== 'undefined' && window.localStorage) {
        savedData = localStorage.getItem('transformers-asr-models');
      } else if (typeof process !== 'undefined' && process.versions && process.versions.electron) {
        // 在 Electron 主进程中，从文件系统读取
        try {
          const fs = require('fs');
          const path = require('path');
          const { app } = require('electron');
          
          const configPath = path.join(app.getPath('userData'), 'transformers-asr-models.json');
          if (fs.existsSync(configPath)) {
            savedData = fs.readFileSync(configPath, 'utf8');
          }
        } catch (error) {
          console.warn('从文件加载模型状态失败:', error);
        }
      }
      
      if (savedData) {
        const statusData = JSON.parse(savedData);
        console.log('ASR: 加载的模型状态:', statusData);
        
        // 更新模型下载状态
        if (statusData.models) {
          statusData.models.forEach((saved: { id: string; downloaded: boolean }) => {
            const modelIndex = this.availableModels.findIndex(m => m.id === saved.id);
            if (modelIndex !== -1) {
              this.availableModels[modelIndex].downloaded = saved.downloaded;
              console.log(`ASR: 从保存状态设置模型 ${saved.id} 下载状态为 ${saved.downloaded}`);
            }
          });
        }
        
        // 更新当前模型
        if (statusData.currentModelId) {
          this.currentModelId = statusData.currentModelId;
          this.options.model = statusData.currentModelId;
          console.log(`ASR: 从保存状态设置当前模型为 ${statusData.currentModelId}`);
        }
      }
      
      // 在加载配置后，验证实际文件存在状态
      // 注意：这里仅在初始化时验证，避免在切换模型时触发
      setTimeout(async () => {
        console.log('ASR: 延迟验证模型文件');
        await this.verifyModelFiles();
      }, 100);
    } catch (error) {
      console.warn('加载模型状态失败:', error);
    }
  }

  get initialized(): boolean {
    return this.isInitialized;
  }

  get listening(): boolean {
    return this.isListening;
  }

  dispose(): void {
    if (this.isListening) {
      this.stopListening();
    }
    // 也要清理麦克风测试
    if (this.isMicTesting) {
      this.cleanupMicTest();
    }
    this.pipeline = null;
    this.isInitialized = false;
    this.removeAllListeners();
  }
}

// 单例实例
let instance: TransformersASR | null = null;

export function getTransformersASRInstance(options?: TransformersASROptions): TransformersASR {
  if (!instance) {
    instance = new TransformersASR(options);
  }
  return instance;
}