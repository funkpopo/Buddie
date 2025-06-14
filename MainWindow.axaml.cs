using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lyxie_desktop;

public partial class MainWindow : Window
{
    private DispatcherTimer? _startupTimer;
    private bool _isAnimating = false; // 防止动画重复触发
    private readonly Dictionary<TextBlock, double> _originalFontSizes = new(); // 存储原始字体大小

    public MainWindow()
    {
        InitializeComponent();
        StartWelcomeSequence();
        SetupViewEvents();
        SetupWindowEvents();
        
        // 初始化设置界面位置
        InitializeSettingsViewPosition();
    }

    private void InitializeSettingsViewPosition()
    {
        var settingsView = this.FindControl<Views.SettingsView>("SettingsView");
        if (settingsView != null)
        {
            var settingsTransform = settingsView.RenderTransform as TranslateTransform ?? new TranslateTransform();
            settingsTransform.X = -this.Width;
            settingsTransform.Y = 0;
            settingsView.RenderTransform = settingsTransform;
        }
    }

    private void SetupViewEvents()
    {
        // 设置MainView的设置按钮事件
        var mainView = this.FindControl<Views.MainView>("MainView");
        if (mainView != null)
        {
            mainView.SettingsRequested += OnSettingsRequested;
        }

        // 设置SettingsView的返回按钮事件
        var settingsView = this.FindControl<Views.SettingsView>("SettingsView");
        if (settingsView != null)
        {
            settingsView.BackToMainRequested += OnBackToMainRequested;
            settingsView.FontSizeChanged += OnFontSizeChanged;

            // 应用初始字体大小
            ApplyFontSize(settingsView.GetCurrentFontSize());
        }
    }

    private void SetupWindowEvents()
    {
        // 监听窗口大小变化事件
        this.SizeChanged += OnWindowSizeChanged;
        this.PropertyChanged += OnWindowPropertyChanged;
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // 重新初始化设置界面位置
        InitializeSettingsViewPosition();
        
        // 确保视图在窗口大小变化时正确显示
        EnsureViewVisibility();
    }

    private void OnWindowPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        // 监听窗口状态变化
        if (e.Property == WindowStateProperty)
        {
            EnsureViewVisibility();
        }
    }

    private void EnsureViewVisibility()
    {
        // 获取当前活动的视图
        var mainView = this.FindControl<Views.MainView>("MainView");
        var settingsView = this.FindControl<Views.SettingsView>("SettingsView");
        var welcomeView = this.FindControl<Views.WelcomeView>("WelcomeView");

        if (mainView != null && settingsView != null && welcomeView != null)
        {
            // 检查当前哪个视图应该可见
            var mainTransform = mainView.RenderTransform as TranslateTransform;
            var settingsTransform = settingsView.RenderTransform as TranslateTransform;
            var welcomeTransform = welcomeView.RenderTransform as TranslateTransform;

            // 确保只有当前活动的视图可见，其他视图隐藏在屏幕外
            if (mainTransform != null && Math.Abs(mainTransform.Y) < 10)
            {
                // MainView是活动的
                if (settingsTransform != null) 
                {
                    // 设置界面应该在左侧隐藏
                    settingsTransform.X = -this.Width;
                    settingsTransform.Y = 0;
                }
                if (welcomeTransform != null) welcomeTransform.Y = -this.Height;
            }
            else if (settingsTransform != null && Math.Abs(settingsTransform.X) < 10)
            {
                // SettingsView是活动的
                if (mainTransform != null) mainTransform.Y = this.Height;
                if (welcomeTransform != null) welcomeTransform.Y = -this.Height;
            }
            else
            {
                // 确保设置界面在正确的隐藏位置
                if (settingsTransform != null)
                {
                    settingsTransform.X = -this.Width;
                    settingsTransform.Y = 0;
                }
            }
        }
    }

    private void StartWelcomeSequence()
    {
        // 2秒后自动切换到主界面
        _startupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        _startupTimer.Tick += async (sender, e) =>
        {
            _startupTimer?.Stop();
            await TransitionToMainView();
        };

        _startupTimer.Start();
    }

    private async Task TransitionToMainView()
    {
        var welcomeView = this.FindControl<Views.WelcomeView>("WelcomeView");
        var mainView = this.FindControl<Views.MainView>("MainView");

        if (welcomeView != null && mainView != null)
        {
            // 获取Transform对象
            var welcomeTransform = welcomeView.RenderTransform as TranslateTransform;
            var mainTransform = mainView.RenderTransform as TranslateTransform;

            // 获取MainView中的Border用于Blur效果
            var mainViewBorder = mainView.FindControl<Border>("MainViewBorder");

            if (welcomeTransform != null && mainTransform != null && mainViewBorder != null)
            {
                // 现代流畅动画实现：使用缓动函数和高帧率
                const int steps = 60; // 60fps效果，更流畅
                const int totalDuration = 1000; // 毫秒，更流畅的过渡
                const int stepDelay = totalDuration / steps;
                const double maxBlurRadius = 15.0; // 最大模糊半径

                for (int i = 0; i <= steps; i++)
                {
                    double progress = (double)i / steps;

                    // 使用缓出三次方缓动函数 (ease-out cubic) - 现代UI常用
                    double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

                    // 欢迎界面向上移动，带有轻微的超调效果
                    double welcomeOffset = -760.0 * easedProgress;
                    welcomeTransform.Y = welcomeOffset;

                    // 主界面从下方移动到中央，使用相同的缓动
                    double mainOffset = 760.0 * (1.0 - easedProgress);
                    mainTransform.Y = mainOffset;

                    // Blur效果：从最大模糊半径逐渐减少到0（从模糊到清晰）
                    double blurRadius = maxBlurRadius * (1.0 - easedProgress);
                    var boxShadow = new BoxShadow
                    {
                        IsInset = true,
                        OffsetX = 0,
                        OffsetY = 0,
                        Blur = blurRadius,
                        Spread = 0,
                        Color = Colors.Black
                    };
                    mainViewBorder.BoxShadow = new BoxShadows(boxShadow);

                    if (i < steps)
                    {
                        await Task.Delay(stepDelay);
                    }
                }
            }
        }
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_isAnimating) return;
        await TransitionToSettingsView();
    }

    private async void OnBackToMainRequested(object? sender, EventArgs e)
    {
        if (_isAnimating) return;
        await TransitionBackToMainView();
    }

    private void OnFontSizeChanged(object? sender, double fontSize)
    {
        ApplyFontSize(fontSize);
    }

    private void ApplyFontSize(double fontSize)
    {
        // 应用字体大小到主界面的文本元素
        var mainView = this.FindControl<Views.MainView>("MainView");
        if (mainView != null)
        {
            ApplyFontSizeToControl(mainView, fontSize);
        }

        var settingsView = this.FindControl<Views.SettingsView>("SettingsView");
        if (settingsView != null)
        {
            ApplyFontSizeToControl(settingsView, fontSize);
        }

        var welcomeView = this.FindControl<Views.WelcomeView>("WelcomeView");
        if (welcomeView != null)
        {
            ApplyFontSizeToControl(welcomeView, fontSize);
        }
    }

    private void ApplyFontSizeToControl(Control control, double baseFontSize)
    {
        // 递归应用字体大小到所有TextBlock控件
        if (control is TextBlock textBlock)
        {
            // 获取或存储原始字体大小
            if (!_originalFontSizes.TryGetValue(textBlock, out var originalFontSize))
            {
                originalFontSize = textBlock.FontSize;
                if (double.IsNaN(originalFontSize) || originalFontSize <= 0)
                {
                    originalFontSize = 16; // 默认字体大小
                }
                _originalFontSizes[textBlock] = originalFontSize;
            }

            // 根据原始字体大小和基准字体大小计算新的字体大小
            var scale = baseFontSize / 16.0; // 16是默认基准字体大小
            textBlock.FontSize = originalFontSize * scale;
        }

        // 递归处理子控件
        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    ApplyFontSizeToControl(childControl, baseFontSize);
                }
            }
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            ApplyFontSizeToControl(contentChild, baseFontSize);
        }
        else if (control is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            ApplyFontSizeToControl(decoratorChild, baseFontSize);
        }
    }

    private async Task TransitionToSettingsView()
    {
        var mainView = this.FindControl<Views.MainView>("MainView");
        var settingsView = this.FindControl<Views.SettingsView>("SettingsView");

        if (mainView != null && settingsView != null)
        {
            _isAnimating = true;

            // 确保设置界面可见
            settingsView.IsVisible = true;
            settingsView.Opacity = 1.0;

            // 获取Transform对象，如果不存在则创建
            var mainTransform = mainView.RenderTransform as TranslateTransform ?? new TranslateTransform();
            var settingsTransform = settingsView.RenderTransform as TranslateTransform ?? new TranslateTransform();
            
            // 确保Transform已设置
            if (mainView.RenderTransform == null) mainView.RenderTransform = mainTransform;
            if (settingsView.RenderTransform == null) settingsView.RenderTransform = settingsTransform;

            if (mainTransform != null && settingsTransform != null)
            {
                // 使用与现有动画相同的参数
                const int steps = 60; // 60fps效果，更流畅
                const int totalDuration = 1000; // 毫秒，更流畅的过渡
                const int stepDelay = totalDuration / steps;

                // 确保设置界面从正确的位置开始
                settingsTransform.X = -this.Width;

                for (int i = 0; i <= steps; i++)
                {
                    double progress = (double)i / steps;

                    // 使用缓出三次方缓动函数 (ease-out cubic)
                    double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

                    // 设置界面从左侧移动到中央，完全遮挡主界面
                    // 从-1280移动到0（完全可见）
                    double settingsOffset = -this.Width * (1.0 - easedProgress);
                    settingsTransform.X = settingsOffset;

                    // 主界面保持在原位置，被设置界面遮挡
                    mainTransform.X = 0;

                    if (i < steps)
                    {
                        await Task.Delay(stepDelay);
                    }
                }
            }

            _isAnimating = false;
        }
    }

    private async Task TransitionBackToMainView()
    {
        var mainView = this.FindControl<Views.MainView>("MainView");
        var settingsView = this.FindControl<Views.SettingsView>("SettingsView");

        if (mainView != null && settingsView != null)
        {
            _isAnimating = true;

            // 获取Transform对象，如果不存在则创建
            var mainTransform = mainView.RenderTransform as TranslateTransform ?? new TranslateTransform();
            var settingsTransform = settingsView.RenderTransform as TranslateTransform ?? new TranslateTransform();
            
            // 确保Transform已设置
            if (mainView.RenderTransform == null) mainView.RenderTransform = mainTransform;
            if (settingsView.RenderTransform == null) settingsView.RenderTransform = settingsTransform;

            if (mainTransform != null && settingsTransform != null)
            {
                // 使用与现有动画相同的参数
                const int steps = 60; // 60fps效果，更流畅
                const int totalDuration = 1000; // 毫秒，更流畅的过渡
                const int stepDelay = totalDuration / steps;

                for (int i = 0; i <= steps; i++)
                {
                    double progress = (double)i / steps;

                    // 使用缓出三次方缓动函数 (ease-out cubic)
                    double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

                    // 设置界面向左移动隐藏
                    // 从0移动到-1280（完全隐藏）
                    double settingsOffset = -this.Width * easedProgress;
                    settingsTransform.X = settingsOffset;

                    // 主界面保持在原位置
                    mainTransform.X = 0;

                    if (i < steps)
                    {
                        await Task.Delay(stepDelay);
                    }
                }
            }

            // 动画完成后，可以选择隐藏设置界面以节省资源（可选）
            // settingsView.IsVisible = false;

            _isAnimating = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _startupTimer?.Stop();
        base.OnClosed(e);
    }
}