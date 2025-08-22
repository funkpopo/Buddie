// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

const { contextBridge, ipcRenderer } = require('electron');

// 安全地暴露 IPC 方法给渲染进程
contextBridge.exposeInMainWorld('electronAPI', {
  dragWindow: (position) => ipcRenderer.send('drag-window', position),
  getWindowPosition: () => ipcRenderer.invoke('get-window-position'),
  getSettings: () => ipcRenderer.invoke('get-settings'),
  saveSettings: (settings) => ipcRenderer.invoke('save-settings', settings),
  saveCurrentCard: (cardIndex) => ipcRenderer.invoke('save-current-card', cardIndex),
  triggerCardSwitch: (direction) => ipcRenderer.invoke('trigger-card-switch', direction),
  refreshCards: () => ipcRenderer.send('refresh-cards'),
  onRefreshCards: (callback) => {
    ipcRenderer.on('refresh-cards', callback);
    return () => ipcRenderer.removeListener('refresh-cards', callback);
  },
  // 对话相关API
  sendChatMessage: (data) => ipcRenderer.invoke('send-chat-message', data),
  showChatInterface: (cardData) => ipcRenderer.invoke('show-chat-interface', cardData),
  // 监听流式响应事件
  onChatStreamChunk: (callback) => {
    ipcRenderer.on('chat-stream-chunk', callback);
    return () => ipcRenderer.removeListener('chat-stream-chunk', callback);
  },
  onChatStreamEnd: (callback) => {
    ipcRenderer.on('chat-stream-end', callback);
    return () => ipcRenderer.removeListener('chat-stream-end', callback);
  },
  onChatStreamError: (callback) => {
    ipcRenderer.on('chat-stream-error', callback);
    return () => ipcRenderer.removeListener('chat-stream-error', callback);
  },
  // 卡片切换同步API
  onCardIndexChange: (callback) => {
    ipcRenderer.on('card-index-changed', callback);
    return () => ipcRenderer.removeListener('card-index-changed', callback);
  },
  onTriggerCardSwitch: (callback) => {
    ipcRenderer.on('trigger-card-switch', callback);
    return () => ipcRenderer.removeListener('trigger-card-switch', callback);
  },
  // 监听卡片切换事件（用于聊天窗口同步）
  onCardSwitched: (callback) => {
    ipcRenderer.on('card-switched', callback);
    return () => ipcRenderer.removeListener('card-switched', callback);
  }
});
