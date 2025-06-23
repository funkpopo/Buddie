using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia;
using Lyxie_desktop.Controls;
using Lyxie_desktop.Helpers;
using Lyxie_desktop.Services;
using Lyxie_desktop.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Threading;
using Material.Icons;
using Material.Icons.Avalonia;

namespace Lyxie_desktop.Views;

public partial class MainView : UserControl
{
    // 事件：请求显示设置界面
    public event EventHandler? SettingsRequested;

    // 工具面板状态
    private bool _isToolPanelVisible = false;
    private bool _isAnimating = false;

    // 词云控件和光晕动画
    private WordCloudControl? _wordCloudControl;
    private DispatcherTimer? _glowAnimationTimer;
    private bool _isGlowAnimationRunning = false;

    // 边框渐变旋转动画
    private bool _isBorderAnimationRunning = false;
    
    // 光晕动画状态
    private bool _isGlowAnimating = false;

    // 对话界面状态
    private bool _isChatVisible = false;
    private CancellationTokenSource? _cancellationTokenSource;

    public MainView()
    {
        InitializeComponent();
        InitializeWordCloud();
        InitializeGlowAnimation();
        InitializeBorderAnimation();

        // 为圆形按钮添加事件
        var button = this.FindControl<Button>("MainCircleButton");
        if (button != null)
        {
            button.Click += OnMainButtonClick;
            button.PointerEntered += OnMainButtonPointerEntered;
            button.PointerExited += OnMainButtonPointerExited;
            button.PointerPressed += OnMainButtonPointerPressed;
            button.PointerReleased += OnMainButtonPointerReleased;
        }

        // 为设置按钮添加点击事件
        var settingsButton = this.FindControl<Button>("SettingsButton");
        if (settingsButton != null)
        {
            settingsButton.Click += OnSettingsButtonClick;
        }

        // 为扳手按钮添加点击事件
        var toolButton = this.FindControl<Button>("ToolButton");
        if (toolButton != null)
        {
            toolButton.Click += OnToolButtonClick;
        }

        // 初始化工具面板开关状态
        InitializeToolToggles();

        // 为开关添加事件
        var ttsToggle = this.FindControl<ToggleSwitch>("TTSToggle");
        if (ttsToggle != null)
        {
            ttsToggle.IsCheckedChanged += OnTTSToggled;
        }

        var dev1Toggle = this.FindControl<ToggleSwitch>("Dev1Toggle");
        if (dev1Toggle != null)
        {
            dev1Toggle.IsCheckedChanged += OnDev1Toggled;
        }

        var dev2Toggle = this.FindControl<ToggleSwitch>("Dev2Toggle");
        if (dev2Toggle != null)
        {
            dev2Toggle.IsCheckedChanged += OnDev2Toggled;
        }

        // 订阅语言变更事件
        App.LanguageService.LanguageChanged += OnLanguageChanged;

        // 初始化界面文本
        UpdateInterfaceTexts();

        // 初始化对话界面
        InitializeChatInterface();
        
        // 初始化LLM API配置显示
        RefreshLlmApiConfig();
    }
    
    // 初始化工具面板开关状态
    private void InitializeToolToggles()
    {
        var ttsToggle = this.FindControl<ToggleSwitch>("TTSToggle");
        var dev1Toggle = this.FindControl<ToggleSwitch>("Dev1Toggle");
        var dev2Toggle = this.FindControl<ToggleSwitch>("Dev2Toggle");
        
        if (ttsToggle != null)
            ttsToggle.IsChecked = App.Settings.EnableTTS;
            
        if (dev1Toggle != null)
            dev1Toggle.IsChecked = App.Settings.EnableDev1;
            
        if (dev2Toggle != null)
            dev2Toggle.IsChecked = App.Settings.EnableDev2;
    }
    
    private async void OnMainButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;

        // 通知词云按钮被按下
        _wordCloudControl?.SetButtonState(false, true);

        // 执行对话界面转换动画
        await ShowChatInterface();

        // 恢复词云状态
        _wordCloudControl?.SetButtonState(false, false);
    }

    private void OnMainButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // 通知词云按钮被悬停
        _wordCloudControl?.SetButtonState(true, false);
        
        // 启动光晕效果
        AnimateButtonGlow(isHover: true, isPressed: false);
    }

    private void OnMainButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // 通知词云按钮悬停结束
        _wordCloudControl?.SetButtonState(false, false);
        
        // 关闭光晕效果
        AnimateButtonGlow(isHover: false, isPressed: false);
    }

    private void OnMainButtonPointerPressed(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // 按压时的光晕效果
        AnimateButtonGlow(isHover: true, isPressed: true);
    }

    private void OnMainButtonPointerReleased(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // 释放时恢复悬停光晕
        AnimateButtonGlow(isHover: true, isPressed: false);
    }

    private async void AnimateButtonGlow(bool isHover, bool isPressed)
    {
        // 防止重复动画
        if (_isGlowAnimating) return;
        
        var glowBackground = this.FindControl<Border>("ButtonGlowBackground");
        if (glowBackground?.Effect is BlurEffect blurEffect)
        {
            _isGlowAnimating = true;
            
            try
            {
                double targetOpacity = 0;
                double targetBlurRadius = 0;
                
                if (isHover)
                {
                    targetOpacity = 0.3; // 进一步降低透明度，更加微妙
                    targetBlurRadius = isPressed ? 6 : 15; // 减小模糊半径，更精细的效果
                }

                // 快速响应的动画过渡
                const int steps = 10;
                const int duration = 200; // 减少动画时间
                const int stepDelay = duration / steps;

                double startOpacity = glowBackground.Opacity;
                double startBlurRadius = blurEffect.Radius;

                for (int i = 0; i <= steps; i++)
                {
                    // 检查动画是否应该被中断
                    if (!_isGlowAnimating) break;
                    
                    double progress = (double)i / steps;
                    
                    // 使用更平滑的缓动函数
                    double easedProgress = Math.Sin(progress * Math.PI * 0.5);
                    
                    glowBackground.Opacity = startOpacity + (targetOpacity - startOpacity) * easedProgress;
                    blurEffect.Radius = startBlurRadius + (targetBlurRadius - startBlurRadius) * easedProgress;

                    if (i < steps)
                    {
                        await Task.Delay(stepDelay);
                    }
                }
            }
            catch (Exception)
            {
                // 动画失败时静默处理
            }
            finally
            {
                _isGlowAnimating = false;
            }
        }
    }

    private void OnSettingsButtonClick(object? sender, RoutedEventArgs e)
    {
        // 触发设置界面请求事件
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnToolButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;

        var toolPanel = this.FindControl<Border>("ToolPanel");
        if (toolPanel == null) return;

        _isAnimating = true;

        if (_isToolPanelVisible)
        {
            await HideToolPanel();
        }
        else
        {
            await ShowToolPanel();
        }

        _isAnimating = false;
    }

    private void OnLanguageChanged(object? sender, Services.Language language)
    {
        UpdateInterfaceTexts();
    }

    private void UpdateInterfaceTexts()
    {
        var languageService = App.LanguageService;

        var clickToStartTextBlock = this.FindControl<TextBlock>("ClickToStartTextBlock");
        if (clickToStartTextBlock != null)
        {
            clickToStartTextBlock.Text = languageService.GetText("ClickToStart");
        }

        // 应用名称保持不变，不需要翻译
        // var appNameTextBlock = this.FindControl<TextBlock>("AppNameTextBlock");
        // if (appNameTextBlock != null)
        // {
        //     appNameTextBlock.Text = "Lyxie";
        // }
    }

    private async Task ShowToolPanel()
    {
        var toolPanel = this.FindControl<Border>("ToolPanel");
        if (toolPanel == null) return;

        _isToolPanelVisible = true;

        // 确保面板是可见的
        toolPanel.IsVisible = true;
        toolPanel.IsHitTestVisible = true; // 确保可以接收点击事件
        
        // 淡入动画 - 优化响应速度
        const int steps = 10;
        const int totalDuration = 150;  // 增加持续时间，使动画更平滑
        const int stepDelay = totalDuration / steps;

        for (int i = 0; i <= steps; i++)
        {
            double progress = (double)i / steps;
            double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

            toolPanel.Opacity = easedProgress;

            if (i < steps)
            {
                await Task.Delay(stepDelay);
            }
        }
        
        // 确保最终不透明度为1
        toolPanel.Opacity = 1.0;
        
        // 确保所有开关可点击
        var ttsToggle = this.FindControl<ToggleSwitch>("TTSToggle");
        var dev1Toggle = this.FindControl<ToggleSwitch>("Dev1Toggle");
        var dev2Toggle = this.FindControl<ToggleSwitch>("Dev2Toggle");
        
        if (ttsToggle != null) ttsToggle.IsHitTestVisible = true;
        if (dev1Toggle != null) dev1Toggle.IsHitTestVisible = true;
        if (dev2Toggle != null) dev2Toggle.IsHitTestVisible = true;
    }

    private async Task HideToolPanel()
    {
        var toolPanel = this.FindControl<Border>("ToolPanel");
        if (toolPanel == null) return;

        _isToolPanelVisible = false;

        // 淡出动画 - 优化响应速度
        const int steps = 10;
        const int totalDuration = 150;  // 增加持续时间，使动画更平滑
        const int stepDelay = totalDuration / steps;

        for (int i = 0; i <= steps; i++)
        {
            double progress = (double)i / steps;
            double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

            toolPanel.Opacity = 1.0 - easedProgress;

            if (i < steps)
            {
                await Task.Delay(stepDelay);
            }
        }
        
        // 确保最终不透明度为0
        toolPanel.Opacity = 0.0;

        // 隐藏面板
        toolPanel.IsVisible = false;
    }

    private void OnTTSToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            App.Settings.EnableTTS = toggle.IsChecked ?? false;
            App.SaveSettings();
            
            // TODO: 实现TTS功能切换
            System.Diagnostics.Debug.WriteLine($"TTS开关状态: {toggle.IsChecked}");
        }
    }

    private void OnDev1Toggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            App.Settings.EnableDev1 = toggle.IsChecked ?? false;
            App.SaveSettings();
            
            // TODO: 实现开发功能1切换
            System.Diagnostics.Debug.WriteLine($"开发功能1开关状态: {toggle.IsChecked}");
        }
    }

    private void OnDev2Toggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            App.Settings.EnableDev2 = toggle.IsChecked ?? false;
            App.SaveSettings();
            
            // TODO: 实现开发功能2切换
            System.Diagnostics.Debug.WriteLine($"开发功能2开关状态: {toggle.IsChecked}");
        }
    }

    #region 词云和光晕动画

    /// <summary>
    /// 初始化词云控件
    /// </summary>
    private void InitializeWordCloud()
    {
        var container = this.FindControl<Border>("WordCloudContainer");
        if (container != null)
        {
            _wordCloudControl = new WordCloudControl();
            container.Child = _wordCloudControl;
            
            // 监听容器大小变化以重新定位词云
            container.SizeChanged += OnWordCloudContainerSizeChanged;
        }
    }

    /// <summary>
    /// 词云容器大小变化处理
    /// </summary>
    private void OnWordCloudContainerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // 当容器大小变化时，确保词云正确重新定位
        _wordCloudControl?.InvalidateVisual();
    }

    /// <summary>
    /// 初始化光晕动画
    /// </summary>
    private void InitializeGlowAnimation()
    {
        _glowAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(17) // 60fps for ultra-smooth animation
        };
        _glowAnimationTimer.Tick += OnGlowAnimationTick;

        // 启动光晕动画
        StartGlowAnimation();
    }

    /// <summary>
    /// 启动光晕动画
    /// </summary>
    private void StartGlowAnimation()
    {
        if (!_isGlowAnimationRunning)
        {
            _isGlowAnimationRunning = true;
            _glowAnimationTimer?.Start();
        }
    }

    /// <summary>
    /// 初始化边框渐变旋转动画
    /// </summary>
    private void InitializeBorderAnimation()
    {
        // 延迟启动边框动画，确保控件已完全加载
        Dispatcher.UIThread.Post(() =>
        {
            var borderContainer = this.FindControl<Border>("BorderContainer");
            if (borderContainer?.BorderBrush is LinearGradientBrush)
            {
                StartBorderRotationAnimation(borderContainer);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 启动边框渐变旋转动画
    /// </summary>
    private void StartBorderRotationAnimation(Border borderContainer)
    {
        if (_isBorderAnimationRunning) return;

        _isBorderAnimationRunning = true;

        try
        {
            // 使用AnimationHelper创建无限循环的渐变旋转动画
            _ = AnimationHelper.CreateInfiniteGradientRotationAnimation(borderContainer, TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"边框动画错误: {ex.Message}");
            _isBorderAnimationRunning = false;
        }
    }

    /// <summary>
    /// 停止光晕动画
    /// </summary>
    private void StopGlowAnimation()
    {
        _isGlowAnimationRunning = false;
        _glowAnimationTimer?.Stop();
    }

    /// <summary>
    /// 光晕动画帧更新 - 增强版呼吸和脉冲效果
    /// </summary>
    private void OnGlowAnimationTick(object? sender, EventArgs e)
    {
        if (!_isGlowAnimationRunning) return;

        var time = DateTime.Now.TimeOfDay.TotalSeconds;

        // 获取光晕层
        var outerGlow = this.FindControl<Border>("OuterGlow");
        var middleGlow = this.FindControl<Border>("MiddleGlow");
        var innerGlow = this.FindControl<Border>("InnerGlow");

        if (outerGlow != null && middleGlow != null && innerGlow != null)
        {
            // 主呼吸周期（慢速）
            var breathingCycle = Math.Sin(time * 0.6) * 0.5 + 0.5; // 0-1范围
            
            // 心跳脉冲效果（快速）
            var pulseCycle = Math.Max(0, Math.Sin(time * 2.5)) * 0.3;
            
            // 微妙的随机波动
            var randomFlicker = Math.Sin(time * 3.7) * 0.02;
            
            // 组合效果
            var baseIntensity = 0.8 + 0.4 * breathingCycle + pulseCycle + randomFlicker;
            
            // 为每层应用不同的强度和相位
            var outerOpacity = Math.Max(0.05, Math.Min(0.25, 0.12 * baseIntensity));
            var middleOpacity = Math.Max(0.08, Math.Min(0.35, 0.18 * baseIntensity * Math.Sin(time * 0.8 + Math.PI / 4)));
            var innerOpacity = Math.Max(0.12, Math.Min(0.45, 0.25 * baseIntensity * Math.Sin(time * 1.1 + Math.PI / 2)));

            outerGlow.Opacity = outerOpacity;
            middleGlow.Opacity = middleOpacity;
            innerGlow.Opacity = innerOpacity;
        }
    }

    /// <summary>
    /// 执行按钮点击动画
    /// </summary>
    private async Task PerformClickAnimation()
    {
        _isAnimating = true;

        var button = this.FindControl<Button>("MainCircleButton");
        var outerGlow = this.FindControl<Border>("OuterGlow");
        var middleGlow = this.FindControl<Border>("MiddleGlow");
        var innerGlow = this.FindControl<Border>("InnerGlow");

        if (button != null && outerGlow != null && middleGlow != null && innerGlow != null)
        {
            // 保存原始透明度
            var originalOuterOpacity = outerGlow.Opacity;
            var originalMiddleOpacity = middleGlow.Opacity;
            var originalInnerOpacity = innerGlow.Opacity;

            try
            {
                // 第一阶段：快速缩小 + 光晕增强 (更平滑的缓动)
                var scaleTask = AnimationHelper.CreateScaleAnimation(button, 1.0, 0.92, TimeSpan.FromMilliseconds(120), new CubicEaseIn());
                var glowTask1 = Task.WhenAll(
                    AnimationHelper.CreateOpacityAnimation(outerGlow, originalOuterOpacity, 0.7, TimeSpan.FromMilliseconds(120)),
                    AnimationHelper.CreateOpacityAnimation(middleGlow, originalMiddleOpacity, 0.8, TimeSpan.FromMilliseconds(120)),
                    AnimationHelper.CreateOpacityAnimation(innerGlow, originalInnerOpacity, 0.9, TimeSpan.FromMilliseconds(120))
                );

                await Task.WhenAll(scaleTask, glowTask1);

                // 第二阶段：弹性放大 + 轻微旋转 (更自然的弹性效果)
                var bounceTask = AnimationHelper.CreateCompositeTransformAnimation(button, 0.92, 1.06, 0, 3, TimeSpan.FromMilliseconds(350), new SineEaseInOut());
                await bounceTask;

                // 第三阶段：平滑回到原始状态
                var restoreTask = AnimationHelper.CreateCompositeTransformAnimation(button, 1.06, 1.0, 3, 0, TimeSpan.FromMilliseconds(300), new CubicEaseOut());
                var glowTask2 = Task.WhenAll(
                    AnimationHelper.CreateOpacityAnimation(outerGlow, 0.7, originalOuterOpacity, TimeSpan.FromMilliseconds(300)),
                    AnimationHelper.CreateOpacityAnimation(middleGlow, 0.8, originalMiddleOpacity, TimeSpan.FromMilliseconds(300)),
                    AnimationHelper.CreateOpacityAnimation(innerGlow, 0.9, originalInnerOpacity, TimeSpan.FromMilliseconds(300))
                );

                await Task.WhenAll(restoreTask, glowTask2);
            }
            catch (Exception)
            {
                // 动画可能被中断，恢复原始状态
                button.RenderTransform = null;
                outerGlow.Opacity = originalOuterOpacity;
                middleGlow.Opacity = originalMiddleOpacity;
                innerGlow.Opacity = originalInnerOpacity;
            }
        }

        _isAnimating = false;
    }

    #endregion

    private void InitializeChatInterface()
    {
        // 获取对话界面相关控件
        var chatBackButton = this.FindControl<Button>("ChatBackButton");
        var chatSettingsButton = this.FindControl<Button>("ChatSettingsButton");
        var sendButton = this.FindControl<Button>("SendButton");
        var voiceInputButton = this.FindControl<Button>("VoiceInputButton");
        var messageInput = this.FindControl<TextBox>("MessageInput");
        
        // 绑定事件处理
        if (chatBackButton != null)
        {
            chatBackButton.Click += OnChatBackButtonClick;
        }
        
        // 绑定设置按钮点击事件
        if (chatSettingsButton != null)
        {
            chatSettingsButton.Click += OnChatSettingsButtonClick;
        }
        
        if (sendButton != null)
        {
            sendButton.Click += OnSendButtonClick;
        }
        
        if (voiceInputButton != null)
        {
            voiceInputButton.Click += OnVoiceInputButtonClick;
        }
        
        if (messageInput != null)
        {
            messageInput.KeyDown += OnMessageInputKeyDown;
        }
        
        // 初始化消息列表
        var messageList = this.FindControl<StackPanel>("MessageList");
        // StackPanel不需要ItemsSource设置

        // 初始化标题信息
        UpdateChatTitle();
    }

    /// <summary>
    /// 更新对话界面标题，显示当前LLM API信息
    /// </summary>
    public void UpdateChatTitle()
    {
        var titleText = this.FindControl<TextBlock>("ChatTitleText");
        var apiInfoText = this.FindControl<TextBlock>("ChatApiInfoText");

        if (titleText != null)
        {
            titleText.Text = "Lyxie";
        }

        if (apiInfoText != null)
        {
            // 检查是否有LLM API配置
            if (App.Settings.LlmApiConfigs == null || App.Settings.LlmApiConfigs.Count == 0)
            {
                apiInfoText.Text = App.LanguageService.CurrentLanguage == Services.Language.SimplifiedChinese 
                    ? "未配置 API" 
                    : "No API Configured";
                return;
            }

            var activeConfigIndex = App.Settings.ActiveLlmConfigIndex;
            if (activeConfigIndex < 0 || activeConfigIndex >= App.Settings.LlmApiConfigs.Count)
            {
                activeConfigIndex = 0; // 默认使用第一个配置
                App.Settings.ActiveLlmConfigIndex = 0;
                App.SaveSettings();
            }

            var config = App.Settings.LlmApiConfigs[activeConfigIndex];
            
            // 格式化API信息显示
            string modelInfo = config.ModelName ?? "未知模型";
            string configName = !string.IsNullOrEmpty(config.Name) ? config.Name : "默认配置";
            
            // 提取API提供商信息
            string provider = "未知";
            if (!string.IsNullOrEmpty(config.ApiUrl))
            {
                if (config.ApiUrl.Contains("openai.com"))
                    provider = "OpenAI";
                else if (config.ApiUrl.Contains("anthropic.com"))
                    provider = "Anthropic";
                else if (config.ApiUrl.Contains("google.com") || config.ApiUrl.Contains("googleapis.com"))
                    provider = "Google";
                else if (config.ApiUrl.Contains("azure.com"))
                    provider = "Azure";
                else if (config.ApiUrl.Contains("cohere.ai"))
                    provider = "Cohere";
                else if (config.ApiUrl.Contains("localhost") || config.ApiUrl.Contains("127.0.0.1"))
                    provider = "本地";
                else
                {
                    // 尝试从URL中提取域名
                    try
                    {
                        var uri = new Uri(config.ApiUrl);
                        provider = uri.Host.Replace("api.", "").Replace("www.", "");
                    }
                    catch
                    {
                        provider = "自定义";
                    }
                }
            }

            // 显示格式：配置名称 - 模型名 (提供商)
            apiInfoText.Text = $"{configName} - {modelInfo} ({provider})";
            
            System.Diagnostics.Debug.WriteLine($"更新API信息显示: {apiInfoText.Text}");
        }
    }

    #region 对话界面动画和交互

    /// <summary>
    /// 刷新LLM API配置信息
    /// </summary>
    public void RefreshLlmApiConfig()
    {
        System.Diagnostics.Debug.WriteLine("刷新LLM API配置信息");
        
        // 确保配置已初始化
        if (App.Settings.LlmApiConfigs == null)
        {
            App.Settings.LlmApiConfigs = new List<Views.LlmApiConfig>();
        }
        
        // 验证活跃配置索引
        if (App.Settings.LlmApiConfigs.Count > 0)
        {
            if (App.Settings.ActiveLlmConfigIndex < 0 || App.Settings.ActiveLlmConfigIndex >= App.Settings.LlmApiConfigs.Count)
            {
                App.Settings.ActiveLlmConfigIndex = 0;
                App.SaveSettings();
            }
        }
        
        // 触发标题更新
        UpdateChatTitle();
        
        System.Diagnostics.Debug.WriteLine($"当前LLM配置数量: {App.Settings.LlmApiConfigs.Count}, 激活索引: {App.Settings.ActiveLlmConfigIndex}");
    }

    /// <summary>
    /// 显示对话界面的动画
    /// </summary>
    private async Task ShowChatInterface()
    {
        if (_isAnimating || _isChatVisible) return;
        _isAnimating = true;

        var chatContainer = this.FindControl<Grid>("ChatContainer");
        var mainButton = this.FindControl<Button>("MainCircleButton");
        var borderContainer = this.FindControl<Border>("BorderContainer");
        var chatInputArea = this.FindControl<Border>("ChatInputArea");
        var chatHistoryArea = this.FindControl<Border>("ChatHistoryArea");
        var glowElements = new[] { "OuterGlow", "MiddleGlow", "InnerGlow" }
            .Select(name => this.FindControl<Border>(name))
            .Where(b => b != null);

        if (chatContainer == null || mainButton == null || borderContainer == null || chatInputArea == null || chatHistoryArea == null)
        {
            _isAnimating = false;
            return;
        }

        // 显示对话容器
        chatContainer.IsVisible = true;

        // 第一步：圆形按钮收缩并向下移动（300ms）
        var buttonAnimation = Task.Run(async () =>
        {
            const int steps = 20;
            const int duration = 300;
            const int stepDelay = duration / steps;

            double startSize = 600;
            double endSize = 80;
            double startY = 0;
            double endY = this.Bounds.Height - 100; // 移动到底部

            for (int i = 0; i <= steps; i++)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    double progress = (double)i / steps;
                    double easedProgress = 1 - Math.Pow(1 - progress, 3); // EaseOutCubic

                    // 缩小按钮
                    double currentSize = startSize + (endSize - startSize) * easedProgress;
                    mainButton.Width = currentSize;
                    mainButton.Height = currentSize;
                    mainButton.CornerRadius = new CornerRadius(currentSize / 2);

                    borderContainer.Width = currentSize + 12;
                    borderContainer.Height = currentSize + 12;
                    borderContainer.CornerRadius = new CornerRadius((currentSize + 12) / 2);

                    // 移动按钮
                    var transform = mainButton.RenderTransform as TranslateTransform;
                    if (transform == null)
                    {
                        transform = new TranslateTransform();
                        mainButton.RenderTransform = transform;
                    }
                    transform.Y = startY + (endY - startY) * easedProgress;

                    borderContainer.RenderTransform = transform;

                    // 淡出文字
                    var textElements = mainButton.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .ToList();
                    foreach (var text in textElements)
                    {
                        text.Opacity = 1 - easedProgress;
                    }
                });

                if (i < steps) await Task.Delay(stepDelay);
            }
        });

        // 同时淡出光晕和词云
        var fadeOutTask = Task.Run(async () =>
        {
            const int steps = 20;
            const int duration = 300;
            const int stepDelay = duration / steps;

            for (int i = 0; i <= steps; i++)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    double opacity = 1 - (double)i / steps;

                    foreach (var glow in glowElements)
                    {
                        if (glow != null)
                            glow.Opacity = opacity * 0.25; // 原始透明度的比例
                    }

                    if (_wordCloudControl != null)
                    {
                        _wordCloudControl.Opacity = opacity;
                    }
                });

                if (i < steps) await Task.Delay(stepDelay);
            }
        });

        await Task.WhenAll(buttonAnimation, fadeOutTask);

        // 隐藏主按钮和边框
        mainButton.IsVisible = false;
        borderContainer.IsVisible = false;

        // 第二步：显示输入区域（200ms）
        chatInputArea.Height = 80;
        chatContainer.Opacity = 1;

        // 第三步：展开对话历史区域（400ms）
        const int expandSteps = 30;
        const int expandDuration = 400;
        const int expandStepDelay = expandDuration / expandSteps;

        chatHistoryArea.Opacity = 0;
        for (int i = 0; i <= expandSteps; i++)
        {
            double progress = (double)i / expandSteps;
            double easedProgress = 1 - Math.Pow(1 - progress, 3); // EaseOutCubic

            chatHistoryArea.Opacity = easedProgress;

            if (i < expandSteps) await Task.Delay(expandStepDelay);
        }

        _isChatVisible = true;
        _isAnimating = false;

        // 聚焦到输入框
        var messageInput = this.FindControl<TextBox>("MessageInput");
        messageInput?.Focus();
    }

    /// <summary>
    /// 隐藏对话界面的动画
    /// </summary>
    private async Task HideChatInterface()
    {
        if (_isAnimating || !_isChatVisible) return;
        _isAnimating = true;

        var chatContainer = this.FindControl<Grid>("ChatContainer");
        var mainButton = this.FindControl<Button>("MainCircleButton");
        var borderContainer = this.FindControl<Border>("BorderContainer");
        var chatInputArea = this.FindControl<Border>("ChatInputArea");
        var chatHistoryArea = this.FindControl<Border>("ChatHistoryArea");
        var glowElements = new[] { "OuterGlow", "MiddleGlow", "InnerGlow" }
            .Select(name => this.FindControl<Border>(name))
            .Where(b => b != null);

        if (chatContainer == null || mainButton == null || borderContainer == null) 
        {
            _isAnimating = false;
            return;
        }

        // 反向动画：先收缩对话历史区域
        const int collapseSteps = 20;
        const int collapseDuration = 300;
        const int collapseStepDelay = collapseDuration / collapseSteps;

        for (int i = collapseSteps; i >= 0; i--)
        {
            double progress = (double)i / collapseSteps;
            if (chatHistoryArea != null)
                chatHistoryArea.Opacity = progress;

            if (i > 0) await Task.Delay(collapseStepDelay);
        }

        // 显示主按钮和边框
        mainButton.IsVisible = true;
        borderContainer.IsVisible = true;

        // 反向动画：按钮放大并移回中心
        var expandTask = Task.Run(async () =>
        {
            const int steps = 20;
            const int duration = 300;
            const int stepDelay = duration / steps;

            for (int i = steps; i >= 0; i--)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    double progress = (double)i / steps;
                    double easedProgress = 1 - Math.Pow(1 - progress, 3);

                    // 放大按钮
                    double currentSize = 600 - (600 - 80) * easedProgress;
                    mainButton.Width = currentSize;
                    mainButton.Height = currentSize;
                    mainButton.CornerRadius = new CornerRadius(currentSize / 2);

                    borderContainer.Width = currentSize + 12;
                    borderContainer.Height = currentSize + 12;
                    borderContainer.CornerRadius = new CornerRadius((currentSize + 12) / 2);

                    // 移动按钮
                    var transform = mainButton.RenderTransform as TranslateTransform;
                    if (transform != null)
                    {
                        transform.Y = (this.Bounds.Height - 100) * easedProgress;
                    }

                    // 淡入文字
                    var textElements = mainButton.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .ToList();
                    foreach (var text in textElements)
                    {
                        text.Opacity = 1 - easedProgress;
                    }
                });

                if (i > 0) await Task.Delay(stepDelay);
            }
        });

        // 同时淡入光晕和词云
        var fadeInTask = Task.Run(async () =>
        {
            const int steps = 20;
            const int duration = 300;
            const int stepDelay = duration / steps;

            for (int i = 0; i <= steps; i++)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    double opacity = (double)i / steps;

                    foreach (var glow in glowElements)
                    {
                        if (glow != null)
                            glow.Opacity = opacity * 0.25; // 原始透明度的比例
                    }

                    if (_wordCloudControl != null)
                    {
                        _wordCloudControl.Opacity = opacity;
                    }
                });

                if (i < steps) await Task.Delay(stepDelay);
            }
        });

        await Task.WhenAll(expandTask, fadeInTask);

        // 隐藏对话容器
        if (chatContainer != null)
        {
            chatContainer.Opacity = 0;
            chatContainer.IsVisible = false;
        }
        if (chatInputArea != null)
            chatInputArea.Height = 0;

        _isChatVisible = false;
        _isAnimating = false;
    }

    private async void OnChatBackButtonClick(object? sender, RoutedEventArgs e)
    {
        // 隐藏对话界面，返回到主界面
        await HideChatInterface();
    }

    private void OnChatSettingsButtonClick(object? sender, RoutedEventArgs e)
    {
        // 触发设置界面请求事件
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSendButtonClick(object? sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void OnVoiceInputButtonClick(object? sender, RoutedEventArgs e)
    {
        // TODO: 实现语音输入功能
        System.Diagnostics.Debug.WriteLine("语音输入按钮被点击");
    }

    private void OnMessageInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (_cancellationTokenSource != null) return; // 如果正在发送，则不处理回车

        if (e.Key == Avalonia.Input.Key.Enter &&
            !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        {
            e.Handled = true;
            SendMessage();
        }
    }

    private void OnStopButtonClick(object? sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        System.Diagnostics.Debug.WriteLine("用户请求停止流式传输");
    }

    private void UpdateSendButtonState(bool isSending)
    {
        var sendButton = this.FindControl<Button>("SendButton");
        var sendButtonIcon = this.FindControl<MaterialIcon>("SendButtonIcon");

        if (sendButton == null || sendButtonIcon == null) return;

        if (isSending)
        {
            sendButtonIcon.Kind = MaterialIconKind.Stop;
            sendButtonIcon.Foreground = Brushes.Red;
            sendButton.Click -= OnSendButtonClick;
            sendButton.Click += OnStopButtonClick;
        }
        else
        {
            sendButtonIcon.Kind = MaterialIconKind.Send;
            // 安全地获取资源，如果失败则使用默认颜色
            try
            {
                var buttonTextBrush = Application.Current?.FindResource("ButtonTextBrush");
                sendButtonIcon.Foreground = (buttonTextBrush as IBrush) ?? Brushes.White;
            }
            catch
            {
                sendButtonIcon.Foreground = Brushes.White;
            }
            sendButton.Click -= OnStopButtonClick;
            sendButton.Click += OnSendButtonClick;
        }
    }

    private async void SendMessage()
    {
        var messageInput = this.FindControl<TextBox>("MessageInput");
        var messageList = this.FindControl<StackPanel>("MessageList");
        var messageScrollViewer = this.FindControl<ScrollViewer>("MessageScrollViewer");
        
        if (messageInput == null || messageList == null) return;
        
        string message = messageInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(message)) return;

        // 设置为发送中状态
        _cancellationTokenSource = new CancellationTokenSource();
        UpdateSendButtonState(isSending: true);

        System.Diagnostics.Debug.WriteLine($"发送消息: {message}");

        // 创建用户消息气泡
        var userBubble = new MessageBubble();
        userBubble.SetMessage(message, true, "您");
        
        messageList.Children.Add(userBubble);
        
        // 清空输入框
        messageInput.Text = "";
        
        // 强制刷新UI，滚动到底部
        Dispatcher.UIThread.Post(() =>
        {
            messageScrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
        
        // 创建AI回复的流式消息气泡
        MessageBubble? aiBubble = null;
        
        try
        {
            // 获取当前激活的LLM API配置（实时读取最新配置）
            if (App.Settings.LlmApiConfigs == null || App.Settings.LlmApiConfigs.Count == 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var errorBubble = new MessageBubble();
                    errorBubble.SetMessage("错误：未配置LLM API。请先在设置中添加API配置。", false, "系统");
                    messageList.Children.Add(errorBubble);
                });
                return;
            }

            var activeConfigIndex = App.Settings.ActiveLlmConfigIndex;
            if (activeConfigIndex < 0 || activeConfigIndex >= App.Settings.LlmApiConfigs.Count)
            {
                activeConfigIndex = 0; // 默认使用第一个配置
                App.Settings.ActiveLlmConfigIndex = 0; // 更新App.Settings
                App.SaveSettings(); // 保存更新后的设置
            }

            var config = App.Settings.LlmApiConfigs[activeConfigIndex];
            
            System.Diagnostics.Debug.WriteLine($"使用LLM配置: {config.Name} ({config.ModelName}) - {config.ApiUrl}");

            // 在UI线程中创建AI消息气泡并初始化为流式模式
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                aiBubble = new MessageBubble();
                aiBubble.InitializeStreamingMessage(false, "Lyxie");
                messageList.Children.Add(aiBubble);
                messageScrollViewer?.ScrollToEnd();
            });

            // 使用LLM API服务发送流式请求
            var apiService = new LlmApiService();
            
            var success = await apiService.SendStreamingMessageAsync(
                config,
                message,
                onDataReceived: (content, isComplete) =>
                {
                    if (aiBubble != null)
                    {
                        if (isComplete)
                        {
                            // 流式接收完成，启用Markdown渲染
                            aiBubble.CompleteStreamingAndEnableMarkdown();
                            Dispatcher.UIThread.Post(() =>
                            {
                                messageScrollViewer?.ScrollToEnd();
                            });
                        }
                        else if (!string.IsNullOrEmpty(content))
                        {
                            // 追加内容
                            aiBubble.AppendContent(content);
                            Dispatcher.UIThread.Post(() =>
                            {
                                messageScrollViewer?.ScrollToEnd();
                            });
                        }
                    }
                },
                onError: (error) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (aiBubble != null && messageList.Children.Contains(aiBubble))
                        {
                            messageList.Children.Remove(aiBubble);
                        }
                        
                        var errorBubble = new MessageBubble();
                        errorBubble.SetMessage($"流式请求错误：{error}", false, "错误");
                        messageList.Children.Add(errorBubble);
                        messageScrollViewer?.ScrollToEnd();
                    });
                },
                _cancellationTokenSource.Token
            );

            if (!success)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (aiBubble != null && messageList.Children.Contains(aiBubble))
                    {
                        messageList.Children.Remove(aiBubble);
                    }
                    
                    var errorBubble = new MessageBubble();
                    errorBubble.SetMessage("无法启动流式请求，请检查API配置。", false, "错误");
                    messageList.Children.Add(errorBubble);
                });
            }
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 请求被用户取消
                System.Diagnostics.Debug.WriteLine("请求被用户取消。");
                if (aiBubble != null && messageList.Children.Contains(aiBubble))
                {
                    messageList.Children.Remove(aiBubble);
                }
                var cancelledBubble = new MessageBubble();
                cancelledBubble.SetMessage("消息请求已取消。", false, "系统");
                messageList.Children.Add(cancelledBubble);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 移除可能存在的AI气泡
                if (aiBubble != null && messageList.Children.Contains(aiBubble))
                {
                    messageList.Children.Remove(aiBubble);
                }

                // 显示异常信息
                var errorBubble = new MessageBubble();
                errorBubble.SetMessage($"发生错误：{ex.Message}", false, "错误");
                messageList.Children.Add(errorBubble);
            });
            
            System.Diagnostics.Debug.WriteLine($"发送消息异常: {ex}");
        }
        finally
        {
            // 恢复按钮状态
            UpdateSendButtonState(isSending: false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
        
        // 最后再滚动到底部
        Dispatcher.UIThread.Post(() =>
        {
            messageScrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    #endregion
}
