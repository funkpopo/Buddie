# 🎵 Buddie - AI助手桌面应用

一个优雅的浮动式AI对话助手，支持多模型配置，带有炫酷的3D卡片界面和流畅的动画效果。

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)
![Electron](https://img.shields.io/badge/electron-v37.3.1-47848f.svg)

## ✨ 核心特性

### 🎨 优雅的用户界面
- **浮动卡片设计** - 透明悬浮窗口，支持拖拽移动
- **3D堆叠效果** - 炫酷的卡片翻页动画，支持鼠标、键盘、滚轮操作
- **个性化主题** - 40种渐变色彩，跟随系统主题或自定义
- **自适应透明度** - 可调节窗口透明度，融入桌面环境

### 🤖 强大的AI功能
- **多模型支持** - 同时配置多个AI模型（OpenAI、Claude、本地模型等）
- **流式对话** - 实时响应，支持打字机效果
- **富文本渲染** - 完整支持Markdown、LaTeX数学公式、Mermaid图表
- **智能记忆** - 保存对话历史和模型配置

### ⚙️ 系统集成
- **系统托盘** - 最小化至托盘，支持快速唤起
- **开机自启** - 可选的开机自动启动
- **位置记忆** - 记住窗口位置，下次启动恢复
- **始终置顶** - 可配置的窗口置顶模式

## 🚀 快速开始

### 安装要求
- Node.js 16.x 或更高版本
- npm 或 yarn 包管理器

### 开发环境运行
```bash
# 克隆项目
git clone <repository-url>
cd Buddie

# 安装依赖
npm install

# 启动开发环境
npm run dev
```

### 构建应用
```bash
# 打包应用（当前平台）
npm run package

# 构建安装包
npm run make
```

## 📱 功能详解

### 卡片界面操作
- **切换卡片**：
  - 鼠标点击左右导航按钮
  - 键盘空格键或回车键
  - 鼠标滚轮上下滚动
  - 设置页面的卡片切换按钮

- **拖拽移动**：按住卡片区域拖拽即可移动窗口位置

- **双击对话**：双击卡片开启AI对话界面

### AI对话功能
- **流式响应**：实时显示AI回复，支持中断
- **格式渲染**：
  - Markdown语法高亮
  - LaTeX数学公式渲染
  - Mermaid流程图/时序图等
  - 代码块语法高亮

- **快捷操作**：
  - `Enter` - 发送消息
  - `Shift + Enter` - 换行

### 设置配置
通过系统托盘 → 设置 或 主界面设置按钮进入：

**通用设置**：
- 主题切换（跟随系统/浅色/深色）
- 透明度调节（30%-100%）
- 开机自启动开关
- 始终置顶开关
- 当前卡片切换

**模型配置**：
- 添加/删除AI模型
- 配置API端点和密钥
- 设置模型参数（温度等）
- 模型名称和描述自定义

## 🔧 AI模型配置指南

### 支持的模型类型
- **OpenAI** - GPT-3.5, GPT-4系列
- **Anthropic** - Claude系列
- **本地模型** - Ollama、LocalAI等
- **其他兼容API** - 任何OpenAI API兼容的服务

### 配置示例

#### OpenAI配置
```
模型名称: GPT-4
API地址: https://api.openai.com/v1/chat/completions
API密钥: sk-...
模型标识: gpt-4
温度: 0.7
```

#### Claude配置
```
模型名称: Claude-3
API地址: https://api.anthropic.com/v1/messages
API密钥: sk-ant-...
模型标识: claude-3-sonnet-20240229
温度: 0.7
```

#### 本地模型配置
```
模型名称: Llama2 Local
API地址: http://localhost:11434/v1/chat/completions
API密钥: ollama
模型标识: llama2
温度: 0.8
```

## 🛠️ 技术栈

### 核心技术
- **Electron** `v37.3.1` - 跨平台桌面应用框架
- **Webpack** - 模块打包和热重载
- **Node.js** - 后端逻辑处理

### 主要依赖
- **markdown-it** - Markdown渲染引擎
- **KaTeX** - LaTeX数学公式渲染
- **highlight.js** - 代码语法高亮
- **mermaid** - 图表渲染支持
- **chokidar** - 文件监听（开发环境热重载）

### 架构设计
```
src/
├── main.js          # 主进程 - 窗口管理、IPC、系统集成
├── renderer.js      # 渲染进程 - 卡片动画、用户交互
├── preload.js       # 预加载脚本 - 安全的IPC桥接
├── chat.js          # 对话功能 - AI集成、流式响应
├── settings.js      # 设置管理 - 配置界面逻辑
├── renderer-utils.js # 渲染工具 - Markdown/LaTeX/Mermaid
├── index.html       # 主界面模板
├── chat.html        # 对话界面模板
├── settings.html    # 设置界面模板
└── assets/          # 静态资源
    └── logo.ico     # 应用图标
```

## 🔒 隐私与安全

- **本地存储** - 所有配置和对话历史仅存储在本地
- **安全通信** - API密钥加密存储，仅在需要时使用
- **进程隔离** - 使用Electron安全最佳实践，启用上下文隔离
- **代理支持** - 支持系统代理设置

## 🎯 开发计划

- [ ] 支持更多AI模型集成
- [ ] 添加对话历史搜索功能
- [ ] 支持插件系统
- [ ] 添加更多主题和动画效果
- [ ] 支持多语言界面
- [ ] 添加数据导入/导出功能

## 📄 许可证

本项目基于 [MIT许可证](LICENSE) 开源。

## 👨‍💻 作者

**funkpopo** - [s767609509@gmail.com](mailto:s767609509@gmail.com)
