import { app, BrowserWindow, Tray, Menu, globalShortcut, ipcMain, session } from 'electron';
import path from 'path';
import { getTransformersASRInstance } from './transformers-asr';

// Electron Forge Vite 插件生成的全局变量
declare const MAIN_WINDOW_VITE_DEV_SERVER_URL: string | undefined;
declare const MAIN_WINDOW_VITE_NAME: string;

// Handle creating/removing shortcuts on Windows when installing/uninstalling.
if (require('electron-squirrel-startup')) {
  app.quit();
}

let mainWindow: BrowserWindow | null = null;
let tray: Tray | null = null;
let speechRecognizer = getTransformersASRInstance();

// 代理配置接口
interface ProxyConfig {
  enabled: boolean;
  useSystemProxy: boolean;
  manualProxy?: {
    host: string;
    port: number;
    auth?: {
      username: string;
      password: string;
    };
  };
}

// 默认代理配置
const defaultProxyConfig: ProxyConfig = {
  enabled: true,
  useSystemProxy: true
};

// 获取系统代理设置
function getSystemProxyConfig(): string {
  // 在 Windows 上获取系统代理
  if (process.platform === 'win32') {
    try {
      const { execSync } = require('child_process');
      const result = execSync('reg query "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings" /v ProxyEnable', { encoding: 'utf8' });
      
      if (result.includes('0x1')) {
        const proxyResult = execSync('reg query "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings" /v ProxyServer', { encoding: 'utf8' });
        const match = proxyResult.match(/ProxyServer\s+REG_SZ\s+(.+)/);
        if (match) {
          return match[1].trim();
        }
      }
    } catch (error) {
      console.log('无法获取系统代理设置:', error);
    }
  }
  
  // 尝试从环境变量获取代理
  return process.env.HTTP_PROXY || process.env.http_proxy || 
         process.env.HTTPS_PROXY || process.env.https_proxy || '';
}

// 配置应用代理
function configureProxy(config: ProxyConfig): void {
  const ses = session.defaultSession;
  
  if (!config.enabled) {
    // 禁用代理
    ses.setProxy({ mode: 'direct' });
    console.log('代理已禁用');
    return;
  }
  
  if (config.useSystemProxy) {
    // 使用系统代理
    const systemProxy = getSystemProxyConfig();
    if (systemProxy) {
      ses.setProxy({
        mode: 'fixed_servers',
        proxyRules: systemProxy
      });
      console.log('使用系统代理:', systemProxy);
    } else {
      // 如果没有系统代理，使用自动检测
      ses.setProxy({ mode: 'auto_detect' });
      console.log('使用自动代理检测');
    }
  } else if (config.manualProxy) {
    // 使用手动配置的代理
    const { host, port, auth } = config.manualProxy;
    let proxyRules = `http://${host}:${port};https://${host}:${port}`;
    
    const proxyConfig: any = {
      mode: 'fixed_servers',
      proxyRules
    };
    
    if (auth) {
      // 注意：Electron 不直接支持在 proxyRules 中设置认证
      // 需要通过 login 事件处理认证
      setupProxyAuthentication(auth.username, auth.password);
    }
    
    ses.setProxy(proxyConfig);
    console.log('使用手动代理:', proxyRules);
  }
}

// 设置代理认证
function setupProxyAuthentication(username: string, password: string): void {
  const ses = session.defaultSession;
  
  ses.on('login', (event, authenticationResponseDetails, authInfo, callback) => {
    if (authInfo.isProxy) {
      event.preventDefault();
      callback(username, password);
    }
  });
}

// 加载代理配置
function loadProxyConfig(): ProxyConfig {
  try {
    const fs = require('fs');
    const configPath = path.join(app.getPath('userData'), 'proxy-config.json');
    
    if (fs.existsSync(configPath)) {
      const configData = fs.readFileSync(configPath, 'utf8');
      return { ...defaultProxyConfig, ...JSON.parse(configData) };
    }
  } catch (error) {
    console.log('加载代理配置失败，使用默认配置:', error);
  }
  
  return defaultProxyConfig;
}

// 保存代理配置
function saveProxyConfig(config: ProxyConfig): void {
  try {
    const fs = require('fs');
    const configPath = path.join(app.getPath('userData'), 'proxy-config.json');
    fs.writeFileSync(configPath, JSON.stringify(config, null, 2));
    console.log('代理配置已保存');
  } catch (error) {
    console.error('保存代理配置失败:', error);
  }
}

const createWindow = () => {
  // Create the browser window.
  mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    minWidth: 300,
    minHeight: 300,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: true,
    },
  });

  // Remove menu bar
  mainWindow.setMenuBarVisibility(false);

  // and load the index.html of the app.
  if (MAIN_WINDOW_VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(MAIN_WINDOW_VITE_DEV_SERVER_URL);
  } else {
    mainWindow.loadFile(path.join(__dirname, `../renderer/${MAIN_WINDOW_VITE_NAME}/index.html`));
  }

  // Open the DevTools.
  mainWindow.webContents.openDevTools();
};

const createTray = () => {
  // 创建系统托盘图标
  let iconPath = path.join(__dirname, 'assets/icon.png');
  
  // 在开发模式下，图标路径不同
  if (process.env.NODE_ENV === 'development') {
    iconPath = path.join(__dirname, '../../assets/icon.png');
  }
  
  // 如果图标不存在，尝试其他路径
  if (!require('fs').existsSync(iconPath)) {
    // 尝试在构建目录中查找
    const alternativePath = path.join(__dirname, 'icon.png');
    if (require('fs').existsSync(alternativePath)) {
      iconPath = alternativePath;
    } else {
      // 最后尝试从项目根目录加载
      const rootIconPath = path.join(__dirname, '../../../assets/icon.png');
      if (require('fs').existsSync(rootIconPath)) {
        iconPath = rootIconPath;
      }
    }
  }
  
  tray = new Tray(iconPath);
  
  // 创建上下文菜单
  const contextMenu = Menu.buildFromTemplate([
    {
      label: '打开主界面',
      click: () => {
        if (mainWindow) {
          mainWindow.show();
        } else {
          createWindow();
        }
      }
    },
    {
      label: '设置',
      click: () => {
        if (mainWindow) {
          mainWindow.show();
          mainWindow.webContents.send('navigate-to-settings');
        } else {
          createWindow();
          // 延迟发送消息，确保窗口已创建
          setTimeout(() => {
            if (mainWindow) {
              mainWindow.webContents.send('navigate-to-settings');
            }
          }, 1000);
        }
      }
    },
    { 
      label: '退出', 
      click: () => {
        app.quit();
      } 
    }
  ]);

  // 设置托盘图标上下文菜单
  tray.setContextMenu(contextMenu);
  
  // 设置托盘图标提示
  tray.setToolTip('Lyxie Desktop');
  
  // 点击托盘图标显示主窗口
  tray.on('click', () => {
    if (mainWindow) {
      mainWindow.show();
    } else {
      createWindow();
    }
  });
};

// 注册全局快捷键
const registerGlobalShortcut = () => {
  const ret = globalShortcut.register('Shift+Alt+F11', () => {
    if (mainWindow) {
      mainWindow.show();
      mainWindow.webContents.send('trigger-home-dialog');
    } else {
      createWindow();
      // 延迟发送消息，确保窗口已创建
      setTimeout(() => {
        if (mainWindow) {
          mainWindow.webContents.send('trigger-home-dialog');
        }
      }, 1000);
    }
  });

  if (!ret) {
    console.log('全局快捷键注册失败');
  }

  // 检查快捷键是否注册成功
  console.log('全局快捷键是否注册成功:', globalShortcut.isRegistered('Shift+Alt+F11'));
};

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.on('ready', () => {
  // 初始化代理配置
  const proxyConfig = loadProxyConfig();
  configureProxy(proxyConfig);
  
  createWindow();
  createTray();
  registerGlobalShortcut();
  
  // 监听语音识别事件
  speechRecognizer.on('result', (result) => {
    sendRecognitionResult(result);
  });
  
  speechRecognizer.on('error', (error) => {
    console.error('Speech recognition error:', error);
    if (mainWindow) {
      mainWindow.webContents.send('speech-recognition-error', error.message);
    }
  });
});

// Quit when all windows are closed, except on macOS. There, it's common
// for applications and their menu bar to stay active until the user quits
// explicitly with Cmd + Q.
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  // On OS X it's common to re-create a window in the app when the
  // dock icon is clicked and there are no other windows open.
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});

// IPC 处理程序：开始语音识别
ipcMain.handle('start-speech-recognition', async () => {
  try {
    await speechRecognizer.startListening();
    return { success: true };
  } catch (error) {
    return { success: false, error: (error as Error).message };
  }
});

// IPC 处理程序：停止语音识别
ipcMain.handle('stop-speech-recognition', async () => {
  try {
    await speechRecognizer.stopListening();
    return { success: true };
  } catch (error) {
    return { success: false, error: (error as Error).message };
  }
});

// IPC 处理程序：获取模型缓存目录
ipcMain.handle('get-model-cache-dir', async () => {
  try {
    if (process.env.NODE_ENV === 'development') {
      // 开发环境：项目根目录下的models文件夹
      return path.join(process.cwd(), 'models');
    } else {
      // 生产环境：exe同级的models文件夹
      const exePath = app.getPath('exe');
      const exeDir = path.dirname(exePath);
      return path.join(exeDir, 'models');
    }
  } catch (error) {
    console.error('Failed to get model cache directory:', error);
    return './models'; // 回退到默认路径
  }
});

// IPC 处理程序：获取代理配置
ipcMain.handle('get-proxy-config', async () => {
  try {
    return loadProxyConfig();
  } catch (error) {
    console.error('Failed to get proxy config:', error);
    return defaultProxyConfig;
  }
});

// IPC 处理程序：设置代理配置
ipcMain.handle('set-proxy-config', async (event, config: ProxyConfig) => {
  try {
    saveProxyConfig(config);
    configureProxy(config);
    return { success: true };
  } catch (error) {
    console.error('Failed to set proxy config:', error);
    return { success: false, error: (error as Error).message };
  }
});

// IPC 处理程序：测试代理连接
ipcMain.handle('test-proxy-connection', async () => {
  try {
    const { net } = require('electron');
    const request = net.request('https://www.google.com');
    
    return new Promise((resolve) => {
      let responseReceived = false;
      
      const timeout = setTimeout(() => {
        if (!responseReceived) {
          resolve({ success: false, error: '连接超时' });
        }
      }, 10000);
      
      request.on('response', (response) => {
        responseReceived = true;
        clearTimeout(timeout);
        resolve({ 
          success: response.statusCode >= 200 && response.statusCode < 300,
          statusCode: response.statusCode 
        });
      });
      
      request.on('error', (error) => {
        responseReceived = true;
        clearTimeout(timeout);
        resolve({ success: false, error: error.message });
      });
      
      request.end();
    });
  } catch (error) {
    return { success: false, error: (error as Error).message };
  }
});

// IPC 处理程序：下载Whisper Tiny ONNX模型
ipcMain.handle('download-whisper-tiny-onnx', async () => {
  try {
    await speechRecognizer.downloadWhisperTinyONNX();
    return { success: true };
  } catch (error) {
    return { success: false, error: (error as Error).message };
  }
});

// 发送识别结果到渲染进程的函数
const sendRecognitionResult = (result: any) => {
  if (mainWindow) {
    mainWindow.webContents.send('speech-recognition-result', result);
  }
};

app.on('will-quit', () => {
  // 注销所有快捷键
  globalShortcut.unregisterAll();
});

// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and import them here.