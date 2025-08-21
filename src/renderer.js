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

// 添加拖动功能和卡片翻动功能
document.addEventListener('DOMContentLoaded', () => {
  const cardStack = document.getElementById('cardStack');
  let isDragging = false;
  let dragOffsetX, dragOffsetY;
  let isFlipping = false;
  let dragStartTime = 0;
  let hasActuallyDragged = false;
  
  // 卡片数据用于无限循环
  const cardData = [
    { emoji: '🎵', title: '唱片 1', subtitle: 'Card One' },
    { emoji: '🎶', title: '唱片 2', subtitle: 'Card Two' },
    { emoji: '🎼', title: '唱片 3', subtitle: 'Card Three' },
    { emoji: '🎤', title: '唱片 4', subtitle: 'Card Four' },
    { emoji: '🎧', title: '唱片 5', subtitle: 'Card Five' }
  ];
  
  let currentIndex = 0;
  
  // 节流函数，限制IPC调用频率
  let throttleTimer = null;
  const throttleDelay = 16; // 约60FPS

  // 鼠标按下事件
  cardStack.addEventListener('mousedown', async (e) => {
    if (isFlipping) return; // 翻页动画中不允许拖拽
    
    isDragging = true;
    dragStartTime = Date.now();
    hasActuallyDragged = false;
    
    // 获取当前窗口的位置
    const windowPosition = await window.electronAPI.getWindowPosition();
    
    // 计算拖动偏移量（鼠标位置与窗口位置的差值）
    dragOffsetX = e.screenX - windowPosition[0];
    dragOffsetY = e.screenY - windowPosition[1];
    cardStack.style.cursor = 'grabbing';
    e.preventDefault(); // 防止默认行为
    e.stopPropagation(); // 阻止事件冒泡
  });

  // 鼠标移动事件
  document.addEventListener('mousemove', (e) => {
    if (isDragging && !isFlipping) {
      hasActuallyDragged = true; // 标记已经进行了实际拖动
      
      // 根据拖动偏移量计算窗口新位置
      const newX = e.screenX - dragOffsetX;
      const newY = e.screenY - dragOffsetY;
      
      // 验证坐标是否为有效数字
      if (typeof newX === 'number' && typeof newY === 'number' && 
          isFinite(newX) && isFinite(newY)) {
        // 使用节流技术减少IPC调用频率
        if (!throttleTimer) {
          window.electronAPI.dragWindow({ x: newX, y: newY });
          throttleTimer = setTimeout(() => {
            throttleTimer = null;
          }, throttleDelay);
        }
      }
    }
  });

  // 鼠标释放事件
  document.addEventListener('mouseup', () => {
    isDragging = false;
    cardStack.style.cursor = 'move';
    
    // 清除节流定时器
    if (throttleTimer) {
      clearTimeout(throttleTimer);
      throttleTimer = null;
    }
  });
  
  // 单击翻页功能 - 增加更严格的检查
  cardStack.addEventListener('click', (e) => {
    // 如果正在拖拽、翻页中，或者刚刚完成了拖动，则不触发翻页
    if (isDragging || isFlipping || hasActuallyDragged) return;
    
    // 检查点击时间，如果距离mousedown事件太短且有移动，可能是拖动意图
    const clickDuration = Date.now() - dragStartTime;
    if (clickDuration < 200 && hasActuallyDragged) return;
    
    e.preventDefault();
    e.stopPropagation();
    flipCard();
  });
  
  // 双击关闭应用
  cardStack.addEventListener('dblclick', (e) => {
    // 双击关闭不受拖动限制
    e.preventDefault();
    e.stopPropagation();
    window.close();
  });
  
  // 翻页函数
  function flipCard() {
    if (isFlipping) return;
    
    isFlipping = true;
    cardStack.classList.add('flipping');
    
    // 0.5秒后更新卡片内容并重置样式
    setTimeout(() => {
      updateCards();
      cardStack.classList.remove('flipping');
      isFlipping = false;
    }, 500);
  }
  
  // 更新卡片内容（无限循环）
  function updateCards() {
    const cards = cardStack.querySelectorAll('.card');
    
    // 移动第一张卡片到最后
    currentIndex = (currentIndex + 1) % cardData.length;
    
    // 更新所有卡片的内容
    cards.forEach((card, index) => {
      const dataIndex = (currentIndex + index) % cardData.length;
      const data = cardData[dataIndex];
      
      const h1 = card.querySelector('h1');
      const p = card.querySelector('p');
      
      h1.textContent = `${data.emoji} ${data.title}`;
      p.textContent = data.subtitle;
      
      // 移除所有颜色类
      card.classList.remove('color-0', 'color-1', 'color-2', 'color-3', 'color-4');
      // 添加对应数据索引的颜色类
      card.classList.add(`color-${dataIndex}`);
    });
    
    // 重置所有卡片的样式
    cards.forEach((card, index) => {
      card.style.animation = '';
      
      // 根据位置设置不同的样式 - 与CSS保持一致
      switch(index) {
        case 0:
          card.style.zIndex = '5';
          card.style.transform = 'translateY(0px) translateZ(0px) rotateY(0deg)';
          card.style.opacity = '1';
          break;
        case 1:
          card.style.zIndex = '4';
          card.style.transform = 'translateY(3px) translateZ(-8px) rotateY(-2deg) translateX(-2px)';
          card.style.opacity = '0.92';
          break;
        case 2:
          card.style.zIndex = '3';
          card.style.transform = 'translateY(6px) translateZ(-16px) rotateY(-4deg) translateX(-4px)';
          card.style.opacity = '0.84';
          break;
        case 3:
          card.style.zIndex = '2';
          card.style.transform = 'translateY(9px) translateZ(-24px) rotateY(-6deg) translateX(-6px)';
          card.style.opacity = '0.76';
          break;
        case 4:
          card.style.zIndex = '1';
          card.style.transform = 'translateY(12px) translateZ(-32px) rotateY(-8deg) translateX(-8px)';
          card.style.opacity = '0.68';
          break;
      }
    });
  }
  
  // 键盘快捷键支持 - 不受拖动状态影响
  document.addEventListener('keydown', (e) => {
    if (e.key === ' ' || e.key === 'Enter') { // 空格键或回车键翻页
      e.preventDefault();
      flipCard();
    }
  });
  
  // 鼠标滚轮翻页 - 不受拖动状态影响
  cardStack.addEventListener('wheel', (e) => {
    e.preventDefault();
    if (Math.abs(e.deltaY) > 30 && !isFlipping) { // 滚轮阈值
      flipCard();
    }
  });
  
  // 监听鼠标离开窗口区域，重置拖动状态
  document.addEventListener('mouseleave', () => {
    // 延迟重置，避免快速移动时的误判
    setTimeout(() => {
      hasActuallyDragged = false;
    }, 100);
  });
});

if (module.hot) {
  module.hot.accept();
}
