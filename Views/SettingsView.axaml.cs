using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lyxie_desktop.Services;

namespace Lyxie_desktop.Views;

// 字体大小枚举
public enum FontSizeLevel
{
    Small = 0,    // 小号 - 14px
    Default = 1,  // 默认 - 16px
    Medium = 2,   // 中号 - 18px
    Large = 3     // 大号 - 20px
}

// LLM API配置类
public class LlmApiConfig
{
    public string Name { get; set; } = "默认配置";
    public string ApiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "gpt-3.5-turbo";
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2000;
}

// 设置数据类
public class AppSettings
{
    public FontSizeLevel FontSizeLevel { get; set; } = FontSizeLevel.Default;
    public int ThemeIndex { get; set; } = 0;
    public int LanguageIndex { get; set; } = 0;
    public List<LlmApiConfig> LlmApiConfigs { get; set; } = new List<LlmApiConfig>();
    public int ActiveLlmConfigIndex { get; set; } = 0;
    
    // 工具设置相关属性
    public bool EnableTTS { get; set; } = false;
    public bool EnableDev1 { get; set; } = false; 
    public bool EnableDev2 { get; set; } = false;

    // 确保LlmApiConfigs不为null
    public void EnsureConfigsInitialized()
    {
        if (LlmApiConfigs == null)
        {
            LlmApiConfigs = new List<LlmApiConfig>();
        }
        
        // 确保活跃配置索引在有效范围内
        if (LlmApiConfigs.Count > 0 && (ActiveLlmConfigIndex < 0 || ActiveLlmConfigIndex >= LlmApiConfigs.Count))
        {
            ActiveLlmConfigIndex = 0;
        }
    }
}

public partial class SettingsView : UserControl
{
    // 事件：请求返回到主界面
    public event EventHandler? BackToMainRequested;

    // 事件：字体大小改变
    public event EventHandler<double>? FontSizeChanged;

    private AppSettings _settings;
    private readonly string _settingsPath;
    private LlmApiConfig? _currentEditingConfig;
    private bool _isAddingNewConfig = false;
    private bool _isEditingConfig = false; // 控制配置详情区域的可见性

    public SettingsView()
    {
        // 设置文件路径
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "Lyxie");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");

        // 加载设置
        _settings = LoadSettings();
        
        // 确保至少有一个默认LLM配置
        _settings.EnsureConfigsInitialized();

        InitializeComponent();

        // 应用已保存的设置
        ApplySettings();

        // 为返回按钮添加点击事件
        var backButton = this.FindControl<Button>("BackButton");
        if (backButton != null)
        {
            backButton.Click += OnBackButtonClick;
        }

        // 为字体大小按钮添加点击事件
        SetupFontSizeButtons();

        // 为主题下拉框添加选择变化事件
        var themeComboBox = this.FindControl<ComboBox>("ThemeComboBox");
        if (themeComboBox != null)
        {
            themeComboBox.SelectionChanged += OnThemeChanged;
        }

        // 为语言下拉框添加选择变化事件
        var languageComboBox = this.FindControl<ComboBox>("LanguageComboBox");
        if (languageComboBox != null)
        {
            languageComboBox.SelectionChanged += OnLanguageChanged;
        }

        // 订阅语言变更事件
        App.LanguageService.LanguageChanged += OnGlobalLanguageChanged;

        // 初始化界面文本（不更新下拉框选项，避免初始化时的问题）
        UpdateInterfaceTexts(false);
        
        // 初始化LLM API配置界面
        InitializeLlmApiConfigUI();
    }
    
    private void OnBackButtonClick(object? sender, RoutedEventArgs e)
    {
        // 触发返回到主界面的事件
        BackToMainRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0)
        {
            _settings.ThemeIndex = comboBox.SelectedIndex;
            SaveSettings();

            // 实时应用主题切换
            var themeMode = Services.ThemeService.GetThemeModeFromIndex(comboBox.SelectedIndex);
            App.ThemeService.SetTheme(themeMode);
        }
    }
    
    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0)
        {
            _settings.LanguageIndex = comboBox.SelectedIndex;
            SaveSettings();

            // 实时应用语言切换
            var language = Services.LanguageService.GetLanguageFromIndex(comboBox.SelectedIndex);
            App.LanguageService.SetLanguage(language);

            // 更新界面文本
            UpdateInterfaceTexts(true);
        }
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                
                // 确保LlmApiConfigs不为null并且活跃配置索引有效
                settings.EnsureConfigsInitialized();
                
                return settings;
            }
        }
        catch (Exception ex)
        {
            // 如果加载失败，使用默认设置
            Console.WriteLine($"Failed to load settings: {ex.Message}");
        }

        var defaultSettings = new AppSettings();
        return defaultSettings;
    }

    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    private void SetupFontSizeButtons()
    {
        var smallButton = this.FindControl<Button>("FontSizeSmallButton");
        var defaultButton = this.FindControl<Button>("FontSizeDefaultButton");
        var mediumButton = this.FindControl<Button>("FontSizeMediumButton");
        var largeButton = this.FindControl<Button>("FontSizeLargeButton");

        if (smallButton != null)
            smallButton.Click += (s, e) => SetFontSize(FontSizeLevel.Small);
        if (defaultButton != null)
            defaultButton.Click += (s, e) => SetFontSize(FontSizeLevel.Default);
        if (mediumButton != null)
            mediumButton.Click += (s, e) => SetFontSize(FontSizeLevel.Medium);
        if (largeButton != null)
            largeButton.Click += (s, e) => SetFontSize(FontSizeLevel.Large);
    }

    private void SetFontSize(FontSizeLevel level)
    {
        _settings.FontSizeLevel = level;

        // 更新按钮样式
        UpdateFontSizeButtonStyles();

        // 触发字体大小改变事件
        var fontSize = GetFontSizeFromLevel(level);
        FontSizeChanged?.Invoke(this, fontSize);

        // 保存设置
        SaveSettings();
    }

    private void UpdateFontSizeButtonStyles()
    {
        var buttons = new[]
        {
            (this.FindControl<Button>("FontSizeSmallButton"), FontSizeLevel.Small),
            (this.FindControl<Button>("FontSizeDefaultButton"), FontSizeLevel.Default),
            (this.FindControl<Button>("FontSizeMediumButton"), FontSizeLevel.Medium),
            (this.FindControl<Button>("FontSizeLargeButton"), FontSizeLevel.Large)
        };

        foreach (var (button, level) in buttons)
        {
            if (button != null)
            {
                if (level == _settings.FontSizeLevel)
                {
                    // 选中状态
                    button.Background = new SolidColorBrush(Color.Parse("#4A90E2"));
                    button.Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"));
                    button.BorderBrush = new SolidColorBrush(Color.Parse("#4A90E2"));
                }
                else
                {
                    // 未选中状态
                    button.Background = new SolidColorBrush(Color.Parse("#3C3C3C"));
                    button.Foreground = new SolidColorBrush(Color.Parse("#E0E0E0"));
                    button.BorderBrush = new SolidColorBrush(Color.Parse("#505050"));
                }
            }
        }
    }

    private static double GetFontSizeFromLevel(FontSizeLevel level)
    {
        return level switch
        {
            FontSizeLevel.Small => 14,
            FontSizeLevel.Default => 16,
            FontSizeLevel.Medium => 18,
            FontSizeLevel.Large => 20,
            _ => 16
        };
    }

    private void ApplySettings()
    {
        try
        {
            // 应用主题设置
            var themeComboBox = this.FindControl<ComboBox>("ThemeComboBox");
            if (themeComboBox != null)
            {
                themeComboBox.SelectedIndex = _settings.ThemeIndex;
            }

            // 应用语言设置
            var languageComboBox = this.FindControl<ComboBox>("LanguageComboBox");
            if (languageComboBox != null)
            {
                languageComboBox.SelectedIndex = _settings.LanguageIndex;
            }

            // 应用字体大小设置
            UpdateFontSizeButtonStyles();
            
            // 触发字体大小改变事件，让主界面应用字体大小
            var fontSize = GetFontSizeFromLevel(_settings.FontSizeLevel);
            FontSizeChanged?.Invoke(this, fontSize);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to apply settings: {ex.Message}");
        }
    }

    // 公共方法：获取当前字体大小
    public double GetCurrentFontSize()
    {
        return GetFontSizeFromLevel(_settings.FontSizeLevel);
    }

    // 全局语言变更事件处理
    private void OnGlobalLanguageChanged(object? sender, Services.Language language)
    {
        UpdateInterfaceTexts(true);
    }

    // 更新界面文本
    private void UpdateInterfaceTexts(bool updateComboBoxes = true)
    {
        var languageService = App.LanguageService;

        // 更新标题
        var titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock");
        if (titleTextBlock != null)
        {
            titleTextBlock.Text = languageService.GetText("Settings");
        }

        // 更新外观卡片
        var appearanceTitle = this.FindControl<TextBlock>("AppearanceTitle");
        if (appearanceTitle != null)
        {
            appearanceTitle.Text = languageService.GetText("Appearance");
        }

        var themeLabel = this.FindControl<TextBlock>("ThemeLabel");
        if (themeLabel != null)
        {
            themeLabel.Text = languageService.GetText("Theme");
        }

        // 更新语言卡片
        var languageTitle = this.FindControl<TextBlock>("LanguageTitle");
        if (languageTitle != null)
        {
            languageTitle.Text = languageService.GetText("Language");
        }

        var languageLabel = this.FindControl<TextBlock>("LanguageLabel");
        if (languageLabel != null)
        {
            languageLabel.Text = languageService.GetText("InterfaceLanguage");
        }

        // 更新字体卡片
        var fontTitle = this.FindControl<TextBlock>("FontTitle");
        if (fontTitle != null)
        {
            fontTitle.Text = languageService.GetText("Font");
        }

        var fontSizeLabel = this.FindControl<TextBlock>("FontSizeLabel");
        if (fontSizeLabel != null)
        {
            fontSizeLabel.Text = languageService.GetText("FontSize");
        }

        // 更新关于卡片
        var aboutTitle = this.FindControl<TextBlock>("AboutTitle");
        if (aboutTitle != null)
        {
            aboutTitle.Text = languageService.GetText("About");
        }

        var versionText = this.FindControl<TextBlock>("VersionText");
        if (versionText != null)
        {
            versionText.Text = languageService.GetText("Version");
        }

        var descriptionText = this.FindControl<TextBlock>("DescriptionText");
        if (descriptionText != null)
        {
            descriptionText.Text = languageService.GetText("Description");
        }
        
        // 更新LLM API设置卡片
        var llmApiSettingsTitle = this.FindControl<TextBlock>("LlmApiSettingsTitle");
        if (llmApiSettingsTitle != null)
        {
            llmApiSettingsTitle.Text = languageService.GetText("LLMAPISettings");
        }
        
        var addLlmConfigButton = this.FindControl<Button>("AddLlmConfigButton");
        if (addLlmConfigButton != null)
        {
            addLlmConfigButton.Content = languageService.GetText("AddNewConfig");
        }
        
        var saveConfigButton = this.FindControl<Button>("SaveLlmConfigButton");
        if (saveConfigButton != null && languageService != null && saveConfigButton.Content?.ToString() != languageService.GetText("ConfigSaved"))
        {
            saveConfigButton.Content = languageService.GetText("SaveConfig");
        }
        
        var cancelConfigButton = this.FindControl<Button>("CancelLlmConfigButton");
        if (cancelConfigButton != null && languageService != null)
        {
            cancelConfigButton.Content = languageService.GetText("CancelConfig");
        }
        
        var saveStatusTextBlock = this.FindControl<TextBlock>("SaveStatusTextBlock");
        if (saveStatusTextBlock != null && saveStatusTextBlock.IsVisible && languageService != null)
        {
            saveStatusTextBlock.Text = languageService.GetText("ConfigSaved");
        }

        // 更新字体大小按钮
        UpdateFontSizeButtonTexts();

        // 更新主题和语言下拉框选项（仅在需要时）
        if (updateComboBoxes)
        {
            UpdateComboBoxItems();
        }
        
        // 更新LLM API配置列表（因为按钮文本需要更新）
        UpdateLlmApiConfigList();
    }

    // 更新字体大小按钮文本
    private void UpdateFontSizeButtonTexts()
    {
        var languageService = App.LanguageService;

        var smallButton = this.FindControl<Button>("FontSizeSmallButton");
        if (smallButton != null)
        {
            smallButton.Content = languageService.GetText("Small");
        }

        var defaultButton = this.FindControl<Button>("FontSizeDefaultButton");
        if (defaultButton != null)
        {
            defaultButton.Content = languageService.GetText("Default");
        }

        var mediumButton = this.FindControl<Button>("FontSizeMediumButton");
        if (mediumButton != null)
        {
            mediumButton.Content = languageService.GetText("Medium");
        }

        var largeButton = this.FindControl<Button>("FontSizeLargeButton");
        if (largeButton != null)
        {
            largeButton.Content = languageService.GetText("Large");
        }
    }

    // 更新下拉框选项
    private void UpdateComboBoxItems()
    {
        var languageService = App.LanguageService;

        // 更新主题下拉框
        var themeComboBox = this.FindControl<ComboBox>("ThemeComboBox");
        if (themeComboBox != null && themeComboBox.Items.Count >= 3)
        {
            // 直接更新现有项目的内容，避免清空和重新添加
            if (themeComboBox.Items[0] is ComboBoxItem item0)
                item0.Content = languageService.GetText("DarkMode");
            if (themeComboBox.Items[1] is ComboBoxItem item1)
                item1.Content = languageService.GetText("LightMode");
            if (themeComboBox.Items[2] is ComboBoxItem item2)
                item2.Content = languageService.GetText("FollowSystem");
        }

        // 更新语言下拉框
        var languageComboBox = this.FindControl<ComboBox>("LanguageComboBox");
        if (languageComboBox != null && languageComboBox.Items.Count >= 2)
        {
            // 直接更新现有项目的内容，避免清空和重新添加
            if (languageComboBox.Items[0] is ComboBoxItem item0)
                item0.Content = languageService.GetText("SimplifiedChinese");
            if (languageComboBox.Items[1] is ComboBoxItem item1)
                item1.Content = languageService.GetText("English");
        }
    }
    
    // 初始化LLM API配置界面
    private void InitializeLlmApiConfigUI()
    {
        // 更新LLM API配置列表
        UpdateLlmApiConfigList();
        
        // 设置添加按钮点击事件
        var addConfigButton = this.FindControl<Button>("AddLlmConfigButton");
        if (addConfigButton != null)
        {
            addConfigButton.Click += OnAddLlmConfigClick;
        }
        
        // 设置保存按钮点击事件
        var saveConfigButton = this.FindControl<Button>("SaveLlmConfigButton");
        if (saveConfigButton != null)
        {
            saveConfigButton.Click += OnSaveLlmConfigClick;
        }
        
        // 设置取消按钮点击事件
        var cancelConfigButton = this.FindControl<Button>("CancelLlmConfigButton");
        if (cancelConfigButton != null)
        {
            cancelConfigButton.Click += OnCancelLlmConfigClick;
        }
        
        // 设置温度滑块值变更事件
        var temperatureSlider = this.FindControl<Slider>("TemperatureSlider");
        if (temperatureSlider != null)
        {
            temperatureSlider.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == "Value")
                {
                    var temperatureValueText = this.FindControl<TextBlock>("TemperatureValueText");
                    if (temperatureValueText != null)
                    {
                        temperatureValueText.Text = temperatureSlider.Value.ToString("0.0");
                    }
                }
            };
        }
        
        // 初始隐藏配置详情区域
        _isEditingConfig = false;
        UpdateConfigDetailVisibility();
    }
    
    // 更新配置详情区域的可见性
    private void UpdateConfigDetailVisibility()
    {
        var configDetailBorder = this.FindControl<Border>("ConfigDetailBorder");
        if (configDetailBorder != null)
        {
            configDetailBorder.IsVisible = _isEditingConfig;
        }
    }
    
    // 更新LLM API配置列表
    private void UpdateLlmApiConfigList()
    {
        var configListPanel = this.FindControl<StackPanel>("LlmConfigListPanel");
        if (configListPanel == null) return;
        
        // 清空现有列表
        configListPanel.Children.Clear();
        
        // 添加每个配置项
        for (int i = 0; i < _settings.LlmApiConfigs.Count; i++)
        {
            var config = _settings.LlmApiConfigs[i];
            var isActive = i == _settings.ActiveLlmConfigIndex;
            
            // 创建配置项面板
            var configItemPanel = new Grid
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            
            configItemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            configItemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            configItemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            configItemPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            // 配置名称
            var nameTextBlock = new TextBlock
            {
                Text = config.Name,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse(isActive ? "#4A90E2" : "#E0E0E0")),
                FontWeight = isActive ? FontWeight.Bold : FontWeight.Normal
            };
            Grid.SetColumn(nameTextBlock, 0);
            configItemPanel.Children.Add(nameTextBlock);
            
            // 激活按钮
            if (!isActive)
            {
                var activateButton = new Button
                {
                    Content = App.LanguageService.GetText("ActivateConfig"),
                    Margin = new Thickness(5, 0, 5, 0),
                    Padding = new Thickness(8, 4, 8, 4),
                    FontSize = 12,
                    Tag = i // 存储配置索引
                };
                activateButton.Click += OnActivateLlmConfigClick;
                Grid.SetColumn(activateButton, 1);
                configItemPanel.Children.Add(activateButton);
            }
            else
            {
                var activeIndicator = new TextBlock
                {
                    Text = App.LanguageService.GetText("Active"),
                    Foreground = new SolidColorBrush(Color.Parse("#4A90E2")),
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(5, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(activeIndicator, 1);
                configItemPanel.Children.Add(activeIndicator);
            }
            
            // 编辑按钮
            var editButton = new Button
            {
                Content = new MaterialIcon { Kind = MaterialIconKind.Edit, Width = 16, Height = 16 },
                Margin = new Thickness(5, 0, 5, 0),
                Padding = new Thickness(8, 4, 8, 4),
                Tag = i // 存储配置索引
            };
            editButton.Click += OnEditLlmConfigClick;
            Grid.SetColumn(editButton, 2);
            configItemPanel.Children.Add(editButton);
            
            // 删除按钮（允许删除所有配置）
            var deleteButton = new Button
            {
                Content = new MaterialIcon { Kind = MaterialIconKind.Delete, Width = 16, Height = 16 },
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(8, 4, 8, 4),
                Tag = i // 存储配置索引
            };
            deleteButton.Click += OnDeleteLlmConfigClick;
            Grid.SetColumn(deleteButton, 3);
            configItemPanel.Children.Add(deleteButton);
            
            configListPanel.Children.Add(configItemPanel);
        }
        
        // 更新编辑区域
        UpdateLlmConfigEditArea();
    }
    
    // 更新LLM配置编辑区域
    private void UpdateLlmConfigEditArea()
    {
        // 获取当前编辑的配置
        if (_currentEditingConfig == null)
        {
            if (_settings.LlmApiConfigs.Count > 0)
            {
                _currentEditingConfig = _settings.LlmApiConfigs[_settings.ActiveLlmConfigIndex];
                _isAddingNewConfig = false;
            }
            else
            {
                _currentEditingConfig = new LlmApiConfig();
                _isAddingNewConfig = true;
            }
        }
        
        // 更新配置名称
        var nameTextBox = this.FindControl<TextBox>("ConfigNameTextBox");
        if (nameTextBox != null)
        {
            nameTextBox.Text = _currentEditingConfig.Name;
        }
        
        // 更新API URL
        var apiUrlTextBox = this.FindControl<TextBox>("ApiUrlTextBox");
        if (apiUrlTextBox != null)
        {
            apiUrlTextBox.Text = _currentEditingConfig.ApiUrl;
        }
        
        // 更新API Key
        var apiKeyTextBox = this.FindControl<TextBox>("ApiKeyTextBox");
        if (apiKeyTextBox != null)
        {
            apiKeyTextBox.Text = _currentEditingConfig.ApiKey;
        }
        
        // 更新Model Name
        var modelNameTextBox = this.FindControl<TextBox>("ModelNameTextBox");
        if (modelNameTextBox != null)
        {
            modelNameTextBox.Text = _currentEditingConfig.ModelName;
        }
        
        // 更新Temperature
        var temperatureSlider = this.FindControl<Slider>("TemperatureSlider");
        if (temperatureSlider != null)
        {
            temperatureSlider.Value = _currentEditingConfig.Temperature;
        }
        
        // 更新Max Tokens
        var maxTokensNumericUpDown = this.FindControl<NumericUpDown>("MaxTokensNumericUpDown");
        if (maxTokensNumericUpDown != null)
        {
            maxTokensNumericUpDown.Value = _currentEditingConfig.MaxTokens;
        }
        
        // 更新保存按钮文本
        var saveConfigButton = this.FindControl<Button>("SaveLlmConfigButton");
        if (saveConfigButton != null)
        {
            saveConfigButton.Content = App.LanguageService.GetText("SaveConfig");
        }
    }
    
    // 添加LLM配置按钮点击事件
    private void OnAddLlmConfigClick(object? sender, RoutedEventArgs e)
    {
        _currentEditingConfig = new LlmApiConfig();
        _isAddingNewConfig = true;
        _isEditingConfig = true;
        UpdateLlmConfigEditArea();
        UpdateConfigDetailVisibility();
    }
    
    // 编辑LLM配置按钮点击事件
    private void OnEditLlmConfigClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && index >= 0 && index < _settings.LlmApiConfigs.Count)
        {
            _currentEditingConfig = _settings.LlmApiConfigs[index];
            _isAddingNewConfig = false;
            _isEditingConfig = true;
            UpdateLlmConfigEditArea();
            UpdateConfigDetailVisibility();
        }
    }
    
    // 删除LLM配置按钮点击事件
    private void OnDeleteLlmConfigClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && index >= 0 && index < _settings.LlmApiConfigs.Count)
        {
            // 删除配置
            _settings.LlmApiConfigs.RemoveAt(index);
            
            // 如果删除的是当前活跃配置或索引超出范围，更新活跃配置索引
            if (_settings.LlmApiConfigs.Count > 0)
            {
                if (_settings.ActiveLlmConfigIndex == index || _settings.ActiveLlmConfigIndex >= _settings.LlmApiConfigs.Count)
                {
                    _settings.ActiveLlmConfigIndex = 0;
                }
                
                // 更新UI
                _currentEditingConfig = _settings.LlmApiConfigs[_settings.ActiveLlmConfigIndex];
            }
            else
            {
                // 如果没有配置了，重置当前编辑的配置
                _currentEditingConfig = null;
                _settings.ActiveLlmConfigIndex = 0;
            }
            
            _isAddingNewConfig = false;
            
            // 保存设置
            SaveSettings();
            
            // 更新UI
            UpdateLlmApiConfigList();
            
            // 如果正在编辑配置，关闭编辑区域
            if (_isEditingConfig)
            {
                _isEditingConfig = false;
                UpdateConfigDetailVisibility();
            }
        }
    }
    
    // 激活LLM配置按钮点击事件
    private void OnActivateLlmConfigClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && index >= 0 && index < _settings.LlmApiConfigs.Count)
        {
            // 设置为活跃配置
            _settings.ActiveLlmConfigIndex = index;
            
            // 保存设置
            SaveSettings();
            
            // 更新UI
            UpdateLlmApiConfigList();
        }
    }
    
    // 取消LLM配置编辑
    private void OnCancelLlmConfigClick(object? sender, RoutedEventArgs e)
    {
        // 隐藏配置详情区域
        _isEditingConfig = false;
        UpdateConfigDetailVisibility();
    }
    
    // 保存LLM配置按钮点击事件
    private void OnSaveLlmConfigClick(object? sender, RoutedEventArgs e)
    {
        if (_currentEditingConfig == null) return;
        
        // 从UI获取配置值
        var nameTextBox = this.FindControl<TextBox>("ConfigNameTextBox");
        var apiUrlTextBox = this.FindControl<TextBox>("ApiUrlTextBox");
        var apiKeyTextBox = this.FindControl<TextBox>("ApiKeyTextBox");
        var modelNameTextBox = this.FindControl<TextBox>("ModelNameTextBox");
        var temperatureSlider = this.FindControl<Slider>("TemperatureSlider");
        var maxTokensNumericUpDown = this.FindControl<NumericUpDown>("MaxTokensNumericUpDown");
        
        if (nameTextBox != null) _currentEditingConfig.Name = nameTextBox.Text ?? "";
        if (apiUrlTextBox != null) _currentEditingConfig.ApiUrl = apiUrlTextBox.Text ?? "";
        if (apiKeyTextBox != null) _currentEditingConfig.ApiKey = apiKeyTextBox.Text ?? "";
        if (modelNameTextBox != null) _currentEditingConfig.ModelName = modelNameTextBox.Text ?? "";
        if (temperatureSlider != null) _currentEditingConfig.Temperature = (float)temperatureSlider.Value;
        if (maxTokensNumericUpDown != null && maxTokensNumericUpDown.Value.HasValue) _currentEditingConfig.MaxTokens = (int)maxTokensNumericUpDown.Value.Value;
        
        // 如果是添加新配置
        if (_isAddingNewConfig)
        {
            _settings.LlmApiConfigs.Add(_currentEditingConfig);
            _settings.ActiveLlmConfigIndex = _settings.LlmApiConfigs.Count - 1;
            _isAddingNewConfig = false;
        }
        
        // 保存设置
        SaveSettings();
        
        // 更新UI
        UpdateLlmApiConfigList();
        
        // 显示保存成功提示
        var saveStatusTextBlock = this.FindControl<TextBlock>("SaveStatusTextBlock");
        if (saveStatusTextBlock != null)
        {
            saveStatusTextBlock.Text = App.LanguageService.GetText("ConfigSaved");
            saveStatusTextBlock.IsVisible = true;
            
            // 2秒后隐藏提示并关闭配置详情区域
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                if (saveStatusTextBlock != null)
                {
                    saveStatusTextBlock.IsVisible = false;
                }
                
                // 保存后隐藏配置详情区域
                _isEditingConfig = false;
                UpdateConfigDetailVisibility();
            };
            
            timer.Start();
        }
    }
}
