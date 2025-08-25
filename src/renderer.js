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

// 面板管理函数 - 声明为全局函数
window.showPanel = function(panelType) {
  const overlay = document.getElementById('panelOverlay');
  const panel = document.getElementById(panelType + 'Panel');
  
  if (overlay && panel) {
    overlay.classList.add('show');
    panel.classList.add('show');
    
    // 加载面板内容
    if (panelType === 'settings') {
      loadSettingsContent();
    } else if (panelType === 'chat') {
      loadChatContent();
    }
  }
};

window.closePanel = function(panelType) {
  const overlay = document.getElementById('panelOverlay');
  const panel = document.getElementById(panelType + 'Panel');
  
  if (overlay && panel) {
    overlay.classList.remove('show');
    panel.classList.remove('show');
  }
};

async function loadSettingsContent() {
  const settingsContent = document.getElementById('settingsContent');
  if (!settingsContent) return;
  
  try {
    const settings = await window.electronAPI.getSettings();
    
    settingsContent.innerHTML = `
      <div class="settings-nav">
        <button class="nav-btn active" onclick="showSettingsPage('general')">通用</button>
        <button class="nav-btn" onclick="showSettingsPage('models')">模型配置</button>
      </div>
      
      <div class="settings-content">
        <div id="general-page" class="settings-page active">
          <div class="setting-item">
            <label>主题</label>
            <select id="theme">
              <option value="auto" ${settings.theme === 'auto' ? 'selected' : ''}>跟随系统</option>
              <option value="light" ${settings.theme === 'light' ? 'selected' : ''}>浅色</option>
              <option value="dark" ${settings.theme === 'dark' ? 'selected' : ''}>深色</option>
            </select>
          </div>

          <div class="setting-item">
            <label>悬浮窗透明度</label>
            <div class="opacity-control">
              <input type="range" id="opacity" min="0.3" max="1" step="0.1" value="${settings.opacity || 1}">
              <span id="opacityValue">${Math.round((settings.opacity || 1) * 100)}%</span>
            </div>
          </div>

          <div class="setting-item">
            <label class="checkbox-label">
              <input type="checkbox" id="autoStart" ${settings.autoStart ? 'checked' : ''}>
              开机自启动
            </label>
          </div>

          <div class="setting-item">
            <label class="checkbox-label">
              <input type="checkbox" id="alwaysOnTop" ${settings.alwaysOnTop ? 'checked' : ''}>
              始终置顶
            </label>
          </div>
        </div>
        
        <div id="models-page" class="settings-page" style="display: none;">
          <div id="modelsList">
            <!-- 模型配置将在这里动态生成 -->
          </div>
          <button class="add-model-btn" onclick="addNewModel()">+ 添加新模型</button>
        </div>
      </div>
    `;
    
    // 添加事件监听器
    setupSettingsEvents();
    
  } catch (error) {
    console.error('加载设置失败:', error);
    settingsContent.innerHTML = '<p>加载设置失败</p>';
  }
}

function loadChatContent() {
  const chatContent = document.getElementById('chatContent');
  if (!chatContent) return;
  
  chatContent.innerHTML = `
    <div class="chat-messages" id="chatMessages">
      <div class="welcome-message">
        <div class="message-bubble system-message">
          <p>你好！我是你的AI助手，有什么可以帮助你的吗？</p>
        </div>
      </div>
    </div>
    <div class="chat-input-area">
      <div class="chat-input-container">
        <textarea class="chat-input" id="chatInput" placeholder="输入你的消息...按Enter发送，Shift+Enter换行" rows="1"></textarea>
        <button class="chat-send-btn" id="chatSendBtn" title="发送消息">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="22" y1="2" x2="11" y2="13"></line>
            <polygon points="22,2 15,22 11,13 2,9"></polygon>
          </svg>
        </button>
      </div>
    </div>
  `;
  
  // 添加聊天事件监听器
  setupChatEvents();
}

function setupSettingsEvents() {
  // 透明度滑块
  const opacitySlider = document.getElementById('opacity');
  const opacityValue = document.getElementById('opacityValue');
  
  if (opacitySlider && opacityValue) {
    opacitySlider.addEventListener('input', (e) => {
      const value = parseFloat(e.target.value);
      opacityValue.textContent = Math.round(value * 100) + '%';
      window.electronAPI.saveSettings({ opacity: value });
    });
  }
  
  // 其他设置事件监听器...
}

function setupChatEvents() {
  const chatInput = document.getElementById('chatInput');
  const chatSendBtn = document.getElementById('chatSendBtn');
  
  if (chatSendBtn) {
    chatSendBtn.addEventListener('click', () => {
      sendMessage();
    });
  }
  
  if (chatInput) {
    chatInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        sendMessage();
      }
    });
  }
}

function sendMessage() {
  const chatInput = document.getElementById('chatInput');
  const message = chatInput.value.trim();
  
  if (message) {
    // 添加用户消息到聊天界面
    addMessageToChat('user', message);
    chatInput.value = '';
    
    // 发送消息给AI
    window.electronAPI.sendChatMessage(message);
  }
}

function addMessageToChat(role, content) {
  const chatMessages = document.getElementById('chatMessages');
  const messageDiv = document.createElement('div');
  messageDiv.className = `message-bubble ${role}-message`;
  messageDiv.innerHTML = `<p>${content}</p>`;
  chatMessages.appendChild(messageDiv);
  chatMessages.scrollTop = chatMessages.scrollHeight;
}

// 添加拖动功能和卡片翻动功能
document.addEventListener('DOMContentLoaded', async () => {
  const cardStack = document.getElementById('cardStack');
  
  if (!cardStack) {
    console.error('未找到 cardStack 元素，拖动功能无法初始化');
    return;
  }
  
  console.log('cardStack 元素找到，初始化拖动功能');
  
  let isDragging = false;
  let dragOffsetX, dragOffsetY;
  let isFlipping = false;
  let dragStartTime = 0;
  let hasActuallyDragged = false;
  
  // 双击检测变量
  let lastClickTime = 0;
  let clickTimeout = null;
  
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
      
      console.log('初始化卡片数据 - 设置信息:', { 
        modelsCount: models.length, 
        models: models.map(m => ({ name: m.name, id: m.id, modelName: m.modelName }))
      });
      
      // 根据模型配置生成卡片数据
      if (models.length > 0) {
        cardData = models.map((model, index) => ({
          emoji: getModelEmoji(index),
          title: model.name || `模型 ${index + 1}`,
          subtitle: model.modelName || 'AI Model',
          modelId: model.id,
          isMultimodal: model.isMultimodal || false
        }));
        console.log('生成的卡片数据:', cardData);
      } else {
        // 如果没有模型配置，使用默认卡片（这种情况应该不会出现，因为main.js确保了至少有一个模型配置）
        cardData = [{
          emoji: '🤖',
          title: '默认AI助手',
          subtitle: 'AI Model',
          modelId: null,
          isMultimodal: false
        }];
        console.log('使用默认卡片数据（没有找到模型配置）');
      }
      
      // 从设置中恢复当前卡片索引
      if (settings.currentCard !== undefined && settings.currentCard >= 0 && settings.currentCard < cardData.length) {
        currentIndex = settings.currentCard;
      } else {
        currentIndex = 0;
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
        modelId: null,
        isMultimodal: false
      }];
      currentIndex = 0;
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
  
  // 监听来自设置页面的卡片切换触发事件
  window.electronAPI.onTriggerCardSwitch((event, direction) => {
    console.log('收到卡片切换触发请求:', direction);
    if (!isFlipping) {
      flipCard(direction);
    }
  });
  
  // 优化的拖拽处理，使用requestAnimationFrame
  let dragAnimationFrame = null;
  let pendingDragUpdate = null;

  // 鼠标按下事件
  cardStack.addEventListener('mousedown', async (e) => {
    console.log('鼠标按下事件触发', { isFlipping, button: e.button });
    
    if (isFlipping) {
      console.log('翻页动画中，不允许拖拽');
      return; // 翻页动画中不允许拖拽
    }
    
    isDragging = true;
    dragStartTime = Date.now();
    hasActuallyDragged = false;
    
    console.log('开始拖拽，获取窗口位置...');
    
    // 获取当前窗口的位置
    const windowPosition = await window.electronAPI.getWindowPosition();
    console.log('窗口位置:', windowPosition);
    
    // 计算拖动偏移量（鼠标位置与窗口位置的差值）
    dragOffsetX = e.screenX - windowPosition.x;
    dragOffsetY = e.screenY - windowPosition.y;
    
    console.log('拖拽偏移量:', { dragOffsetX, dragOffsetY, screenX: e.screenX, screenY: e.screenY });
    
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
      
      console.log('拖拽移动:', { screenX: e.screenX, screenY: e.screenY, newX, newY, dragOffsetX, dragOffsetY });
      
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
            console.log('发送拖拽位置更新:', pendingDragUpdate);
            window.electronAPI.dragWindow(pendingDragUpdate);
            pendingDragUpdate = null;
          }
          dragAnimationFrame = null;
        });
      } else {
        console.warn('无效的拖拽坐标:', { newX, newY });
      }
    }
  });

  // 鼠标释放事件
  document.addEventListener('mouseup', (e) => {
    const wasDragging = isDragging;
    const hadActuallyDragged = hasActuallyDragged;
    
    console.log('鼠标释放事件', { wasDragging, hadActuallyDragged });
    
    isDragging = false;
    cardStack.style.cursor = 'move';
    
    // 清除拖拽动画帧
    if (dragAnimationFrame) {
      cancelAnimationFrame(dragAnimationFrame);
      dragAnimationFrame = null;
      pendingDragUpdate = null;
      console.log('清除拖拽动画帧');
    }
    
    // 如果没有实际拖动，检测双击
    if (wasDragging && !hadActuallyDragged && e.target.closest('.card')) {
      const currentTime = Date.now();
      const timeSinceLastClick = currentTime - lastClickTime;
      
      console.log('点击检测:', { currentTime, lastClickTime, timeSinceLastClick });
      
      if (timeSinceLastClick < 400) { 
        // 双击事件（移除了 lastClickTime > 0 的条件）
        console.log('检测到双击');
        if (clickTimeout) {
          clearTimeout(clickTimeout);
          clickTimeout = null;
        }
        handleCardDoubleClick(e);
        lastClickTime = 0; // 重置，避免连续触发
      } else {
        // 第一次点击或超时的点击
        console.log('第一次点击或超时点击');
        lastClickTime = currentTime;
      }
    }
  });
  
  
  
  // 翻页函数 - 重新设计，支持方向参数
  function flipCard(direction = 'next') {
    if (isFlipping) return;
    
    isFlipping = true;
    const cards = cardStack.querySelectorAll('.card');
    if (cards.length === 0) return;
    
    let nextIndex;
    if (direction === 'next') {
      // 下一张卡片
      nextIndex = (currentIndex + 1) % cardData.length;
    } else if (direction === 'prev') {
      // 上一张卡片
      nextIndex = (currentIndex - 1 + cardData.length) % cardData.length;
    }
    
    const nextData = cardData[nextIndex];
    
    // 为所有卡片添加动画标记
    cards.forEach(card => card.classList.add('animating'));
    
    if (direction === 'next') {
      // 向前翻页逻辑（原有逻辑）
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
      
      // 启动动画
      cardStack.classList.add('flipping');
      
      // 动画完成后处理
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
        
        // 保存当前卡片索引
        setTimeout(saveCurrentCardIndex, 50);
      }, 400);
    } else if (direction === 'prev') {
      // 向后翻页逻辑 - 与向前翻页相反但视觉一致
      cards.forEach((card, index) => {
        if (index === 0) {
          // 第一张卡片执行翻出动画（反向）
          card.classList.add('flip-out-reverse');
        } else {
          // 其他卡片执行向后移动动画
          card.classList.add('move-backward');
          
          // 计算目标位置（向后移动一个位置）
          const newIndex = index + 1;
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
      
      // 启动动画
      cardStack.classList.add('flipping');
      
      // 动画完成后处理
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
        
        // 保存当前卡片索引
        setTimeout(saveCurrentCardIndex, 50);
      }, 400);
    }
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
      card.classList.remove('animating', 'flip-out', 'flip-out-reverse', 'flip-in', 'move-forward', 'move-backward');
      
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
  
  // 导航按钮事件监听器
  const prevBtn = document.getElementById('prevBtn');
  const nextBtn = document.getElementById('nextBtn');
  
  if (prevBtn) {
    prevBtn.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      if (!isFlipping) {
        flipCard('prev');
      }
    });
  }
  
  if (nextBtn) {
    nextBtn.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      if (!isFlipping) {
        flipCard('next');
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
  
  // 保存当前卡片索引到设置
  const saveCurrentCardIndex = async () => {
    try {
      await window.electronAPI.saveCurrentCard(currentIndex);
      console.log('当前卡片索引已保存:', currentIndex);
    } catch (error) {
      console.error('保存当前卡片索引失败:', error);
    }
  };
  
  // 对话界面相关函数
  const handleCardDoubleClick = async (e) => {
    console.log('双击卡片，显示对话界面');
    const currentCard = cardData[currentIndex];
    console.log('当前卡片数据:', currentCard);
    
    try {
      const response = await window.electronAPI.showChatInterface(currentCard);
      if (response.success) {
        console.log('对话界面显示成功');
      } else {
        console.error('显示对话界面失败:', response.error);
      }
    } catch (error) {
      console.error('调用showChatInterface失败:', error);
    }
  };
  
  // 工具栏按钮事件监听器
  const settingsBtn = document.getElementById('settingsBtn');
  const chatBtn = document.getElementById('chatBtn');
  const panelOverlay = document.getElementById('panelOverlay');
  
  if (settingsBtn) {
    settingsBtn.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      window.showPanel('settings');
    });
  }
  
  if (chatBtn) {
    chatBtn.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      window.showPanel('chat');
    });
  }
  
  // 点击遮罩关闭面板
  if (panelOverlay) {
    panelOverlay.addEventListener('click', (e) => {
      if (e.target === panelOverlay) {
        window.closePanel('settings');
        window.closePanel('chat');
      }
    });
  }
  
  // 监听窗口关闭事件，保存当前卡片状态
  window.addEventListener('beforeunload', () => {
    saveCurrentCardIndex();
  });
});

if (module.hot) {
  module.hot.accept();
}
