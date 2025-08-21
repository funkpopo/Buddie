/**
 * This file will automatically be loaded by webpack and run in the "renderer" context.
 * To learn more about the differences between the "main" and the "renderer" context in
 * Electron, visit:
 *
 * https://electronjs.org/docs/tutorial/process-model
 *
 * By default, Node.js integration in this file is disabled. When enabling Node.js integration
 * in a renderer process, please be aware of potential security implications. You can read
 * more about security risks here:
 *
 * https://electronjs.org/docs/tutorial/security
 *
 * To enable Node.js integration in this file, open up `main.js` and enable the `nodeIntegration`
 * flag:
 *
 * ```
 *  // Create the browser window.
 *  mainWindow = new BrowserWindow({
 *    width: 800,
 *    height: 600,
 *    webPreferences: {
 *      nodeIntegration: true
 *    }
 *  });
 * ```
 */

import './index.css';

console.log('👋 This message is being logged by "renderer.js", included via webpack');

// 添加拖动功能
document.addEventListener('DOMContentLoaded', () => {
  const circle = document.getElementById('circle');
  let isDragging = false;
  let dragOffsetX, dragOffsetY;
  
  // 节流函数，限制IPC调用频率
  let throttleTimer = null;
  const throttleDelay = 16; // 约60FPS

  // 鼠标按下事件
  circle.addEventListener('mousedown', async (e) => {
    isDragging = true;
    // 获取当前窗口的位置
    const windowPosition = await window.electronAPI.getWindowPosition();
    
    // 计算拖动偏移量（鼠标位置与窗口位置的差值）
    dragOffsetX = e.screenX - windowPosition[0];
    dragOffsetY = e.screenY - windowPosition[1];
    circle.style.cursor = 'grabbing';
    e.preventDefault(); // 防止默认行为
    e.stopPropagation(); // 阻止事件冒泡
  });

  // 鼠标移动事件
  document.addEventListener('mousemove', (e) => {
    if (isDragging) {
      // 根据拖动偏移量计算窗口新位置
      const newX = e.screenX - dragOffsetX;
      const newY = e.screenY - dragOffsetY;
      
      // 使用节流技术减少IPC调用频率
      if (!throttleTimer) {
        window.electronAPI.dragWindow({ x: newX, y: newY });
        throttleTimer = setTimeout(() => {
          throttleTimer = null;
        }, throttleDelay);
      }
    }
  });

  // 鼠标释放事件
  document.addEventListener('mouseup', () => {
    isDragging = false;
    circle.style.cursor = 'grab';
    
    // 清除节流定时器
    if (throttleTimer) {
      clearTimeout(throttleTimer);
      throttleTimer = null;
    }
  });
  
  // 双击关闭应用
  circle.addEventListener('dblclick', () => {
    window.close();
  });
});

if (module.hot) {
  module.hot.accept();
}
