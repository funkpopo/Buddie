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
      updateCardsContent();
      
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
      updateCardsContent();
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
    const totalColors = 40; // 总共有40种颜色样式
    
    // 重置颜色映射数组
    cardColors = [];
    
    // 颜色分组：按色系组织，确保相邻颜色过渡自然
    const colorGroups = [
      [0, 1, 2, 3, 4, 5, 6],      // 红色系到橙色系 (0-6)
      [7, 8, 9, 10, 11, 12],      // 橙色系到黄色系 (7-12)
      [13, 14, 15, 16, 17, 18],   // 黄绿色系 (13-18)
      [19, 20, 21, 22, 23, 24],   // 绿色系到青色系 (19-24)
      [25, 26, 27, 28, 29, 30],   // 青色系到蓝色系 (25-30)
      [31, 32, 33, 34, 35, 36],   // 蓝色系到紫色系 (31-36)
      [37, 38, 39, 40],   // 紫色系到粉色系 (37-42)
    ];
    
    if (cardCount <= totalColors) {
      // 如果卡片数量不多，优先选择连续的颜色组合
      let selectedColors = [];
      let currentGroupIndex = Math.floor(Math.random() * colorGroups.length);
      
      // 从随机色组开始，循环选择相邻的颜色
      for (let i = 0; i < cardCount; i++) {
        const currentGroup = colorGroups[currentGroupIndex % colorGroups.length];
        const colorInGroup = currentGroup[i % currentGroup.length];
        selectedColors.push(colorInGroup);
        
        // 当前组的颜色用完后，移动到下一个相邻的色组
        if ((i + 1) % currentGroup.length === 0) {
          currentGroupIndex++;
        }
      }
      
      cardColors = selectedColors;
    } else {
      // 卡片数量多时，使用更智能的分布算法
      const usedColors = new Set();
      for (let i = 0; i < cardCount; i++) {
        let colorIndex;
        // 优先从未使用的相邻颜色中选择
        if (i > 0) {
          const lastColor = cardColors[i - 1];
          const nearbyColors = [];
          
          // 查找相邻的颜色（±1，±2范围内）
          for (let offset = -2; offset <= 2; offset++) {
            const nearby = (lastColor + offset + totalColors) % totalColors;
            if (!usedColors.has(nearby) && nearby !== lastColor) {
              nearbyColors.push(nearby);
            }
          }
          
          if (nearbyColors.length > 0) {
            colorIndex = nearbyColors[Math.floor(Math.random() * nearbyColors.length)];
          } else {
            colorIndex = Math.floor(Math.random() * totalColors);
          }
        } else {
          colorIndex = Math.floor(Math.random() * totalColors);
        }
        
        usedColors.add(colorIndex);
        cardColors.push(colorIndex);
      }
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
  
  // 优化的拖拽处理，使用requestAnimationFrame
  let dragAnimationFrame = null;
  let pendingDragUpdate = null;

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
        
        // 使用requestAnimationFrame优化拖拽性能
        if (dragAnimationFrame) {
          cancelAnimationFrame(dragAnimationFrame);
        }
        
        pendingDragUpdate = { x: newX, y: newY };
        
        dragAnimationFrame = requestAnimationFrame(() => {
          if (pendingDragUpdate) {
            window.electronAPI.dragWindow(pendingDragUpdate);
            pendingDragUpdate = null;
          }
          dragAnimationFrame = null;
        });
      }
    }
  });

  // 鼠标释放事件
  document.addEventListener('mouseup', () => {
    isDragging = false;
    cardStack.style.cursor = 'move';
    
    // 清除拖拽动画帧
    if (dragAnimationFrame) {
      cancelAnimationFrame(dragAnimationFrame);
      dragAnimationFrame = null;
      pendingDragUpdate = null;
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
  
  // 翻页函数 - 重新设计
  function flipCard() {
    if (isFlipping) return;
    
    isFlipping = true;
    const cards = cardStack.querySelectorAll('.card');
    if (cards.length === 0) return;
    
    // 阶段1：准备动画 - 预设下一张卡片内容
    const nextIndex = (currentIndex + 1) % cardData.length;
    const nextData = cardData[nextIndex];
    
    // 为所有卡片添加动画标记
    cards.forEach(card => card.classList.add('animating'));
    
    // 阶段2：设置动画类和样式
    cards.forEach((card, index) => {
      if (index === 0) {
        // 第一张卡片执行翻出动画
        card.classList.add('flip-out');
      } else {
        // 其他卡片执行向前移动动画
        card.classList.add('move-forward');
        
        // 计算目标位置（向前移动一个位置）
        const newIndex = index - 1;
        const targetZIndex = cards.length - newIndex;
        const targetTranslateY = newIndex * 3;
        const targetTranslateZ = newIndex * -8;
        const targetRotateY = newIndex * -2;
        const targetTranslateX = newIndex * -2;
        const targetOpacity = Math.max(0.3, 1 - newIndex * 0.08);
        
        // 设置CSS自定义属性用于动画
        card.style.setProperty('--start-transform', card.style.transform || 'translateY(0px) translateZ(0px) rotateY(0deg) translateX(0px)');
        card.style.setProperty('--start-opacity', card.style.opacity || '1');
        card.style.setProperty('--end-transform', `translateY(${targetTranslateY}px) translateZ(${targetTranslateZ}px) rotateY(${targetRotateY}deg) translateX(${targetTranslateX}px)`);
        card.style.setProperty('--end-opacity', targetOpacity.toString());
      }
    });
    
    // 阶段3：启动动画
    cardStack.classList.add('flipping');
    
    // 阶段4：动画完成后处理
    setTimeout(() => {
      // 移动第一张卡片到末尾
      const firstCard = cards[0];
      cardStack.appendChild(firstCard);
      
      // 更新当前索引
      currentIndex = nextIndex;
      
      // 更新所有卡片内容和样式
      updateCardsContent();
      
      // 清理动画状态
      cleanupAnimation();
      
      isFlipping = false;
    }, 400); // 动画时长400ms，与CSS保持一致
  }
  
  // 更新卡片内容 - 新函数
  function updateCardsContent() {
    const cards = cardStack.querySelectorAll('.card');
    
    cards.forEach((card, index) => {
      const dataIndex = (currentIndex + index) % cardData.length;
      const data = cardData[dataIndex];
      
      const h1 = card.querySelector('h1');
      const p = card.querySelector('p');
      
      h1.textContent = `${data.emoji} ${data.title}`;
      p.textContent = data.subtitle;
      
      // 更新颜色类
      for (let i = 0; i < 50; i++) {
        card.classList.remove(`color-${i}`);
      }
      card.classList.add(`color-${cardColors[dataIndex]}`);
      
      // 设置正确的静态位置和样式
      const zIndex = cards.length - index;
      const translateY = index * 3;
      const translateZ = index * -8;
      const rotateY = index * -2;
      const translateX = index * -2;
      const opacity = Math.max(0.3, 1 - index * 0.08);
      
      card.style.zIndex = zIndex.toString();
      card.style.transform = `translateY(${translateY}px) translateZ(${translateZ}px) rotateY(${rotateY}deg) translateX(${translateX}px)`;
      card.style.opacity = opacity.toString();
    });
  }
  
  // 清理动画状态 - 新函数
  function cleanupAnimation() {
    const cards = cardStack.querySelectorAll('.card');
    
    cards.forEach(card => {
      // 移除动画类
      card.classList.remove('animating', 'flip-out', 'move-forward');
      
      // 清理CSS自定义属性
      card.style.removeProperty('--start-transform');
      card.style.removeProperty('--start-opacity');
      card.style.removeProperty('--end-transform');
      card.style.removeProperty('--end-opacity');
      
      // 强制重新计算样式
      card.offsetHeight;
    });
    
    // 移除容器的翻页状态
    cardStack.classList.remove('flipping');
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
