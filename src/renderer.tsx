/** 
 * This file will automatically be loaded by vite and run in the "renderer" context.
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
 */

/** 
 * This file will automatically be loaded by vite and run in the "renderer" context.
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
 */

import './index.css';
import React, { useMemo } from 'react';
import ReactDOM from 'react-dom/client';
import { createTheme, ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import App from './App';
import { useTheme, ThemeProvider as CustomThemeProvider } from './ThemeContext';

// Create a theme based on the current theme mode
const AppThemeProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { theme: themeMode } = useTheme();
  
  const theme = useMemo(() => createTheme({
    palette: {
      mode: themeMode,
      primary: {
        main: themeMode === 'dark' ? '#90caf9' : '#1976d2',
      },
      secondary: {
        main: '#f48fb1',
      },
      background: {
        default: themeMode === 'dark' ? '#303030' : '#fafafa',
        paper: themeMode === 'dark' ? '#424242' : '#ffffff',
      },
    },
    // 禁用过渡动画以提高性能
    components: {
      MuiCssBaseline: {
        styleOverrides: `
          *, *::before, *::after {
            transition: none !important;
            animation: none !important;
          }
          
          body.dark {
            background-color: #303030;
            color: #ffffff;
          }
          
          body.light {
            background-color: #fafafa;
            color: #000000;
          }
        `,
      },
    },
  }), [themeMode]);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
};

// 性能优化：仅在开发环境中使用StrictMode
const RootComponent = () => (
  <CustomThemeProvider>
    <AppThemeProvider>
      <App />
    </AppThemeProvider>
  </CustomThemeProvider>
);

const root = ReactDOM.createRoot(document.getElementById('root')!);

// 在生产环境中移除 StrictMode 以提高性能
if (__DEV__) {
  root.render(
    <React.StrictMode>
      <RootComponent />
    </React.StrictMode>
  );
} else {
  root.render(<RootComponent />);
}

console.log('👋 This message is being logged by "renderer", included via Vite');