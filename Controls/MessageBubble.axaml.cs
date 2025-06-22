using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdown.Avalonia;
using Lyxie_desktop.Helpers;

namespace Lyxie_desktop.Controls;

public partial class MessageBubble : UserControl
{
    public MessageBubble()
    {
        InitializeComponent();
    }

    public void SetMessage(string message, bool isUser, string? senderName = null, bool useMarkdown = false)
    {
        var messageText = this.FindControl<TextBlock>("MessageText");
        var messageMarkdown = this.FindControl<MarkdownScrollViewer>("MessageMarkdown");
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

        if (messageText != null)
        {
            messageText.Text = displayMessage;
            System.Diagnostics.Debug.WriteLine($"设置消息文本: {displayMessage}");
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
        if (messageText != null && messageMarkdown != null)
        {
            if (!isUser && useMarkdown)
            {
                // AI消息且需要Markdown渲染
                messageText.IsVisible = false;
                messageMarkdown.IsVisible = true;
                messageMarkdown.Markdown = displayMessage; // 使用处理后的消息
            }
            else
            {
                // 用户消息或普通文本
                messageText.IsVisible = true;
                messageMarkdown.IsVisible = false;
                messageText.Text = displayMessage; // 使用处理后的消息
            }
        }

        // 设置气泡样式
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
                messageText.SetValue(TextBlock.ForegroundProperty, this.FindResource("UserMessageTextBrush"));
            
            if (senderText != null)
                senderText.SetValue(TextBlock.ForegroundProperty, this.FindResource("SecondaryTextBrush"));
        }
        else
        {
            if (messageText != null)
                messageText.SetValue(TextBlock.ForegroundProperty, this.FindResource("AiMessageTextBrush"));
            
            if (senderText != null)
                senderText.SetValue(TextBlock.ForegroundProperty, this.FindResource("SecondaryTextBrush"));
        }
    }
} 