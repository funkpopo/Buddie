using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lyxie_desktop.Services;
using Lyxie_desktop.Views;
using System;
using System.IO;
using System.Text.Json;

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
            var tempPath = Path.Combine(AppContext.BaseDirectory, "temp");
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
        try
        {
            // 获取设置文件路径（与SettingsView中的路径保持一致）
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "Lyxie");
            var settingsPath = Path.Combine(appFolder, "settings.json");

            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
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
            else
            {
                // 如果设置文件不存在，使用默认设置
                ThemeService.InitializeTheme(ThemeMode.System);
                LanguageService.SetLanguage(Language.SimplifiedChinese);
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
    
    // 保存设置
    public static void SaveSettings()
    {
        try
        {
            // 设置文件路径
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "Lyxie");
            Directory.CreateDirectory(appFolder);
            var settingsPath = Path.Combine(appFolder, "settings.json");
            
            // 序列化设置并写入文件
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}