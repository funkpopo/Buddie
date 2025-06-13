using Avalonia.Controls;
using System;

namespace Lyxie_desktop.Views;

public partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();

        // 订阅语言变更事件
        App.LanguageService.LanguageChanged += OnLanguageChanged;

        // 初始化界面文本
        UpdateInterfaceTexts();
    }

    private void OnLanguageChanged(object? sender, Services.Language language)
    {
        UpdateInterfaceTexts();
    }

    private void UpdateInterfaceTexts()
    {
        var languageService = App.LanguageService;

        var welcomeTextBlock = this.FindControl<TextBlock>("WelcomeTextBlock");
        if (welcomeTextBlock != null)
        {
            welcomeTextBlock.Text = languageService.GetText("Welcome");
        }

        var startingTextBlock = this.FindControl<TextBlock>("StartingTextBlock");
        if (startingTextBlock != null)
        {
            startingTextBlock.Text = languageService.GetText("Starting");
        }
    }
}
