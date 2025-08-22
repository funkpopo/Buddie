const { app, BrowserWindow, Tray, Menu, ipcMain, shell, screen } = require('electron');
const path = require('path');
const fs = require('fs');

if (require('electron-squirrel-startup')) {
  app.quit();
}

let mainWindow;
let tray;
let settingsWindow;
let chatWindow;
let settings = {
  theme: 'default',
  opacity: 0.9,
  autoStart: false,
  alwaysOnTop: true,
  position: { x: 100, y: 100 }
};

const isDev = process.env.NODE_ENV === 'development';
const settingsPath = isDev 
  ? path.join(__dirname, '..', 'settings.json')
  : path.join(path.dirname(process.execPath), 'settings.json');

function loadSettings() {
  try {
    if (fs.existsSync(settingsPath)) {
      const data = fs.readFileSync(settingsPath, 'utf8');
      settings = { ...settings, ...JSON.parse(data) };
    }
  } catch (error) {
    console.error('Failed to load settings:', error);
  }
}

function saveSettings() {
  try {
    fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2));
  } catch (error) {
    console.error('Failed to save settings:', error);
  }
}

function getIconPath() {
  // Use a simple fallback to avoid path issues
  try {
    if (process.platform === 'win32') {
      const iconPath = path.join(__dirname, '..', '..', 'src', 'assets', 'logo.ico');
      if (fs.existsSync(iconPath)) {
        return iconPath;
      }
    }
    
    // Fallback: create a simple tray without custom icon
    return null;
  } catch (error) {
    console.warn('Icon path error:', error);
    return null;
  }
}

function createMainWindow() {
  const { x, y } = settings.position;
  const display = screen.getDisplayNearestPoint({ x, y });
  
  const windowOptions = {
    width: 300,
    height: 400,
    x: x,
    y: y,
    frame: false,
    transparent: true,
    alwaysOnTop: settings.alwaysOnTop,
    skipTaskbar: false,
    resizable: false,
    opacity: settings.opacity,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY,
    }
  };

  // Add icon only if it exists
  const iconPath = getIconPath();
  if (iconPath && fs.existsSync(iconPath)) {
    windowOptions.icon = iconPath;
  }

  mainWindow = new BrowserWindow(windowOptions);

  mainWindow.loadURL(MAIN_WINDOW_WEBPACK_ENTRY);

  if (isDev) {
    mainWindow.webContents.openDevTools({ mode: 'detach' });
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  mainWindow.on('move', () => {
    if (mainWindow) {
      const [x, y] = mainWindow.getPosition();
      settings.position = { x, y };
      saveSettings();
    }
  });

  mainWindow.on('minimize', () => {
    if (process.platform === 'darwin') {
      app.dock.hide();
    }
  });

  mainWindow.on('restore', () => {
    if (process.platform === 'darwin') {
      app.dock.show();
    }
  });

  if (process.platform === 'win32') {
    app.setAppUserModelId('com.buddie.desktop');
  }
}

function createTray() {
  const iconPath = getIconPath();
  
  // Create tray with or without custom icon
  if (iconPath && fs.existsSync(iconPath)) {
    tray = new Tray(iconPath);
  } else {
    // Use system default tray icon
    tray = new Tray(require('electron').nativeImage.createEmpty());
  }
  
  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Show',
      click: () => {
        if (mainWindow) {
          mainWindow.show();
          mainWindow.focus();
        } else {
          createMainWindow();
        }
      }
    },
    {
      label: 'Hide',
      click: () => {
        if (mainWindow) {
          mainWindow.hide();
        }
      }
    },
    {
      label: 'Settings',
      click: () => {
        showSettingsWindow();
      }
    },
    { type: 'separator' },
    {
      label: 'Quit',
      click: () => {
        app.quit();
      }
    }
  ]);

  tray.setToolTip('Buddie');
  tray.setContextMenu(contextMenu);

  tray.on('click', () => {
    if (mainWindow) {
      if (mainWindow.isVisible()) {
        mainWindow.hide();
      } else {
        mainWindow.show();
        mainWindow.focus();
      }
    } else {
      createMainWindow();
    }
  });

  tray.on('double-click', () => {
    if (mainWindow) {
      mainWindow.show();
      mainWindow.focus();
    } else {
      createMainWindow();
    }
  });
}

function showSettingsWindow() {
  if (settingsWindow) {
    settingsWindow.focus();
    return;
  }

  let parentBounds = { x: 100, y: 100 };
  if (mainWindow) {
    parentBounds = mainWindow.getBounds();
  }

  settingsWindow = new BrowserWindow({
    width: 400,
    height: 500,
    x: parentBounds.x + 50,
    y: parentBounds.y + 50,
    resizable: false,
    minimizable: false,
    maximizable: false,
    alwaysOnTop: true,
    parent: mainWindow,
    modal: false,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js')
    }
  });

  // Add icon only if it exists
  const iconPath = getIconPath();
  if (iconPath && fs.existsSync(iconPath)) {
    settingsWindow.setIcon(iconPath);
  }

  const settingsHTML = `
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Settings - Buddie</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            min-height: calc(100vh - 40px);
        }
        
        .container {
            max-width: 360px;
            margin: 0 auto;
        }
        
        h1 {
            text-align: center;
            margin-bottom: 30px;
            font-size: 24px;
            font-weight: 300;
        }
        
        .setting-group {
            background: rgba(255, 255, 255, 0.1);
            border-radius: 10px;
            padding: 20px;
            margin-bottom: 20px;
            backdrop-filter: blur(10px);
        }
        
        .setting-item {
            margin-bottom: 15px;
        }
        
        .setting-item:last-child {
            margin-bottom: 0;
        }
        
        label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
        }
        
        input[type="range"] {
            width: 100%;
            height: 6px;
            border-radius: 3px;
            background: rgba(255, 255, 255, 0.3);
            outline: none;
            -webkit-appearance: none;
        }
        
        input[type="range"]::-webkit-slider-thumb {
            -webkit-appearance: none;
            appearance: none;
            width: 20px;
            height: 20px;
            border-radius: 50%;
            background: white;
            cursor: pointer;
            box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
        }
        
        input[type="checkbox"] {
            width: 18px;
            height: 18px;
            margin-right: 10px;
            cursor: pointer;
        }
        
        select {
            width: 100%;
            padding: 8px 12px;
            border: none;
            border-radius: 6px;
            background: rgba(255, 255, 255, 0.2);
            color: white;
            font-size: 14px;
            cursor: pointer;
        }
        
        select option {
            background: #333;
            color: white;
        }
        
        .checkbox-container {
            display: flex;
            align-items: center;
            cursor: pointer;
        }
        
        .opacity-value {
            display: inline-block;
            margin-left: 10px;
            font-weight: bold;
            min-width: 30px;
        }
        
        .buttons {
            display: flex;
            gap: 10px;
            margin-top: 30px;
        }
        
        button {
            flex: 1;
            padding: 12px 20px;
            border: none;
            border-radius: 6px;
            font-size: 14px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s ease;
        }
        
        .btn-primary {
            background: rgba(255, 255, 255, 0.9);
            color: #333;
        }
        
        .btn-secondary {
            background: rgba(255, 255, 255, 0.2);
            color: white;
        }
        
        button:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
        }
        
        .position-info {
            font-size: 12px;
            color: rgba(255, 255, 255, 0.8);
            margin-top: 5px;
        }
    </style>
</head>
<body>
    <div class="container">
        <h1>Settings</h1>
        
        <div class="setting-group">
            <div class="setting-item">
                <label for="theme">Theme</label>
                <select id="theme">
                    <option value="default">Default</option>
                    <option value="dark">Dark</option>
                    <option value="light">Light</option>
                    <option value="colorful">Colorful</option>
                </select>
            </div>
            
            <div class="setting-item">
                <label for="opacity">Opacity <span class="opacity-value" id="opacityValue">90%</span></label>
                <input type="range" id="opacity" min="0.3" max="1" step="0.1" value="0.9">
            </div>
        </div>
        
        <div class="setting-group">
            <div class="setting-item">
                <div class="checkbox-container">
                    <input type="checkbox" id="alwaysOnTop">
                    <label for="alwaysOnTop">Always on top</label>
                </div>
            </div>
            
            <div class="setting-item">
                <div class="checkbox-container">
                    <input type="checkbox" id="autoStart">
                    <label for="autoStart">Start with system</label>
                </div>
            </div>
        </div>
        
        <div class="setting-group">
            <div class="setting-item">
                <label>Window Position</label>
                <div class="position-info" id="positionInfo">X: 100, Y: 100</div>
            </div>
        </div>
        
        <div class="buttons">
            <button type="button" class="btn-secondary" onclick="closeSettings()">Cancel</button>
            <button type="button" class="btn-primary" onclick="applySettings()">Apply</button>
        </div>
    </div>

    <script>
        let currentSettings = {};
        
        // Load current settings
        window.electronAPI.getSettings().then(settings => {
            currentSettings = settings;
            
            document.getElementById('theme').value = settings.theme || 'default';
            document.getElementById('opacity').value = settings.opacity || 0.9;
            document.getElementById('alwaysOnTop').checked = settings.alwaysOnTop !== false;
            document.getElementById('autoStart').checked = settings.autoStart === true;
            
            updateOpacityDisplay();
            updatePositionDisplay();
        });
        
        // Update opacity display
        document.getElementById('opacity').addEventListener('input', updateOpacityDisplay);
        
        function updateOpacityDisplay() {
            const opacity = document.getElementById('opacity').value;
            document.getElementById('opacityValue').textContent = Math.round(opacity * 100) + '%';
        }
        
        function updatePositionDisplay() {
            const pos = currentSettings.position || { x: 100, y: 100 };
            document.getElementById('positionInfo').textContent = \`X: \${pos.x}, Y: \${pos.y}\`;
        }
        
        function applySettings() {
            const newSettings = {
                theme: document.getElementById('theme').value,
                opacity: parseFloat(document.getElementById('opacity').value),
                alwaysOnTop: document.getElementById('alwaysOnTop').checked,
                autoStart: document.getElementById('autoStart').checked,
                position: currentSettings.position
            };
            
            window.electronAPI.saveSettings(newSettings).then(() => {
                window.close();
            });
        }
        
        function closeSettings() {
            window.close();
        }
    </script>
</body>
</html>`;

  settingsWindow.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(settingsHTML));

  settingsWindow.on('closed', () => {
    settingsWindow = null;
  });
}

function createChatWindow(cardData) {
  return new Promise((resolve, reject) => {
    try {
      let parentBounds = { x: 100, y: 100 };
      if (mainWindow) {
        parentBounds = mainWindow.getBounds();
      }

      chatWindow = new BrowserWindow({
        width: 800,
        height: 600,
        x: parentBounds.x + 320,
        y: parentBounds.y,
        resizable: true,
        minimizable: true,
        maximizable: true,
        alwaysOnTop: false,
        parent: mainWindow,
        modal: false,
        webPreferences: {
          nodeIntegration: false,
          contextIsolation: true,
          preload: path.join(__dirname, 'preload.js')
        }
      });

      // Add icon only if it exists
      const iconPath = getIconPath();
      if (iconPath && fs.existsSync(iconPath)) {
        chatWindow.setIcon(iconPath);
      }

      const chatHTML = `
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Chat - ${cardData.title}</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            height: 100vh;
            display: flex;
            flex-direction: column;
        }
        
        .chat-header {
            background: rgba(255, 255, 255, 0.1);
            padding: 20px;
            backdrop-filter: blur(10px);
            border-bottom: 1px solid rgba(255, 255, 255, 0.2);
            text-align: center;
        }
        
        .chat-header h1 {
            font-size: 24px;
            font-weight: 300;
            margin-bottom: 5px;
        }
        
        .chat-header p {
            font-size: 14px;
            opacity: 0.8;
        }
        
        .chat-container {
            flex: 1;
            display: flex;
            flex-direction: column;
            overflow: hidden;
            padding: 20px;
        }
        
        .chat-messages {
            flex: 1;
            overflow-y: auto;
            padding: 20px;
            background: rgba(255, 255, 255, 0.05);
            border-radius: 10px;
            margin-bottom: 20px;
            backdrop-filter: blur(10px);
        }
        
        .message {
            margin-bottom: 15px;
            padding: 12px 16px;
            border-radius: 18px;
            max-width: 70%;
            word-wrap: break-word;
        }
        
        .message.user {
            background: rgba(255, 255, 255, 0.2);
            margin-left: auto;
            text-align: right;
        }
        
        .message.assistant {
            background: rgba(255, 255, 255, 0.1);
            margin-right: auto;
        }
        
        .message-time {
            font-size: 11px;
            opacity: 0.6;
            margin-top: 5px;
        }
        
        .chat-input-container {
            display: flex;
            gap: 10px;
            align-items: flex-end;
        }
        
        .chat-input {
            flex: 1;
            background: rgba(255, 255, 255, 0.1);
            border: 1px solid rgba(255, 255, 255, 0.2);
            border-radius: 20px;
            padding: 12px 16px;
            color: white;
            font-size: 14px;
            resize: none;
            max-height: 120px;
            min-height: 44px;
            backdrop-filter: blur(10px);
        }
        
        .chat-input::placeholder {
            color: rgba(255, 255, 255, 0.6);
        }
        
        .chat-input:focus {
            outline: none;
            border-color: rgba(255, 255, 255, 0.4);
            background: rgba(255, 255, 255, 0.15);
        }
        
        .send-button {
            background: rgba(255, 255, 255, 0.2);
            border: none;
            border-radius: 50%;
            width: 44px;
            height: 44px;
            color: white;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.2s ease;
            backdrop-filter: blur(10px);
        }
        
        .send-button:hover {
            background: rgba(255, 255, 255, 0.3);
            transform: scale(1.05);
        }
        
        .send-button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
            transform: none;
        }
        
        .welcome-message {
            text-align: center;
            opacity: 0.6;
            font-style: italic;
            margin: 40px 20px;
        }
        
        .typing-indicator {
            display: none;
            padding: 12px 16px;
            margin-bottom: 15px;
            opacity: 0.7;
        }
        
        .typing-dots {
            display: inline-flex;
            gap: 4px;
        }
        
        .typing-dots span {
            width: 6px;
            height: 6px;
            background: rgba(255, 255, 255, 0.6);
            border-radius: 50%;
            animation: typing 1.4s infinite ease-in-out;
        }
        
        .typing-dots span:nth-child(1) { animation-delay: -0.32s; }
        .typing-dots span:nth-child(2) { animation-delay: -0.16s; }
        
        @keyframes typing {
            0%, 80%, 100% { transform: scale(0.8); opacity: 0.5; }
            40% { transform: scale(1); opacity: 1; }
        }
        
        /* 滚动条样式 */
        .chat-messages::-webkit-scrollbar {
            width: 6px;
        }
        
        .chat-messages::-webkit-scrollbar-track {
            background: rgba(255, 255, 255, 0.1);
            border-radius: 3px;
        }
        
        .chat-messages::-webkit-scrollbar-thumb {
            background: rgba(255, 255, 255, 0.3);
            border-radius: 3px;
        }
        
        .chat-messages::-webkit-scrollbar-thumb:hover {
            background: rgba(255, 255, 255, 0.5);
        }
    </style>
</head>
<body>
    <div class="chat-header">
        <h1 id="cardTitle">${cardData.emoji} ${cardData.title}</h1>
        <p id="cardSubtitle">${cardData.subtitle}</p>
    </div>
    
    <div class="chat-container">
        <div class="chat-messages" id="chatMessages">
            <div class="welcome-message">
                <p>欢迎使用 AI 对话！请输入您的问题开始聊天。</p>
            </div>
        </div>
        
        <div class="typing-indicator" id="typingIndicator">
            <div class="typing-dots">
                <span></span>
                <span></span>
                <span></span>
            </div>
            <span style="margin-left: 8px;">AI 正在思考...</span>
        </div>
        
        <div class="chat-input-container">
            <textarea id="chatInput" class="chat-input" placeholder="输入消息..." rows="1"></textarea>
            <button id="sendButton" class="send-button">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="22" y1="2" x2="11" y2="13"></line>
                    <polygon points="22,2 15,22 11,13 2,9"></polygon>
                </svg>
            </button>
        </div>
    </div>

    <script>
        let currentCardData = ${JSON.stringify(cardData)};
        
        const chatMessages = document.getElementById('chatMessages');
        const chatInput = document.getElementById('chatInput');
        const sendButton = document.getElementById('sendButton');
        const typingIndicator = document.getElementById('typingIndicator');
        
        // 自动调整输入框高度
        chatInput.addEventListener('input', function() {
            this.style.height = 'auto';
            this.style.height = Math.min(this.scrollHeight, 120) + 'px';
        });
        
        // 发送消息
        function sendMessage() {
            const message = chatInput.value.trim();
            if (!message) return;
            
            // 添加用户消息
            addMessage(message, 'user');
            chatInput.value = '';
            chatInput.style.height = 'auto';
            
            // 显示输入状态
            showTyping();
            
            // 模拟 AI 响应（这里应该调用实际的 AI API）
            setTimeout(() => {
                hideTyping();
                addMessage('这是一个示例响应。实际的 AI 功能需要集成 OpenAI API。', 'assistant');
            }, 2000);
        }
        
        // 添加消息到聊天窗口
        function addMessage(content, sender) {
            const messageDiv = document.createElement('div');
            messageDiv.className = \`message \${sender}\`;
            
            const now = new Date();
            const timeString = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            
            messageDiv.innerHTML = \`
                <div>\${content}</div>
                <div class="message-time">\${timeString}</div>
            \`;
            
            // 移除欢迎消息
            const welcomeMessage = chatMessages.querySelector('.welcome-message');
            if (welcomeMessage) {
                welcomeMessage.remove();
            }
            
            chatMessages.appendChild(messageDiv);
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }
        
        // 显示输入状态
        function showTyping() {
            typingIndicator.style.display = 'block';
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }
        
        // 隐藏输入状态
        function hideTyping() {
            typingIndicator.style.display = 'none';
        }
        
        // 更新卡片数据
        function updateCardData(cardData) {
            currentCardData = cardData;
            document.getElementById('cardTitle').textContent = \`\${cardData.emoji} \${cardData.title}\`;
            document.getElementById('cardSubtitle').textContent = cardData.subtitle;
            document.title = \`Chat - \${cardData.title}\`;
        }
        
        // 监听卡片数据更新
        if (window.electronAPI) {
            window.electronAPI.onUpdateCardData && window.electronAPI.onUpdateCardData((event, cardData) => {
                updateCardData(cardData);
            });
        }
        
        // 事件监听器
        sendButton.addEventListener('click', sendMessage);
        
        chatInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMessage();
            }
        });
        
        // 聚焦到输入框
        setTimeout(() => {
            chatInput.focus();
        }, 100);
    </script>
</body>
</html>`;

      chatWindow.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(chatHTML));

      chatWindow.on('closed', () => {
        chatWindow = null;
      });

      chatWindow.once('ready-to-show', () => {
        chatWindow.show();
        resolve();
      });

    } catch (error) {
      reject(error);
    }
  });
}

// IPC handlers
ipcMain.handle('drag-window', async (event, { x, y }) => {
  if (mainWindow) {
    mainWindow.setPosition(x, y);
    settings.position = { x, y };
    saveSettings();
  }
});

ipcMain.handle('get-window-position', async () => {
  if (mainWindow) {
    const [x, y] = mainWindow.getPosition();
    return { x, y };
  }
  return settings.position;
});

ipcMain.handle('get-settings', async () => {
  return settings;
});

ipcMain.handle('save-settings', async (event, newSettings) => {
  const oldSettings = { ...settings };
  settings = { ...settings, ...newSettings };
  saveSettings();

  if (mainWindow) {
    if (oldSettings.opacity !== settings.opacity) {
      mainWindow.setOpacity(settings.opacity);
    }
    
    if (oldSettings.alwaysOnTop !== settings.alwaysOnTop) {
      mainWindow.setAlwaysOnTop(settings.alwaysOnTop);
    }
  }

  return true;
});

ipcMain.handle('open-external', async (event, url) => {
  await shell.openExternal(url);
});

ipcMain.handle('show-settings', async () => {
  showSettingsWindow();
});

ipcMain.handle('quit-app', async () => {
  app.quit();
});

// Chat and card management handlers
ipcMain.handle('save-current-card', async (event, cardIndex) => {
  settings.currentCardIndex = cardIndex;
  saveSettings();
  return true;
});

ipcMain.handle('trigger-card-switch', async (event, direction) => {
  if (mainWindow) {
    mainWindow.webContents.send('trigger-card-switch', direction);
  }
  return true;
});

ipcMain.handle('send-chat-message', async (event, data) => {
  // Placeholder for chat functionality
  // This would integrate with OpenAI API in a complete implementation
  console.log('Chat message received:', data);
  return { success: true, message: 'Chat functionality not fully implemented' };
});

ipcMain.handle('show-chat-interface', async (event, cardData) => {
  console.log('Show chat interface for card:', cardData);
  
  if (chatWindow) {
    chatWindow.focus();
    // 发送新的卡片数据到现有聊天窗口
    chatWindow.webContents.send('update-card-data', cardData);
    return { success: true };
  }

  // 创建新的聊天窗口
  try {
    await createChatWindow(cardData);
    return { success: true };
  } catch (error) {
    console.error('创建聊天窗口失败:', error);
    return { success: false, error: error.message };
  }
});

ipcMain.on('refresh-cards', (event) => {
  if (mainWindow) {
    mainWindow.webContents.send('refresh-cards');
  }
});

ipcMain.on('drag-window', (event, position) => {
  if (mainWindow) {
    mainWindow.setPosition(position.x, position.y);
    settings.position = position;
    saveSettings();
  }
});

ipcMain.handle('update-card-data', async (event, cardData) => {
  if (chatWindow && !chatWindow.isDestroyed()) {
    chatWindow.webContents.send('update-card-data', cardData);
    return { success: true };
  }
  return { success: false, error: 'Chat window not available' };
});

// Hot reload in development
if (isDev) {
  const chokidar = require('chokidar');
  
  const watcher = chokidar.watch([
    path.join(__dirname, 'main.js'),
    path.join(__dirname, 'preload.js')
  ], {
    ignored: /node_modules/,
    persistent: true
  });

  watcher.on('change', () => {
    console.log('Main process file changed, restarting...');
    app.relaunch();
    app.exit();
  });
}

// App event handlers
app.whenReady().then(() => {
  loadSettings();
  createMainWindow();
  
  setTimeout(() => {
    createTray();
  }, process.platform === 'darwin' ? 1000 : 100);

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createMainWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('before-quit', () => {
  if (mainWindow) {
    const [x, y] = mainWindow.getPosition();
    settings.position = { x, y };
    saveSettings();
  }
});

app.on('second-instance', () => {
  if (mainWindow) {
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.focus();
  }
});

const gotTheLock = app.requestSingleInstanceLock();

if (!gotTheLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.focus();
    }
  });
}

// Auto-updater events (placeholder for future implementation)
app.on('ready', () => {
  if (process.platform === 'win32') {
    app.setAppUserModelId('com.buddie.desktop');
  }
});

// Security: Prevent new window creation
app.on('web-contents-created', (event, contents) => {
  contents.on('new-window', (navigationEvent, navigationURL) => {
    navigationEvent.preventDefault();
    shell.openExternal(navigationURL);
  });
});

// Handle certificate errors
app.on('certificate-error', (event, webContents, url, error, certificate, callback) => {
  if (isDev) {
    event.preventDefault();
    callback(true);
  } else {
    callback(false);
  }
});

module.exports = { mainWindow, settingsWindow, tray };