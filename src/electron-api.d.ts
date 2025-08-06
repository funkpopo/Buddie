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

export interface IElectronAPI {
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
    // 模型下载方法
    downloadWhisperTinyONNX: () => Promise<{ success: boolean; error?: string }>;
    downloadWhisperTinyENONNX: () => Promise<{ success: boolean; error?: string }>;
    downloadWhisperBaseENONNX: () => Promise<{ success: boolean; error?: string }>;
    downloadWhisperBaseONNX: () => Promise<{ success: boolean; error?: string }>;
    verifyModelFiles: () => Promise<{ success: boolean; downloadedModels?: string[] }>;
    refreshModelStatus: () => Promise<{ success: boolean; downloadedModels?: string[]; error?: string }>;
    saveModelStatus: (statusData: any) => Promise<{ success: boolean; error?: string }>;
  };
  
  // 系统方法
  system: {
    getModelsRootDir: () => Promise<string>;
  };
  
  // 代理方法
  proxy: {
    getConfig: () => Promise<ProxyConfig>;
    setConfig: (config: ProxyConfig) => Promise<{ success: boolean; error?: string }>;
    testConnection: () => Promise<{ success: boolean; error?: string; statusCode?: number }>;
  };
}

declare global {
  interface Window {
    electronAPI: IElectronAPI;
  }
}