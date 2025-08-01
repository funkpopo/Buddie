import { getVoskInstance, VoskResult } from './vosk-wrapper';
import { EventEmitter } from 'events';

// 定义语音识别结果的类型
export interface SpeechRecognitionResult {
  text: string;
  isFinal: boolean;
  confidence?: number;
  language?: string;
}

// 定义语音识别事件的类型
export interface SpeechRecognitionEvent {
  result?: SpeechRecognitionResult;
  error?: string;
}

// 语音识别类
export class SpeechRecognizer extends EventEmitter {
  private isListening = false;
  private isInitialized = false;
  private audioStream: MediaStream | null = null;
  private audioContext: AudioContext | null = null;
  private processor: ScriptProcessorNode | null = null;
  private vosk = getVoskInstance();

  constructor() {
    super();
  }

  // 初始化语音识别器
  public async initialize(): Promise<void> {
    if (this.isInitialized) {
      return;
    }

    try {
      await this.vosk.initialize({
        language: 'zh',
        threads: 4,
        sampleRate: 16000
      });
      this.isInitialized = true;
      console.log('VOSK initialized successfully');
    } catch (error) {
      console.error('Failed to initialize VOSK:', error);
      throw error;
    }
  }

  // 开始监听
  public async startListening(): Promise<void> {
    if (this.isListening) {
      throw new Error('Already listening');
    }

    if (!this.isInitialized) {
      await this.initialize();
    }

    try {
      // 请求麦克风权限
      this.audioStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          sampleRate: 16000,
          channelCount: 1,
          echoCancellation: true,
          noiseSuppression: true
        }
      });

      // 创建音频上下文
      this.audioContext = new AudioContext({ sampleRate: 16000 });
      const source = this.audioContext.createMediaStreamSource(this.audioStream);
      
      // 创建音频处理器
      this.processor = this.audioContext.createScriptProcessor(4096, 1, 1);
      
      // 音频处理回调
      this.processor.onaudioprocess = (event) => {
        if (this.isListening) {
          const inputData = event.inputBuffer.getChannelData(0);
          this.processAudioChunk(inputData);
        }
      };

      source.connect(this.processor);
      this.processor.connect(this.audioContext.destination);
      
      // 启动VOSK识别
      await this.vosk.startRecognition();
      
      this.isListening = true;
      this.emit('start');
      
    } catch (error) {
      console.error('Error starting speech recognition:', error);
      throw error;
    }
  }

  // 停止监听
  public async stopListening(): Promise<void> {
    if (!this.isListening) {
      return;
    }

    try {
      // 停止VOSK识别
      await this.vosk.stopRecognition();

      // 清理音频资源
      if (this.processor) {
        this.processor.disconnect();
        this.processor = null;
      }

      if (this.audioContext) {
        await this.audioContext.close();
        this.audioContext = null;
      }

      if (this.audioStream) {
        this.audioStream.getTracks().forEach(track => track.stop());
        this.audioStream = null;
      }

      this.isListening = false;
      this.emit('stop');
      
    } catch (error) {
      console.error('Error stopping speech recognition:', error);
      throw error;
    }
  }

  // 处理音频数据块
  private async processAudioChunk(audioData: Float32Array): Promise<void> {
    try {
      // 使用VOSK处理音频数据
      const result: VoskResult = await this.vosk.processAudio(audioData);
      
      if (result.text && result.text.trim()) {
        const speechResult: SpeechRecognitionResult = {
          text: result.text,
          isFinal: result.isFinal,
          confidence: result.confidence,
          language: result.language
        };
        
        this.emit('result', speechResult);
      }
    } catch (error) {
      console.error('Error processing audio chunk:', error);
      this.emit('error', error);
    }
  }

  // 检查是否正在监听
  public get listening(): boolean {
    return this.isListening;
  }

  // 检查是否已初始化
  public get initialized(): boolean {
    return this.isInitialized;
  }

  // 清理资源
  public dispose(): void {
    if (this.isListening) {
      this.stopListening();
    }
    this.vosk.dispose();
    this.removeAllListeners();
  }
}