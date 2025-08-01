// 定义语音识别结果的类型
interface SpeechRecognitionResult {
  text: string;
  isFinal: boolean;
  confidence?: number;
  language?: string;
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
  };
}

declare global {
  interface Window {
    electronAPI: IElectronAPI;
  }
}