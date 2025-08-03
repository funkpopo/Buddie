import { pipeline, AutomaticSpeechRecognitionPipeline, env } from '@xenova/transformers';

// 获取模型缓存目录
async function getModelCacheDir(): Promise<string> {
  // 检查是否在Electron渲染进程中
  if (typeof window !== 'undefined' && window.electronAPI) {
    try {
      return await window.electronAPI.system.getModelCacheDir();
    } catch (error) {
      console.warn('Failed to get model cache dir from main process:', error);
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
  private modelCacheDir: string;
  
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
    this.modelCacheDir = './models'; // 初始默认值
    this.loadModelStatus();
    this.initializeModelCacheDir(); // 异步初始化缓存目录
  }

  // 异步初始化模型缓存目录
  private async initializeModelCacheDir(): Promise<void> {
    try {
      this.modelCacheDir = await getModelCacheDir();
      console.log('Model cache directory set to:', this.modelCacheDir);
    } catch (error) {
      console.warn('Failed to initialize model cache directory:', error);
      this.modelCacheDir = './models'; // 回退到默认值
    }
  }

  async initialize(): Promise<void> {
    if (this.isInitialized) {
      return;
    }

    try {
      console.log('Initializing Transformers ASR...');
      
      // 确保缓存目录已初始化
      if (this.modelCacheDir === './models') {
        await this.initializeModelCacheDir();
      }
      
      // 检查当前模型是否已下载
      const currentModel = this.availableModels.find(m => m.id === this.currentModelId);
      if (!currentModel || !currentModel.downloaded) {
        throw new Error(`当前模型 ${this.currentModelId} 尚未下载，请先在设置中下载模型`);
      }
      
      // 配置环境变量
      env.allowRemoteModels = true;
      env.allowLocalModels = true;
      env.useBrowserCache = true;
      env.remoteURL = 'https://huggingface.co/';
      env.remotePathTemplate = '{model}/resolve/{revision}/{file}';
      
      console.log(`Loading model: ${this.currentModelId}`);
      console.log(`Model cache dir: ${this.modelCacheDir}`);
      
      this.pipeline = await pipeline(
        'automatic-speech-recognition',
        this.currentModelId,
        {
          quantized: true,
          revision: 'main',
          cache_dir: this.modelCacheDir,
          local_files_only: false,
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
      if (result && result.text && result.text.trim()) {
        const asrResult: TransformersASRResult = {
          text: result.text.trim(),
          confidence: 0.9, // transformers.js doesn't provide confidence scores
          isFinal: true,
          timestamp: Date.now()
        };

        this.emit('result', asrResult);
        console.log('ASR Result:', asrResult.text);
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
    const model = this.availableModels.find(m => m.id === modelId);
    if (!model) {
      throw new Error(`模型 ${modelId} 不存在`);
    }
    
    if (!model.downloaded) {
      throw new Error(`模型 ${model.name} 尚未下载，请先下载模型`);
    }
    
    // 如果当前正在使用相同模型，无需切换
    if (this.currentModelId === modelId) {
      return;
    }
    
    // 停止当前的识别
    if (this.isListening) {
      await this.stopListening();
    }
    
    // 重置初始化状态
    this.isInitialized = false;
    this.pipeline = null;
    
    // 更新当前模型
    this.currentModelId = modelId;
    this.options.model = modelId;
    
    // 保存模型状态
    this.saveModelStatus();
    
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
      this.availableModels[modelIndex] = {
        ...this.availableModels[modelIndex],
        downloading: progress < 100,
        downloadProgress: progress,
        downloaded: progress >= 100
      };
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress,
        status: progress >= 100 ? 'completed' : 'downloading'
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      if (progress >= 100) {
        this.saveModelStatus();
      }
    }
  }

  // 下载指定模型
  async downloadModel(modelId: string): Promise<void> {
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
    
    // 确保缓存目录已初始化
    if (this.modelCacheDir === './models') {
      await this.initializeModelCacheDir();
    }
    
    // 标记为下载中
    this.availableModels[modelIndex] = {
      ...model,
      downloading: true,
      downloadProgress: 0
    };
    
    this.emit('modelListUpdated', this.getAvailableModels());
    
    try {
      console.log(`开始下载模型: ${model.name}`);
      console.log(`模型缓存目录: ${this.modelCacheDir}`);
      console.log(`模型ID: ${modelId}`);
      console.log(`Hugging Face URL: https://huggingface.co/${modelId}/tree/main`);
      
      // 检测网络连接
      await this.checkNetworkConnection();
      
      // 配置环境变量以支持应用内下载
      env.allowRemoteModels = true;
      env.allowLocalModels = true;
      env.useBrowserCache = false; // 禁用浏览器缓存避免冲突
      env.remoteURL = 'https://huggingface.co/';
      env.remotePathTemplate = '{model}/resolve/{revision}/{file}';
      
      // 添加请求头以避免CORS问题
      if (!env.customHeaders) {
        env.customHeaders = {};
      }
      env.customHeaders['User-Agent'] = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36';
      env.customHeaders['Accept'] = 'application/json, application/octet-stream, */*';
      
      // 下载模型
      const testPipeline = await pipeline(
        'automatic-speech-recognition',
        modelId,
        {
          quantized: true,
          revision: 'main',
          cache_dir: this.modelCacheDir,
          local_files_only: false,
          trust_remote_code: false, // 安全考虑，不执行远程代码
          use_auth_token: false, // 公开模型无需认证
          progress_callback: (progress: { status: string; progress?: number; file?: string }) => {
            if (progress.status === 'downloading' || progress.status === 'loading') {
              const progressPercent = progress.progress || 0;
              
              // 更新下载进度
              this.availableModels[modelIndex] = {
                ...this.availableModels[modelIndex],
                downloadProgress: progressPercent
              };
              
              const downloadProgress: ModelDownloadProgress = {
                modelId,
                progress: progressPercent,
                status: 'downloading'
              };
              
              this.emit('modelDownloadProgress', downloadProgress);
              this.emit('modelListUpdated', this.getAvailableModels());
              
              console.log(`下载进度 ${model.name}: ${progressPercent.toFixed(1)}%`);
            }
          }
        }
      );
      
      // 下载成功
      this.availableModels[modelIndex] = {
        ...model,
        downloaded: true,
        downloading: false,
        downloadProgress: 100
      };
      
      // 释放测试pipeline资源
      testPipeline.dispose?.();
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress: 100,
        status: 'completed'
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      this.saveModelStatus();
      
      console.log(`模型下载完成: ${model.name}`);
      
    } catch (error) {
      // 下载失败
      this.availableModels[modelIndex] = {
        ...model,
        downloading: false,
        downloadProgress: 0
      };
      
      let errorMessage = (error as Error).message;
      
      // 检查是否是网络或CDN相关错误，提供更详细的错误信息
      if (errorMessage.includes('SyntaxError') && errorMessage.includes('<!DOCTYPE')) {
        errorMessage = '网络连接问题或CDN暂时不可用。可能原因：1) 网络代理阻止了Hugging Face访问 2) DNS解析问题 3) 防火墙限制。请检查网络连接后重试';
      } else if (errorMessage.includes('fetch')) {
        errorMessage = '网络连接失败，无法访问Hugging Face模型库，请检查网络连接';
      } else if (errorMessage.includes('timeout')) {
        errorMessage = '下载超时，请检查网络连接后重试';
      } else if (errorMessage.includes('CORS')) {
        errorMessage = '跨域请求被阻止，这可能是浏览器安全限制导致的';
      } else if (errorMessage.includes('404') || errorMessage.includes('Not Found')) {
        errorMessage = `模型 ${modelId} 在Hugging Face上不存在或已被移动`;
      }
      
      const downloadProgress: ModelDownloadProgress = {
        modelId,
        progress: 0,
        status: 'error',
        error: errorMessage
      };
      
      this.emit('modelDownloadProgress', downloadProgress);
      this.emit('modelListUpdated', this.getAvailableModels());
      
      console.error(`模型下载失败 ${model.name}:`, error);
      throw new Error(errorMessage);
    }
  }
  
  // 删除模型（清除缓存）
  async deleteModel(modelId: string): Promise<void> {
    const modelIndex = this.availableModels.findIndex(m => m.id === modelId);
    if (modelIndex === -1) {
      throw new Error(`模型 ${modelId} 不存在`);
    }
    
    const model = this.availableModels[modelIndex];
    
    // 如果是当前使用的模型，先切换到默认模型
    if (this.currentModelId === modelId) {
      const defaultModel = this.availableModels.find(m => m.id === 'Xenova/whisper-tiny');
      if (defaultModel && defaultModel.downloaded) {
        await this.switchToModel('Xenova/whisper-tiny');
      } else {
        // 停止当前的识别并重置状态
        if (this.isListening) {
          await this.stopListening();
        }
        this.isInitialized = false;
        this.pipeline = null;
        this.currentModelId = 'Xenova/whisper-tiny';
      }
    }
    
    // 标记为未下载
    this.availableModels[modelIndex] = {
      ...model,
      downloaded: false,
      downloading: false,
      downloadProgress: 0
    };
    
    this.emit('modelListUpdated', this.getAvailableModels());
    this.saveModelStatus();
    
    console.log(`已删除模型: ${model.name}`);
  }
  
  // 保存模型状态到本地存储
  private saveModelStatus(): void {
    const modelStatus = this.availableModels.map(model => ({
      id: model.id,
      downloaded: model.downloaded
    }));
    
    const statusData = {
      models: modelStatus,
      currentModelId: this.currentModelId
    };
    
    // 检查是否在浏览器环境中
    if (typeof window !== 'undefined' && window.localStorage) {
      localStorage.setItem('transformers-asr-models', JSON.stringify(statusData));
    } else if (typeof process !== 'undefined' && process.versions && process.versions.electron) {
      // 在 Electron 主进程中，使用文件系统存储
      try {
        const fs = require('fs');
        const path = require('path');
        const { app } = require('electron');
        
        const configPath = path.join(app.getPath('userData'), 'transformers-asr-models.json');
        fs.writeFileSync(configPath, JSON.stringify(statusData, null, 2));
      } catch (error) {
        console.warn('保存模型状态到文件失败:', error);
      }
    }
  }
  
  // 验证模型文件实际存在状态
  private async verifyModelFiles(): Promise<void> {
    try {
      // 确保缓存目录已初始化
      if (this.modelCacheDir === './models') {
        await this.initializeModelCacheDir();
      }

      // 检查是否在Electron环境中
      if (typeof window !== 'undefined' && window.electronAPI) {
        // 在渲染进程中，使用IPC调用主进程检查文件
        try {
          const result = await window.electronAPI.system.verifyModelFiles();
          if (result.success) {
            // 更新模型状态
            this.availableModels.forEach((model, index) => {
              const isDownloaded = result.downloadedModels.includes(model.id);
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
          let isDownloaded = false;
          
          if (model.id === 'Xenova/whisper-tiny') {
            // 对于Whisper Tiny，检查decoder和encoder文件
            const decoderPath = path.join(this.modelCacheDir, 'decoder_model_q4.onnx');
            const encoderPath = path.join(this.modelCacheDir, 'encoder_model_q4.onnx');
            isDownloaded = fs.existsSync(decoderPath) && fs.existsSync(encoderPath);
          } else {
            // 对于其他模型，检查标准的transformers.js缓存结构
            const modelDir = path.join(this.modelCacheDir, 'models--' + model.id.replace('/', '--'));
            isDownloaded = fs.existsSync(modelDir);
          }
          
          this.availableModels[index].downloaded = isDownloaded;
        });
        
        this.emit('modelListUpdated', this.getAvailableModels());
      }
    } catch (error) {
      console.warn('验证模型文件失败:', error);
    }
  }

  // 手动刷新模型状态
  async refreshModelStatus(): Promise<void> {
    await this.verifyModelFiles();
    this.saveModelStatus();
  }

  // 从本地存储加载模型状态
  private loadModelStatus(): void {
    try {
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
        
        // 更新模型下载状态
        if (statusData.models) {
          statusData.models.forEach((saved: { id: string; downloaded: boolean }) => {
            const modelIndex = this.availableModels.findIndex(m => m.id === saved.id);
            if (modelIndex !== -1) {
              this.availableModels[modelIndex].downloaded = saved.downloaded;
            }
          });
        }
        
        // 更新当前模型
        if (statusData.currentModelId) {
          this.currentModelId = statusData.currentModelId;
          this.options.model = statusData.currentModelId;
        }
      }
      
      // 在加载配置后，验证实际文件存在状态
      setTimeout(() => {
        this.verifyModelFiles();
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