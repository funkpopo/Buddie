// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

const { contextBridge, ipcRenderer } = require('electron');

// 安全地暴露 IPC 方法给渲染进程
contextBridge.exposeInMainWorld('electronAPI', {
  dragWindow: (position) => ipcRenderer.send('drag-window', position),
  getWindowPosition: () => ipcRenderer.invoke('get-window-position')
});
