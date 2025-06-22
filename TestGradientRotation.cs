using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lyxie_desktop.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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

    public static void RunTest()
    {
        Debug.WriteLine("Testing gradient rotation...");
        // 原有的测试代码可以在这里
    }
    
    /// <summary>
    /// 测试思考内容段落分割功能
    /// </summary>
    public static void TestThinkContentSplitting()
    {
        Debug.WriteLine("=== 测试思考内容段落分割功能 ===");
        
        // 测试用例1：包含双换行符的多段落内容
        var testContent1 = @"这是第一段内容，包含一些思考过程。

这是第二段内容，继续进行分析。

这是第三段内容，得出结论。";
        
        var paragraphs1 = MessageProcessor.SplitThinkContentToParagraphs(testContent1);
        Debug.WriteLine($"测试用例1 - 段落数量: {paragraphs1.Count}");
        for (int i = 0; i < paragraphs1.Count; i++)
        {
            Debug.WriteLine($"段落{i + 1}: {paragraphs1[i]}");
        }
        
        // 测试用例2：只有单换行符的内容
        var testContent2 = @"第一行内容
第二行内容
第三行内容";
        
        var paragraphs2 = MessageProcessor.SplitThinkContentToParagraphs(testContent2);
        Debug.WriteLine($"\n测试用例2 - 段落数量: {paragraphs2.Count}");
        for (int i = 0; i < paragraphs2.Count; i++)
        {
            Debug.WriteLine($"段落{i + 1}: {paragraphs2[i]}");
        }
        
        // 测试用例3：包含markdown格式的内容
        var testContent3 = @"# 标题内容

这是一段包含**粗体**和*斜体*的文本。

```code
一些代码内容
```

最后一段普通文本。";
        
        var paragraphs3 = MessageProcessor.SplitThinkContentToParagraphs(testContent3);
        Debug.WriteLine($"\n测试用例3 - 段落数量: {paragraphs3.Count}");
        for (int i = 0; i < paragraphs3.Count; i++)
        {
            var formatted = MessageProcessor.FormatThinkParagraph(paragraphs3[i]);
            Debug.WriteLine($"段落{i + 1} (格式化前): {paragraphs3[i]}");
            Debug.WriteLine($"段落{i + 1} (格式化后): {formatted}");
        }
        
        Debug.WriteLine("=== 测试完成 ===");
    }
}
