using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lyxie_desktop.Controls;

public partial class MessageBubble : UserControl
{
    public MessageBubble()
    {
        InitializeComponent();
    }

    public void SetMessage(string message, bool isUser, string? senderName = null)
    {
        var messageText = this.FindControl<TextBlock>("MessageText");
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
            System.Diagnostics.Debug.WriteLine($"设置发送者: {senderName}");
        }

        if (bubbleBorder != null)
        {
            if (isUser)
            {
                // 用户消息 - 右对齐，蓝色背景
                bubbleBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
                bubbleBorder.Background = Brushes.LightBlue;
                
                if (messageText != null)
                    messageText.Foreground = Brushes.Black;
                
                if (senderText != null)
                    senderText.Foreground = Brushes.Black;
            }
            else
            {
                // AI消息 - 左对齐，灰色背景
                bubbleBorder.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                bubbleBorder.Background = Brushes.LightGray;
                
                if (messageText != null)
                    messageText.Foreground = Brushes.Black;
                
                if (senderText != null)
                    senderText.Foreground = Brushes.Black;
            }
            
            System.Diagnostics.Debug.WriteLine($"设置气泡样式: isUser={isUser}");
        }
    }
} 