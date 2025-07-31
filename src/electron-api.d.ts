export interface IElectronAPI {
  ipcRenderer: {
    on: (channel: string, func: (...args: any[]) => void) => void;
    removeListener: (channel: string, func: (...args: any[]) => void) => void;
  };
}

declare global {
  interface Window {
    electronAPI: IElectronAPI;
  }
}