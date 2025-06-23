using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Material.Icons.Avalonia;
using System;
using System.Threading.Tasks;

namespace Lyxie_desktop.Controls;

public partial class CodeBlockControl : UserControl
{
    private string _codeContent = string.Empty;
    private string _language = string.Empty;

    public CodeBlockControl()
    {
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        var copyButton = this.FindControl<Button>("CopyButton");
        var floatingCopyButton = this.FindControl<Button>("FloatingCopyButton");

        if (copyButton != null)
        {
            copyButton.Click += OnCopyButtonClick;
        }

        if (floatingCopyButton != null)
        {
            floatingCopyButton.Click += OnCopyButtonClick;
        }
    }

    /// <summary>
    /// 设置代码内容
    /// </summary>
    public void SetCodeContent(string code, string language = "")
    {
        _codeContent = code;
        _language = language;

        var codeText = this.FindControl<SelectableTextBlock>("CodeText");
        var languageLabel = this.FindControl<TextBlock>("LanguageLabel");
        var codeBlockHeader = this.FindControl<Border>("CodeBlockHeader");

        if (codeText != null)
        {
            codeText.Text = code;
        }

        // 如果有语言信息，显示头部
        if (!string.IsNullOrWhiteSpace(language) && codeBlockHeader != null && languageLabel != null)
        {
            codeBlockHeader.IsVisible = true;
            languageLabel.Text = language;
        }
        else if (codeBlockHeader != null)
        {
            codeBlockHeader.IsVisible = false;
        }
    }

    /// <summary>
    /// 复制按钮点击事件
    /// </summary>
    private async void OnCopyButtonClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_codeContent))
            {
                // 复制到剪贴板
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(_codeContent);
                    await ShowCopySuccess();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"复制代码失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示复制成功反馈
    /// </summary>
    private async Task ShowCopySuccess()
    {
        var copyIcon = this.FindControl<MaterialIcon>("CopyIcon");
        var successIcon = this.FindControl<MaterialIcon>("SuccessIcon");

        if (copyIcon != null && successIcon != null)
        {
            // 切换图标
            copyIcon.IsVisible = false;
            successIcon.IsVisible = true;

            // 等待1.5秒后恢复
            await Task.Delay(1500);

            // 在UI线程中恢复图标
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                copyIcon.IsVisible = true;
                successIcon.IsVisible = false;
            });
        }
    }

    /// <summary>
    /// 获取代码内容
    /// </summary>
    public string GetCodeContent()
    {
        return _codeContent;
    }

    /// <summary>
    /// 获取语言类型
    /// </summary>
    public string GetLanguage()
    {
        return _language;
    }
} 