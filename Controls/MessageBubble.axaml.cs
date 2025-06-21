using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdown.Avalonia;

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

        if (messageText != null)
        {
            messageText.Text = message;
            System.Diagnostics.Debug.WriteLine($"设置消息文本: {message}");
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
                messageMarkdown.Markdown = message;
            }
            else
            {
                // 用户消息或普通文本
                messageText.IsVisible = true;
                messageMarkdown.IsVisible = false;
                messageText.Text = message;
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