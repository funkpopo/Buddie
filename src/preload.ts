// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

import { contextBridge, ipcRenderer } from 'electron';

// 定义API接口以提高类型安全性
interface IElectronAPI {
  // 可以在这里添加IPC通信方法
  // 例如: sendMessage: (channel: string, data: any) => void;
}

// 创建安全的API桥接
const electronAPI: IElectronAPI = {
  // 实现API方法
};

// 将API安全地暴露给渲染进程
contextBridge.exposeInMainWorld('electronAPI', electronAPI);

// 性能优化：禁用不必要的功能
process.env.ELECTRON_DISABLE_SECURITY_WARNINGS = 'true';