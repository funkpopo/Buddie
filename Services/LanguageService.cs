using System;
using System.Collections.Generic;

namespace Lyxie_desktop.Services;

// 语言枚举
public enum Language
{
    SimplifiedChinese = 0,  // 简体中文
    English = 1             // 英语
}

// 语言服务类
public class LanguageService
{
    // 语言变更事件
    public event EventHandler<Language>? LanguageChanged;

    private Language _currentLanguage = Language.SimplifiedChinese;

    // 获取当前语言
    public Language CurrentLanguage => _currentLanguage;

    // 设置语言
    public void SetLanguage(Language language)
    {
        if (_currentLanguage == language) return;

        _currentLanguage = language;
        LanguageChanged?.Invoke(this, language);
    }

    // 根据索引获取语言
    public static Language GetLanguageFromIndex(int index)
    {
        return index switch
        {
            0 => Language.SimplifiedChinese,
            1 => Language.English,
            _ => Language.SimplifiedChinese
        };
    }

    // 根据语言获取索引
    public static int GetIndexFromLanguage(Language language)
    {
        return language switch
        {
            Language.SimplifiedChinese => 0,
            Language.English => 1,
            _ => 0
        };
    }

    // 语言资源字典
    private static readonly Dictionary<Language, Dictionary<string, string>> Resources = new()
    {
        [Language.SimplifiedChinese] = new Dictionary<string, string>
        {
            // 设置界面
            ["Settings"] = "设置",
            ["Appearance"] = "外观",
            ["Language"] = "语言",
            ["Font"] = "字体",
            ["Theme"] = "主题",
            ["InterfaceLanguage"] = "界面语言",
            ["FontSize"] = "字体大小",
            ["About"] = "关于",
            
            // 主题选项
            ["DarkMode"] = "深色模式",
            ["LightMode"] = "浅色模式",
            ["FollowSystem"] = "跟随系统",
            
            // 语言选项
            ["SimplifiedChinese"] = "简体中文",
            ["English"] = "English",
            
            // 字体大小选项
            ["Small"] = "小号",
            ["Default"] = "默认",
            ["Medium"] = "中号",
            ["Large"] = "大号",
            
            // 关于信息
            ["Version"] = "版本 1.0.0",
            ["Description"] = "基于 Avalonia UI 开发的现代化 AI 助手",
            
            // 主界面
            ["Welcome"] = "欢迎使用 Lyxie",
            ["Starting"] = "正在启动...",
            ["ClickToStart"] = "点击开始对话",

            // LLM API设置
            ["LLMAPISettings"] = "LLM API 设置",
            ["DefaultConfig"] = "默认配置",
            ["APIUrl"] = "API URL",
            ["APIKey"] = "API Key",
            ["ModelName"] = "模型名称",
            ["Temperature"] = "温度值",
            ["MaxTokens"] = "最大令牌数",
            ["SaveConfig"] = "保存配置",
            ["ConfigSaved"] = "已保存",
            ["AddNewConfig"] = "添加新的 LLM 配置",
            ["Active"] = "当前使用",
            ["ActivateConfig"] = "设为当前使用",
            ["Custom"] = "自定义",
            ["CancelConfig"] = "取消"
        },
        
        [Language.English] = new Dictionary<string, string>
        {
            // 设置界面
            ["Settings"] = "Settings",
            ["Appearance"] = "Appearance",
            ["Language"] = "Language",
            ["Font"] = "Font",
            ["Theme"] = "Theme",
            ["InterfaceLanguage"] = "Interface Language",
            ["FontSize"] = "Font Size",
            ["About"] = "About",
            
            // 主题选项
            ["DarkMode"] = "Dark Mode",
            ["LightMode"] = "Light Mode",
            ["FollowSystem"] = "Follow System",
            
            // 语言选项
            ["SimplifiedChinese"] = "简体中文",
            ["English"] = "English",
            
            // 字体大小选项
            ["Small"] = "Small",
            ["Default"] = "Default",
            ["Medium"] = "Medium",
            ["Large"] = "Large",
            
            // 关于信息
            ["Version"] = "Version 1.0.0",
            ["Description"] = "Modern AI Assistant built with Avalonia UI",
            
            // 主界面
            ["Welcome"] = "Welcome to Lyxie",
            ["Starting"] = "Starting...",
            ["ClickToStart"] = "Click to start conversation",

            // LLM API设置
            ["LLMAPISettings"] = "LLM API Settings",
            ["DefaultConfig"] = "Default Configuration",
            ["APIUrl"] = "API URL",
            ["APIKey"] = "API Key",
            ["ModelName"] = "Model Name",
            ["Temperature"] = "Temperature",
            ["MaxTokens"] = "Max Tokens",
            ["SaveConfig"] = "Save Configuration",
            ["ConfigSaved"] = "Saved",
            ["AddNewConfig"] = "Add New LLM Configuration",
            ["Active"] = "Active",
            ["ActivateConfig"] = "Set as Active",
            ["Custom"] = "Custom",
            ["CancelConfig"] = "Cancel"
        }
    };

    // 获取本地化文本
    public string GetText(string key)
    {
        if (Resources.TryGetValue(_currentLanguage, out var languageResources) &&
            languageResources.TryGetValue(key, out var text))
        {
            return text;
        }

        // 如果找不到，返回键名作为后备
        return key;
    }

    // 获取指定语言的文本
    public static string GetText(string key, Language language)
    {
        if (Resources.TryGetValue(language, out var languageResources) &&
            languageResources.TryGetValue(key, out var text))
        {
            return text;
        }

        // 如果找不到，返回键名作为后备
        return key;
    }
}
