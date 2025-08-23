// See the Electron documentation for details on how to use preload scripts:
// https://www.electronjs.org/docs/latest/tutorial/process-model#preload-scripts

const { app, BrowserWindow, Tray, Menu, nativeImage, ipcMain, shell, dialog, desktopCapturer } = require('electron');
const path = require('path');
const fs = require('fs').promises;
const chokidar = require('chokidar');

// 处理创建/删除快捷方式到桌面
if (require('electron-squirrel-startup')) {
  app.quit();
}

let mainWindow = null;
let settingsWindow = null;
let chatWindow = null;
let tray = null;
let refreshWatcher = null; // 热重载监听器

// 设置存储路径
let settingsPath;
let tempDir;

if (process.env.NODE_ENV === 'development') {
  // 开发环境：使用项目根目录，避免webpack编译目录问题
  settingsPath = path.join(process.cwd(), 'settings.json');
  tempDir = path.join(process.cwd(), 'temp');
} else {
  // 生产环境：使用可执行文件目录
  settingsPath = path.join(path.dirname(process.execPath), 'settings.json');
  tempDir = path.join(path.dirname(process.execPath), 'temp');
}

console.log('设置文件路径:', settingsPath);
console.log('临时文件目录:', tempDir);

// 创建临时目录
async function ensureTempDir() {
  try {
    await fs.mkdir(tempDir, { recursive: true });
    console.log('临时目录已创建或已存在:', tempDir);
  } catch (error) {
    console.error('创建临时目录失败:', error);
  }
}

// 清理临时目录
async function cleanupTempDir() {
  try {
    const files = await fs.readdir(tempDir);
    for (const file of files) {
      const filePath = path.join(tempDir, file);
      await fs.unlink(filePath);
    }
    console.log('临时目录已清理');
  } catch (error) {
    console.error('清理临时目录失败:', error);
  }
}

// 默认设置（仅在用户没有配置时作为后备）
const defaultSettings = {
  theme: 'auto',
  opacity: 1,
  autoStart: false,
  alwaysOnTop: true,
  windowPosition: { x: null, y: null },
  currentCard: 0,
  models: [
    {
      id: 'default_placeholder',
      name: '请配置AI模型',
      apiUrl: '',
      apiKey: '',
      modelName: 'gpt-3.5-turbo',
      temperature: 0.7,
      isMultimodal: false
    }
  ],
  useSystemProxy: true
};

// 读取设置
const getSettings = async () => {
  try {
    const settingsData = await fs.readFile(settingsPath, 'utf8');
    const settings = JSON.parse(settingsData);
    
    // 直接返回文件中的设置，不合并默认值
    // 这样可以避免每次读取时都用默认值覆盖用户设置
    return settings;
  } catch (error) {
    // 如果文件不存在或读取失败，返回默认设置
    console.log('读取设置失败，返回默认设置:', error.message);
    return { ...defaultSettings };
  }
};

// 保存设置
const saveSettings = async (newSettings) => {
  try {
    const currentSettings = await getSettings();
    
    // 智能合并设置，特殊处理数组类型的配置
    const updatedSettings = { ...currentSettings };
    
    // 逐个处理新设置中的每个字段
    for (const [key, value] of Object.entries(newSettings)) {
      if (key === 'models' && Array.isArray(value)) {
        // 对于models数组，直接替换而不是合并
        updatedSettings.models = value;
      } else {
        // 其他配置项正常覆盖
        updatedSettings[key] = value;
      }
    }
    
    console.log('保存设置:', {
      settingsPath,
      newSettings: Object.keys(newSettings),
      modelsCount: updatedSettings.models?.length || 0
    });
    
    await fs.writeFile(settingsPath, JSON.stringify(updatedSettings, null, 2));
    
    // 应用设置到主窗口
    if (mainWindow && !mainWindow.isDestroyed()) {
      if (newSettings.opacity !== undefined) {
        mainWindow.setOpacity(newSettings.opacity);
      }
      if (newSettings.alwaysOnTop !== undefined) {
        mainWindow.setAlwaysOnTop(newSettings.alwaysOnTop);
      }
    }
    
    return updatedSettings;
  } catch (error) {
    console.error('保存设置失败:', error);
    throw error;
  }
};

// 确保设置文件存在并迁移现有用户配置
const ensureSettingsFile = async () => {
  try {
    // 检查设置文件是否存在
    const stats = await fs.stat(settingsPath);
    if (stats.size === 0) {
      console.log('设置文件为空，需要初始化');
      throw new Error('Settings file is empty');
    }
    
    // 尝试读取和验证设置文件
    const settingsData = await fs.readFile(settingsPath, 'utf8');
    const settings = JSON.parse(settingsData);
    console.log('设置文件存在并有效:', settingsPath, '配置的模型数量:', settings.models?.length || 0);
    
    // 如果用户有模型配置，确保它们被保留
    if (settings.models && settings.models.length > 0) {
      console.log('发现用户自定义模型配置:', settings.models.map(m => m.name));
    }
  } catch (error) {
    console.error('读取设置文件失败或文件不存在:', error.message);
    
    try {
      // 创建默认配置文件
      await fs.writeFile(settingsPath, JSON.stringify(defaultSettings, null, 2));
      console.log('默认设置文件已创建');
    } catch (writeError) {
      console.error('创建设置文件失败:', writeError);
      throw writeError;
    }
  }
};

const createMainWindow = async () => {
  // 确保设置文件存在并加载设置
  await ensureSettingsFile();
  const settings = await getSettings();
  
  console.log('应用启动 - 加载的设置:', {
    settingsPath,
    modelsCount: settings.models?.length || 0,
    models: settings.models?.map(m => ({ name: m.name, id: m.id, hasApiKey: !!m.apiKey })) || []
  });
  
  // 根据环境和平台确定正确的图标路径
  let iconPath;
  if (process.env.NODE_ENV === 'development') {
    // 开发环境：从项目根目录的 src/assets 查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, '..', 'src', 'assets', 'logo.ico')
      : path.join(__dirname, '..', 'src', 'assets', 'logo.png');
  } else {
    // 生产环境：从打包后的 assets 目录查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, 'assets', 'logo.ico')
      : path.join(__dirname, 'assets', 'logo.png');
  }
  
  const appIcon = nativeImage.createFromPath(iconPath);
  
  // 创建浏览器窗口
  mainWindow = new BrowserWindow({
    width: 260,
    height: 300,
    x: settings.windowPosition?.x || undefined,
    y: settings.windowPosition?.y || undefined,
    frame: false,
    transparent: true,
    resizable: false,
    alwaysOnTop: settings.alwaysOnTop !== undefined ? settings.alwaysOnTop : defaultSettings.alwaysOnTop,
    skipTaskbar: false,
    icon: appIcon,
    webPreferences: {
      preload: MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY,
      nodeIntegration: false,
      contextIsolation: true,
    },
    title: 'Buddie',
    show: false,
  });

  // 设置应用程序用户模型ID (Windows)
  if (process.platform === 'win32') {
    app.setAppUserModelId('com.buddie.app');
  }

  // 设置透明度
  mainWindow.setOpacity(settings.opacity !== undefined ? settings.opacity : defaultSettings.opacity);

  // 加载应用程序的index.html
  mainWindow.loadURL(MAIN_WINDOW_WEBPACK_ENTRY);

  // 窗口准备好显示时显示
  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
    
    // 开发环境下打开开发者工具
    if (process.env.NODE_ENV === 'development') {
      mainWindow.webContents.openDevTools({ mode: 'detach' });
    }
  });

  // 监听窗口关闭事件，隐藏到系统托盘而不是退出
  mainWindow.on('close', (event) => {
    if (!app.isQuitting) {
      event.preventDefault();
      mainWindow.hide();
    }
  });

  // 当所有窗口关闭时
  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  return mainWindow;
};

// 创建系统托盘
const createTray = () => {
  // 根据环境和平台确定正确的图标路径
  let iconPath;
  if (process.env.NODE_ENV === 'development') {
    // 开发环境：从项目根目录的 src/assets 查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, '..', 'src', 'assets', 'logo.ico')
      : path.join(__dirname, '..', 'src', 'assets', 'logo.png');
  } else {
    // 生产环境：从打包后的 assets 目录查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, 'assets', 'logo.ico')
      : path.join(__dirname, 'assets', 'logo.png');
  }
  
  const trayIcon = nativeImage.createFromPath(iconPath);
  
  tray = new Tray(trayIcon);

  const contextMenu = Menu.buildFromTemplate([
    {
      label: '显示',
      click: () => {
        mainWindow.show();
      }
    },
    {
      label: '设置',
      click: () => {
        showSettings();
      }
    },
    {
      type: 'separator'
    },
    {
      label: '退出',
      click: () => {
        app.isQuitting = true;
        app.quit();
      }
    }
  ]);

  tray.setToolTip('Buddie');
  tray.setContextMenu(contextMenu);

  // 点击托盘图标时显示/隐藏主窗口
  tray.on('click', () => {
    if (mainWindow.isVisible()) {
      mainWindow.hide();
    } else {
      mainWindow.show();
    }
  });
};

// 在Electron完成初始化并准备创建浏览器窗口时调用此方法
app.whenReady().then(async () => {
  // 创建临时目录
  await ensureTempDir();
  
  await createMainWindow();
  
  // 延迟创建托盘，确保窗口已经创建
  setTimeout(() => {
    createTray();
  }, 1000);
  
  // 在开发环境中设置热重载
  if (process.env.NODE_ENV === 'development') {
    setupHotReload();
  }
});

// 当所有窗口关闭时退出应用程序（macOS例外）
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  // 在macOS上，当点击dock图标且没有其他窗口打开时重新创建窗口
  if (BrowserWindow.getAllWindows().length === 0) {
    createMainWindow();
  }
});

// 防止多实例
const gotTheLock = app.requestSingleInstanceLock();

if (!gotTheLock) {
  app.quit();
} else {
  app.on('second-instance', (event, commandLine, workingDirectory) => {
    // 当运行第二个实例时，聚焦主窗口
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.focus();
      mainWindow.show();
    }
  });
}

// 热重载功能（仅开发环境）
const setupHotReload = () => {
  // 在开发环境中使用项目根目录作为基准
  const projectRoot = path.resolve(__dirname, '..', '..');
  const watchPaths = [
    path.join(projectRoot, 'src', 'main.js'),
    path.join(projectRoot, 'src', 'renderer.js'),
    path.join(projectRoot, 'src', 'preload.js'),
    path.join(projectRoot, 'src', 'index.html'),
    path.join(projectRoot, 'src', 'index.css'),
    path.join(projectRoot, 'src', 'settings.html'),
    path.join(projectRoot, 'src', 'settings.css'),
    path.join(projectRoot, 'src', 'settings.js'),
    path.join(projectRoot, 'src', 'chat.html'),
    path.join(projectRoot, 'src', 'chat.css'),
    path.join(projectRoot, 'src', 'chat.js'),
  ];

  refreshWatcher = chokidar.watch(watchPaths, {
    ignored: /node_modules/,
    persistent: true
  });

  refreshWatcher.on('change', (filePath) => {
    console.log(`文件变化: ${filePath}`);
    
    // 主进程文件变化时重启应用
    if (filePath.includes('main.js')) {
      console.log('主进程文件变化，重启应用...');
      app.relaunch();
      app.exit();
    } else {
      // 渲染进程文件变化时重新加载页面
      console.log('渲染进程文件变化，重新加载页面...');
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.reload();
      }
      if (settingsWindow && !settingsWindow.isDestroyed()) {
        settingsWindow.webContents.reload();
      }
      if (chatWindow && !chatWindow.isDestroyed()) {
        chatWindow.webContents.reload();
      }
    }
  });
};

// 应用退出时清理
app.on('before-quit', async () => {
  if (refreshWatcher) {
    refreshWatcher.close();
  }
  // 清理临时目录
  await cleanupTempDir();
});

// 设置窗口功能
const showSettings = () => {
  if (settingsWindow) {
    settingsWindow.show();
    settingsWindow.focus();
    settingsWindow.setAlwaysOnTop(true);
    return;
  }

  const mainPos = mainWindow.getPosition();
  const mainBounds = mainWindow.getBounds();
  
  // 根据环境和平台确定正确的图标路径
  let iconPath;
  if (process.env.NODE_ENV === 'development') {
    // 开发环境：从项目根目录的 src/assets 查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, '..', 'src', 'assets', 'logo.ico')
      : path.join(__dirname, '..', 'src', 'assets', 'logo.png');
  } else {
    // 生产环境：从打包后的 assets 目录查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, 'assets', 'logo.ico')
      : path.join(__dirname, 'assets', 'logo.png');
  }
  
  const appIcon = nativeImage.createFromPath(iconPath);
  
  settingsWindow = new BrowserWindow({
    width: 400,
    height: 500,
    x: mainPos[0] + (mainBounds.width - 400) / 2, // 水平居中
    y: mainPos[1] - 510, // 向上显示在主窗口正上方，留10px间距
    frame: false,
    transparent: true,
    resizable: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    icon: appIcon,
    webPreferences: {
      preload: MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY,
      nodeIntegration: false,
      contextIsolation: true,
    },
    title: 'Buddie 设置',
  });
  
  // 添加焦点事件处理，确保点击时窗口显示在最上层
  settingsWindow.on('focus', () => {
    settingsWindow.setAlwaysOnTop(true);
    settingsWindow.show();
  });
  
  settingsWindow.on('blur', () => {
    // 可选：失去焦点时仍保持置顶
    settingsWindow.setAlwaysOnTop(true);
  });

  // 加载设置页面
  try {
    // In development, always use loadFile to avoid webpack issues
    if (process.env.NODE_ENV === 'development') {
      // 在开发环境中使用项目根目录作为基准
      const projectRoot = path.resolve(__dirname, '..', '..');
      const settingsPath = path.join(projectRoot, 'src', 'settings.html');
      console.log('Loading settings window from:', settingsPath);
      settingsWindow.loadFile(settingsPath);
    } else if (typeof SETTINGS_WINDOW_WEBPACK_ENTRY !== 'undefined') {
      settingsWindow.loadURL(SETTINGS_WINDOW_WEBPACK_ENTRY);
    } else {
      // Fallback
      const settingsPath = path.resolve(__dirname, '..', 'src', 'settings.html');
      settingsWindow.loadFile(settingsPath);
    }
  } catch (error) {
    console.error('Error loading settings window:', error);
    // Fallback method
    const projectRoot = path.resolve(__dirname, '..', '..');
    const settingsPath = path.join(projectRoot, 'src', 'settings.html');
    console.log('Fallback: Loading settings window from:', settingsPath);
    settingsWindow.loadFile(settingsPath);
  }

  // 注入卡片数据到设置页面
  settingsWindow.webContents.once('dom-ready', () => {
    // 可以在这里注入数据
  });

  // 监听主窗口位置变化，让设置窗口跟随移动
  const updateSettingsPosition = () => {
    if (settingsWindow && !settingsWindow.isDestroyed() && mainWindow && !mainWindow.isDestroyed()) {
      const mainPos = mainWindow.getPosition();
      const mainBounds = mainWindow.getBounds();
      settingsWindow.setPosition(
        mainPos[0] + (mainBounds.width - 260) / 2, // 水平居中
        mainPos[1] - 310 // 向上显示在主窗口正上方，留10px间距
      );
    }
  };

  // 监听主窗口移动事件
  mainWindow.on('move', updateSettingsPosition);

  settingsWindow.on('closed', () => {
    settingsWindow = null;
    // 移除主窗口的move监听器（先检查主窗口是否存在）
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.removeListener('move', updateSettingsPosition);
    }
  });
};

const showChatInterface = (cardData) => {
  if (chatWindow) {
    chatWindow.show();
    chatWindow.focus();
    chatWindow.setAlwaysOnTop(true);
    return;
  }

  const mainPos = mainWindow.getPosition();
  const mainBounds = mainWindow.getBounds();
  
  // 根据环境和平台确定正确的图标路径
  let iconPath;
  if (process.env.NODE_ENV === 'development') {
    // 开发环境：从项目根目录的 src/assets 查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, '..', 'src', 'assets', 'logo.ico')
      : path.join(__dirname, '..', 'src', 'assets', 'logo.png');
  } else {
    // 生产环境：从打包后的 assets 目录查找
    iconPath = process.platform === 'win32' 
      ? path.join(__dirname, 'assets', 'logo.ico')
      : path.join(__dirname, 'assets', 'logo.png');
  }
  
  const appIcon = nativeImage.createFromPath(iconPath);
  
  chatWindow = new BrowserWindow({
    width: 500,
    height: 700,
    x: mainPos[0] + mainBounds.width - 500 + 10, // 右边缘对齐，留10px间距
    y: mainPos[1] - 710, // 上方显示，留10px间距
    frame: false,
    transparent: true,
    resizable: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    icon: appIcon,
    webPreferences: {
      preload: MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY,
      nodeIntegration: false,
      contextIsolation: true,
    },
    title: 'Buddie 对话',
  });
  
  // 添加焦点事件处理，确保点击时窗口显示在最上层
  chatWindow.on('focus', () => {
    chatWindow.setAlwaysOnTop(true);
    chatWindow.show();
  });
  
  chatWindow.on('blur', () => {
    // 可选：失去焦点时仍保持置顶
    chatWindow.setAlwaysOnTop(true);
  });
  
  // 开发环境下打开开发者工具
  if (process.env.NODE_ENV === 'development') {
    chatWindow.webContents.openDevTools({ mode: 'detach' });
  }

  // 加载对话页面
  try {
    // In development, always use loadFile to avoid webpack issues
    if (process.env.NODE_ENV === 'development') {
      // 在开发环境中使用项目根目录作为基准
      const projectRoot = path.resolve(__dirname, '..', '..');
      const chatPath = path.join(projectRoot, 'src', 'chat.html');
      console.log('Loading chat window from:', chatPath);
      chatWindow.loadFile(chatPath);
    } else if (typeof CHAT_WINDOW_WEBPACK_ENTRY !== 'undefined') {
      chatWindow.loadURL(CHAT_WINDOW_WEBPACK_ENTRY);
    } else {
      // Fallback
      const chatPath = path.resolve(__dirname, '..', 'src', 'chat.html');
      chatWindow.loadFile(chatPath);
    }
  } catch (error) {
    console.error('Error loading chat window:', error);
    // Fallback method
    const projectRoot = path.resolve(__dirname, '..', '..');
    const chatPath = path.join(projectRoot, 'src', 'chat.html');
    console.log('Fallback: Loading chat window from:', chatPath);
    chatWindow.loadFile(chatPath);
  }

  // 注入卡片数据到对话页面
  chatWindow.webContents.once('dom-ready', () => {
    if (cardData) {
      chatWindow.webContents.executeJavaScript(`
        if (window.initializeCardData) {
          window.initializeCardData(${JSON.stringify(cardData)});
        }
      `);
    }
  });

  // 监听主窗口位置变化，让对话窗口跟随移动
  const updateChatPosition = () => {
    if (chatWindow && !chatWindow.isDestroyed() && mainWindow && !mainWindow.isDestroyed()) {
      const mainPos = mainWindow.getPosition();
      const mainBounds = mainWindow.getBounds();
      chatWindow.setPosition(
        mainPos[0] + mainBounds.width - 500 + 10, // 右边缘对齐
        mainPos[1] - 710 // 向上显示在主窗口正上方，留10px间距
      );
    }
  };

  // 监听主窗口移动事件
  mainWindow.on('move', updateChatPosition);

  chatWindow.on('closed', () => {
    chatWindow = null;
    // 移除主窗口的move监听器（先检查主窗口是否存在）
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.removeListener('move', updateChatPosition);
    }
  });
};

// 处理拖动窗口的IPC消息
let dragPositionTimeout;

ipcMain.on('drag-window', (event, position) => {
  console.log('主进程收到拖拽请求:', position);
  if (mainWindow) {
    console.log('设置窗口位置:', position.x, position.y);
    mainWindow.setPosition(position.x, position.y);
    
    // 防抖保存位置
    clearTimeout(dragPositionTimeout);
    dragPositionTimeout = setTimeout(async () => {
      await saveSettings({ 
        windowPosition: { x: position.x, y: position.y } 
      });
      console.log('保存窗口位置到设置');
    }, 500);
  } else {
    console.warn('主窗口不存在，无法拖拽');
  }
});

// 获取窗口位置
ipcMain.handle('get-window-position', () => {
  if (mainWindow) {
    const position = mainWindow.getPosition();
    const result = { x: position[0], y: position[1] };
    console.log('获取窗口位置:', result);
    return result;
  }
  console.warn('主窗口不存在，返回默认位置');
  return { x: 0, y: 0 };
});

// 设置相关的IPC处理
ipcMain.handle('get-settings', getSettings);
ipcMain.handle('save-settings', async (event, settings) => {
  return await saveSettings(settings);
});

// 保存当前卡片索引
ipcMain.handle('save-current-card', async (event, cardIndex) => {
  await saveSettings({ currentCard: cardIndex });
});

// 卡片切换相关的IPC处理
ipcMain.handle('trigger-card-switch', (event, direction) => {
  try {
    // 将切换请求转发给主窗口
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('trigger-card-switch', direction);
      return { success: true };
    }
    return { success: false, error: '主窗口不可用' };
  } catch (error) {
    console.error('卡片切换失败:', error);
    return { success: false, error: error.message };
  }
});

// 刷新卡片
ipcMain.on('refresh-cards', (event) => {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send('refresh-cards');
  }
});

// 聊天功能相关的IPC处理
ipcMain.handle('show-chat-interface', (event, cardData) => {
  try {
    showChatInterface(cardData);
    return { success: true };
  } catch (error) {
    console.error('显示对话界面失败:', error);
    return { success: false, error: error.message };
  }
});

// 添加发送聊天消息的IPC处理
ipcMain.handle('send-chat-message', async (event, data) => {
  const { message, modelId, image } = data;
  
  try {
    // 获取当前设置以获取模型配置
    const settings = await getSettings();
    const models = settings.models || [];
    
    console.log('发送聊天消息 - 设置信息:', {
      modelsCount: models.length,
      modelId: modelId,
      hasImage: !!image,
      availableModels: models.map(m => ({ name: m.name, id: m.id, modelName: m.modelName, isMultimodal: m.isMultimodal }))
    });
    
    // 查找对应的模型配置
    let modelConfig = models.find(m => m.id === modelId);
    if (!modelConfig && models.length > 0) {
      // 如果没找到对应模型，使用第一个可用模型
      modelConfig = models[0];
      console.log('未找到指定模型，使用第一个可用模型:', { 
        requestedModelId: modelId, 
        usingModel: { name: modelConfig.name, id: modelConfig.id }
      });
    }
    
    if (!modelConfig || !modelConfig.apiUrl || !modelConfig.apiKey) {
      console.error('模型配置检查失败:', { modelConfig });
      throw new Error('模型配置不完整，请检查API URL和API Key');
    }
    
    console.log('开始发送聊天消息:', { message, hasImage: !!image, modelConfig: { ...modelConfig, apiKey: '***' } });
    
    // 构建消息内容，支持多模态
    let messageContent;
    if (image && modelConfig.isMultimodal) {
      // 多模态消息：包含文本和图片
      messageContent = [
        {
          type: "text",
          text: message || "请分析这张图片"
        },
        {
          type: "image_url",
          image_url: {
            url: image.dataUrl
          }
        }
      ];
    } else {
      // 纯文本消息
      messageContent = message;
    }
    
    // 发送请求到AI API
    const response = await fetch(modelConfig.apiUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${modelConfig.apiKey}`,
      },
      body: JSON.stringify({
        model: modelConfig.modelName,
        messages: [
          {
            role: 'user',
            content: messageContent
          }
        ],
        temperature: modelConfig.temperature || 0.7,
        stream: true, // 启用流式响应
      }),
    });
    
    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`API请求失败: ${response.status} ${response.statusText}\n${errorText}`);
    }
    
    // 处理流式响应
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    
    try {
      while (true) {
        const { done, value } = await reader.read();
        
        if (done) {
          // 流式响应完成
          if (chatWindow && !chatWindow.isDestroyed()) {
            chatWindow.webContents.send('chat-stream-end');
          }
          break;
        }
        
        // 解析SSE数据
        const chunk = decoder.decode(value);
        const lines = chunk.split('\n');
        
        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6).trim();
            
            if (data === '[DONE]') {
              // 流式响应完成
              if (chatWindow && !chatWindow.isDestroyed()) {
                chatWindow.webContents.send('chat-stream-end');
              }
              return;
            }
            
            try {
              const parsed = JSON.parse(data);
              const content = parsed.choices?.[0]?.delta?.content;
              
              if (content && chatWindow && !chatWindow.isDestroyed()) {
                chatWindow.webContents.send('chat-stream-chunk', content);
              }
            } catch (parseError) {
              // 忽略解析错误，继续处理下一行
              console.warn('解析SSE数据失败:', parseError, 'data:', data);
            }
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
    
    return { success: true };
    
  } catch (error) {
    console.error('发送聊天消息失败:', error);
    
    // 发送错误到聊天窗口
    if (chatWindow && !chatWindow.isDestroyed()) {
      chatWindow.webContents.send('chat-stream-error', error.message);
    }
    
  }
});

// TTS 功能相关的IPC处理
ipcMain.handle('send-tts-request', async (event, data) => {
  const { text, ttsConfigId } = data;
  
  try {
    // 获取当前设置以获取TTS配置
    const settings = await getSettings();
    const ttsConfigs = settings.ttsConfigs || [];
    
    console.log('发送TTS请求 - 配置信息:', {
      ttsConfigsCount: ttsConfigs.length,
      ttsConfigId: ttsConfigId,
      availableConfigs: ttsConfigs.map(t => ({ name: t.name, id: t.id }))
    });
    
    // 查找对应的TTS配置
    let ttsConfig = ttsConfigs.find(t => t.id === ttsConfigId);
    if (!ttsConfig && ttsConfigs.length > 0) {
      // 如果没找到对应配置，使用第一个可用配置
      ttsConfig = ttsConfigs[0];
      console.log('未找到指定TTS配置，使用第一个可用配置:', { 
        requestedConfigId: ttsConfigId, 
        usingConfig: { name: ttsConfig.name, id: ttsConfig.id }
      });
    }
    
    if (!ttsConfig || !ttsConfig.apiUrl || !ttsConfig.apiKey) {
      console.error('TTS配置检查失败:', { ttsConfig });
      throw new Error('TTS配置不完整，请检查API URL和API Key');
    }
    
    console.log('开始TTS请求:', { text: text.substring(0, 50) + '...', ttsConfig: { ...ttsConfig, apiKey: '***' } });
    
    // 发送请求到TTS API
    const response = await fetch(ttsConfig.apiUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${ttsConfig.apiKey}`,
      },
      body: JSON.stringify({
        model: ttsConfig.model || 'tts-1',
        input: text,
        voice: ttsConfig.voice || 'alloy',
        speed: ttsConfig.speed || 1.0,
        response_format: ttsConfig.responseFormat || 'mp3'
      }),
    });
    
    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`TTS API请求失败: ${response.status} ${response.statusText}\n${errorText}`);
    }
    
    // 获取音频数据
    const audioBuffer = await response.arrayBuffer();
    
    // 生成唯一的文件名
    const timestamp = Date.now();
    const fileName = `tts_${timestamp}.${ttsConfig.responseFormat || 'mp3'}`;
    const filePath = path.join(tempDir, fileName);
    
    // 保存音频文件到临时目录
    await fs.writeFile(filePath, Buffer.from(audioBuffer));
    console.log('TTS音频文件已保存:', filePath);
    
    return { 
      success: true, 
      filePath: filePath,
      format: ttsConfig.responseFormat || 'mp3'
    };
    
  } catch (error) {
    console.error('TTS请求失败:', error);
    throw error;
  }
});

// 获取TTS配置列表
ipcMain.handle('get-tts-configs', async () => {
  try {
    const settings = await getSettings();
    return settings.ttsConfigs || [];
  } catch (error) {
    console.error('获取TTS配置失败:', error);
    return [];
  }
});

// 屏幕截图功能
ipcMain.handle('capture-screen', async () => {
  try {
    // 保存当前窗口状态
    const windowStates = [];
    
    // 隐藏所有应用窗口
    if (mainWindow && !mainWindow.isDestroyed()) {
      windowStates.push({
        window: mainWindow,
        wasVisible: mainWindow.isVisible(),
        opacity: mainWindow.getOpacity()
      });
      mainWindow.hide();
    }
    
    if (settingsWindow && !settingsWindow.isDestroyed()) {
      windowStates.push({
        window: settingsWindow,
        wasVisible: settingsWindow.isVisible(),
        opacity: settingsWindow.getOpacity()
      });
      settingsWindow.hide();
    }
    
    if (chatWindow && !chatWindow.isDestroyed()) {
      windowStates.push({
        window: chatWindow,
        wasVisible: chatWindow.isVisible(),
        opacity: chatWindow.getOpacity()
      });
      chatWindow.hide();
    }
    
    // 等待一小段时间确保窗口完全隐藏
    await new Promise(resolve => setTimeout(resolve, 200));
    
    try {
      // 获取所有屏幕源
      const sources = await desktopCapturer.getSources({ 
        types: ['screen'],
        thumbnailSize: { width: 1920, height: 1080 }
      });
      
      if (sources.length > 0) {
        // 返回主屏幕的截图数据URL
        const screenSource = sources[0];
        const result = {
          success: true,
          dataUrl: screenSource.thumbnail.toDataURL(),
          name: screenSource.name
        };
        
        return result;
      }
      
      return {
        success: false,
        error: '未找到可用的屏幕'
      };
    } finally {
      // 恢复所有应用窗口
      windowStates.forEach(state => {
        if (state.window && !state.window.isDestroyed()) {
          if (state.wasVisible) {
            state.window.show();
            state.window.setOpacity(state.opacity);
          }
        }
      });
    }
  } catch (error) {
    console.error('屏幕截图失败:', error);
    
    // 确保在错误情况下也恢复窗口
    try {
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.show();
      }
      if (settingsWindow && !settingsWindow.isDestroyed()) {
        settingsWindow.show();
      }
      if (chatWindow && !chatWindow.isDestroyed()) {
        chatWindow.show();
      }
    } catch (restoreError) {
      console.error('恢复窗口失败:', restoreError);
    }
    
    return {
      success: false,
      error: error.message
    };
  }
});

// 获取可用的屏幕源（用于选择屏幕）
ipcMain.handle('get-screen-sources', async () => {
  try {
    // 同样需要隐藏窗口来获取干净的预览
    const windowStates = [];
    
    // 隐藏所有应用窗口
    if (mainWindow && !mainWindow.isDestroyed()) {
      windowStates.push({
        window: mainWindow,
        wasVisible: mainWindow.isVisible(),
        opacity: mainWindow.getOpacity()
      });
      mainWindow.hide();
    }
    
    if (settingsWindow && !settingsWindow.isDestroyed()) {
      windowStates.push({
        window: settingsWindow,
        wasVisible: settingsWindow.isVisible(),
        opacity: settingsWindow.getOpacity()
      });
      settingsWindow.hide();
    }
    
    if (chatWindow && !chatWindow.isDestroyed()) {
      windowStates.push({
        window: chatWindow,
        wasVisible: chatWindow.isVisible(),
        opacity: chatWindow.getOpacity()
      });
      chatWindow.hide();
    }
    
    // 等待一小段时间确保窗口完全隐藏
    await new Promise(resolve => setTimeout(resolve, 100));
    
    try {
      const sources = await desktopCapturer.getSources({ 
        types: ['screen'],
        thumbnailSize: { width: 150, height: 150 }
      });
      
      return {
        success: true,
        sources: sources.map(source => ({
          id: source.id,
          name: source.name,
          thumbnail: source.thumbnail.toDataURL()
        }))
      };
    } finally {
      // 恢复所有应用窗口
      windowStates.forEach(state => {
        if (state.window && !state.window.isDestroyed()) {
          if (state.wasVisible) {
            state.window.show();
            state.window.setOpacity(state.opacity);
          }
        }
      });
    }
  } catch (error) {
    console.error('获取屏幕源失败:', error);
    
    // 确保在错误情况下也恢复窗口
    try {
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.show();
      }
      if (settingsWindow && !settingsWindow.isDestroyed()) {
        settingsWindow.show();
      }
      if (chatWindow && !chatWindow.isDestroyed()) {
        chatWindow.show();
      }
    } catch (restoreError) {
      console.error('恢复窗口失败:', restoreError);
    }
    
    return {
      success: false,
      error: error.message
    };
  }
});

// 截取指定屏幕
ipcMain.handle('capture-specific-screen', async (event, sourceId) => {
  try {
    // 保存当前窗口状态
    const windowStates = [];
    
    // 隐藏所有应用窗口
    if (mainWindow && !mainWindow.isDestroyed()) {
      windowStates.push({
        window: mainWindow,
        wasVisible: mainWindow.isVisible(),
        opacity: mainWindow.getOpacity()
      });
      mainWindow.hide();
    }
    
    if (settingsWindow && !settingsWindow.isDestroyed()) {
      windowStates.push({
        window: settingsWindow,
        wasVisible: settingsWindow.isVisible(),
        opacity: settingsWindow.getOpacity()
      });
      settingsWindow.hide();
    }
    
    if (chatWindow && !chatWindow.isDestroyed()) {
      windowStates.push({
        window: chatWindow,
        wasVisible: chatWindow.isVisible(),
        opacity: chatWindow.getOpacity()
      });
      chatWindow.hide();
    }
    
    // 等待一小段时间确保窗口完全隐藏
    await new Promise(resolve => setTimeout(resolve, 200));
    
    try {
      const sources = await desktopCapturer.getSources({ 
        types: ['screen'],
        thumbnailSize: { width: 1920, height: 1080 }
      });
      
      const targetSource = sources.find(source => source.id === sourceId);
      if (targetSource) {
        return {
          success: true,
          dataUrl: targetSource.thumbnail.toDataURL(),
          name: targetSource.name
        };
      }
      
      return {
        success: false,
        error: '未找到指定的屏幕'
      };
    } finally {
      // 恢复所有应用窗口
      windowStates.forEach(state => {
        if (state.window && !state.window.isDestroyed()) {
          if (state.wasVisible) {
            state.window.show();
            state.window.setOpacity(state.opacity);
          }
        }
      });
    }
  } catch (error) {
    console.error('截取指定屏幕失败:', error);
    
    // 确保在错误情况下也恢复窗口
    try {
      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.show();
      }
      if (settingsWindow && !settingsWindow.isDestroyed()) {
        settingsWindow.show();
      }
      if (chatWindow && !chatWindow.isDestroyed()) {
        chatWindow.show();
      }
    } catch (restoreError) {
      console.error('恢复窗口失败:', restoreError);
    }
    
    return {
      success: false,
      error: error.message
    };
  }
});

// 通知卡片索引变化（从主窗口到设置窗口）
ipcMain.on('card-index-changed', (event, cardIndex) => {
  // 转发给设置窗口
  if (settingsWindow && !settingsWindow.isDestroyed()) {
    settingsWindow.webContents.send('card-index-changed', cardIndex);
  }
});

// 在main.js中添加卡片切换同步
ipcMain.on('card-switched', (event, cardData) => {
  console.log('主进程收到卡片切换事件:', cardData);
  
  // 转发给聊天窗口
  if (chatWindow && !chatWindow.isDestroyed()) {
    console.log('转发卡片切换事件到聊天窗口');
    chatWindow.webContents.send('card-switched', cardData);
  }
});

// 在此文件中处理其他任何特定于应用程序的需求的代码...