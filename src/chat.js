// 动态加载CSS和样式
function loadStyles() {
  // 只在直接HTML加载时加载CSS（webpack环境下会自动处理）
  if (!document.querySelector('link[href*="chat.css"]')) {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = 'chat.css';
    document.head.appendChild(link);
  }
  
  // 加载KaTeX CSS
  try {
    if (typeof require !== 'undefined') {
      // webpack环境下直接require CSS文件
      require('katex/dist/katex.min.css');
    } else {
      // 直接HTML环境下的回退方案
      if (!document.querySelector('link[href*="katex"]')) {
        const katexCss = document.createElement('link');
        katexCss.rel = 'stylesheet';
        katexCss.href = './node_modules/katex/dist/katex.min.css';
        document.head.appendChild(katexCss);
      }
    }
  } catch (error) {
    console.warn('Could not load local KaTeX CSS:', error);
  }

  // 加载highlight.js CSS
  try {
    if (typeof require !== 'undefined') {
      // webpack环境下直接require CSS文件  
      require('highlight.js/styles/github.css');
    } else {
      // 直接HTML环境下的回退方案
      if (!document.querySelector('link[href*="highlight.js"]')) {
        const hljsCss = document.createElement('link');
        hljsCss.rel = 'stylesheet';
        hljsCss.href = './node_modules/highlight.js/styles/github.css';
        document.head.appendChild(hljsCss);
      }
    }
  } catch (error) {
    console.warn('Could not load highlight.js CSS:', error);
  }
}

// 加载渲染器模块
let contentRenderer = null;

// 导入完善的渲染器
async function loadRenderer() {
  try {
    console.log('尝试加载ContentRenderer...');
    // 直接导入ContentRenderer类
    if (typeof require !== 'undefined') {
      try {
        // 首先尝试加载renderer-utils模块
        console.log('使用require加载renderer-utils模块...');
        const rendererModule = require('./renderer-utils');
        console.log('成功加载renderer-utils模块:', Object.keys(rendererModule));
        
        if (rendererModule && rendererModule.ContentRenderer) {
          console.log('发现ContentRenderer类');
          return rendererModule.ContentRenderer;
        } else {
          console.warn('renderer-utils模块中没有找到ContentRenderer');
          return null;
        }
      } catch (requireError) {
        console.error('require失败:', requireError);
        return null;
      }
    } else {
      console.log('require不可用，运行在浏览器环境');
      return null;
    }
  } catch (error) {
    console.error('加载ContentRenderer时发生错误:', error);
    return null;
  }
}

// 专门的重新渲染函数，用于流式输出完成后的最终渲染
async function reRenderContent(contentDiv, content) {
  try {
    if (contentRenderer && contentRenderer.initialized) {
      console.log('执行内容重新渲染，内容长度:', content.length);
      console.log('使用的渲染器:', contentRenderer.constructor ? contentRenderer.constructor.name : 'Fallback');
      
      // 渲染markdown内容
      const renderedContent = await contentRenderer.render(content);
      console.log('渲染完成，HTML长度:', renderedContent.length);
      contentDiv.innerHTML = renderedContent;
      
      // 只有完整版ContentRenderer才处理mermaid图表
      if (contentRenderer.mermaid) {
        const mermaidElements = contentDiv.querySelectorAll('.language-mermaid, code[class*="language-mermaid"], pre code.mermaid');
        if (mermaidElements.length > 0) {
          console.log('发现mermaid元素，开始渲染图表');
          for (const element of mermaidElements) {
            try {
              const mermaidCode = element.textContent;
              const mermaidContainer = document.createElement('div');
              mermaidContainer.className = 'mermaid-container';
              
              // 生成唯一ID
              const id = 'mermaid-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
              
              // 渲染mermaid图表
              const { svg } = await contentRenderer.mermaid.render(id, mermaidCode);
              mermaidContainer.innerHTML = svg;
              
              // 替换原始代码块
              element.closest('pre') ? element.closest('pre').replaceWith(mermaidContainer) : element.replaceWith(mermaidContainer);
            } catch (mermaidError) {
              console.warn('Mermaid个别图表渲染失败:', mermaidError);
              const errorDiv = document.createElement('div');
              errorDiv.className = 'mermaid-error';
              errorDiv.textContent = 'Mermaid图表渲染失败: ' + mermaidError.message;
              element.replaceWith(errorDiv);
            }
          }
        }
      }
      
      console.log('内容重新渲染完成');
      return true;
    } else {
      console.warn('ContentRenderer未初始化，使用纯文本渲染');
      // 渲染器未初始化，使用纯文本
      contentDiv.textContent = content;
      return false;
    }
  } catch (error) {
    console.error('重新渲染失败:', error);
    contentDiv.textContent = content;
    return false;
  }
}

// 初始化渲染器
async function initializeRenderer() {
  console.log('开始初始化渲染器...');
  try {
    const RendererClass = await loadRenderer();
    if (RendererClass) {
      console.log('成功加载ContentRenderer类');
      contentRenderer = new RendererClass();
      console.log('ContentRenderer初始化完成:', {
        initialized: contentRenderer.initialized,
        hasMermaid: !!contentRenderer.mermaid,
        hasRenderMethod: typeof contentRenderer.render === 'function'
      });
    } else {
      console.log('ContentRenderer加载失败，使用fallback renderer');
      // 使用简化的内联渲染器
      contentRenderer = createFallbackRenderer();
      console.log('Fallback renderer初始化完成:', {
        initialized: contentRenderer.initialized,
        hasRenderMethod: typeof contentRenderer.render === 'function'
      });
    }
  } catch (error) {
    console.error('初始化渲染器失败:', error);
    contentRenderer = createFallbackRenderer();
    console.log('使用fallback renderer作为最终方案');
  }
}

// 创建回退渲染器
function createFallbackRenderer() {
  console.log('创建fallback renderer...');
  let MarkdownIt, katex, hljs;
  let dependenciesLoaded = false;
  
  try {
    if (typeof require !== 'undefined') {
      console.log('尝试加载markdown依赖...');
      MarkdownIt = require('markdown-it');
      katex = require('katex');
      hljs = require('highlight.js');
      dependenciesLoaded = true;
      console.log('成功加载所有markdown依赖');
    }
  } catch (error) {
    console.error('加载markdown依赖失败:', error);
    dependenciesLoaded = false;
  }

  const renderer = {
    initialized: true,
    dependenciesLoaded: dependenciesLoaded,
    mermaid: null, // 简化版不支持mermaid
    render: async (content) => {
      console.log('Fallback renderer开始渲染，内容长度:', content.length);
      
      try {
        if (!MarkdownIt || !dependenciesLoaded) {
          console.log('使用基础文本渲染');
          // 最基本的文本处理，保留换行和基础格式
          const escaped = content
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#x27;');
          
          // 简单的markdown格式处理
          const processed = escaped
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>') // 粗体
            .replace(/\*(.*?)\*/g, '<em>$1</em>') // 斜体
            .replace(/`([^`]+)`/g, '<code>$1</code>') // 行内代码
            .replace(/\n/g, '<br>'); // 换行
          
          console.log('基础文本渲染完成');
          return processed;
        }

        console.log('使用markdown-it渲染');
        // 使用markdown-it渲染markdown
        const md = new MarkdownIt({
          html: true,
          breaks: true,
          linkify: true,
          typographer: true
        });
        
        // 配置代码高亮
        if (hljs) {
          md.set({
            highlight: function (str, lang) {
              if (lang && hljs.getLanguage(lang)) {
                try {
                  return '<pre class="hljs"><code class="language-' + lang + '">' +
                         hljs.highlight(str, { language: lang, ignoreIllegals: true }).value +
                         '</code></pre>';
                } catch (__) {}
              }
              return '<pre class="hljs"><code>' + md.utils.escapeHtml(str) + '</code></pre>';
            }
          });
        }
        
        let rendered = md.render(content);
        console.log('markdown-it渲染完成，结果长度:', rendered.length);
        
        // 处理数学公式
        if (katex) {
          console.log('处理数学公式...');
          // 处理块级公式 $$...$$
          rendered = rendered.replace(/\$\$([^$]+?)\$\$/g, (match, formula) => {
            try {
              return katex.renderToString(formula.trim(), {
                displayMode: true,
                throwOnError: false
              });
            } catch (error) {
              return `<div class="math-error">公式渲染错误: ${formula}</div>`;
            }
          });

          // 处理内联公式 $...$
          rendered = rendered.replace(/\$([^$\n]+?)\$/g, (match, formula) => {
            try {
              return katex.renderToString(formula.trim(), {
                displayMode: false,
                throwOnError: false
              });
            } catch (error) {
              return match; // 保持原样
            }
          });
        }
        
        console.log('最终渲染完成，结果长度:', rendered.length);
        return rendered;
      } catch (error) {
        console.error('Fallback渲染错误:', error);
        return '<p>' + content.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;') + '</p>';
      }
    }
  };
  
  console.log('Fallback renderer创建完成:', {
    initialized: renderer.initialized,
    dependenciesLoaded: renderer.dependenciesLoaded
  });
  
  return renderer;
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
    console.log('添加AI消息，内容长度:', content.length);
    console.log('ContentRenderer状态:', {
      exists: !!contentRenderer,
      initialized: contentRenderer?.initialized,
      type: contentRenderer?.constructor?.name || 'Unknown'
    });
    
    try {
      if (contentRenderer && contentRenderer.initialized) {
        console.log('使用ContentRenderer渲染消息');
        const renderedContent = await contentRenderer.render(content);
        console.log('渲染结果长度:', renderedContent.length);
        const contentDiv = document.createElement('div');
        contentDiv.className = 'markdown-content';
        contentDiv.innerHTML = renderedContent;
        messageDiv.appendChild(contentDiv);
      } else {
        console.warn('ContentRenderer不可用，使用纯文本');
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
    console.log('=== 流式响应完成，开始最终渲染 ===');
    console.log('最终内容长度:', streamingContent.length);
    console.log('ContentRenderer状态:', {
      exists: !!contentRenderer,
      initialized: contentRenderer?.initialized,
      type: contentRenderer?.constructor?.name || 'Fallback'
    });
    
    // 清除防抖计时器
    if (renderTimeout) {
      clearTimeout(renderTimeout);
    }
    
    try {
      // 使用专门的重新渲染函数进行最终渲染
      const success = await reRenderContent(contentDiv, streamingContent);
      console.log('最终渲染结果:', success ? '成功' : '失败');
    } catch (error) {
      console.error('Final render error:', error);
      contentDiv.textContent = streamingContent;
    }
    
    // 滚动到底部并启用发送按钮
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
  console.log('=== DOMContentLoaded 事件开始 ===');
  
  // 1. 首先加载样式
  loadStyles();
  
  // 2. 初始化渲染器 - 确保在其他操作之前完成
  console.log('开始初始化渲染器...');
  await initializeRenderer();
  console.log('渲染器初始化完成，状态:', {
    exists: !!contentRenderer,
    initialized: contentRenderer?.initialized,
    type: contentRenderer?.constructor?.name || 'Unknown'
  });
  
  // 3. 设置事件处理器
  setTimeout(() => {
    console.log('Setting up event handlers after timeout');
    setupEventHandlers();
  }, 500);
  
  // 4. 立即尝试设置事件处理器
  setupEventHandlers();
  
  // 5. 监听卡片切换事件
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
  
  console.log('=== DOMContentLoaded 初始化完成 ===');
});