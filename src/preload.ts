// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from 'electron';

// 定义语音识别结果的类型
interface SpeechRecognitionResult {
  text: string;
  isFinal: boolean;
  confidence?: number;
  language?: string;
}

// 定义API接口以提高类型安全性
interface IElectronAPI {
  // IPC通信方法
  ipcRenderer: {
    on: (channel: string, func: (...args: any[]) => void) => void;
    removeListener: (channel: string, func: (...args: any[]) => void) => void;
    invoke: (channel: string, ...args: any[]) => Promise<any>;
  };
  
  // 语音识别方法
  speechRecognition: {
    start: () => Promise<{ success: boolean; error?: string }>;
    stop: () => Promise<{ success: boolean; error?: string }>;
    onResult: (callback: (result: SpeechRecognitionResult) => void) => void;
    onError: (callback: (error: string) => void) => void;
  };
  
  // 系统方法
  system: {
    getModelCacheDir: () => Promise<string>;
  };
}

// 创建安全的API桥接
const electronAPI: IElectronAPI = {
  ipcRenderer: {
    on: (channel, func) => {
      ipcRenderer.on(channel, (event, ...args) => func(...args));
    },
    removeListener: (channel, func) => {
      ipcRenderer.removeListener(channel, func);
    },
    invoke: (channel, ...args) => ipcRenderer.invoke(channel, ...args)
  },
  speechRecognition: {
    start: () => ipcRenderer.invoke('start-speech-recognition'),
    stop: () => ipcRenderer.invoke('stop-speech-recognition'),
    onResult: (callback) => {
      ipcRenderer.on('speech-recognition-result', (event, result) => callback(result));
    },
    onError: (callback) => {
      ipcRenderer.on('speech-recognition-error', (event, error) => callback(error));
    }
  },
  system: {
    getModelCacheDir: () => ipcRenderer.invoke('get-model-cache-dir')
  }
};

// 将API安全地暴露给渲染进程
contextBridge.exposeInMainWorld('electronAPI', electronAPI);

// 性能优化：禁用不必要的功能
process.env.ELECTRON_DISABLE_SECURITY_WARNINGS = 'true';