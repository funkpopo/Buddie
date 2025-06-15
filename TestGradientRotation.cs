using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lyxie_desktop.Utils;
using System;

namespace Lyxie_desktop;

/// <summary>
/// 测试渐变旋转功能的简单类
/// </summary>
public static class TestGradientRotation
{
    /// <summary>
    /// 测试LinearGradientBrushHelper的基本功能
    /// </summary>
    public static void TestBasicRotation()
    {
        try
        {
            // 创建一个测试用的LinearGradientBrush
            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };

            gradientBrush.GradientStops.Add(new GradientStop(Colors.Blue, 0));
            gradientBrush.GradientStops.Add(new GradientStop(Colors.Red, 0.5));
            gradientBrush.GradientStops.Add(new GradientStop(Colors.Purple, 1));

            // 创建一个测试矩形
            var testRect = new Rect(0, 0, 600, 600);

            // 测试不同角度的旋转
            var angles = new double[] { 0, Math.PI / 4, Math.PI / 2, Math.PI, Math.PI * 3 / 2, Math.PI * 2 };

            foreach (var angle in angles)
            {
                LinearGradientBrushHelper.SetGradientRotation(testRect, gradientBrush, angle);
                
                System.Diagnostics.Debug.WriteLine($"角度: {angle:F2} 弧度 ({angle * 180 / Math.PI:F0}度)");
                System.Diagnostics.Debug.WriteLine($"起点: {gradientBrush.StartPoint}");
                System.Diagnostics.Debug.WriteLine($"终点: {gradientBrush.EndPoint}");
                System.Diagnostics.Debug.WriteLine("---");
            }

            System.Diagnostics.Debug.WriteLine("LinearGradientBrushHelper 测试完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"测试失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试附加属性功能
    /// </summary>
    public static void TestAttachedProperty()
    {
        try
        {
            // 创建一个测试按钮
            var button = new Button
            {
                Width = 600,
                Height = 600,
                BorderBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Colors.Cyan, 0),
                        new GradientStop(Colors.Blue, 0.43),
                        new GradientStop(Colors.Purple, 1)
                    }
                }
            };

            // 测试附加属性
            LinearGradientBrushHelper.SetRotateAngle(button, Math.PI / 2);
            var angle = LinearGradientBrushHelper.GetRotateAngle(button);
            
            System.Diagnostics.Debug.WriteLine($"设置角度: {Math.PI / 2:F2}, 获取角度: {angle:F2}");
            System.Diagnostics.Debug.WriteLine("附加属性测试完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"附加属性测试失败: {ex.Message}");
        }
    }
}
