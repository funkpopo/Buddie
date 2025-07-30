import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vitejs.dev/config
export default defineConfig({
  plugins: [react({
    // 在生产环境中关闭React Fast Refresh以提高性能
    fastRefresh: process.env.NODE_ENV !== 'production',
    // JSX编译优化
    jsxRuntime: 'automatic'
  })],
  build: {
    rollupOptions: {
      external: ['electron'],
    },
    // 启用压缩以减小bundle大小
    minify: 'esbuild',
    // 启用CSS代码分割
    cssCodeSplit: true,
    // 提高sourcemap性能
    sourcemap: process.env.NODE_ENV === 'development' ? 'cheap-module' : false
  },
  resolve: {
    extensions: ['.tsx', '.ts', '.js'],
  },
  // 启用生产环境的优化
  optimizeDeps: {
    include: ['react', 'react-dom', '@mui/material', '@mui/icons-material'],
    // 预构建优化
    esbuildOptions: {
      // 启用tree-shaking
      treeShaking: true
    }
  },
  // 开发服务器优化
  server: {
    // 预构建依赖以提高启动速度
    preTransformRequests: true,
    // 启用缓存以提高重启速度
    strictPort: false,
    // 禁用host检查以提高开发体验
    host: true
  },
  // 开发环境特有配置
  define: {
    // 环境变量定义
    __DEV__: process.env.NODE_ENV === 'development'
  }
});