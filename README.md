# Lyxie-Desktop

一个基于Avalonia UI框架开发的现代化.NET桌面AI助手应用。

## 📋 项目概述

Lyxie是一个功能强大的AI助手桌面应用，旨在为用户提供智能化的交互体验。应用采用现代化的UI设计，支持跨平台运行。

## 🛠️ 技术栈

- **.NET 9.0** - 最新的.NET框架
- **Avalonia UI 11.2.1** - 跨平台UI框架
- **Fluent Design** - 现代化UI设计语言
- **C#** - 主要开发语言

## ✨ 功能特性

- 🤖 智能AI对话交互
- 🎨 现代化Fluent UI设计
- 🖥️ 跨平台支持 (Windows, macOS, Linux)
- ⚡ 高性能响应
- 🔧 可扩展架构

## 📦 项目结构

```
Lyxie-desktop/
├── App.axaml              # 应用程序主配置
├── App.axaml.cs           # 应用程序代码后台
├── MainWindow.axaml       # 主窗口UI定义
├── MainWindow.axaml.cs    # 主窗口代码后台
├── Program.cs             # 程序入口点
├── Lyxie-desktop.csproj   # 项目配置文件
├── app.manifest           # 应用清单文件
└── README.md              # 项目说明文档
```

## 🚀 快速开始

### 环境要求

- .NET 9.0 SDK 或更高版本
- Visual Studio 2022 或 JetBrains Rider (推荐)
- 支持的操作系统：Windows 10+, macOS 10.15+, Linux

### 安装步骤

1. **克隆项目**
   ```bash
   git clone <repository-url>
   cd Lyxie-desktop
   ```

2. **还原依赖**
   ```bash
   dotnet restore
   ```

3. **运行应用**
   ```bash
   dotnet run
   ```

### 开发环境设置

1. **使用Visual Studio**
   - 打开 `Lyxie-desktop.sln` 解决方案文件
   - 按 F5 启动调试

2. **使用命令行**
   ```bash
   # 开发模式运行
   dotnet run --configuration Debug
   
   # 发布版本构建
   dotnet build --configuration Release
   ```

## 🔧 构建和部署

### 调试构建
```bash
dotnet build --configuration Debug
```

### 发布构建
```bash
dotnet publish --configuration Release --self-contained true --runtime win-x64
dotnet publish -c Release --runtime win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

### 跨平台发布
```bash
# Windows
dotnet publish -r win-x64 --self-contained true

# macOS
dotnet publish -r osx-x64 --self-contained true

# Linux
dotnet publish -r linux-x64 --self-contained true
```

## 🎯 应用规格

- **窗口尺寸**: 1280 × 760 像素 (初始及最小尺寸)
- **启动位置**: 屏幕居中
- **主题支持**: 跟随系统主题 (亮色/暗色)

## 🤝 贡献指南

1. Fork 本项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 📞 联系方式

如有问题或建议，请通过以下方式联系：

- 项目Issues: [GitHub Issues](../../issues)
- 邮箱: [your-email@example.com]

## 🔄 更新日志

### v1.0.0 (开发中)
- 初始项目结构
- 基础Avalonia UI框架集成
- 主窗口布局设计

---

**注意**: 本项目目前处于开发阶段，功能持续完善中。
