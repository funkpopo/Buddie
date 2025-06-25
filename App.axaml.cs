using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lyxie_desktop.Services;
using Lyxie_desktop.Views;
using System;
using System.IO;
using System.Text.Json;
using Lyxie_desktop.Helpers;

namespace Lyxie_desktop;

public partial class App : Application
{
    // 全局主题服务实例
    public static ThemeService ThemeService { get; private set; } = new ThemeService();

    // 全局语言服务实例
    public static LanguageService LanguageService { get; private set; } = new LanguageService();
    
    // 全局应用程序设置
    public static AppSettings Settings { get; private set; } = new AppSettings();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 在创建主窗口前加载并应用设置
        LoadAndApplySettings();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.ShutdownRequested += OnShutdown;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdown(object? sender, ShutdownRequestedEventArgs e)
    {
        // 清理TTS缓存目录
        try
        {
            var tempPath = AppDataHelper.GetTempPath();
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clear TTS cache: {ex.Message}");
        }
    }

    // 加载并应用设置
    private void LoadAndApplySettings()
    {
        var settingsFile = AppDataHelper.GetSettingsFilePath();
        if (File.Exists(settingsFile))
        {
            try
            {
                var json = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<Views.AppSettings>(json);

                if (settings != null)
                {
                    // 保存到静态Settings属性
                    Settings = settings;
                    
                    // 应用主题设置
                    var themeMode = ThemeService.GetThemeModeFromIndex(settings.ThemeIndex);
                    ThemeService.InitializeTheme(themeMode);

                    // 应用语言设置
                    var language = LanguageService.GetLanguageFromIndex(settings.LanguageIndex);
                    LanguageService.SetLanguage(language);
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，使用默认设置
                Console.WriteLine($"Failed to load settings: {ex.Message}");
                ThemeService.InitializeTheme(ThemeMode.System);
                LanguageService.SetLanguage(Language.SimplifiedChinese);
            }
        }
        else
        {
            // 如果设置文件不存在，使用默认设置
            ThemeService.InitializeTheme(ThemeMode.System);
            LanguageService.SetLanguage(Language.SimplifiedChinese);
        }
    }
    
    // 保存设置
    private void SaveSettings()
    {
        var settingsFile = AppDataHelper.GetSettingsFilePath();
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}