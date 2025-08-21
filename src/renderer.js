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
document.addEventListener('DOMContentLoaded', async () => {
  const cardStack = document.getElementById('cardStack');
  let isDragging = false;
  let dragOffsetX, dragOffsetY;
  let isFlipping = false;
  let dragStartTime = 0;
  let hasActuallyDragged = false;
  
  // 动态卡片数据 - 根据模型配置生成
  let cardData = [];
  let currentIndex = 0;
  let cardColors = []; // 存储每张卡片的颜色索引
  
  // 默认卡片数据
  const defaultCardData = [
    { emoji: '🎵', title: '唱片 1', subtitle: 'Default Card', modelId: null },
    { emoji: '🎶', title: '唱片 2', subtitle: 'Default Card', modelId: null },
    { emoji: '🎼', title: '唱片 3', subtitle: 'Default Card', modelId: null },
    { emoji: '🎤', title: '唱片 4', subtitle: 'Default Card', modelId: null },
    { emoji: '🎧', title: '唱片 5', subtitle: 'Default Card', modelId: null }
  ];

  // 初始化卡片数据
  async function initializeCards() {
    try {
      const settings = await window.electronAPI.getSettings();
      const models = settings.models || [];
      
      // 根据模型配置生成卡片数据
      if (models.length > 0) {
        cardData = models.map((model, index) => ({
          emoji: getModelEmoji(index),
          title: model.name || `模型 ${index + 1}`,
          subtitle: model.modelName || 'AI Model',
          modelId: model.id
        }));
      } else {
        // 如果没有模型配置，使用默认卡片（这种情况应该不会出现，因为main.js确保了至少有一个模型配置）
        cardData = [{
          emoji: '🤖',
          title: '默认AI助手',
          subtitle: 'AI Model',
          modelId: null
        }];
      }
      
      // 重新生成HTML卡片
      generateCardElements();
      updateCards();
      
    } catch (error) {
      console.error('初始化卡片失败:', error);
      // 错误情况下使用默认卡片
      cardData = [{
        emoji: '🤖',
        title: '默认AI助手',
        subtitle: 'AI Model',
        modelId: null
      }];
      generateCardElements();
      updateCards();
    }
  }
  
  // 根据索引获取emoji
  function getModelEmoji(index) {
    const emojis = ['🤖', '🧠', '⚡', '🔮', '🎯', '🚀', '⭐', '💡', '🔥', '💎'];
    return emojis[index % emojis.length];
  }
  
  // 动态生成卡片元素
  function generateCardElements() {
    cardStack.innerHTML = '';
    const cardCount = cardData.length; // 不限制卡片数量，根据模型配置数量显示
    const totalColors = 20; // 总共有20种颜色样式
    
    // 重置颜色映射数组
    cardColors = [];
    
    // 生成不重复的颜色索引数组
    const usedColors = new Set();
    
    for (let i = 0; i < cardCount; i++) {
      let colorIndex;
      // 如果可用颜色数量足够，确保不重复
      if (cardCount <= totalColors) {
        do {
          colorIndex = Math.floor(Math.random() * totalColors);
        } while (usedColors.has(colorIndex));
        usedColors.add(colorIndex);
      } else {
        // 如果卡片数量超过颜色数量，允许重复但尽量分散
        colorIndex = Math.floor(Math.random() * totalColors);
      }
      cardColors.push(colorIndex);
    }
    
    for (let i = 0; i < cardCount; i++) {
      const cardElement = document.createElement('div');
      cardElement.className = `card color-${cardColors[i]}`;
      cardElement.innerHTML = `
        <h1></h1>
        <p></p>
      `;
      cardStack.appendChild(cardElement);
    }
  }

  // 初始化
  await initializeCards();
  
  // 监听来自设置页面的刷新卡片事件
  window.electronAPI.onRefreshCards(() => {
    console.log('收到刷新卡片请求');
    initializeCards();
  });
  
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
    
    // 如果是初始化，不需要移动索引
    if (cards.length === 0) return;
    
    // 移动第一张卡片到最后（仅在翻页时）
    if (isFlipping) {
      currentIndex = (currentIndex + 1) % cardData.length;
    }
    
    // 更新所有卡片的内容
    cards.forEach((card, index) => {
      const dataIndex = (currentIndex + index) % cardData.length;
      const data = cardData[dataIndex];
      
      const h1 = card.querySelector('h1');
      const p = card.querySelector('p');
      
      h1.textContent = `${data.emoji} ${data.title}`;
      p.textContent = data.subtitle;
      
      // 移除所有可能的颜色类
      for (let i = 0; i < 20; i++) {
        card.classList.remove(`color-${i}`);
      }
      // 添加对应数据索引的颜色类
      card.classList.add(`color-${cardColors[dataIndex]}`);
    });
    
    // 重置所有卡片的样式
    cards.forEach((card, index) => {
      card.style.animation = '';
      
      // 动态计算卡片样式，支持任意数量的卡片
      const zIndex = cards.length - index;
      const translateY = index * 3;
      const translateZ = index * -8;
      const rotateY = index * -2;
      const translateX = index * -2;
      const opacity = Math.max(0.3, 1 - index * 0.08); // 最小透明度0.3
      
      card.style.zIndex = zIndex.toString();
      card.style.transform = `translateY(${translateY}px) translateZ(${translateZ}px) rotateY(${rotateY}deg) translateX(${translateX}px)`;
      card.style.opacity = opacity.toString();
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
