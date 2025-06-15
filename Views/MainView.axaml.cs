using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Media;
using Lyxie_desktop.Controls;
using Lyxie_desktop.Utils;
using System;
using System.Threading.Tasks;

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

        // 测试渐变旋转功能
        TestGradientRotation.TestBasicRotation();
        TestGradientRotation.TestAttachedProperty();
    }
    
    private async void OnMainButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;

        // 通知词云按钮被按下
        _wordCloudControl?.SetButtonState(false, true);

        // 执行点击动画
        await PerformClickAnimation();

        // 恢复词云状态
        _wordCloudControl?.SetButtonState(false, false);

        // 测试边框动画 - 如果还没有启动，则启动边框动画
        if (!_isBorderAnimationRunning)
        {
            var button = this.FindControl<Button>("MainCircleButton");
            if (button?.BorderBrush is LinearGradientBrush)
            {
                StartBorderRotationAnimation(button);
            }
        }

        // TODO: 实现AI对话功能
        // 这里可以添加打开对话窗口或切换到对话界面的逻辑
    }

    private void OnMainButtonPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // 通知词云按钮被悬停
        _wordCloudControl?.SetButtonState(true, false);
    }

    private void OnMainButtonPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        // 通知词云按钮悬停结束
        _wordCloudControl?.SetButtonState(false, false);
    }

    private void OnSettingsButtonClick(object? sender, RoutedEventArgs e)
    {
        // 触发设置界面请求事件
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnToolButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;

        if (_isToolPanelVisible)
        {
            await HideToolPanel();
        }
        else
        {
            await ShowToolPanel();
        }
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

        _isAnimating = true;
        _isToolPanelVisible = true;

        // 显示面板
        toolPanel.IsVisible = true;

        // 淡入动画 - 优化响应速度
        const int steps = 10;
        const int totalDuration = 50;  // 从300ms减少到150ms，提升响应速度
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

        _isAnimating = false;
    }

    private async Task HideToolPanel()
    {
        var toolPanel = this.FindControl<Border>("ToolPanel");
        if (toolPanel == null) return;

        _isAnimating = true;
        _isToolPanelVisible = false;

        // 淡出动画 - 优化响应速度
        const int steps = 10;
        const int totalDuration = 50;  // 从300ms减少到150ms，提升响应速度
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

        // 隐藏面板
        toolPanel.IsVisible = false;
        _isAnimating = false;
    }

    private void OnTTSToggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            // TODO: 实现TTS功能切换
            System.Diagnostics.Debug.WriteLine($"TTS开关状态: {toggle.IsChecked}");
        }
    }

    private void OnDev1Toggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
            // TODO: 实现开发功能1切换
            System.Diagnostics.Debug.WriteLine($"开发功能1开关状态: {toggle.IsChecked}");
        }
    }

    private void OnDev2Toggled(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle)
        {
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
            var button = this.FindControl<Button>("MainCircleButton");
            if (button?.BorderBrush is LinearGradientBrush)
            {
                StartBorderRotationAnimation(button);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 启动边框渐变旋转动画
    /// </summary>
    private void StartBorderRotationAnimation(Button button)
    {
        if (_isBorderAnimationRunning) return;

        _isBorderAnimationRunning = true;

        try
        {
            // 使用AnimationHelper创建无限循环的渐变旋转动画
            _ = AnimationHelper.CreateInfiniteGradientRotationAnimation(button, TimeSpan.FromSeconds(4));
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
                // 第一阶段：快速缩小 + 光晕增强
                var scaleTask = AnimationHelper.CreateScaleAnimation(button, 1.0, 0.95, TimeSpan.FromMilliseconds(100));
                var glowTask1 = Task.WhenAll(
                    AnimationHelper.CreateOpacityAnimation(outerGlow, originalOuterOpacity, 0.6, TimeSpan.FromMilliseconds(100)),
                    AnimationHelper.CreateOpacityAnimation(middleGlow, originalMiddleOpacity, 0.7, TimeSpan.FromMilliseconds(100)),
                    AnimationHelper.CreateOpacityAnimation(innerGlow, originalInnerOpacity, 0.8, TimeSpan.FromMilliseconds(100))
                );

                await Task.WhenAll(scaleTask, glowTask1);

                // 第二阶段：弹性放大 + 旋转
                var bounceTask = AnimationHelper.CreateCompositeTransformAnimation(button, 0.95, 1.05, 0, 2, TimeSpan.FromMilliseconds(200));
                await bounceTask;

                // 第三阶段：回到原始状态
                var restoreTask = AnimationHelper.CreateCompositeTransformAnimation(button, 1.05, 1.0, 2, 0, TimeSpan.FromMilliseconds(300));
                var glowTask2 = Task.WhenAll(
                    AnimationHelper.CreateOpacityAnimation(outerGlow, 0.6, originalOuterOpacity, TimeSpan.FromMilliseconds(300)),
                    AnimationHelper.CreateOpacityAnimation(middleGlow, 0.7, originalMiddleOpacity, TimeSpan.FromMilliseconds(300)),
                    AnimationHelper.CreateOpacityAnimation(innerGlow, 0.8, originalInnerOpacity, TimeSpan.FromMilliseconds(300))
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
}
