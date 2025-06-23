using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lyxie_desktop.Helpers;
using System;
using System.Text;

namespace Lyxie_desktop.Controls;

public partial class MessageBubble : UserControl
{
    private StringBuilder _contentBuilder = new StringBuilder();
    private bool _isStreamingMode = false;
    private bool _isUser = false;
    private string? _senderName = null;

    public MessageBubble()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 设置完整消息（非流式模式）
    /// </summary>
    public void SetMessage(string message, bool isUser, string? senderName = null, bool useMarkdown = false)
    {
        _isStreamingMode = false;
        _isUser = isUser;
        _senderName = senderName;
        
        var messageText = this.FindControl<SelectableTextBlock>("MessageText");
        var markdownContainer = this.FindControl<ScrollViewer>("MarkdownContainer");
        var senderText = this.FindControl<TextBlock>("SenderText");
        var bubbleBorder = this.FindControl<Border>("BubbleBorder");
        var thinkBlockContainer = this.FindControl<StackPanel>("ThinkBlockContainer");

        // 处理think标签（仅对AI消息处理）
        string displayMessage = message;
        if (!isUser && MessageProcessor.HasThinkTags(message))
        {
            var processResult = MessageProcessor.ProcessThinkTags(message);
            displayMessage = processResult.CleanedMessage;
            
            // 创建think blocks
            if (processResult.HasThinkContent && thinkBlockContainer != null)
            {
                thinkBlockContainer.Children.Clear();
                thinkBlockContainer.IsVisible = true;
                
                foreach (var thinkContent in processResult.ThinkBlocks)
                {
                    var thinkBlock = new ThinkBlock();
                    thinkBlock.SetThinkContent(thinkContent);
                    thinkBlockContainer.Children.Add(thinkBlock);
                }
            }
        }
        else if (thinkBlockContainer != null)
        {
            // 隐藏think容器
            thinkBlockContainer.IsVisible = false;
            thinkBlockContainer.Children.Clear();
        }

        if (senderText != null && !string.IsNullOrEmpty(senderName))
        {
            senderText.Text = senderName;
            senderText.IsVisible = true;
        }
        else if (senderText != null)
        {
            senderText.IsVisible = false;
        }

        // 根据消息类型和useMarkdown参数决定显示方式
        if (messageText != null && markdownContainer != null)
        {
            if (!isUser && useMarkdown)
            {
                // AI消息且需要Markdown渲染
                messageText.IsVisible = false;
                markdownContainer.IsVisible = true;
                
                // 使用新的Markdown渲染器
                var markdownPanel = MarkdownRenderer.RenderToPanel(displayMessage);
                markdownContainer.Content = markdownPanel;
                
                System.Diagnostics.Debug.WriteLine("使用Markdig渲染Markdown内容");
            }
            else
            {
                // 用户消息或普通文本
                messageText.IsVisible = true;
                markdownContainer.IsVisible = false;
                messageText.Text = displayMessage;
            }
        }

        SetBubbleStyle(isUser);
    }

    /// <summary>
    /// 初始化流式消息（流式模式）
    /// </summary>
    public void InitializeStreamingMessage(bool isUser, string? senderName = null)
    {
        _isStreamingMode = true;
        _isUser = isUser;
        _senderName = senderName;
        _contentBuilder.Clear();

        var messageText = this.FindControl<SelectableTextBlock>("MessageText");
        var markdownContainer = this.FindControl<ScrollViewer>("MarkdownContainer");
        var senderText = this.FindControl<TextBlock>("SenderText");
        var thinkBlockContainer = this.FindControl<StackPanel>("ThinkBlockContainer");

        // 流式模式下初始化为空内容
        if (messageText != null)
        {
            messageText.Text = "";
            messageText.IsVisible = true;
        }

        if (markdownContainer != null)
        {
            markdownContainer.IsVisible = false;
        }

        if (senderText != null && !string.IsNullOrEmpty(senderName))
        {
            senderText.Text = senderName;
            senderText.IsVisible = true;
        }
        else if (senderText != null)
        {
            senderText.IsVisible = false;
        }

        // 隐藏think容器（流式模式下暂不处理think标签）
        if (thinkBlockContainer != null)
        {
            thinkBlockContainer.IsVisible = false;
            thinkBlockContainer.Children.Clear();
        }

        SetBubbleStyle(isUser);
    }

    /// <summary>
    /// 追加内容（流式模式）
    /// </summary>
    public void AppendContent(string content)
    {
        if (!_isStreamingMode) return;

        _contentBuilder.Append(content);
        
        // 在UI线程中更新显示
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var messageText = this.FindControl<SelectableTextBlock>("MessageText");
                if (messageText != null && messageText.IsVisible)
                {
                    messageText.Text = _contentBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"追加内容时出错: {ex.Message}");
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 完成流式接收并启用Markdown渲染
    /// </summary>
    public void CompleteStreamingAndEnableMarkdown()
    {
        if (_isUser) return; // 用户消息不需要Markdown渲染

        var fullContent = _contentBuilder.ToString();
        _isStreamingMode = false;
        
        // 即使没有内容也要处理，避免空白消息气泡

        // 在UI线程中处理最终渲染
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // 处理think标签
                string processedContent = fullContent;
                var thinkBlockContainer = this.FindControl<StackPanel>("ThinkBlockContainer");
                
                if (MessageProcessor.HasThinkTags(fullContent))
                {
                    var processResult = MessageProcessor.ProcessThinkTags(fullContent);
                    processedContent = processResult.CleanedMessage;
                    
                    // 创建think blocks
                    if (processResult.HasThinkContent && thinkBlockContainer != null)
                    {
                        thinkBlockContainer.Children.Clear();
                        thinkBlockContainer.IsVisible = true;
                        
                        foreach (var thinkContent in processResult.ThinkBlocks)
                        {
                            var thinkBlock = new ThinkBlock();
                            thinkBlock.SetThinkContent(thinkContent);
                            thinkBlockContainer.Children.Add(thinkBlock);
                        }
                    }
                }

                // 使用新的Markdown渲染器
                var messageText = this.FindControl<SelectableTextBlock>("MessageText");
                var markdownContainer = this.FindControl<ScrollViewer>("MarkdownContainer");
                
                if (messageText != null && markdownContainer != null)
                {
                    // 决定要渲染的内容：如果processedContent为空或只有空白，则使用原始内容
                    var contentToRender = string.IsNullOrWhiteSpace(processedContent) ? fullContent : processedContent;
                    
                    // 启用Markdown渲染
                    messageText.IsVisible = false;
                    markdownContainer.IsVisible = true;
                    
                    var markdownPanel = MarkdownRenderer.RenderToPanel(contentToRender);
                    markdownContainer.Content = markdownPanel;
                    
                    System.Diagnostics.Debug.WriteLine($"流式接收完成，启用Markdig渲染。原始长度: {fullContent.Length}, 处理后长度: {processedContent.Length}, 渲染内容长度: {contentToRender.Length}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"完成流式渲染时出错: {ex.Message}");
                
                // 如果Markdown渲染失败，回退到文本显示
                var messageText = this.FindControl<SelectableTextBlock>("MessageText");
                var markdownContainer = this.FindControl<ScrollViewer>("MarkdownContainer");
                
                if (messageText != null && markdownContainer != null)
                {
                    messageText.Text = fullContent;
                    messageText.IsVisible = true;
                    markdownContainer.IsVisible = false;
                }
            }
        });
    }

    /// <summary>
    /// 设置气泡样式
    /// </summary>
    private void SetBubbleStyle(bool isUser)
    {
        var bubbleBorder = this.FindControl<Border>("BubbleBorder");
        var messageText = this.FindControl<SelectableTextBlock>("MessageText");
        var senderText = this.FindControl<TextBlock>("SenderText");

        if (bubbleBorder != null)
        {
            if (isUser)
            {
                // 用户消息样式
                bubbleBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
                bubbleBorder.SetValue(Border.BackgroundProperty, this.FindResource("UserMessageBackgroundBrush"));
                bubbleBorder.SetValue(Border.BorderBrushProperty, this.FindResource("UserMessageBackgroundBrush"));
            }
            else
            {
                // AI消息样式
                bubbleBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                bubbleBorder.SetValue(Border.BackgroundProperty, this.FindResource("AiMessageBackgroundBrush"));
                bubbleBorder.SetValue(Border.BorderBrushProperty, this.FindResource("AiMessageBackgroundBrush"));
            }
        }

        // 设置文本颜色
        if (isUser)
        {
            if (messageText != null)
                messageText.SetValue(SelectableTextBlock.ForegroundProperty, this.FindResource("UserMessageTextBrush"));
            
            if (senderText != null)
                senderText.SetValue(TextBlock.ForegroundProperty, this.FindResource("SecondaryTextBrush"));
        }
        else
        {
            if (messageText != null)
                messageText.SetValue(SelectableTextBlock.ForegroundProperty, this.FindResource("AiMessageTextBrush"));
            
            if (senderText != null)
                senderText.SetValue(TextBlock.ForegroundProperty, this.FindResource("SecondaryTextBrush"));
        }
    }
} 