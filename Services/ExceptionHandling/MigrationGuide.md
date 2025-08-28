# 统一异常处理系统 - 迁移指南

## 概述

本文档说明如何从散布的 try-catch 块迁移到统一的异常处理系统。

## 核心服务类

### ExceptionHandlingService

位于 `Services/ExceptionHandling/ExceptionHandlingService.cs`，提供以下功能：

1. **统一异常处理策略**
2. **分类的异常处理器**（Database、Network、TTS、UI）
3. **自动错误消息生成**
4. **日志记录能力**

## 迁移示例

### 1. 基本同步操作

**之前:**
```csharp
try
{
    SomeRiskyOperation();
}
catch (Exception ex)
{
    // Handle error silently
}
```

**之后:**
```csharp
ExceptionHandlingService.ExecuteSafely(
    () => SomeRiskyOperation(),
    ExceptionHandlingService.HandlingStrategy.Silent,
    new ExceptionHandlingService.ExceptionContext
    {
        Component = "ComponentName",
        Operation = "操作描述"
    });
```

### 2. 异步操作

**之前:**
```csharp
try
{
    await SomeAsyncOperation();
}
catch (Exception)
{
    // Handle silently
}
```

**之后:**
```csharp
await ExceptionHandlingService.ExecuteSafelyAsync(
    () => SomeAsyncOperation(),
    ExceptionHandlingService.HandlingStrategy.Silent,
    new ExceptionHandlingService.ExceptionContext
    {
        Component = "ComponentName", 
        Operation = "异步操作"
    });
```

### 3. 数据库操作

**之前:**
```csharp
try
{
    await appSettings.SaveToDatabaseAsync();
}
catch (Exception)
{
    // Handle save error silently
}
```

**之后:**
```csharp
await ExceptionHandlingService.Database.ExecuteSafelyAsync(
    () => appSettings.SaveToDatabaseAsync(),
    "保存应用设置");
```

### 4. 网络请求

**之前:**
```csharp
try
{
    var result = await httpClient.GetAsync(url);
    return await result.Content.ReadAsStringAsync();
}
catch (HttpRequestException ex)
{
    MessageBox.Show($"网络请求失败: {ex.Message}");
    return null;
}
```

**之后:**
```csharp
return await ExceptionHandlingService.Network.ExecuteSafelyAsync(
    async () => {
        var result = await httpClient.GetAsync(url);
        return await result.Content.ReadAsStringAsync();
    },
    defaultValue: null,
    "获取远程数据");
```

### 5. TTS 操作

**之前:**
```csharp
try
{
    await ttsService.SynthesizeAsync(text);
}
catch (Exception ex)
{
    MessageBox.Show($"TTS失败: {ex.Message}");
}
```

**之后:**
```csharp
await ExceptionHandlingService.Tts.ExecuteSafelyAsync(
    () => ttsService.SynthesizeAsync(text),
    "文本转语音");
```

### 6. UI 操作

**之前:**
```csharp
try
{
    UpdateUI();
}
catch (Exception)
{
    // Silently handle UI update failures
}
```

**之后:**
```csharp
ExceptionHandlingService.UI.ExecuteSafely(
    () => UpdateUI(),
    "更新界面");
```

### 7. 带返回值的操作

**之前:**
```csharp
try
{
    var result = ComputeSomething();
    return result;
}
catch (Exception ex)
{
    Debug.WriteLine($"计算失败: {ex.Message}");
    return defaultValue;
}
```

**之后:**
```csharp
return ExceptionHandlingService.ExecuteSafely(
    () => ComputeSomething(),
    ExceptionHandlingService.HandlingStrategy.LogOnly,
    defaultValue,
    new ExceptionHandlingService.ExceptionContext
    {
        Component = "Calculator",
        Operation = "数值计算"
    });
```

## 处理策略说明

- **Silent**: 静默处理，不显示消息但可能记录日志
- **ShowMessage**: 显示用户友好的错误消息
- **ShowMessageAndLog**: 显示消息并记录详细日志
- **LogOnly**: 仅记录日志
- **Rethrow**: 记录后重新抛出异常

## 专门化的异常处理器

### Database 异常处理器
- 默认策略: LogOnly（静默记录日志）
- 适用于: 数据库保存、加载操作
- 示例:
```csharp
// 保存配置
await ExceptionHandlingService.Database.ExecuteSafelyAsync(
    () => config.SaveAsync(),
    "保存配置");

// 加载数据
var data = await ExceptionHandlingService.Database.ExecuteSafelyAsync(
    () => repository.LoadDataAsync(),
    defaultValue: null,
    "加载数据");
```

### Network 异常处理器  
- 默认策略: ShowMessageAndLog
- 自动识别常见网络异常类型
- 生成用户友好的错误消息
- 示例:
```csharp
// API 调用
var response = await ExceptionHandlingService.Network.ExecuteSafelyAsync(
    () => apiClient.CallAsync(request),
    defaultValue: null,
    "调用API服务");
```

### TTS 异常处理器
- 默认策略: ShowMessageAndLog
- 专门处理文本转语音相关异常
- 示例:
```csharp
// 语音合成
await ExceptionHandlingService.Tts.ExecuteSafelyAsync(
    () => ttsService.SynthesizeAsync(text, voice),
    "合成语音");
```

### UI 异常处理器
- 默认策略: LogOnly（静默处理）
- 适用于: UI更新、主题切换等
- 示例:
```csharp
// 主题更新
ExceptionHandlingService.UI.ExecuteSafely(
    () => ApplyTheme(isDarkMode),
    "应用主题");

// 界面刷新
ExceptionHandlingService.UI.ExecuteSafely(
    () => RefreshDataGrid(),
    "刷新数据列表");
```

## 自定义异常类型

位于 `Services/ExceptionHandling/CustomExceptions.cs`:

- `DatabaseException`: 数据库操作异常
- `ApiConfigurationException`: API配置异常
- `AudioProcessingException`: 音频处理异常

注意：TtsException 已存在于 `Services/Tts/TtsModels.cs` 中。

## 已完成的迁移示例

### App.xaml.cs
```csharp
// 应用启动异常处理
await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
{
    // 初始化逻辑...
},
ExceptionHandlingService.HandlingStrategy.ShowMessage,
new ExceptionHandlingService.ExceptionContext
{
    Component = "App",
    Operation = "应用程序启动"
});

// 应用退出时的清理操作
ExceptionHandlingService.ExecuteSafely(() =>
{
    DatabaseManager.CleanupTtsAudioCache();
},
ExceptionHandlingService.HandlingStrategy.Silent,
new ExceptionHandlingService.ExceptionContext
{
    Component = "App",
    Operation = "清理TTS缓存"
});
```

### FloatingWindow.xaml.cs
```csharp
// 主题变更时的数据库保存
SettingsControl.DarkThemeChanged += async (s, value) => {
    appSettings.IsDarkTheme = value;
    ApplyTheme();
    await ExceptionHandlingService.Database.ExecuteSafelyAsync(
        () => appSettings.SaveToDatabaseAsync(),
        "保存主题设置");
};

// API配置变更时的保存
SettingsControl.ApiConfigurationChanged += async (s, e) => {
    await ExceptionHandlingService.Database.ExecuteSafelyAsync(
        () => appSettings.SaveToDatabaseAsync(),
        "保存API配置");
    
    UpdateCardsFromApiConfigurations();
    UpdateCardDisplay();
};

// TTS配置激活
SettingsControl.TtsConfigurationActivated += async (s, config) => {
    await ExceptionHandlingService.Tts.ExecuteSafelyAsync(
        () => appSettings.ActivateTtsConfigurationAsync(config),
        "激活TTS配置");
};
```

### Controls/TtsConfigControl.xaml.cs
```csharp
// TTS配置添加
private void AddTtsConfig_Click(object sender, RoutedEventArgs e)
{
    ExceptionHandlingService.UI.ExecuteSafely(() =>
    {
        var configurations = TtsConfigList.ItemsSource as ObservableCollection<TtsConfiguration>;
        if (configurations == null) return;

        var newConfig = new TtsConfiguration
        {
            Name = $"TTS配置 {configurations.Count + 1}",
            ChannelType = TtsChannelType.OpenAI,
            IsEditMode = true,
            IsSaved = false,
            IsActive = false
        };
        
        configurations.Add(newConfig);
        UpdateNoTtsConfigMessageVisibility(configurations);
        ConfigurationAdded?.Invoke(this, newConfig);
    }, "添加TTS配置");
}
```

## 智能错误消息生成

系统会根据异常类型自动生成用户友好的错误消息：

- `HttpRequestException` → "网络请求失败：{具体错误}"
- `TaskCanceledException` → "操作已取消或超时"
- `JsonException` → "数据解析失败，请检查数据格式"
- `UnauthorizedAccessException` → "访问权限不足"
- `ArgumentException` → "参数错误：{具体错误}"
- `InvalidOperationException` → "当前操作无效，请稍后重试"
- `NotSupportedException` → "当前操作不受支持"
- 其他异常 → "{操作}失败：{具体错误}"

## 注意事项

1. **保持现有行为**: 迁移不应改变现有的错误处理行为
2. **选择合适的策略**: 根据操作的重要性选择合适的异常处理策略  
3. **提供上下文**: 总是提供有意义的操作描述
4. **渐进式迁移**: 可以逐步迁移，新旧系统可以并存
5. **线程安全**: 系统会自动处理UI线程的消息框显示

## 待迁移的主要文件

按优先级排序：

### 高优先级
1. `Controls/DialogControl.xaml.cs` - 对话控制异常（大量try-catch块）
2. `Database/DatabaseManager.cs` - 数据库管理异常
3. `Database/DatabaseService.cs` - 数据库服务异常

### 中优先级
4. `Controls/ApiConfigControl.xaml.cs` - API配置异常
5. `Services/Tts/OpenAiTtsService.cs` - OpenAI TTS服务异常
6. `Services/Tts/ElevenLabsTtsService.cs` - ElevenLabs TTS服务异常
7. `Services/Tts/MiniMaxTtsService.cs` - MiniMax TTS服务异常

### 低优先级
8. `Database/SqliteConnectionPool.cs` - 连接池异常
9. `Services/Tts/MiniMaxTtsValidator.cs` - TTS验证异常

## 迁移清单

在迁移每个文件时，请检查：

- [ ] 添加 `using Buddie.Services.ExceptionHandling;`
- [ ] 识别所有 try-catch 块
- [ ] 选择合适的异常处理策略
- [ ] 提供有意义的操作描述
- [ ] 测试迁移后的功能是否正常
- [ ] 确保错误消息对用户友好

## 测试建议

1. **正常流程测试**: 确保迁移不影响正常功能
2. **异常流程测试**: 故意触发异常，验证错误处理
3. **消息显示测试**: 验证错误消息是否用户友好
4. **日志记录测试**: 检查控制台是否正确记录异常信息

## 性能影响

统一异常处理系统的性能影响极小：
- 正常执行路径没有额外开销
- 异常发生时会有轻微的消息构建开销
- 日志记录是非阻塞的
- UI消息显示会自动处理线程调度