using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lyxie_desktop.Services;
using Lyxie_desktop.Views;
using System;
using System.IO;
using System.Text.Json;
using Lyxie_desktop.Helpers;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

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
            // 设置应用关闭模式，使得只有在明确调用 Shutdown() 时应用才会退出。
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            // 创建托盘图标
            CreateTrayIcon(desktop);
            
            desktop.ShutdownRequested += OnShutdown;
            
            // 应用启动时直接打开主窗口
            ShowMainWindow(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var openMenuItem = new NativeMenuItem("打开");
        openMenuItem.Click += (sender, args) => ShowMainWindow(desktop);

        var exitMenuItem = new NativeMenuItem("退出");
        exitMenuItem.Click += (sender, args) => desktop.Shutdown();

        var trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Lyxie-desktop/Lyxie.ico"))),
            ToolTipText = "Lyxie",
            Menu = new NativeMenu
            {
                Items =
                {
                    openMenuItem,
                    new NativeMenuItemSeparator(),
                    exitMenuItem
                }
            }
        };

        TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow == null)
        {
            desktop.MainWindow = new MainWindow();
            // 当主窗口关闭时，我们只是隐藏它而不是关闭应用
            desktop.MainWindow.Closing += (s, e) =>
            {
                if (e is WindowClosingEventArgs closingEventArgs)
                {
                    closingEventArgs.Cancel = true;
                }
                desktop.MainWindow.Hide();
            };
        }
        
        desktop.MainWindow.Show();
        desktop.MainWindow.WindowState = WindowState.Normal;
        desktop.MainWindow.Activate();
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
    public static void SaveSettings()
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