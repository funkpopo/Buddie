using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyxie_desktop.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;

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
            try
            {
                // 获取Transform对象
                var welcomeTransform = welcomeView.RenderTransform as TranslateTransform;
                var mainTransform = mainView.RenderTransform as TranslateTransform;

                if (welcomeTransform != null && mainTransform != null)
                {
                    // 确保初始位置正确
                    welcomeTransform.Y = 0;
                    mainTransform.Y = 760;
                    
                    // 使用并行动画
                    var tasks = new List<Task>();
                    
                    // 欢迎页面向上移动
                    tasks.Add(AnimationHelper.CreateTranslateAnimation(
                        welcomeView, 0, 0, 0, -760,
                        TimeSpan.FromMilliseconds(800)
                    ));
                    
                    // 主页面从下向上移动
                    tasks.Add(AnimationHelper.CreateTranslateAnimation(
                        mainView, 0, 0, 760, 0,
                        TimeSpan.FromMilliseconds(800)
                    ));
                    
                    // 同时运行两个动画
                    await Task.WhenAll(tasks);
                    
                    // 确保最终位置正确
                    welcomeTransform.Y = -760;
                    mainTransform.Y = 0;
                }
                else
                {
                    // 如果Transform不存在，创建一个简单的淡入淡出效果
                    welcomeView.Opacity = 1.0;
                    mainView.Opacity = 0.0;
                    mainView.IsVisible = true;
                    
                    await AnimationHelper.CreateOpacityAnimation(welcomeView, 1.0, 0.0, TimeSpan.FromMilliseconds(300));
                    await AnimationHelper.CreateOpacityAnimation(mainView, 0.0, 1.0, TimeSpan.FromMilliseconds(300));
                    
                    welcomeView.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                // 如果动画出错，直接显示主界面
                Console.WriteLine($"Animation error: {ex.Message}");
                welcomeView.IsVisible = false;
                mainView.IsVisible = true;
                
                var mainTransform = mainView.RenderTransform as TranslateTransform;
                if (mainTransform != null)
                {
                    mainTransform.Y = 0;
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

            try
            {
                // 确保设置界面可见
                settingsView.IsVisible = true;
                settingsView.Opacity = 1.0;

                // 获取Transform对象
                var settingsTransform = settingsView.RenderTransform as TranslateTransform;
                
                if (settingsTransform != null)
                {
                    // 确保初始位置正确
                    settingsTransform.X = -this.Width;
                    settingsTransform.Y = 0;
                    
                    // 使用平移动画，从左侧滑入
                    await AnimationHelper.CreateTranslateAnimation(
                        settingsView, -this.Width, 0, 0, 0,
                        TimeSpan.FromMilliseconds(600)
                    );
                    
                    // 确保最终位置正确
                    settingsTransform.X = 0;
                    settingsTransform.Y = 0;
                }
                else
                {
                    // 如果Transform不存在，使用简单的淡入效果
                    settingsView.Opacity = 0.0;
                    await AnimationHelper.CreateOpacityAnimation(settingsView, 0.0, 1.0, TimeSpan.FromMilliseconds(300));
                }
            }
            catch (Exception ex)
            {
                // 如果动画出错，直接显示设置界面
                Console.WriteLine($"Settings animation error: {ex.Message}");
                var settingsTransform = settingsView.RenderTransform as TranslateTransform;
                if (settingsTransform != null)
                {
                    settingsTransform.X = 0;
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

            try
            {
                // 获取Transform对象
                var settingsTransform = settingsView.RenderTransform as TranslateTransform;
                
                if (settingsTransform != null)
                {
                    // 确保初始位置正确
                    settingsTransform.X = 0;
                    
                    // 使用平移动画，向左侧滑出
                    await AnimationHelper.CreateTranslateAnimation(
                        settingsView, 0, -this.Width, 0, 0,
                        TimeSpan.FromMilliseconds(600)
                    );
                    
                    // 确保最终位置正确
                    settingsTransform.X = -this.Width;
                    settingsTransform.Y = 0;
                }
                else
                {
                    // 如果Transform不存在，使用简单的淡出效果
                    await AnimationHelper.CreateOpacityAnimation(settingsView, 1.0, 0.0, TimeSpan.FromMilliseconds(300));
                    settingsView.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                // 如果动画出错，直接隐藏设置界面
                Console.WriteLine($"Back animation error: {ex.Message}");
                var settingsTransform = settingsView.RenderTransform as TranslateTransform;
                if (settingsTransform != null)
                {
                    settingsTransform.X = -this.Width;
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