using System;
using Lyxie_desktop.Services;

namespace Lyxie_desktop.Interfaces;

public interface ILanguageService
{
    event EventHandler<Language>? LanguageChanged;
    
    Language CurrentLanguage { get; }
    
    void SetLanguage(Language language);
    
    string GetText(string key);
} 