using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Material.Icons;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyxie_desktop.Helpers;
using System.Timers;

namespace Lyxie_desktop.Controls;

public partial class ThinkBlock : UserControl
{
    private bool _isExpanded = false;
    private bool _isAnimating = false;

    public ThinkBlock()
    {
        InitializeComponent();
        
        // 绑定折叠按钮点击事件
        var toggleButton = this.FindControl<Button>("ToggleButton");
        if (toggleButton != null)
        {
            toggleButton.Click += OnToggleButtonClick;
        }
    }

    /// <summary>
    /// 设置思考内容
    /// </summary>
    /// <param name="content">思考内容文本</param>
    public void SetThinkContent(string content)
    {
        var contentText = this.FindControl<SelectableTextBlock>("ContentText");
        var previewText = this.FindControl<TextBlock>("PreviewText");
        
        if (contentText != null)
        {
            if (!string.IsNullOrEmpty(content))
            {
                // 简化处理：直接设置原始内容，确保换行符正确处理
                var processedContent = content.Trim();
                
                // 标准化换行符
                processedContent = processedContent.Replace("\r\n", "\n").Replace("\r", "\n");
                
                // 确保段落间有足够间距（双换行符）
                processedContent = System.Text.RegularExpressions.Regex.Replace(processedContent, @"\n\s*\n", "\n\n");
                
                contentText.Text = processedContent;
                
                System.Diagnostics.Debug.WriteLine($"ThinkBlock设置内容长度: {processedContent.Length}");
                System.Diagnostics.Debug.WriteLine($"ThinkBlock内容预览: {processedContent.Substring(0, Math.Min(100, processedContent.Length))}");
            }
            else
            {
                contentText.Text = "";
            }
        }
        
        // 设置预览文本（取前25个字符，移除换行符和特殊字符）
        if (previewText != null && !string.IsNullOrEmpty(content))
        {
            var preview = content.Trim()
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Replace('\t', ' ');
            
            // 移除多余的空格
            while (preview.Contains("  "))
            {
                preview = preview.Replace("  ", " ");
            }
            
            // 移除markdown和特殊字符
            preview = System.Text.RegularExpressions.Regex.Replace(preview, @"[#*`_\[\](){}]", "");
            
            if (preview.Length > 20)
            {
                // 在单词边界处截断
                var truncated = preview.Substring(0, 20);
                var lastSpace = truncated.LastIndexOf(' ');
                if (lastSpace > 12) // 如果空格位置合理
                {
                    preview = truncated.Substring(0, lastSpace).Trim() + "...";
                }
                else
                {
                    preview = truncated.Trim() + "...";
                }
            }
            
            previewText.Text = preview;
        }
        else if (previewText != null)
        {
            previewText.Text = "";
        }
    }

    /// <summary>
    /// 设置标题文本
    /// </summary>
    /// <param name="title">标题文本</param>
    public void SetTitle(string title)
    {
        var titleText = this.FindControl<TextBlock>("TitleText");
        if (titleText != null)
        {
            titleText.Text = title ?? "💭 思考过程";
        }
    }

    /// <summary>
    /// 获取是否展开状态
    /// </summary>
    public bool IsExpanded => _isExpanded;

    /// <summary>
    /// 切换展开/折叠状态
    /// </summary>
    private async void OnToggleButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_isAnimating) return;

        await ToggleExpansion();
    }

    /// <summary>
    /// 切换展开/折叠状态
    /// </summary>
    public async Task ToggleExpansion()
    {
        if (_isAnimating) return;

        _isAnimating = true;
        _isExpanded = !_isExpanded;

        var contentBorder = this.FindControl<Border>("ContentBorder");
        var toggleIcon = this.FindControl<Material.Icons.Avalonia.MaterialIcon>("ToggleIcon");
        var previewText = this.FindControl<TextBlock>("PreviewText");

        if (contentBorder == null || toggleIcon == null) 
        {
            _isAnimating = false;
            return;
        }

        try
        {
            if (_isExpanded)
            {
                // 展开动画
                await ExpandContent(contentBorder, toggleIcon, previewText);
            }
            else
            {
                // 折叠动画
                await CollapseContent(contentBorder, toggleIcon, previewText);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ThinkBlock动画错误: {ex.Message}");
        }
        finally
        {
            _isAnimating = false;
        }
    }

    /// <summary>
    /// 展开内容动画
    /// </summary>
    private async Task ExpandContent(Border contentBorder, Material.Icons.Avalonia.MaterialIcon toggleIcon, TextBlock? previewText)
    {
        // 更新图标
        toggleIcon.Kind = MaterialIconKind.ChevronDown;
        
        // 隐藏预览文本和折叠指示器
        if (previewText != null)
        {
            previewText.IsVisible = false;
        }
        
        var collapseIndicator = this.FindControl<TextBlock>("CollapseIndicator");
        if (collapseIndicator != null)
        {
            collapseIndicator.IsVisible = false;
        }

        // 显示内容边框
        contentBorder.IsVisible = true;
        contentBorder.Opacity = 0;

        // 平滑的淡入动画
        const int steps = 15;
        const int duration = 200;
        const int stepDelay = duration / steps;

        for (int i = 0; i <= steps; i++)
        {
            double progress = (double)i / steps;
            double easedProgress = Math.Sin(progress * Math.PI * 0.5); // EaseOutSine
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                contentBorder.Opacity = easedProgress;
            });

            if (i < steps)
            {
                await Task.Delay(stepDelay);
            }
        }
    }

    /// <summary>
    /// 折叠内容动画
    /// </summary>
    private async Task CollapseContent(Border contentBorder, Material.Icons.Avalonia.MaterialIcon toggleIcon, TextBlock? previewText)
    {
        // 平滑的淡出动画
        const int steps = 15;
        const int duration = 200;
        const int stepDelay = duration / steps;

        for (int i = steps; i >= 0; i--)
        {
            double progress = (double)i / steps;
            double easedProgress = Math.Sin(progress * Math.PI * 0.5); // EaseOutSine
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                contentBorder.Opacity = easedProgress;
            });

            if (i > 0)
            {
                await Task.Delay(stepDelay);
            }
        }

        // 隐藏内容边框
        contentBorder.IsVisible = false;
        
        // 更新图标
        toggleIcon.Kind = MaterialIconKind.ChevronRight;
        
        // 显示预览文本和折叠指示器
        if (previewText != null)
        {
            previewText.IsVisible = true;
        }
        
        var collapseIndicator = this.FindControl<TextBlock>("CollapseIndicator");
        if (collapseIndicator != null)
        {
            collapseIndicator.IsVisible = true;
        }
    }

    /// <summary>
    /// 程序化展开内容
    /// </summary>
    public async Task Expand()
    {
        if (!_isExpanded)
        {
            await ToggleExpansion();
        }
    }

    /// <summary>
    /// 程序化折叠内容
    /// </summary>
    public async Task Collapse()
    {
        if (_isExpanded)
        {
            await ToggleExpansion();
        }
    }
} 