using Avalonia;
using Avalonia.Styling;
using System;

namespace Lyxie_desktop.Services;

// 主题模式枚举
public enum ThemeMode
{
    Dark = 0,      // 深色模式
    Light = 1,     // 浅色模式
    System = 2     // 跟随系统
}

// 主题服务类
public class ThemeService
{
    // 主题变更事件
    public event EventHandler<ThemeMode>? ThemeChanged;

    private ThemeMode _currentTheme = ThemeMode.System;

    // 获取当前主题
    public ThemeMode CurrentTheme => _currentTheme;

    // 设置主题
    public void SetTheme(ThemeMode theme)
    {
        if (_currentTheme == theme) return;

        _currentTheme = theme;
        ApplyTheme(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    // 应用主题到应用程序
    private void ApplyTheme(ThemeMode theme)
    {
        if (Application.Current == null) return;

        ThemeVariant themeVariant = theme switch
        {
            ThemeMode.Dark => ThemeVariant.Dark,
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.System => ThemeVariant.Default,
            _ => ThemeVariant.Default
        };

        Application.Current.RequestedThemeVariant = themeVariant;
    }

    // 从索引获取主题模式（兼容现有设置）
    public static ThemeMode GetThemeModeFromIndex(int index)
    {
        return index switch
        {
            0 => ThemeMode.Dark,
            1 => ThemeMode.Light,
            2 => ThemeMode.System,
            _ => ThemeMode.System
        };
    }

    // 从主题模式获取索引（兼容现有设置）
    public static int GetIndexFromThemeMode(ThemeMode theme)
    {
        return (int)theme;
    }

    // 初始化主题（在应用启动时调用）
    public void InitializeTheme(ThemeMode theme)
    {
        _currentTheme = theme;
        ApplyTheme(theme);
    }
}
