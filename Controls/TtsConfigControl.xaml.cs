using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

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

                System.Diagnostics.Debug.WriteLine($"Creating new TTS configuration, current count: {configurations.Count}");
                
                var newConfig = new TtsConfiguration
                {
                    Name = $"TTS配置 {configurations.Count + 1}",
                    ChannelType = TtsChannelType.OpenAI,
                    IsEditMode = true,
                    IsSaved = false,
                    IsActive = false
                };
                
                System.Diagnostics.Debug.WriteLine($"Adding new config: {newConfig.Name}");
                configurations.Add(newConfig);
                
                System.Diagnostics.Debug.WriteLine("Updating visibility");
                UpdateNoTtsConfigMessageVisibility(configurations);
                
                System.Diagnostics.Debug.WriteLine("Invoking ConfigurationAdded event");
                ConfigurationAdded?.Invoke(this, newConfig);
                
                System.Diagnostics.Debug.WriteLine("AddTtsConfig_Click completed successfully");
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
                // 验证必填字段
                if (string.IsNullOrWhiteSpace(config.Name) || 
                    string.IsNullOrWhiteSpace(config.ApiUrl) || 
                    string.IsNullOrWhiteSpace(config.Model) ||
                    string.IsNullOrWhiteSpace(config.Voice))
                {
                    MessageBox.Show(
                        "请填写完整的配置信息（配置名称、API URL、模型、语音为必填项）", 
                        "验证错误", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                    return;
                }

                config.IsEditMode = false;
                config.IsSaved = true;
                ConfigurationUpdated?.Invoke(this, config);
            }
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
                System.Diagnostics.Debug.WriteLine($"🖱️ 用户通过复选框激活TTS配置: {config.Name} (ID: {config.Id})");
                ConfigurationActivated?.Invoke(this, config);
            }
        }

        private void ActivateTts_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox?.DataContext is TtsConfiguration config)
            {
                System.Diagnostics.Debug.WriteLine($"🖱️ 用户通过复选框取消激活TTS配置: {config.Name} (ID: {config.Id})");
                // 取消激活时，直接设置为非激活状态
                config.IsActive = false;
                // 如果配置已保存，立即更新到数据库
                if (config.IsSaved && config.Id > 0)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var appSettings = Application.Current.MainWindow?.DataContext as AppSettings;
                            if (appSettings != null)
                            {
                                await appSettings.SaveTtsConfigurationAsync(config);
                                System.Diagnostics.Debug.WriteLine($"✅ TTS配置 {config.Name} 的非激活状态已保存到数据库");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ 保存TTS配置激活状态失败: {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ℹ️ TTS配置 {config.Name} 未保存或ID无效，跳过数据库更新");
                }
            }
        }

        private void ActivateTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is TtsConfiguration config)
            {
                System.Diagnostics.Debug.WriteLine($"🖱️ 用户通过按钮激活TTS配置: {config.Name} (ID: {config.Id})");
                ConfigurationActivated?.Invoke(this, config);
            }
        }

        private void ChannelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is TtsConfiguration config && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                // 获取选中的渠道类型
                var channelTypeString = selectedItem.Tag?.ToString();
                if (Enum.TryParse<TtsChannelType>(channelTypeString, out var channelType))
                {
                    // 设置渠道类型，这会触发TtsConfiguration中的UpdateDefaultsForChannel方法
                    config.ChannelType = channelType;
                    
                    System.Diagnostics.Debug.WriteLine($"TTS渠道类型已更改为: {channelType}");
                    System.Diagnostics.Debug.WriteLine($"API URL已更新为: {config.ApiUrl}");
                    System.Diagnostics.Debug.WriteLine($"模型已更新为: {config.Model}");
                    System.Diagnostics.Debug.WriteLine($"语音已更新为: {config.Voice}");
                }
            }
        }
    }
}
