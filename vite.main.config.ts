import { defineConfig } from 'vite';
import copy from 'rollup-plugin-copy';

// https://vitejs.dev/config
export default defineConfig({
  plugins: [
    copy({
      targets: [
        { 
          src: 'src/assets/icon.png', 
          dest: ['.vite/build/assets/', '.vite/build/'] 
        }
      ],
      hook: 'writeBundle' // 在写入 bundle 后复制
    })
  ]
});
