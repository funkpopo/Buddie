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
                const int totalDuration = 300; // 毫秒，适中的速度
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

            // 获取Transform对象
            var mainTransform = mainView.RenderTransform as TranslateTransform;
            var settingsTransform = settingsView.RenderTransform as TranslateTransform;

            if (mainTransform != null && settingsTransform != null)
            {
                // 使用与现有动画相同的参数
                const int steps = 60; // 60fps效果，更流畅
                const int totalDuration = 300; // 毫秒，适中的速度
                const int stepDelay = totalDuration / steps;

                for (int i = 0; i <= steps; i++)
                {
                    double progress = (double)i / steps;

                    // 使用缓出三次方缓动函数 (ease-out cubic)
                    double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

                    // 主界面向右移动
                    double mainOffset = 1280.0 * easedProgress;
                    mainTransform.X = mainOffset;

                    // 设置界面从左侧移动到中央
                    double settingsOffset = -1280.0 * (1.0 - easedProgress);
                    settingsTransform.X = settingsOffset;

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

            // 获取Transform对象
            var mainTransform = mainView.RenderTransform as TranslateTransform;
            var settingsTransform = settingsView.RenderTransform as TranslateTransform;

            if (mainTransform != null && settingsTransform != null)
            {
                // 使用与现有动画相同的参数
                const int steps = 60; // 60fps效果，更流畅
                const int totalDuration = 300; // 毫秒，适中的速度
                const int stepDelay = totalDuration / steps;

                for (int i = 0; i <= steps; i++)
                {
                    double progress = (double)i / steps;

                    // 使用缓出三次方缓动函数 (ease-out cubic)
                    double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3.0);

                    // 主界面从右侧移动回中央
                    double mainOffset = 1280.0 * (1.0 - easedProgress);
                    mainTransform.X = mainOffset;

                    // 设置界面向左移动隐藏
                    double settingsOffset = -1280.0 * easedProgress;
                    settingsTransform.X = settingsOffset;

                    if (i < steps)
                    {
                        await Task.Delay(stepDelay);
                    }
                }
            }

            _isAnimating = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _startupTimer?.Stop();
        base.OnClosed(e);
    }
}