using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace Lyxie_desktop.Utils;

/// <summary>
/// 线性渐变的画刷帮助类，支持角度旋转
/// </summary>
public class LinearGradientBrushHelper
{
    /// <summary>
    /// RotateAngle AttachedProperty definition
    /// 指示渐变的旋转角度（弧度制）
    /// </summary>
    public static readonly AttachedProperty<double> RotateAngleProperty =
        AvaloniaProperty.RegisterAttached<LinearGradientBrushHelper, StyledElement, double>("RotateAngle", coerce: OnRotateAngleChanged);

    private static double OnRotateAngleChanged(AvaloniaObject @object, double angle)
    {
        LinearGradientBrush? gradientBrush = null;
        Visual? visual = null;

        if (@object is Border border && border.BorderBrush is LinearGradientBrush borderGradient)
        {
            gradientBrush = borderGradient;
            visual = border;
        }
        else if (@object is Button button && button.BorderBrush is LinearGradientBrush buttonGradient)
        {
            gradientBrush = buttonGradient;
            visual = button;
        }

        if (gradientBrush != null && visual != null)
        {
            SetGradientRotation(visual, gradientBrush, angle);
        }

        return angle;
    }

    /// <summary>
    /// 设置渐变角度的附加属性访问器
    /// </summary>
    public static void SetRotateAngle(StyledElement element, double value) =>
        element.SetValue(RotateAngleProperty, value);

    /// <summary>
    /// 获取渐变角度的附加属性访问器
    /// </summary>
    public static double GetRotateAngle(StyledElement element) =>
        element.GetValue(RotateAngleProperty);

    /// <summary>
    /// 设置渐变色的角度
    /// </summary>
    /// <param name="visual">目标视觉元素</param>
    /// <param name="linearGradientBrush">线性渐变画刷</param>
    /// <param name="rotation">旋转角度（弧度制）</param>
    public static void SetGradientRotation(Visual visual, LinearGradientBrush linearGradientBrush, double rotation)
    {
        if (linearGradientBrush == null) return;

        // 对于Button，使用固定的600x600尺寸（圆形按钮的尺寸）
        var borderRect = visual is Button ? new Rect(0, 0, 600, 600) : new Rect(visual.Bounds.Size);

        // 如果边界为空，使用默认尺寸
        if (borderRect.Width <= 0 || borderRect.Height <= 0)
        {
            borderRect = new Rect(0, 0, 600, 600);
        }

        SetGradientRotation(borderRect, linearGradientBrush, rotation);
    }

    /// <summary>
    /// 根据矩形区域设置渐变色的角度
    /// </summary>
    /// <param name="borderRect">边界矩形</param>
    /// <param name="linearGradientBrush">线性渐变画刷</param>
    /// <param name="rotation">旋转角度（弧度制）</param>
    public static void SetGradientRotation(Rect borderRect, LinearGradientBrush linearGradientBrush, double rotation)
    {
        if (linearGradientBrush == null || borderRect.Width <= 0 || borderRect.Height <= 0) return;

        var m = Math.Tan(rotation);

        // 标准化角度到 [0, 2π) 范围
        double Normalize(double rotation)
        {
            return rotation % (2 * Math.PI);
        }

        // 检查特殊角度
        bool IsP90(double nrotation) => Math.Abs(nrotation - (Math.PI / 2)) < 1e-10;
        bool IsN90(double nrotation) => Math.Abs(nrotation - (Math.PI / 2 + Math.PI)) < 1e-10;
        bool IsP180(double nrotation) => Math.Abs(nrotation - Math.PI) < 1e-10;
        bool IsP0(double nrotation) => Math.Abs(nrotation) < 1e-10 || Math.Abs(nrotation - Math.PI * 2) < 1e-10;

        // 根据直线方程计算坐标
        double GetY(double x) => m * (x - borderRect.Center.X) + borderRect.Center.Y;
        double GetX(double y) => (y - borderRect.Center.Y) / m + borderRect.Center.X;

        // 获取正方向交点
        Point GetFollowDirectionIntersectionCore(double nrotation)
        {
            if (nrotation > 0 && nrotation < Math.PI / 2)
            {
                var bottomY = borderRect.Height;
                var bottomX = GetX(bottomY);
                var rightX = borderRect.Width;
                var rightY = GetY(rightX);

                return bottomY < rightY ? new Point(bottomX, bottomY) : new Point(rightX, rightY);
            }
            else if (nrotation > Math.PI / 2 && nrotation < Math.PI)
            {
                var bottomY = borderRect.Height;
                var bottomX = GetX(bottomY);
                var leftX = 0;
                var leftY = GetY(leftX);

                return bottomY < leftY ? new Point(bottomX, bottomY) : new Point(leftX, leftY);
            }
            else if (nrotation > Math.PI && nrotation < Math.PI * 3d / 2d)
            {
                var topY = 0;
                var topX = GetX(topY);
                var leftX = 0;
                var leftY = GetY(leftX);

                return topY > leftY ? new Point(topX, topY) : new Point(leftX, leftY);
            }
            else if (nrotation > Math.PI * 3d / 2d && nrotation < Math.PI * 2)
            {
                var topY = 0;
                var topX = GetX(topY);
                var rightX = borderRect.Width;
                var rightY = GetY(rightX);

                return topY > rightY ? new Point(topX, topY) : new Point(rightX, rightY);
            }
            throw new Exception("角度不在有效范围内");
        }

        // 获取反方向交点
        Point GetReverseDirectionIntersectionCore(double nrotation)
        {
            var vrotation = (nrotation + Math.PI) % (Math.PI * 2);
            return GetFollowDirectionIntersectionCore(vrotation);
        }

        // 转换为相对坐标
        Point GetFollowDirectionIntersection(double nrotation)
        {
            var point = GetFollowDirectionIntersectionCore(nrotation);
            return new Point(point.X / borderRect.Width, point.Y / borderRect.Height);
        }

        Point GetReverseDirectionIntersection(double nrotation)
        {
            var point = GetReverseDirectionIntersectionCore(nrotation);
            return new Point(point.X / borderRect.Width, point.Y / borderRect.Height);
        }

        // 设置渐变起点和终点
        void SetPoint(Point startPoint, Point endPoint)
        {
            linearGradientBrush.StartPoint = new RelativePoint(startPoint, RelativeUnit.Relative);
            linearGradientBrush.EndPoint = new RelativePoint(endPoint, RelativeUnit.Relative);
        }

        var nrotation = Normalize(rotation);
        Point startPoint, endPoint;

        // 处理特殊角度
        if (IsP90(nrotation))
        {
            startPoint = new Point(0.5, 0);
            endPoint = new Point(0.5, 1);
        }
        else if (IsN90(nrotation))
        {
            startPoint = new Point(0.5, 1);
            endPoint = new Point(0.5, 0);
        }
        else if (IsP0(nrotation))
        {
            startPoint = new Point(0, 0.5);
            endPoint = new Point(1, 0.5);
        }
        else if (IsP180(nrotation))
        {
            startPoint = new Point(1, 0.5);
            endPoint = new Point(0, 0.5);
        }
        else
        {
            startPoint = GetReverseDirectionIntersection(nrotation);
            endPoint = GetFollowDirectionIntersection(nrotation);
        }

        SetPoint(startPoint, endPoint);
    }
}
