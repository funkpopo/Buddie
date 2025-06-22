using System;
using Lyxie_desktop.Services;

namespace Lyxie_desktop.Interfaces;

public interface IThemeService
{
    event EventHandler<ThemeMode>? ThemeChanged;
    
    ThemeMode CurrentTheme { get; }
    
    void SetTheme(ThemeMode theme);
    
    void InitializeTheme(ThemeMode theme);
} 