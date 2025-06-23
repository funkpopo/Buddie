using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Controls.Documents;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Lyxie_desktop.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lyxie_desktop.Helpers;

/// <summary>
/// Markdown 渲染器，将 Markdown 文档转换为 Avalonia 控件
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// 将 Markdown 文本渲染为 Avalonia 控件集合
    /// </summary>
    public static StackPanel RenderToPanel(string markdownText)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8
        };

        if (string.IsNullOrWhiteSpace(markdownText))
            return panel;

        try
        {
            var document = Markdown.Parse(markdownText, Pipeline);
            
            foreach (var block in document)
            {
                var control = RenderBlock(block);
                if (control != null)
                {
                    panel.Children.Add(control);
                }
            }
        }
        catch (Exception ex)
        {
            // 如果解析失败，显示原始文本
            var errorText = new SelectableTextBlock
            {
                Text = markdownText,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush.Parse("#FF6B6B")
            };
            panel.Children.Add(errorText);
            System.Diagnostics.Debug.WriteLine($"Markdown 解析错误: {ex.Message}");
        }

        return panel;
    }

    private static Control? RenderBlock(Block block)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading),
            ParagraphBlock paragraph => RenderParagraph(paragraph),
            ListBlock list => RenderList(list),
            CodeBlock code => RenderCodeBlock(code),
            QuoteBlock quote => RenderQuote(quote),
            ThematicBreakBlock => RenderHorizontalRule(),
            _ => RenderParagraph(block as ParagraphBlock ?? new ParagraphBlock())
        };
    }

    private static SelectableTextBlock RenderHeading(HeadingBlock heading)
    {
        var text = ExtractInlineText(heading.Inline);
        var fontSize = heading.Level switch
        {
            1 => 24,
            2 => 20,
            3 => 18,
            4 => 16,
            5 => 14,
            _ => 12
        };

        return new SelectableTextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, heading.Level == 1 ? 16 : 12, 0, 8)
        };
    }

    private static Control RenderParagraph(ParagraphBlock paragraph)
    {
        if (paragraph?.Inline == null)
        {
            return new SelectableTextBlock { Text = "", TextWrapping = TextWrapping.Wrap };
        }

        // 对于段落，我们提取纯文本并应用基本格式
        var text = ExtractInlineText(paragraph.Inline);
        
        return new SelectableTextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 8)
        };
    }

    private static Control RenderList(ListBlock list)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Avalonia.Thickness(0, 0, 0, 8)
        };

        int index = 1;
        foreach (var item in list.Cast<ListItemBlock>())
        {
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Avalonia.Thickness(20, 0, 0, 0)
            };

            // 列表标记
            var marker = new SelectableTextBlock
            {
                Text = list.IsOrdered ? $"{index}." : "•",
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 20
            };
            itemPanel.Children.Add(marker);

            // 列表内容
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 4
            };

            foreach (var block in item)
            {
                var control = RenderBlock(block);
                if (control != null)
                {
                    contentPanel.Children.Add(control);
                }
            }

            itemPanel.Children.Add(contentPanel);
            panel.Children.Add(itemPanel);
            index++;
        }

        return panel;
    }

    private static Control RenderCodeBlock(CodeBlock codeBlock)
    {
        var text = codeBlock is FencedCodeBlock fenced 
            ? fenced.Lines.ToString() 
            : codeBlock.Lines.ToString();

        var language = codeBlock is FencedCodeBlock fencedBlock 
            ? fencedBlock.Info ?? string.Empty 
            : string.Empty;

        // 使用新的代码块控件
        var codeBlockControl = new CodeBlockControl();
        codeBlockControl.SetCodeContent(text, language);
        
        return codeBlockControl;
    }

    private static Control RenderQuote(QuoteBlock quote)
    {
        var border = new Border
        {
            BorderBrush = Brush.Parse("#DFE2E5"),
            BorderThickness = new Avalonia.Thickness(4, 0, 0, 0),
            Background = Brush.Parse("#F6F8FA"),
            Padding = new Avalonia.Thickness(16, 12, 16, 12),
            Margin = new Avalonia.Thickness(0, 0, 0, 8)
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4
        };

        foreach (var block in quote)
        {
            var control = RenderBlock(block);
            if (control != null)
            {
                // 为引用内容设置特殊样式
                if (control is SelectableTextBlock tb)
                {
                    tb.Foreground = Brush.Parse("#6A737D");
                    tb.FontStyle = FontStyle.Italic;
                }
                panel.Children.Add(control);
            }
        }

        border.Child = panel;
        return border;
    }

    private static Control RenderHorizontalRule()
    {
        return new Border
        {
            Height = 1,
            Background = Brush.Parse("#E1E4E8"),
            Margin = new Avalonia.Thickness(0, 16, 0, 16)
        };
    }

    private static string ExtractInlineText(Markdig.Syntax.Inlines.Inline? inline)
    {
        if (inline == null) return string.Empty;

        return inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            CodeInline code => code.Content,
            EmphasisInline emphasis => ExtractInlineText(emphasis.FirstChild),
            LinkInline link => ExtractInlineText(link.FirstChild),
            ContainerInline container => ExtractInlineText(container),
            _ => string.Empty
        };
    }

    private static string ExtractInlineText(ContainerInline? container)
    {
        if (container == null) return string.Empty;

        var text = string.Empty;
        foreach (var inline in container)
        {
            text += ExtractInlineText(inline);
        }
        return text;
    }
} 