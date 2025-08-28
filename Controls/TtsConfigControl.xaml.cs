using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Buddie.Controls
{
    public partial class TtsConfigControl : UserControl
    {
        public event EventHandler<TtsConfiguration>? ConfigurationAdded;
        public event EventHandler<TtsConfiguration>? ConfigurationRemoved;
        public event EventHandler<TtsConfiguration>? ConfigurationUpdated;
        public event EventHandler<TtsConfiguration>? ConfigurationActivated;

        public TtsConfigControl()
        {
            InitializeComponent();
        }

        public void Initialize(ObservableCollection<TtsConfiguration> configurations)
        {
            TtsConfigList.ItemsSource = configurations;
            UpdateNoTtsConfigMessageVisibility(configurations);
        }

        private void UpdateNoTtsConfigMessageVisibility(ObservableCollection<TtsConfiguration> configurations)
        {
            if (NoTtsConfigMessage != null)
            {
                NoTtsConfigMessage.Visibility = configurations.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void AddTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configurations = TtsConfigList.ItemsSource as ObservableCollection<TtsConfiguration>;
                if (configurations == null)
                {
                    System.Diagnostics.Debug.WriteLine("TtsConfigList.ItemsSource is null");
                    return;
                }
                
                var newConfig = new TtsConfiguration
                {
                    Name = $"TTS配置 {configurations.Count + 1}",
                    ChannelType = TtsChannelType.OpenAI,
                    IsEditMode = true,
                    IsSaved = false,
                    IsActive = false
                };
                
                configurations.Add(newConfig);
                UpdateNoTtsConfigMessageVisibility(configurations);
                ConfigurationAdded?.Invoke(this, newConfig);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddTtsConfig_Click: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"添加TTS配置时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is TtsConfiguration config)
            {
                // 动态验证必填字段
                var missingFields = new List<string>();

                if (string.IsNullOrWhiteSpace(config.Name))
                    missingFields.Add("配置名称");

                if (config.RequiresApiUrl && string.IsNullOrWhiteSpace(config.ApiUrl))
                    missingFields.Add("API URL");

                if (config.RequiresApiKey && string.IsNullOrWhiteSpace(config.ApiKey))
                    missingFields.Add(config.ApiKeyLabel.TrimEnd(':'));

                if (config.RequiresModel && string.IsNullOrWhiteSpace(config.Model))
                    missingFields.Add(config.ModelLabel.TrimEnd(':'));

                if (config.RequiresVoice && string.IsNullOrWhiteSpace(config.Voice))
                    missingFields.Add(config.VoiceLabel.TrimEnd(':'));

                if (missingFields.Count > 0)
                {
                    var message = $"请填写以下必填项：\n• {string.Join("\n• ", missingFields)}";
                    MessageBox.Show(message, "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }



                // 渠道特定验证
                var validationResult = ValidateChannelSpecificSettings(config);
                if (!string.IsNullOrEmpty(validationResult))
                {
                    MessageBox.Show(validationResult, "配置验证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                config.IsEditMode = false;
                config.IsSaved = true;
                ConfigurationUpdated?.Invoke(this, config);
            }
        }



        private string ValidateChannelSpecificSettings(TtsConfiguration config)
        {
            switch (config.ChannelType)
            {
                case TtsChannelType.ElevenLabs:
                    if (!config.ApiUrl.Contains("{voice_id}"))
                        return "ElevenLabs API URL 应包含 {voice_id} 占位符";
                    
                    if (config.Voice.Length != 20)
                        return "ElevenLabs Voice ID 应为20位字符";
                    
                    break;

                case TtsChannelType.OpenAI:
                    if (!config.Voice.Equals("alloy") && !config.Voice.Equals("echo") && 
                        !config.Voice.Equals("fable") && !config.Voice.Equals("onyx") && 
                        !config.Voice.Equals("nova") && !config.Voice.Equals("shimmer"))
                        return "请使用有效的 OpenAI 语音（alloy、echo、fable、onyx、nova、shimmer）";
                    
                    break;

                case TtsChannelType.MiniMax:
                    // MiniMax特定验证
                    if (string.IsNullOrWhiteSpace(config.ApiUrl) || !config.ApiUrl.Contains("minimax"))
                        return "请使用有效的 MiniMax API URL";
                    
                    if (config.Speed < 0.5 || config.Speed > 2.0)
                        return "MiniMax TTS 语速范围为 0.5-2.0";
                    
                    // 异步验证MiniMax配置（可选）
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var (isValid, message) = await Buddie.Services.Tts.MiniMaxTtsValidator.ValidateConfigurationAsync(config);
                            if (!isValid)
                            {
                                System.Diagnostics.Debug.WriteLine($"MiniMax配置验证失败: {message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"MiniMax配置验证异常: {ex.Message}");
                        }
                    });
                    
                    break;
            }

            return string.Empty;
        }

        private void CancelTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is TtsConfiguration config)
            {
                var configurations = TtsConfigList.ItemsSource as ObservableCollection<TtsConfiguration>;
                if (configurations == null) return;

                if (!config.IsSaved)
                {
                    // 新配置，直接删除
                    configurations.Remove(config);
                    UpdateNoTtsConfigMessageVisibility(configurations);
                    ConfigurationRemoved?.Invoke(this, config);
                }
                else
                {
                    // 已保存的配置，恢复到显示模式
                    config.IsEditMode = false;
                }
            }
        }

        private void EditTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is TtsConfiguration config)
            {
                config.IsEditMode = true;
            }
        }

        private void RemoveTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is TtsConfiguration config)
            {
                var result = MessageBox.Show(
                    $"确定要删除TTS配置 \"{config.Name}\" 吗？", 
                    "确认删除", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var configurations = TtsConfigList.ItemsSource as ObservableCollection<TtsConfiguration>;
                    if (configurations != null)
                    {
                        configurations.Remove(config);
                        UpdateNoTtsConfigMessageVisibility(configurations);
                        ConfigurationRemoved?.Invoke(this, config);
                    }
                }
            }
        }

        private void TtsEditSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 这个方法主要用于实时更新显示的速度值，数据绑定会自动处理实际值的更新
        }

        private void TextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        private void TestTts_Click(object sender, RoutedEventArgs e)
        {
            var testTextBox = TestTextBox;
            
            if (testTextBox != null)
            {
                string testText = testTextBox.Text;
                
                // TTS测试功能的实现可以在这里添加
                // 目前仅作为占位符
            }
        }

        private void ActivateTts_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox?.DataContext is TtsConfiguration config)
            {
                ConfigurationActivated?.Invoke(this, config);
            }
        }

        private void ActivateTts_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox?.DataContext is TtsConfiguration config)
            {
                // 取消激活时，直接设置为非激活状态
                config.IsActive = false;
                // 如果配置已保存，立即更新到数据库
                if (config.IsSaved && config.Id > 0)
                {
                    // 获取AppSettings的更可靠方法 - 通过SettingsControl的DataContext
                    var settingsControl = this.Parent;
                    while (settingsControl != null && !(settingsControl is Controls.SettingsControl))
                    {
                        settingsControl = LogicalTreeHelper.GetParent(settingsControl);
                    }
                    
                    var appSettings = (settingsControl as Controls.SettingsControl)?.DataContext as AppSettings;
                    if (appSettings != null)
                    {
                        // 使用Task.Run但添加异常处理和超时机制
                        var saveTask = Task.Run(async () =>
                        {
                            try
                            {
                                await appSettings.SaveTtsConfigurationAsync(config);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ 保存TTS配置激活状态失败: {ex.Message}");
                            }
                        });
                        
                        // 不等待，但记录任务以便调试
                        saveTask.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ TTS配置保存任务失败: {t.Exception?.GetBaseException().Message}");
                            }
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
            }
        }

        private void ActivateTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is TtsConfiguration config)
            {
                ConfigurationActivated?.Invoke(this, config);
            }
        }

        private void ChannelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is TtsConfiguration config)
            {
                // SelectedValuePath="Tag" 会自动处理绑定到ChannelType属性
                // UpdateDefaultsForChannel方法会在属性setter中自动调用
                System.Diagnostics.Debug.WriteLine($"TTS渠道类型已更改为: {config.ChannelType}");
                
                // 确保模型和语音字段被更新为新渠道的默认值
                // 这是额外的保险措施，防止UI绑定问题
                var preset = TtsPresetChannels.GetPresetChannel(config.ChannelType);
                
                if (preset.SupportedModels.Length > 0)
                {
                    var newModel = preset.SupportedModels[0];
                    if (config.Model != newModel)
                    {
                        System.Diagnostics.Debug.WriteLine($"强制更新模型: {config.Model} -> {newModel}");
                        config.Model = newModel;
                    }
                }
                
                if (preset.SupportedVoices.Length > 0)
                {
                    var newVoice = preset.SupportedVoices[0];
                    if (config.Voice != newVoice)
                    {
                        System.Diagnostics.Debug.WriteLine($"强制更新语音: {config.Voice} -> {newVoice}");
                        config.Voice = newVoice;
                    }
                }
                
                // 更新API URL
                if (!string.IsNullOrEmpty(preset.DefaultApiUrl) && config.ApiUrl != preset.DefaultApiUrl)
                {
                    System.Diagnostics.Debug.WriteLine($"强制更新API URL: {config.ApiUrl} -> {preset.DefaultApiUrl}");
                    config.ApiUrl = preset.DefaultApiUrl;
                }
            }
        }
    }
}
