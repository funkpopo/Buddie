// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from 'electron';

// 定义API接口以提高类型安全性
interface IElectronAPI {
  // IPC通信方法
  ipcRenderer: {
    on: (channel: string, func: (...args: any[]) => void) => void;
    removeListener: (channel: string, func: (...args: any[]) => void) => void;
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
    }
  }
};

// 将API安全地暴露给渲染进程
contextBridge.exposeInMainWorld('electronAPI', electronAPI);

// 性能优化：禁用不必要的功能
process.env.ELECTRON_DISABLE_SECURITY_WARNINGS = 'true';