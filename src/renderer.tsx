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
import React from 'react';
import ReactDOM from 'react-dom/client';
import { createTheme, ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import App from './App';

// Create a theme
const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#1976d2',
    },
    secondary: {
      main: '#dc004e',
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
      `,
    },
  },
});

// 性能优化：仅在开发环境中使用StrictMode
const RootComponent = () => (
  <ThemeProvider theme={theme}>
    <CssBaseline />
    <App />
  </ThemeProvider>
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

console.log('👋 This message is being logged by "renderer.ts", included via Vite');