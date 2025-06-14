using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;

namespace Lyxie_desktop.Utils;

/// <summary>
/// 动画辅助工具类，提供常用的动画效果
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// 创建缩放动画
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="fromScale">起始缩放</param>
    /// <param name="toScale">结束缩放</param>
    /// <param name="duration">持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreateScaleAnimation(Control target, double fromScale, double toScale, 
        TimeSpan duration, Easing? easing = null)
    {
        easing ??= new CubicEaseOut();
        
        var scaleTransform = target.RenderTransform as ScaleTransform ?? new ScaleTransform();
        target.RenderTransform = scaleTransform;
        target.RenderTransformOrigin = RelativePoint.Center;

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(ScaleTransform.ScaleXProperty, fromScale),
                               new Setter(ScaleTransform.ScaleYProperty, fromScale) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(ScaleTransform.ScaleXProperty, toScale),
                               new Setter(ScaleTransform.ScaleYProperty, toScale) }
                }
            }
        };

        await animation.RunAsync(scaleTransform);
    }

    /// <summary>
    /// 创建旋转动画
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="fromAngle">起始角度</param>
    /// <param name="toAngle">结束角度</param>
    /// <param name="duration">持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreateRotateAnimation(Control target, double fromAngle, double toAngle,
        TimeSpan duration, Easing? easing = null)
    {
        easing ??= new CubicEaseInOut();

        var rotateTransform = target.RenderTransform as RotateTransform ?? new RotateTransform();
        target.RenderTransform = rotateTransform;
        target.RenderTransformOrigin = RelativePoint.Center;

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(RotateTransform.AngleProperty, fromAngle) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(RotateTransform.AngleProperty, toAngle) }
                }
            }
        };

        await animation.RunAsync(rotateTransform);
    }

    /// <summary>
    /// 创建透明度动画
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="fromOpacity">起始透明度</param>
    /// <param name="toOpacity">结束透明度</param>
    /// <param name="duration">持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreateOpacityAnimation(Control target, double fromOpacity, double toOpacity,
        TimeSpan duration, Easing? easing = null)
    {
        easing ??= new CubicEaseInOut();

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(Visual.OpacityProperty, fromOpacity) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(Visual.OpacityProperty, toOpacity) }
                }
            }
        };

        await animation.RunAsync(target);
    }

    /// <summary>
    /// 创建组合变换动画（缩放+旋转）
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="fromScale">起始缩放</param>
    /// <param name="toScale">结束缩放</param>
    /// <param name="fromAngle">起始角度</param>
    /// <param name="toAngle">结束角度</param>
    /// <param name="duration">持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreateCompositeTransformAnimation(Control target, 
        double fromScale, double toScale, double fromAngle, double toAngle,
        TimeSpan duration, Easing? easing = null)
    {
        easing ??= new CubicEaseOut();

        var transformGroup = new TransformGroup();
        var scaleTransform = new ScaleTransform();
        var rotateTransform = new RotateTransform();
        
        transformGroup.Children.Add(scaleTransform);
        transformGroup.Children.Add(rotateTransform);
        
        target.RenderTransform = transformGroup;
        target.RenderTransformOrigin = RelativePoint.Center;

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { 
                        new Setter(ScaleTransform.ScaleXProperty, fromScale),
                        new Setter(ScaleTransform.ScaleYProperty, fromScale),
                        new Setter(RotateTransform.AngleProperty, fromAngle)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { 
                        new Setter(ScaleTransform.ScaleXProperty, toScale),
                        new Setter(ScaleTransform.ScaleYProperty, toScale),
                        new Setter(RotateTransform.AngleProperty, toAngle)
                    }
                }
            }
        };

        await animation.RunAsync(transformGroup);
    }

    /// <summary>
    /// 创建弹性缩放动画（点击效果）
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="intensity">强度（0.0-1.0）</param>
    public static async Task CreateBounceClickAnimation(Control target, double intensity = 0.1)
    {
        var scaleDown = 1.0 - intensity;
        var scaleUp = 1.0 + intensity * 0.5;

        // 第一阶段：快速缩小
        await CreateScaleAnimation(target, 1.0, scaleDown, TimeSpan.FromMilliseconds(100), new CubicEaseIn());
        
        // 第二阶段：弹性放大
        await CreateScaleAnimation(target, scaleDown, scaleUp, TimeSpan.FromMilliseconds(200), new ElasticEaseOut());
        
        // 第三阶段：回到原始大小
        await CreateScaleAnimation(target, scaleUp, 1.0, TimeSpan.FromMilliseconds(300), new CubicEaseOut());
    }

    /// <summary>
    /// 创建呼吸光晕动画
    /// </summary>
    /// <param name="target">目标Border（光晕层）</param>
    /// <param name="minIntensity">最小强度</param>
    /// <param name="maxIntensity">最大强度</param>
    /// <param name="duration">一个周期的持续时间</param>
    public static async Task CreateBreathingGlowAnimation(Border target, double minIntensity, double maxIntensity, TimeSpan duration)
    {
        var animation = new Animation
        {
            Duration = duration,
            IterationCount = IterationCount.Infinite,
            PlaybackDirection = PlaybackDirection.Alternate,
            Easing = new SineEaseInOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(Visual.OpacityProperty, minIntensity) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(Visual.OpacityProperty, maxIntensity) }
                }
            }
        };

        await animation.RunAsync(target);
    }

    /// <summary>
    /// 创建路径动画（用于词云移动）
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="startPoint">起始点</param>
    /// <param name="endPoint">结束点</param>
    /// <param name="duration">持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreatePathAnimation(Control target, Point startPoint, Point endPoint,
        TimeSpan duration, Easing? easing = null)
    {
        easing ??= new LinearEasing();

        var translateTransform = target.RenderTransform as TranslateTransform ?? new TranslateTransform();
        target.RenderTransform = translateTransform;

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { 
                        new Setter(TranslateTransform.XProperty, startPoint.X),
                        new Setter(TranslateTransform.YProperty, startPoint.Y)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { 
                        new Setter(TranslateTransform.XProperty, endPoint.X),
                        new Setter(TranslateTransform.YProperty, endPoint.Y)
                    }
                }
            }
        };

        await animation.RunAsync(translateTransform);
    }
}
