import { app, BrowserWindow, Tray, Menu, globalShortcut, ipcMain, session } from 'electron';
import path from 'path';
import { getTransformersASRInstance, TransformersASRResult } from './transformers-asr';
import * as fs from 'fs';

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
let modelFileWatcher: fs.FSWatcher | null = null;
let isSavingModelStatus = false; // 防止重复保存
let isRefreshingModelStatus = false; // 防止重复刷新
let currentPage: string | null = null; // 跟踪当前页面

// 代理配置接口
interface ProxyConfig {
  enabled: boolean;
  useSystemProxy: boolean;
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
  }
}

// 获取默认代理配置
function getDefaultProxyConfig(): ProxyConfig {
  return defaultProxyConfig;
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

// 防止在保存模型状态时触发文件监控的标志
// let isSavingModelStatus = false; // 已在全局变量中定义

// 设置模型文件监控
const setupModelFileWatcher = () => {
  try {
    // 获取模型根目录
    let modelsRootDir: string;
    if (process.env.NODE_ENV === 'development') {
      modelsRootDir = path.join(process.cwd(), 'models');
    } else {
      const exePath = app.getPath('exe');
      const exeDir = path.dirname(exePath);
      modelsRootDir = path.join(exeDir, 'models');
    }
    
    // 确保目录存在
    if (!fs.existsSync(modelsRootDir)) {
      fs.mkdirSync(modelsRootDir, { recursive: true });
    }
    
    // 防抖定时器
    let fileChangeTimeout: NodeJS.Timeout | null = null;
    
    // 监控目录变化
    modelFileWatcher = fs.watch(modelsRootDir, { recursive: true }, (eventType, filename) => {
      // 如果正在保存模型状态，则跳过监控
      if (isSavingModelStatus) {
        console.log('跳过模型状态保存期间的文件监控');
        return;
      }
      
      if (filename && (filename.endsWith('.onnx') || filename.endsWith('.json') || filename.endsWith('.txt'))) {
        console.log(`检测到模型文件变化: ${eventType} ${filename}`);
        
        // 清除之前的定时器
        if (fileChangeTimeout) {
          clearTimeout(fileChangeTimeout);
        }
        
        // 设置防抖延迟，避免频繁检查
        fileChangeTimeout = setTimeout(() => {
          verifyAndUpdateModelStatus();
        }, 2000); // 2秒防抖延迟
      }
    });
    
    console.log(`已开始监控模型目录: ${modelsRootDir}`);
    
    // 初始检查一次模型状态
    setTimeout(() => {
      verifyAndUpdateModelStatus();
    }, 1000);
  } catch (error) {
    console.error('设置模型文件监控失败:', error);
  }
};

// 验证并更新模型状态
const verifyAndUpdateModelStatus = async () => {
  // 防止重复检查
  if (isRefreshingModelStatus) {
    console.log('正在刷新模型状态，跳过重复检查');
    return;
  }
  
  try {
    isRefreshingModelStatus = true;
    console.log('正在验证模型文件状态...');
    
    // 直接验证文件存在状态
    const result = await verifyModelFilesSync();
    
    if (mainWindow) {
      // 通知渲染进程更新模型状态
      mainWindow.webContents.send('model-files-changed');
      console.log('已通知渲染进程更新模型状态');
    }
  } catch (error) {
    console.error('验证模型状态失败:', error);
  } finally {
    // 延迟重置标志，避免频繁触发
    setTimeout(() => {
      isRefreshingModelStatus = false;
    }, 3000);
  }
};

// 同步验证模型文件
const verifyModelFilesSync = () => {
  try {
    // 获取模型根目录
    let modelsRootDir: string;
    if (process.env.NODE_ENV === 'development') {
      modelsRootDir = path.join(process.cwd(), 'models');
    } else {
      const exePath = app.getPath('exe');
      const exeDir = path.dirname(exePath);
      modelsRootDir = path.join(exeDir, 'models');
    }
    
    // 确保目录存在
    if (!fs.existsSync(modelsRootDir)) {
      fs.mkdirSync(modelsRootDir, { recursive: true });
    }
    
    const downloadedModels: string[] = [];
    
    // 模型映射
    const modelConfigs = {
      'Xenova/whisper-tiny': 'whisper-tiny',
      'Xenova/whisper-tiny.en': 'whisper-tiny-en',
      'Xenova/whisper-base': 'whisper-base',
      'Xenova/whisper-base.en': 'whisper-base-en'
    };
    
    // 配置文件列表
    const configFiles = [
      'added_tokens.json', 'config.json', 'generation_config.json', 'merges.txt',
      'normalizer.json', 'preprocessor_config.json', 'quant_config.json',
      'quantize_config.json', 'special_tokens_map.json', 'tokenizer.json',
      'tokenizer_config.json', 'vocab.json'
    ];
    
    // ONNX文件列表
    const onnxFiles = ['encoder_model_q4.onnx', 'decoder_model_q4.onnx'];
    
    for (const [modelId, modelName] of Object.entries(modelConfigs)) {
      const modelDir = path.join(modelsRootDir, modelName);
      const onnxDir = path.join(modelDir, 'onnx');
      
      // 检查配置文件
      const configFilesExist = configFiles.every(filename => {
        const filePath = path.join(modelDir, filename);
        const exists = fs.existsSync(filePath);
        if (!exists) {
          console.log(`配置文件不存在: ${filePath}`);
        }
        return exists;
      });
      
      // 检查ONNX文件
      const onnxFilesExist = onnxFiles.every(filename => {
        const filePath = path.join(onnxDir, filename);
        const exists = fs.existsSync(filePath);
        if (!exists) {
          console.log(`ONNX文件不存在: ${filePath}`);
        }
        return exists;
      });
      
      if (configFilesExist && onnxFilesExist) {
        downloadedModels.push(modelId);
        console.log(`模型 ${modelId} 所有文件已完整下载`);
      } else {
        console.log(`模型 ${modelId} 文件不完整 - 配置文件: ${configFilesExist}, ONNX文件: ${onnxFilesExist}`);
      }
    }
    
    console.log('当前已完整下载的模型:', downloadedModels);
    return { success: true, downloadedModels };
    
  } catch (error) {
    console.error('同步验证模型文件失败:', error);
    return { success: false, error: (error as Error).message };
  }
};

const createTray = () => {
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
  const proxyConfig = getDefaultProxyConfig();
  configureProxy(proxyConfig);
  
  createWindow();
  createTray();
  registerGlobalShortcut();
  setupModelFileWatcher(); // 设置模型文件监控
  
  // 监听语音识别事件
  speechRecognizer.on('result', (result: TransformersASRResult) => {
    sendRecognitionResult(result);
  });
  
  speechRecognizer.on('error', (error: Error) => {
    console.error('Speech recognition error:', error);
    if (mainWindow) {
      mainWindow.webContents.send('speech-recognition-error', error.message);
    }
  });
  
  // 监听页面导航变化
  ipcMain.on('navigate-to-page', (event, page: string) => {
    currentPage = page;
    console.log(`导航到页面: ${page}`);
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

// IPC 处理程序：获取模型根目录
ipcMain.handle('get-models-root-dir', async () => {
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
    console.error('Failed to get models root directory:', error);
    return './models'; // 回退到默认路径
  }
});

// IPC 处理程序：路径拼接
ipcMain.handle('join-path', async (event, base: string, ...paths: string[]) => {
  try {
    return path.join(base, ...paths);
  } catch (error) {
    console.error('Failed to join paths:', error);
    return base; // 回退到基础路径
  }
});

// IPC 处理程序：获取代理配置
ipcMain.handle('get-proxy-config', async () => {
  try {
    return getDefaultProxyConfig();
  } catch (error) {
    console.error('Failed to get proxy config:', error);
    return defaultProxyConfig;
  }
});

// IPC 处理程序：设置代理配置
ipcMain.handle('set-proxy-config', async (event, config: ProxyConfig) => {
  try {
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
      
      request.on('response', (response: any) => {
        responseReceived = true;
        clearTimeout(timeout);
        resolve({ 
          success: response.statusCode >= 200 && response.statusCode < 300,
          statusCode: response.statusCode 
        });
      });
      
      request.on('error', (error: any) => {
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

// 通用模型下载函数 - 下载ONNX和配置文件
async function downloadWhisperModel(modelConfig: {
  id: string;
  name: string;
  folderName: string;
}) {
  try {
    const fs = require('fs');
    const path = require('path');
    const { net } = require('electron');
    
    // 获取模型根目录
    let modelsRootDir: string;
    if (process.env.NODE_ENV === 'development') {
      modelsRootDir = path.join(process.cwd(), 'models');
    } else {
      const exePath = app.getPath('exe');
      const exeDir = path.dirname(exePath);
      modelsRootDir = path.join(exeDir, 'models');
    }
    
    // 创建模型文件夹和onnx子文件夹
    const modelDir = path.join(modelsRootDir, modelConfig.folderName);
    const onnxDir = path.join(modelDir, 'onnx');
    
    if (!fs.existsSync(modelDir)) {
      fs.mkdirSync(modelDir, { recursive: true });
    }
    if (!fs.existsSync(onnxDir)) {
      fs.mkdirSync(onnxDir, { recursive: true });
    }

    // 配置文件列表
    const configFiles = [
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/added_tokens.json`, filename: 'added_tokens.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/config.json`, filename: 'config.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/generation_config.json`, filename: 'generation_config.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/merges.txt`, filename: 'merges.txt', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/normalizer.json`, filename: 'normalizer.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/preprocessor_config.json`, filename: 'preprocessor_config.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/quant_config.json`, filename: 'quant_config.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/quantize_config.json`, filename: 'quantize_config.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/special_tokens_map.json`, filename: 'special_tokens_map.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/tokenizer.json`, filename: 'tokenizer.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/tokenizer_config.json`, filename: 'tokenizer_config.json', targetDir: modelDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/vocab.json`, filename: 'vocab.json', targetDir: modelDir },
    ];

    // ONNX文件列表
    const onnxFiles = [
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/onnx/encoder_model_q4.onnx`, filename: 'encoder_model_q4.onnx', targetDir: onnxDir },
      { url: `https://huggingface.co/${modelConfig.id}/resolve/main/onnx/decoder_model_q4.onnx`, filename: 'decoder_model_q4.onnx', targetDir: onnxDir },
    ];

    const allFiles = [...configFiles, ...onnxFiles];

    // 检查所有文件是否已存在
    const allFilesExist = allFiles.every(file => {
      const filePath = path.join(file.targetDir, file.filename);
      return fs.existsSync(filePath);
    });

    if (allFilesExist) {
      console.log(`所有${modelConfig.name}模型文件已存在`);
      return { success: true };
    }

    console.log(`开始下载${modelConfig.name}模型文件...`);

    // 下载单个文件的函数
    const downloadFile = (fileInfo: typeof allFiles[0]): Promise<void> => {
      return new Promise((resolve, reject) => {
        const filePath = path.join(fileInfo.targetDir, fileInfo.filename);
        
        // 如果文件已存在，跳过下载
        if (fs.existsSync(filePath)) {
          console.log(`${fileInfo.filename}已存在: ${filePath}`);
          resolve();
          return;
        }

        console.log(`开始下载${fileInfo.filename}: ${fileInfo.url}`);
        console.log(`保存到: ${filePath}`);

        const file = fs.createWriteStream(filePath);
        let downloadedBytes = 0;
        let totalBytes = 0;

        const makeRequest = (requestUrl: string, maxRedirects = 5) => {
          if (maxRedirects <= 0) {
            reject(new Error('重定向次数过多'));
            return;
          }

          const request = net.request({
            method: 'GET',
            url: requestUrl
          });

          // 增加超时时间到 5 分钟
          const timeoutId = setTimeout(() => {
            request.abort();
            file.close();
            if (fs.existsSync(filePath)) {
              fs.unlinkSync(filePath);
            }
            reject(new Error(`${fileInfo.filename}下载超时`));
          }, 300000); // 5 分钟

          request.on('response', (response: any) => {
            // 处理重定向
            if (response.statusCode >= 300 && response.statusCode < 400) {
              const location = response.headers.location;
              if (location) {
                console.log(`${fileInfo.filename}重定向到: ${location}`);
                makeRequest(location as string, maxRedirects - 1);
                return;
              }
            }

            if (response.statusCode !== 200) {
              clearTimeout(timeoutId);
              file.close();
              if (fs.existsSync(filePath)) {
                fs.unlinkSync(filePath);
              }
              reject(new Error(`${fileInfo.filename}下载失败 - HTTP ${response.statusCode}: ${response.statusMessage}`));
              return;
            }

            const contentLength = response.headers['content-length'];
            totalBytes = contentLength ? parseInt(contentLength as string) : 0;
            console.log(`${fileInfo.filename}文件大小: ${(totalBytes / 1024 / 1024).toFixed(2)} MB`);

            response.on('data', (chunk: Buffer) => {
              downloadedBytes += chunk.length;
              const progress = totalBytes > 0 ? (downloadedBytes / totalBytes) * 100 : 0;

              // 每 10MB 或每 10% 更新一次进度
              if (downloadedBytes % (10 * 1024 * 1024) < chunk.length || Math.floor(progress) % 10 === 0) {
                console.log(`${fileInfo.filename}下载进度: ${progress.toFixed(1)}% (${(downloadedBytes / 1024 / 1024).toFixed(1)} MB)`);
              }

              file.write(chunk);
            });

            response.on('end', () => {
              clearTimeout(timeoutId);
              file.close();
              console.log(`${fileInfo.filename}下载完成: ${filePath}`);
              console.log(`${fileInfo.filename}文件大小: ${(downloadedBytes / 1024 / 1024).toFixed(2)} MB`);
              resolve();
            });

            response.on('error', (err: any) => {
              clearTimeout(timeoutId);
              file.close();
              if (fs.existsSync(filePath)) {
                fs.unlinkSync(filePath);
              }
              console.error(`${fileInfo.filename}响应错误:`, err);
              reject(err);
            });
          });

          request.on('error', (err: any) => {
            file.close();
            if (fs.existsSync(filePath)) {
              fs.unlinkSync(filePath);
            }
            console.error(`${fileInfo.filename}请求错误:`, err);
            reject(err);
          });

          file.on('error', (err: any) => {
            file.close();
            if (fs.existsSync(filePath)) {
              fs.unlinkSync(filePath);
            }
            console.error(`${fileInfo.filename}文件写入错误:`, err);
            reject(err);
          });

          request.end();
        };

        makeRequest(fileInfo.url);
      });
    };

    // 依次下载所有文件
    let totalProgress = 0;
    for (let i = 0; i < allFiles.length; i++) {
      const fileInfo = allFiles[i];
      try {
        await downloadFile(fileInfo);
        totalProgress = ((i + 1) / allFiles.length) * 100;
        
        // 发送整体进度更新到渲染进程
        if (mainWindow) {
          mainWindow.webContents.send('download-progress', { 
            progress: totalProgress,
            loaded: i + 1,
            total: allFiles.length,
            currentFile: fileInfo.filename
          });
        }
      } catch (error) {
        console.error(`下载${fileInfo.filename}失败:`, error);
        throw error;
      }
    }

    console.log(`所有${modelConfig.name}模型文件下载完成`);
    return { success: true };
    
  } catch (error) {
    console.error(`下载${modelConfig.name}模型失败:`, error);
    return { success: false, error: (error as Error).message };
  }
}

// 模型配置
const modelConfigs = {
  'whisper-tiny': {
    id: 'Xenova/whisper-tiny',
    name: 'Whisper Tiny',
    folderName: 'whisper-tiny'
  },
  'whisper-tiny-en': {
    id: 'Xenova/whisper-tiny.en',
    name: 'Whisper Tiny EN',
    folderName: 'whisper-tiny-en'
  },
  'whisper-base-en': {
    id: 'Xenova/whisper-base.en',
    name: 'Whisper Base EN',
    folderName: 'whisper-base-en'
  },
  'whisper-base': {
    id: 'Xenova/whisper-base',
    name: 'Whisper Base',
    folderName: 'whisper-base'
  }
};

// IPC 处理程序：下载Whisper Tiny ONNX模型（保持向后兼容）
ipcMain.handle('download-whisper-tiny-onnx', async () => {
  const result = await downloadWhisperModel(modelConfigs['whisper-tiny']);
  // 下载完成后通知渲染进程刷新模型状态
  if (result.success && mainWindow) {
    mainWindow.webContents.send('model-files-changed');
  }
  return result;
});

// IPC 处理程序：下载Whisper Tiny EN模型
ipcMain.handle('download-whisper-tiny-en', async () => {
  return await downloadWhisperModel(modelConfigs['whisper-tiny-en']);
});

// IPC 处理程序：下载Whisper Tiny EN ONNX模型
ipcMain.handle('download-whisper-tiny-en-onnx', async () => {
  return await downloadWhisperModel(modelConfigs['whisper-tiny-en']);
});

// IPC 处理程序：下载Whisper Base EN模型
ipcMain.handle('download-whisper-base-en', async () => {
  return await downloadWhisperModel(modelConfigs['whisper-base-en']);
});

// IPC 处理程序：下载Whisper Base EN ONNX模型
ipcMain.handle('download-whisper-base-en-onnx', async () => {
  return await downloadWhisperModel(modelConfigs['whisper-base-en']);
});

// IPC 处理程序：下载Whisper Base模型
ipcMain.handle('download-whisper-base', async () => {
  return await downloadWhisperModel(modelConfigs['whisper-base']);
});

// IPC 处理程序：下载Whisper Base ONNX模型
ipcMain.handle('download-whisper-base-onnx', async () => {
  return await downloadWhisperModel(modelConfigs['whisper-base']);
});

// IPC 处理程序：验证模型文件存在状态
ipcMain.handle('verify-model-files', async () => {
  try {
    const fs = require('fs');
    const path = require('path');
    
    // 获取模型根目录
    let modelsRootDir: string;
    if (process.env.NODE_ENV === 'development') {
      modelsRootDir = path.join(process.cwd(), 'models');
    } else {
      const exePath = app.getPath('exe');
      const exeDir = path.dirname(exePath);
      modelsRootDir = path.join(exeDir, 'models');
    }
    
    // 确保目录存在
    if (!fs.existsSync(modelsRootDir)) {
      fs.mkdirSync(modelsRootDir, { recursive: true });
    }
    
    const downloadedModels: string[] = [];
    
    // 模型映射
    const modelMapping = {
      'Xenova/whisper-tiny': 'whisper-tiny',
      'Xenova/whisper-tiny.en': 'whisper-tiny-en',
      'Xenova/whisper-base': 'whisper-base',
      'Xenova/whisper-base.en': 'whisper-base-en'
    };
    
    // 配置文件列表
    const configFiles = [
      'added_tokens.json', 'config.json', 'generation_config.json', 'merges.txt',
      'normalizer.json', 'preprocessor_config.json', 'quant_config.json',
      'quantize_config.json', 'special_tokens_map.json', 'tokenizer.json',
      'tokenizer_config.json', 'vocab.json'
    ];
    
    // ONNX文件列表
    const onnxFiles = ['encoder_model_q4.onnx', 'decoder_model_q4.onnx'];
    
    for (const [modelId, modelName] of Object.entries(modelMapping)) {
      const modelDir = path.join(modelsRootDir, modelName);
      const onnxDir = path.join(modelDir, 'onnx');
      
      // 检查配置文件
      const configFilesExist = configFiles.every(filename => {
        const filePath = path.join(modelDir, filename);
        return fs.existsSync(filePath);
      });
      
      // 检查ONNX文件
      const onnxFilesExist = onnxFiles.every(filename => {
        const filePath = path.join(onnxDir, filename);
        return fs.existsSync(filePath);
      });
      
      if (configFilesExist && onnxFilesExist) {
        downloadedModels.push(modelId);
      }
    }
    
    console.log('已下载的模型:', downloadedModels);
    return { success: true, downloadedModels };
    
  } catch (error) {
    console.error('验证模型文件失败:', error);
    return { success: false, error: (error as Error).message };
  }
});

// IPC 处理程序：保存模型状态
ipcMain.handle('save-model-status', async (event, statusData: any) => {
  try {
    // 设置标志以防止在保存模型状态时触发文件监控
    isSavingModelStatus = true;
    
    // 保存到文件系统
    const fs = require('fs');
    const path = require('path');
    
    const configPath = path.join(app.getPath('userData'), 'transformers-asr-models.json');
    const statusJson = JSON.stringify(statusData);
    fs.writeFileSync(configPath, statusJson, 'utf8');
    
    // 延迟重置标志，确保文件操作完成
    setTimeout(() => {
      isSavingModelStatus = false;
    }, 1000);
    
    console.log('模型状态保存成功');
    return { success: true };
  } catch (error) {
    isSavingModelStatus = false;
    console.error('保存模型状态失败:', error);
    return { success: false, error: (error as Error).message };
  }
});

// IPC 处理程序：手动刷新模型状态
ipcMain.handle('refresh-model-status', async () => {
  try {
    // 检查是否在设置页面，如果是则拒绝执行
    if (currentPage === 'settings') {
      console.log('在设置页面不允许手动刷新模型状态');
      return { success: false, error: '在设置页面不允许手动刷新模型状态' };
    }
    
    console.log('手动刷新模型状态...');
    const result = verifyModelFilesSync();
    
    // 不在这里发送 model-files-changed 事件，避免无限循环
    // 只有在文件系统实际变化时才发送该事件
    
    return result;
  } catch (error) {
    console.error('手动刷新模型状态失败:', error);
    return { success: false, error: (error as Error).message };
  }
});

// 发送识别结果到渲染进程的函数
const sendRecognitionResult = (result: TransformersASRResult) => {
  if (mainWindow) {
    mainWindow.webContents.send('speech-recognition-result', result);
  }
};

app.on('will-quit', () => {
  // 注销所有快捷键
  globalShortcut.unregisterAll();
  
  // 清理文件监控器
  if (modelFileWatcher) {
    modelFileWatcher.close();
    modelFileWatcher = null;
  }
});

// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and import them here.