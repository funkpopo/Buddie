import { app, BrowserWindow, Tray, Menu, globalShortcut } from 'electron';
import path from 'path';

// Handle creating/removing shortcuts on Windows when installing/uninstalling.
if (require('electron-squirrel-startup')) {
  app.quit();
}

let mainWindow: BrowserWindow | null = null;
let tray: Tray | null = null;

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
    iconPath = path.join(__dirname, '../src/assets/icon.png');
  }
  
  // 如果图标不存在，尝试其他路径
  if (!require('fs').existsSync(iconPath)) {
    // 尝试在构建目录中查找
    const alternativePath = path.join(__dirname, 'icon.png');
    if (require('fs').existsSync(alternativePath)) {
      iconPath = alternativePath;
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
  createWindow();
  createTray();
  registerGlobalShortcut();
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

app.on('will-quit', () => {
  // 注销所有快捷键
  globalShortcut.unregisterAll();
});

// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and import them here.