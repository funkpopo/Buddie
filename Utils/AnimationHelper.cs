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

        await animation.RunAsync(target);
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

        await animation.RunAsync(target);
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

        await animation.RunAsync(target);
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

        await animation.RunAsync(target);
    }

    /// <summary>
    /// 创建渐变旋转动画
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="fromAngle">起始角度（弧度）</param>
    /// <param name="toAngle">结束角度（弧度）</param>
    /// <param name="duration">持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreateGradientRotationAnimation(Control target,
        double fromAngle, double toAngle, TimeSpan duration, Easing? easing = null)
    {
        easing ??= new LinearEasing();

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(LinearGradientBrushHelper.RotateAngleProperty, fromAngle) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(LinearGradientBrushHelper.RotateAngleProperty, toAngle) }
                }
            }
        };

        await animation.RunAsync(target);
    }

    /// <summary>
    /// 创建无限循环的渐变旋转动画
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="duration">单次旋转持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreateInfiniteGradientRotationAnimation(Control target,
        TimeSpan duration, Easing? easing = null)
    {
        easing ??= new LinearEasing();

        var animation = new Animation
        {
            Duration = duration,
            IterationCount = IterationCount.Infinite,
            Easing = easing,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(LinearGradientBrushHelper.RotateAngleProperty, 0.0) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(LinearGradientBrushHelper.RotateAngleProperty, Math.PI * 2) }
                }
            }
        };

        await animation.RunAsync(target);
    }

    /// <summary>
    /// 创建平移动画
    /// </summary>
    /// <param name="target">目标控件</param>
    /// <param name="fromX">起始X坐标</param>
    /// <param name="toX">结束X坐标</param>
    /// <param name="fromY">起始Y坐标</param>
    /// <param name="toY">结束Y坐标</param>
    /// <param name="duration">持续时间</param>
    /// <param name="easing">缓动函数</param>
    public static async Task CreateTranslateAnimation(Control target, 
        double fromX, double toX, double fromY, double toY,
        TimeSpan duration, Easing? easing = null)
    {
        easing ??= new CubicEaseOut();

        var translateTransform = target.RenderTransform as TranslateTransform ?? new TranslateTransform();
        target.RenderTransform = translateTransform;

        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { 
                        new Setter(TranslateTransform.XProperty, fromX),
                        new Setter(TranslateTransform.YProperty, fromY)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { 
                        new Setter(TranslateTransform.XProperty, toX),
                        new Setter(TranslateTransform.YProperty, toY)
                    }
                }
            }
        };

        await animation.RunAsync(target);
    }

    /// <summary>
    /// 创建页面切换组合动画（包含平移和模糊效果）
    /// </summary>
    /// <param name="exitingView">退出的视图</param>
    /// <param name="enteringView">进入的视图</param>
    /// <param name="exitFromY">退出视图的起始Y坐标</param>
    /// <param name="exitToY">退出视图的结束Y坐标</param>
    /// <param name="enterFromY">进入视图的起始Y坐标</param>
    /// <param name="enterToY">进入视图的结束Y坐标</param>
    /// <param name="duration">持续时间</param>
    /// <param name="withBlur">是否包含模糊效果</param>
    public static async Task CreatePageTransitionAnimation(
        Control exitingView, Control enteringView,
        double exitFromY, double exitToY,
        double enterFromY, double enterToY,
        TimeSpan duration, bool withBlur = true)
    {
        var easing = new CubicEaseOut();
        
        // 确保视图有Transform
        var exitingTransform = exitingView.RenderTransform as TranslateTransform ?? new TranslateTransform();
        var enteringTransform = enteringView.RenderTransform as TranslateTransform ?? new TranslateTransform();
        exitingView.RenderTransform = exitingTransform;
        enteringView.RenderTransform = enteringTransform;

        // 设置初始位置
        exitingTransform.Y = exitFromY;
        enteringTransform.Y = enterFromY;

        // 创建退出动画
        var exitAnimation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(TranslateTransform.YProperty, exitFromY) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.YProperty, exitToY) }
                }
            }
        };

        // 创建进入动画
        var enterAnimation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(TranslateTransform.YProperty, enterFromY) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.YProperty, enterToY) }
                }
            }
        };

        // 如果需要模糊效果，为进入视图添加透明度动画
        Task? blurTask = null;
        if (withBlur && enteringView is Border border)
        {
            // 创建模糊效果动画（使用透明度模拟）
            var blurAnimation = new Animation
            {
                Duration = duration,
                Easing = easing,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(Visual.OpacityProperty, 0.3) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                    }
                }
            };
            blurTask = blurAnimation.RunAsync(border);
        }

        // 并行运行所有动画
        var tasks = new[] { 
            exitAnimation.RunAsync(exitingView),
            enterAnimation.RunAsync(enteringView)
        };
        
        if (blurTask != null)
        {
            await Task.WhenAll(tasks[0], tasks[1], blurTask);
        }
        else
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// 创建横向滑动切换动画
    /// </summary>
    /// <param name="exitingView">退出的视图</param>
    /// <param name="enteringView">进入的视图</param>
    /// <param name="slideDirection">滑动方向（1为右滑，-1为左滑）</param>
    /// <param name="distance">滑动距离</param>
    /// <param name="duration">持续时间</param>
    public static async Task CreateSlideTransitionAnimation(
        Control exitingView, Control enteringView,
        int slideDirection, double distance, TimeSpan duration)
    {
        var easing = new CubicEaseOut();
        
        // 确保视图有Transform
        var exitingTransform = exitingView.RenderTransform as TranslateTransform ?? new TranslateTransform();
        var enteringTransform = enteringView.RenderTransform as TranslateTransform ?? new TranslateTransform();
        exitingView.RenderTransform = exitingTransform;
        enteringView.RenderTransform = enteringTransform;

        // 设置初始位置
        enteringTransform.X = -distance * slideDirection;

        // 创建动画
        var exitAnimation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(TranslateTransform.XProperty, 0.0) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.XProperty, distance * slideDirection) }
                }
            }
        };

        var enterAnimation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(TranslateTransform.XProperty, -distance * slideDirection) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.XProperty, 0.0) }
                }
            }
        };

        // 并行运行动画
        await Task.WhenAll(
            exitAnimation.RunAsync(exitingView),
            enterAnimation.RunAsync(enteringView)
        );
    }
}
