import { join } from 'path';
import { app } from 'electron';
import { EventEmitter } from 'events';

// 导入真实的VOSK库
import { createModel, Model, KaldiRecognizer } from 'vosk-browser';

export interface VoskResult {
  text: string;
  confidence: number;
  language: string;
  isFinal: boolean;
}

export interface VoskOptions {
  modelPath?: string;
  language?: 'zh' | 'en' | 'auto';
  threads?: number;
  sampleRate?: number;
}

class VoskWrapper extends EventEmitter {
  private isInitialized = false;
  private isListening = false;
  private modelPath: string;
  private sampleRate: number;
  private threads: number;
  private language: string;
  
  // 真实的VOSK实例
  private voskModel: Model | null = null;
  private recognizer: KaldiRecognizer | null = null;

  constructor() {
    super();
    this.modelPath = '';
    this.sampleRate = 16000;
    this.threads = 4;
    this.language = 'zh';
  }

  /**
   * 初始化VOSK模型
   */
  async initialize(options: VoskOptions = {}): Promise<void> {
    if (this.isInitialized) {
      return;
    }

    this.modelPath = options.modelPath || this.getDefaultModelPath();
    this.sampleRate = options.sampleRate || 16000;
    this.threads = options.threads || 4;
    this.language = options.language || 'zh';

    try {
      // 检查模型文件是否存在
      const fs = require('fs');
      if (!fs.existsSync(this.modelPath)) {
        throw new Error(`VOSK model not found at: ${this.modelPath}`);
      }

      console.log(`Initializing VOSK with model: ${this.modelPath}`);
      console.log(`Sample rate: ${this.sampleRate}, Threads: ${this.threads}`);

      // 加载真实的VOSK模型
      this.voskModel = await createModel(this.modelPath);
      
      // 等待模型准备就绪
      await this.waitForModelReady();
      
      // 创建识别器
      this.recognizer = new this.voskModel.KaldiRecognizer(this.sampleRate);
      
      // 监听识别结果
      this.recognizer.on('result', (message) => {
        this.handleRecognitionResult(message);
      });

      this.isInitialized = true;
      console.log('VOSK initialized successfully');
    } catch (error) {
      console.error('Failed to initialize VOSK:', error);
      throw error;
    }
  }

  /**
   * 等待模型准备就绪
   */
  private async waitForModelReady(): Promise<void> {
    return new Promise((resolve) => {
      if (this.voskModel!.ready) {
        resolve();
      } else {
        this.voskModel!.on('load', (message) => {
          if ('result' in message && message.result) {
            resolve();
          }
        });
      }
    });
  }

  /**
   * 开始语音识别
   */
  async startRecognition(): Promise<void> {
    if (!this.isInitialized) {
      throw new Error('VOSK not initialized. Call initialize() first.');
    }

    if (this.isListening) {
      throw new Error('Already listening');
    }

    this.isListening = true;
    this.emit('start');
    console.log('VOSK recognition started');
  }

  /**
   * 停止语音识别
   */
  async stopRecognition(): Promise<void> {
    if (!this.isListening) {
      return;
    }

    this.isListening = false;
    this.emit('stop');
    console.log('VOSK recognition stopped');
  }

  /**
   * 处理音频数据
   */
  async processAudio(audioData: Float32Array): Promise<VoskResult> {
    if (!this.isInitialized || !this.isListening || !this.recognizer) {
      throw new Error('VOSK not initialized or not listening');
    }

    try {
      // 使用真实的VOSK处理音频数据
      this.recognizer.acceptWaveformFloat(audioData, this.sampleRate);
      
      // 返回一个默认结果，实际结果会通过事件回调
      return {
        text: '',
        confidence: 0,
        language: this.language,
        isFinal: false
      };
    } catch (error) {
      console.error('Error processing audio:', error);
      this.emit('error', error);
      throw error;
    }
  }

  /**
   * 处理识别结果
   */
  private handleRecognitionResult(message: any): void {
    try {
      const result = message.result;
      if (result && result.text && result.text.trim()) {
        const voskResult: VoskResult = {
          text: result.text,
          confidence: result.confidence || 0.9,
          language: this.language,
          isFinal: true
        };
        
        this.emit('result', voskResult);
      }
    } catch (error) {
      console.error('Error handling recognition result:', error);
    }
  }

  /**
   * 获取默认模型路径
   */
  private getDefaultModelPath(): string {
    if (app.isPackaged) {
      return join(process.resourcesPath, 'models', 'vosk-model');
    } else {
      return join(__dirname, '..', 'models', 'vosk-model');
    }
  }

  /**
   * 检查是否已初始化
   */
  get initialized(): boolean {
    return this.isInitialized;
  }

  /**
   * 检查是否正在监听
   */
  get listening(): boolean {
    return this.isListening;
  }

  /**
   * 获取模型路径
   */
  get modelPathInfo(): string {
    return this.modelPath;
  }

  /**
   * 清理资源
   */
  dispose(): void {
    if (this.isListening) {
      this.stopRecognition();
    }
    
    if (this.recognizer) {
      this.recognizer.remove();
      this.recognizer = null;
    }
    
    if (this.voskModel) {
      this.voskModel.terminate();
      this.voskModel = null;
    }
    
    this.isInitialized = false;
    this.removeAllListeners();
  }
}

// 单例模式
let instance: VoskWrapper | null = null;

export function getVoskInstance(): VoskWrapper {
  if (!instance) {
    instance = new VoskWrapper();
  }
  return instance;
}

export { VoskWrapper }; 