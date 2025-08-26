using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace Buddie.Controls
{
    public partial class TtsConfigControl : UserControl
    {
        public event EventHandler<OpenAiTtsConfiguration>? ConfigurationAdded;
        public event EventHandler<OpenAiTtsConfiguration>? ConfigurationRemoved;
        public event EventHandler<OpenAiTtsConfiguration>? ConfigurationUpdated;

        public TtsConfigControl()
        {
            InitializeComponent();
        }

        public void Initialize(ObservableCollection<OpenAiTtsConfiguration> configurations)
        {
            TtsConfigList.ItemsSource = configurations;
            UpdateNoTtsConfigMessageVisibility(configurations);
        }

        private void UpdateNoTtsConfigMessageVisibility(ObservableCollection<OpenAiTtsConfiguration> configurations)
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
            var configurations = TtsConfigList.ItemsSource as ObservableCollection<OpenAiTtsConfiguration>;
            if (configurations == null) return;

            var newConfig = new OpenAiTtsConfiguration
            {
                Name = $"TTS配置 {configurations.Count + 1}",
                ApiUrl = "http://localhost:5050/v1/audio/speech",
                Model = "tts-1",
                Voice = "alloy",
                Speed = 1.0,
                IsEditMode = true,
                IsSaved = false
            };
            
            configurations.Add(newConfig);
            UpdateNoTtsConfigMessageVisibility(configurations);
            ConfigurationAdded?.Invoke(this, newConfig);
        }

        private void SaveTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is OpenAiTtsConfiguration config)
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
            if (button?.DataContext is OpenAiTtsConfiguration config)
            {
                var configurations = TtsConfigList.ItemsSource as ObservableCollection<OpenAiTtsConfiguration>;
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
            if (button?.DataContext is OpenAiTtsConfiguration config)
            {
                config.IsEditMode = true;
            }
        }

        private void RemoveTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is OpenAiTtsConfiguration config)
            {
                var result = MessageBox.Show(
                    $"确定要删除TTS配置 \"{config.Name}\" 吗？", 
                    "确认删除", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var configurations = TtsConfigList.ItemsSource as ObservableCollection<OpenAiTtsConfiguration>;
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

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 延迟选择所有文本，确保用户体验更好
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
    }
}
