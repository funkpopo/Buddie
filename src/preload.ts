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

// 定义代理配置类型
interface ProxyConfig {
  enabled: boolean;
  useSystemProxy: boolean;
}

// 定义API接口以提高类型安全性
interface IElectronAPI {
  // IPC通信方法
  ipcRenderer: {
    on: (channel: string, func: (...args: any[]) => void) => void;
    removeListener: (channel: string, func: (...args: any[]) => void) => void;
    invoke: (channel: string, ...args: any[]) => Promise<any>;
  };
  
  // 页面导航方法
  navigation: {
    navigateToPage: (page: string) => Promise<void>;
  };
  
  // 语音识别方法
  speechRecognition: {
    start: () => Promise<{ success: boolean; error?: string }>;
    stop: () => Promise<{ success: boolean; error?: string }>;
    downloadWhisperTinyONNX: () => Promise<{ success: boolean; error?: string }>;
    downloadWhisperTinyENONNX: () => Promise<{ success: boolean; error?: string }>;
    downloadWhisperBaseENONNX: () => Promise<{ success: boolean; error?: string }>;
    downloadWhisperBaseONNX: () => Promise<{ success: boolean; error?: string }>;
    verifyModelFiles: () => Promise<{ success: boolean; downloadedModels?: string[] }>;
    refreshModelStatus: () => Promise<{ success: boolean; downloadedModels?: string[] }>;
    saveModelStatus: (statusData: any) => Promise<{ success: boolean; error?: string }>;
    onResult: (callback: (result: SpeechRecognitionResult) => void) => void;
    onError: (callback: (error: string) => void) => void;
  };
  
  // 系统方法
  system: {
    getModelsRootDir: () => Promise<string>;
    joinPath: (base: string, ...paths: string[]) => Promise<string>;
  };
  
  // 代理方法
  proxy: {
    getConfig: () => Promise<ProxyConfig>;
    setConfig: (config: ProxyConfig) => Promise<{ success: boolean; error?: string }>;
    testConnection: () => Promise<{ success: boolean; error?: string; statusCode?: number }>;
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
  navigation: {
    navigateToPage: (page: string) => ipcRenderer.invoke('navigate-to-page', page)
  },
  speechRecognition: {
    start: () => ipcRenderer.invoke('start-speech-recognition'),
    stop: () => ipcRenderer.invoke('stop-speech-recognition'),
    downloadWhisperTinyONNX: () => ipcRenderer.invoke('download-whisper-tiny-onnx'),
    downloadWhisperTinyENONNX: () => ipcRenderer.invoke('download-whisper-tiny-en-onnx'),
    downloadWhisperBaseENONNX: () => ipcRenderer.invoke('download-whisper-base-en-onnx'),
    downloadWhisperBaseONNX: () => ipcRenderer.invoke('download-whisper-base-onnx'),
    verifyModelFiles: () => ipcRenderer.invoke('verify-model-files'),
    refreshModelStatus: () => ipcRenderer.invoke('refresh-model-status'),
    saveModelStatus: (statusData) => ipcRenderer.invoke('save-model-status', statusData),
    onResult: (callback) => {
      ipcRenderer.on('speech-recognition-result', (event, result) => callback(result));
    },
    onError: (callback) => {
      ipcRenderer.on('speech-recognition-error', (event, error) => callback(error));
    }
  },
  system: {
    getModelsRootDir: () => ipcRenderer.invoke('get-models-root-dir'),
    joinPath: (base: string, ...paths: string[]) => ipcRenderer.invoke('join-path', base, ...paths)
  },
  proxy: {
    getConfig: () => ipcRenderer.invoke('get-proxy-config'),
    setConfig: (config) => ipcRenderer.invoke('set-proxy-config', config),
    testConnection: () => ipcRenderer.invoke('test-proxy-connection')
  }
};

// 将API安全地暴露给渲染进程
contextBridge.exposeInMainWorld('electronAPI', electronAPI);

// 性能优化：禁用不必要的功能
process.env.ELECTRON_DISABLE_SECURITY_WARNINGS = 'true';