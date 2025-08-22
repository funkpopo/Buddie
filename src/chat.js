// 动态加载CSS和样式
function loadStyles() {
  // 只在直接HTML加载时加载CSS（webpack环境下会自动处理）
  if (!document.querySelector('link[href*="chat.css"]')) {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = 'chat.css';
    document.head.appendChild(link);
  }
  
  // 动态加载KaTeX CSS（如果需要）
  try {
    const katexCss = document.createElement('link');
    katexCss.rel = 'stylesheet';
    katexCss.href = 'https://cdn.jsdelivr.net/npm/katex@0.16.8/dist/katex.min.css';
    document.head.appendChild(katexCss);
  } catch (error) {
    console.warn('Could not load KaTeX CSS:', error);
  }
}

// 加载渲染器模块
let contentRenderer = null;

// 内联渲染器类定义
class SimpleContentRenderer {
  constructor() {
    this.marked = null;
    this.katex = null;
    this.mermaid = null;
    this.initialized = false;
    this.initializeLibraries();
  }

  initializeLibraries() {
    try {
      // 尝试加载渲染库（在webpack环境中可用）
      if (typeof require !== 'undefined') {
        this.marked = require('marked');
        this.katex = require('katex');
        this.mermaid = require('mermaid');
      } else {
        // 在直接HTML环境中，这些库可能不可用，使用简单回退
        console.warn('Rendering libraries not available in this environment');
      }
      
      if (this.marked) {
        this.marked.setOptions({
          gfm: true,
          breaks: true,
          sanitize: false,
          smartLists: true,
          smartypants: false
        });
      }
      
      if (this.mermaid) {
        this.mermaid.initialize({
          startOnLoad: false,
          theme: 'default',
          securityLevel: 'loose'
        });
      }
      
      this.initialized = true;
      console.log('Rendering libraries loaded successfully');
    } catch (error) {
      console.warn('Some rendering libraries failed to load:', error);
    }
  }

  escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  processMathFormulas(content) {
    // 处理块级公式 $$...$$
    let result = content;
    let startIndex = 0;
    
    while (true) {
      const start = result.indexOf('$$', startIndex);
      if (start === -1) break;
      
      const end = result.indexOf('$$', start + 2);
      if (end === -1) break;
      
      const formula = result.substring(start + 2, end);
      try {
        const rendered = this.katex.renderToString(formula.trim(), {
          displayMode: true,
          throwOnError: false
        });
        result = result.substring(0, start) + rendered + result.substring(end + 2);
        startIndex = start + rendered.length;
      } catch (error) {
        const errorDiv = '<div class="math-error">数学公式错误: ' + this.escapeHtml(formula) + '</div>';
        result = result.substring(0, start) + errorDiv + result.substring(end + 2);
        startIndex = start + errorDiv.length;
      }
    }
    
    // 处理内联公式 $...$（避免与已处理的$$冲突）
    startIndex = 0;
    while (true) {
      const start = result.indexOf('$', startIndex);
      if (start === -1) break;
      
      // 跳过如果这是$$的一部分
      if (start > 0 && result.charAt(start - 1) === '$') {
        startIndex = start + 1;
        continue;
      }
      if (start < result.length - 1 && result.charAt(start + 1) === '$') {
        startIndex = start + 2;
        continue;
      }
      
      const end = result.indexOf('$', start + 1);
      if (end === -1) break;
      
      const formula = result.substring(start + 1, end);
      if (formula.includes('\n')) {
        startIndex = start + 1;
        continue;
      }
      
      try {
        const rendered = this.katex.renderToString(formula.trim(), {
          displayMode: false,
          throwOnError: false
        });
        result = result.substring(0, start) + rendered + result.substring(end + 1);
        startIndex = start + rendered.length;
      } catch (error) {
        startIndex = start + 1;
      }
    }
    
    return result;
  }

  async render(content) {
    try {
      if (!this.initialized || !this.marked) {
        // 回退到简单处理
        return content
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;')
          .replace(/'/g, '&#x27;')
          .replace(/\n/g, '<br>');
      }

      // 处理 LaTeX 数学公式（在 markdown 处理之前）
      if (this.katex) {
        // 简单的字符串处理，避免复杂的正则表达式
        content = this.processMathFormulas(content);
      }
      
      // 处理 markdown
      let rendered = this.marked.parse(content);
      
      return rendered;
    } catch (error) {
      console.error('Rendering error:', error);
      return '<p>' + this.escapeHtml(content) + '</p>';
    }
  }
}

// 初始化渲染器
async function initializeRenderer() {
  try {
    contentRenderer = new SimpleContentRenderer();
    console.log('Content renderer initialized successfully');
  } catch (error) {
    console.error('Failed to initialize content renderer:', error);
    // 创建一个简单的fallback渲染器
    contentRenderer = {
      render: async (content) => {
        return content
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;')
          .replace(/'/g, '&#x27;')
          .replace(/\n/g, '<br>');
      }
    };
  }
}

// 获取卡片数据（由main.js传入）
let cardData = null;

// 初始化函数 - 由main.js调用
window.initializeCardData = function(data) {
  cardData = data;
  console.log('Card data initialized:', cardData);
  
  // 更新UI元素
  const chatModelName = document.getElementById('chatModelName');
  const chatModelType = document.getElementById('chatModelType');
  
  if (chatModelName && cardData) {
    chatModelName.textContent = (cardData.emoji ? cardData.emoji + ' ' : '') + (cardData.title || 'AI助手');
  }
  
  if (chatModelType && cardData) {
    chatModelType.textContent = cardData.subtitle || 'Chat';
  }
};

function closeChatInterface() {
  window.close();
}

// Make closeChatInterface globally accessible
window.closeChatInterface = closeChatInterface;

function adjustTextareaHeight() {
  const chatInput = document.getElementById('chatInput');
  if (chatInput) {
    // 重置高度以计算实际需要的高度
    chatInput.style.height = 'auto';
    // 设置高度，限制在最大120px内
    const newHeight = Math.min(chatInput.scrollHeight, 120);
    chatInput.style.height = newHeight + 'px';
  }
}

async function addMessage(content, type = 'user') {
  const chatMessages = document.getElementById('chatMessages');
  const messageDiv = document.createElement('div');
  messageDiv.className = 'message-bubble ' + type + '-message';
  
  if (type === 'assistant') {
    // AI回复支持markdown/latex/mermaid渲染
    try {
      if (contentRenderer) {
        const renderedContent = await contentRenderer.render(content);
        const contentDiv = document.createElement('div');
        contentDiv.className = 'markdown-content';
        contentDiv.innerHTML = renderedContent;
        messageDiv.appendChild(contentDiv);
      } else {
        // 渲染器未初始化，使用纯文本
        const messageP = document.createElement('p');
        messageP.textContent = content;
        messageDiv.appendChild(messageP);
      }
    } catch (error) {
      console.error('Content rendering failed:', error);
      // 渲染失败时回退到纯文本
      const messageP = document.createElement('p');
      messageP.textContent = content;
      messageDiv.appendChild(messageP);
    }
  } else {
    // 用户消息保持纯文本
    const messageP = document.createElement('p');
    messageP.textContent = content;
    messageDiv.appendChild(messageP);
  }
  
  chatMessages.appendChild(messageDiv);
  
  // 滚动到底部
  chatMessages.scrollTop = chatMessages.scrollHeight;
}

async function sendMessage() {
  console.log('sendMessage function called');
  
  // 重新获取DOM元素，确保它们已经被正确加载
  const chatInput = document.getElementById('chatInput');
  const chatSendBtn = document.getElementById('chatSendBtn');
  const chatMessages = document.getElementById('chatMessages');
  
  if (!chatInput) {
    console.error('chatInput element is null');
    return;
  }
  
  console.log('chatInput element:', chatInput.tagName, chatInput.id);
  console.log('chatInput value:', chatInput.value);
  
  const message = chatInput.value.trim();
  if (!message) {
    console.log('Message is empty, not sending');
    return;
  }
  
  console.log('Sending message:', message);
  
  // 添加用户消息
  await addMessage(message, 'user');
  
  // 清空输入框
  chatInput.value = '';
  adjustTextareaHeight();
  
  // 禁用发送按钮
  chatSendBtn.disabled = true;
  
  // 创建一个空的助手消息气泡用于流式显示
  const assistantMessageDiv = document.createElement('div');
  assistantMessageDiv.className = 'message-bubble assistant-message';
  
  const contentDiv = document.createElement('div');
  contentDiv.className = 'markdown-content';
  contentDiv.innerHTML = '';
  assistantMessageDiv.appendChild(contentDiv);
  
  chatMessages.appendChild(assistantMessageDiv);
  chatMessages.scrollTop = chatMessages.scrollHeight;
  
  let streamingContent = '';
  let renderTimeout = null;
  
  // 设置流式响应监听器
  const cleanupChunk = window.electronAPI.onChatStreamChunk((event, chunk) => {
    streamingContent += chunk;
    
    // 防抖渲染：避免频繁渲染导致性能问题
    if (renderTimeout) {
      clearTimeout(renderTimeout);
    }
    
    renderTimeout = setTimeout(async () => {
      try {
        if (contentRenderer) {
          const renderedContent = await contentRenderer.render(streamingContent);
          contentDiv.innerHTML = renderedContent;
        } else {
          contentDiv.textContent = streamingContent;
        }
      } catch (error) {
        console.warn('Streaming render error:', error);
        contentDiv.textContent = streamingContent;
      }
      chatMessages.scrollTop = chatMessages.scrollHeight;
    }, 100); // 100ms 防抖
  });
  
  const cleanupEnd = window.electronAPI.onChatStreamEnd(async (event) => {
    console.log('流式响应完成');
    
    // 清除防抖计时器并进行最终渲染
    if (renderTimeout) {
      clearTimeout(renderTimeout);
    }
    
    try {
      if (contentRenderer) {
        const finalRenderedContent = await contentRenderer.render(streamingContent);
        contentDiv.innerHTML = finalRenderedContent;
      } else {
        contentDiv.textContent = streamingContent;
      }
    } catch (error) {
      console.error('Final render error:', error);
      contentDiv.textContent = streamingContent;
    }
    
    chatMessages.scrollTop = chatMessages.scrollHeight;
    chatSendBtn.disabled = false;
    
    // 清理监听器
    cleanupChunk();
    cleanupEnd();
    cleanupError();
  });
  
  const cleanupError = window.electronAPI.onChatStreamError((event, errorMessage) => {
    console.error('流式响应错误:', errorMessage);
    
    // 清除防抖计时器
    if (renderTimeout) {
      clearTimeout(renderTimeout);
    }
    
    contentDiv.innerHTML = '<p>抱歉，发送消息时出现错误：' + errorMessage + '</p>';
    assistantMessageDiv.className = 'message-bubble system-message';
    chatSendBtn.disabled = false;
    
    // 清理监听器
    cleanupChunk();
    cleanupEnd();
    cleanupError();
  });
  
  try {
    // 启动流式响应
    await window.electronAPI.sendChatMessage({
      message: message,
      modelId: cardData ? cardData.modelId : null
    });
  } catch (error) {
    console.error('发送消息失败:', error);
    
    // 清除防抖计时器
    if (renderTimeout) {
      clearTimeout(renderTimeout);
    }
    
    contentDiv.innerHTML = '<p>抱歉，发送消息时出现错误，请稍后重试。</p>';
    assistantMessageDiv.className = 'message-bubble system-message';
    chatSendBtn.disabled = false;
    
    // 清理监听器
    cleanupChunk();
    cleanupEnd();
    cleanupError();
  }
}

// 定义事件处理器设置函数
function setupEventHandlers() {
  console.log('Setting up event handlers');
  
  // 重新获取DOM元素，确保它们已经被正确加载
  const chatInput = document.getElementById('chatInput');
  const chatSendBtn = document.getElementById('chatSendBtn');
  
  // 添加全局点击监听器来调试
  document.addEventListener('click', function(e) {
    console.log('Document clicked, target:', e.target.tagName, e.target.id, e.target.className);
  });
  
  // 设置发送按钮事件
  if (chatSendBtn) {
    console.log('Found send button, setting up click handler');
    chatSendBtn.disabled = false;
    
    // 添加新的事件监听器
    chatSendBtn.addEventListener('click', (e) => {
      console.log('Send button clicked');
      e.preventDefault();
      e.stopPropagation();
      sendMessage();
    });
    console.log('Send button event listener added');
  } else {
    console.error('chatSendBtn element not found');
  }
  
  // 设置输入框事件
  if (chatInput) {
    console.log('Found input element:', chatInput.tagName, chatInput.id);
    
    // 强制设置所有必要的属性
    chatInput.disabled = false;
    chatInput.readOnly = false;
    chatInput.style.pointerEvents = 'auto';
    chatInput.style.zIndex = '1000';
    
    console.log('Input element properties:', {
      disabled: chatInput.disabled,
      readOnly: chatInput.readOnly,
      style: chatInput.style.cssText,
      offsetWidth: chatInput.offsetWidth,
      offsetHeight: chatInput.offsetHeight
    });
    
    // 添加多种事件监听
    ['mousedown', 'mouseup', 'click', 'focus', 'blur', 'keydown', 'keyup', 'input'].forEach(eventType => {
      chatInput.addEventListener(eventType, function(e) {
        console.log(`Input ${eventType} event:`, e.type, e.target.value);
      });
    });
    
    // 键盘事件处理 - 支持Shift+Enter换行
    chatInput.addEventListener('keydown', function(e) {
      if (e.key === 'Enter') {
        if (e.shiftKey) {
          // Shift+Enter: 允许换行（不阻止默认行为）
          console.log('Shift+Enter: allowing line break');
          // 不阻止默认行为，让textarea自然换行
        } else {
          // 单独Enter: 发送消息
          console.log('Enter key detected, sending message');
          e.preventDefault(); // 阻止换行
          sendMessage();
        }
      }
    });
    
    // 输入事件 - 调整高度
    chatInput.addEventListener('input', function(e) {
      console.log('Input event triggered, new value:', chatInput.value);
      adjustTextareaHeight(); // 调整textarea高度
    });
    
    // 强制聚焦
    setTimeout(() => {
      console.log('Attempting to focus input...');
      chatInput.focus();
      console.log('Input focused, activeElement:', document.activeElement === chatInput);
      // 初始调整高度
      adjustTextareaHeight();
    }, 500);
    
    console.log('Input event handlers set up successfully');
  } else {
    console.error('chatInput element not found');
  }
}

// 初始化时聚焦输入框
window.addEventListener('DOMContentLoaded', async () => {
  console.log('DOMContentLoaded event fired');
  
  // 加载样式
  loadStyles();
  
  // 确保DOM元素已加载
  setTimeout(() => {
    console.log('Setting up event handlers after timeout');
    setupEventHandlers();
  }, 500);
  
  // 初始化渲染器
  await initializeRenderer();
  
  // 立即尝试设置事件处理器
  setupEventHandlers();
  
  // 监听卡片切换事件，确保electronAPI已加载
  if (window.electronAPI && window.electronAPI.onCardSwitched) {
    console.log('Setting up card-switched listener');
    window.electronAPI.onCardSwitched((event, cardData) => {
      console.log('聊天窗口收到卡片切换通知:', cardData);
      // 更新聊天窗口的卡片显示信息
      const chatModelName = document.getElementById('chatModelName');
      const chatModelType = document.getElementById('chatModelType');
      
      console.log('Found chat elements:', { chatModelName: !!chatModelName, chatModelType: !!chatModelType });
      
      if (chatModelName && cardData) {
        const newTitle = cardData.emoji + ' ' + cardData.title;
        console.log('Updating chat model name to:', newTitle);
        chatModelName.textContent = newTitle;
      }
      
      if (chatModelType && cardData) {
        console.log('Updating chat model type to:', cardData.subtitle);
        chatModelType.textContent = cardData.subtitle;
      }
    });
  } else {
    console.error('electronAPI or onCardSwitched not available');
    console.error('Available electronAPI methods:', window.electronAPI ? Object.keys(window.electronAPI) : 'electronAPI not found');
  }
});