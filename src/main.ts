import { app, BrowserWindow } from 'electron';
import path from 'node:path';
import started from 'electron-squirrel-startup';

// Handle creating/removing shortcuts on Windows when installing/uninstalling.
if (started) {
  app.quit();
}

// 在生产环境中禁用DevTools以提高性能
const isDev = process.env.NODE_ENV === 'development';

// 添加性能优化选项
app.commandLine.appendSwitch('disable-frame-rate-limit');
app.commandLine.appendSwitch('disable-gpu-vsync');
app.commandLine.appendSwitch('disable-background-timer-throttling');
app.commandLine.appendSwitch('disable-renderer-backgrounding');

const createWindow = () => {
  // Create the browser window with optimized settings.
  const mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    minWidth: 550,
    minHeight: 550,
    // 启用硬件加速
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      // 在生产环境中禁用Node.js集成以提高安全性
      nodeIntegration: isDev,
      contextIsolation: true,
      // 启用硬件加速
      devTools: isDev,
      // 启用沙箱模式以提高安全性
      sandbox: !isDev,
      // 启用GPU加速
      webgl: true,
      // 启用实验性功能以提高性能
      experimentalFeatures: true
    },
  });

  // and load the index.html of the app.
  if (MAIN_WINDOW_VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(MAIN_WINDOW_VITE_DEV_SERVER_URL);
  } else {
    mainWindow.loadFile(path.join(__dirname, `../renderer/${MAIN_WINDOW_VITE_NAME}/index.html`));
  }

  // 仅在开发环境中打开DevTools
  if (isDev) {
    mainWindow.webContents.openDevTools();
  }
  
  // 优化渲染进程性能
  mainWindow.setBackgroundColor('#ffffff');
  mainWindow.setHasShadow(false);
  
  // 禁用动画以提高性能
  mainWindow.webContents.on('did-finish-load', () => {
    if (!isDev) {
      mainWindow.webContents.insertCSS(`
        *, *::before, *::after {
          transition: none !important;
          animation: none !important;
        }
      `);
    }
  });
};

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.on('ready', createWindow);

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

// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and import them here.
