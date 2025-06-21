using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using System;

namespace Lyxie_desktop.Controls;

public partial class TypingIndicator : UserControl
{
    private DispatcherTimer? _animationTimer;
    private int _currentStep = 0;
    private readonly double[] _dotOpacities = { 0.3, 0.6, 1.0, 0.6, 0.3 };

    public TypingIndicator()
    {
        InitializeComponent();
        InitializeAnimation();
    }

    private void InitializeAnimation()
    {
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400) // 每个点的动画间隔
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var dot1 = this.FindControl<Ellipse>("Dot1");
        var dot2 = this.FindControl<Ellipse>("Dot2");
        var dot3 = this.FindControl<Ellipse>("Dot3");

        if (dot1 == null || dot2 == null || dot3 == null) return;

        // 计算每个点的动画阶段
        int dot1Phase = _currentStep % _dotOpacities.Length;
        int dot2Phase = (_currentStep + 1) % _dotOpacities.Length;
        int dot3Phase = (_currentStep + 2) % _dotOpacities.Length;

        // 设置透明度
        dot1.Opacity = _dotOpacities[dot1Phase];
        dot2.Opacity = _dotOpacities[dot2Phase];
        dot3.Opacity = _dotOpacities[dot3Phase];

        _currentStep = (_currentStep + 1) % _dotOpacities.Length;
    }

    public void SetSender(string senderName)
    {
        var senderText = this.FindControl<TextBlock>("SenderText");
        if (senderText != null)
        {
            senderText.Text = senderName;
        }
    }

    public void StopAnimation()
    {
        _animationTimer?.Stop();
    }

    public void StartAnimation()
    {
        _animationTimer?.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer?.Stop();
        _animationTimer = null;
    }
} 