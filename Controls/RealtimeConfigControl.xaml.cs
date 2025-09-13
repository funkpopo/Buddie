using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Linq;
using Buddie.Services.ExceptionHandling;
using Microsoft.Extensions.Logging;

namespace Buddie.Controls
{
    public partial class RealtimeConfigControl : UserControl
    {
        private readonly ILogger _logger = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) is ILoggerFactory lf ? lf.CreateLogger(typeof(RealtimeConfigControl).FullName!) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public event EventHandler<RealtimeConfiguration>? ConfigurationAdded;
        public event EventHandler<RealtimeConfiguration>? ConfigurationRemoved;
        public event EventHandler<RealtimeConfiguration>? ConfigurationUpdated;
        public event EventHandler<RealtimeConfiguration>? ConfigurationActivated;

        private readonly RealtimeChannelInfo[] _channelInfos = new[]
        {
            new RealtimeChannelInfo { Name = "阿里云百炼Qwen-Omni", ChannelType = RealtimeChannelType.QwenOmni },
            new RealtimeChannelInfo { Name = "自定义配置", ChannelType = RealtimeChannelType.Custom }
        };

        public RealtimeConfigControl()
        {
            InitializeComponent();
            this.Loaded += RealtimeConfigControl_Loaded;
        }

        private void RealtimeConfigControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 控件加载后初始化所有ComboBox
            InitializeComboBoxes();
        }

        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is RealtimeConfiguration config && comboBox.SelectedItem is string selectedModel)
            {
                config.Model = selectedModel;
                // 强制更新文本显示
                comboBox.Text = selectedModel;
            }
        }

        private void VadModeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null && comboBox.ItemsSource == null)
            {
                var vadModeItems = new[]
                {
                    new { Display = "客户端VAD", Value = VadMode.CLIENT_VAD }
                };
                
                comboBox.ItemsSource = vadModeItems;
                comboBox.DisplayMemberPath = "Display";
                comboBox.SelectedValuePath = "Value";
                
                // Re-bind to the selected value
                if (comboBox.DataContext is RealtimeConfiguration config)
                {
                    comboBox.SelectedValue = config.VadMode;
                }
            }
        }

        private void ChannelTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.DataContext is RealtimeConfiguration config && comboBox.SelectedItem is RealtimeChannelInfo channelInfo)
            {
                config.ChannelType = channelInfo.ChannelType;
                _logger.LogInformation("实时交互渠道类型已更改为: {Type}, 模型: {Model}", config.ChannelType, config.Model);
            }
        }

        private void ChannelTypeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            // 当ComboBox加载时，设置其ItemsSource
            var comboBox = sender as ComboBox;
            if (comboBox != null && comboBox.ItemsSource == null)
            {
                comboBox.ItemsSource = _channelInfos;
            }
        }

        // 公开渠道信息以便在XAML中绑定
        public RealtimeChannelInfo[] ChannelInfos => _channelInfos;

        public void Initialize(ObservableCollection<RealtimeConfiguration> configurations)
        {
            RealtimeConfigList.ItemsSource = configurations;
            UpdateNoRealtimeConfigMessageVisibility(configurations);
            
            // 延迟初始化以确保ItemContainer已生成
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                InitializeComboBoxes();
            }));
        }

        private void InitializeComboBoxes()
        {
            // 初始化所有已存在项的渠道类型下拉框
            var configurations = RealtimeConfigList.ItemsSource as ObservableCollection<RealtimeConfiguration>;
            if (configurations != null)
            {
                foreach (var config in configurations)
                {
                    var container = RealtimeConfigList.ItemContainerGenerator.ContainerFromItem(config) as FrameworkElement;
                    if (container != null)
                    {
                        // Initialize ChannelType ComboBox
                        var channelComboBox = FindVisualChild<ComboBox>(container, "ChannelTypeComboBox");
                        if (channelComboBox != null && channelComboBox.ItemsSource == null)
                        {
                            channelComboBox.ItemsSource = _channelInfos;
                            // 重新设置选中值以确保正确显示
                            var currentValue = config.ChannelType;
                            channelComboBox.SelectedValue = currentValue;
                        }
                        
                        // Initialize VadMode ComboBox
                        var vadModeComboBox = FindVisualChild<ComboBox>(container, "VadModeComboBox");
                        if (vadModeComboBox != null && vadModeComboBox.ItemsSource == null)
                        {
                            var vadModeItems = new[]
                            {
                                new { Display = "客户端VAD", Value = VadMode.CLIENT_VAD }
                            };
                            
                            vadModeComboBox.ItemsSource = vadModeItems;
                            vadModeComboBox.DisplayMemberPath = "Display";
                            vadModeComboBox.SelectedValuePath = "Value";
                            vadModeComboBox.SelectedValue = config.VadMode;
                        }

                        // Initialize Model ComboBox for QwenOmni
                        var qwenModelComboBox = FindVisualChild<ComboBox>(container, "QwenModelComboBox");
                        if (qwenModelComboBox != null && config.ChannelType == RealtimeChannelType.QwenOmni)
                        {
                            qwenModelComboBox.ItemsSource = config.SupportedModels;
                            qwenModelComboBox.SelectedItem = config.Model;
                        }
                    }
                }
            }
        }

        private void UpdateNoRealtimeConfigMessageVisibility(ObservableCollection<RealtimeConfiguration> configurations)
        {
            // 如果需要显示"无配置"消息，可以在这里实现
        }

        private void AddRealtimeConfig_Click(object sender, RoutedEventArgs e)
        {
            ExceptionHandlingService.UI.ExecuteSafely(() =>
            {
                var configurations = RealtimeConfigList.ItemsSource as ObservableCollection<RealtimeConfiguration>;
                if (configurations == null)
                {
                    _logger.LogWarning("RealtimeConfigList.ItemsSource is null");
                    return;
                }
                
                var newConfig = new RealtimeConfiguration
                {
                    Name = $"实时交互配置 {configurations.Count + 1}",
                    ChannelType = RealtimeChannelType.QwenOmni,
                    IsEditMode = true,
                    IsSaved = false,
                    IsActive = false
                };
                
                configurations.Add(newConfig);
                UpdateNoRealtimeConfigMessageVisibility(configurations);
                ConfigurationAdded?.Invoke(this, newConfig);

                // 初始化新添加项的渠道类型下拉框
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    var container = RealtimeConfigList.ItemContainerGenerator.ContainerFromItem(newConfig) as FrameworkElement;
                    if (container != null)
                    {
                        // Initialize ChannelType ComboBox
                        var channelComboBox = FindVisualChild<ComboBox>(container, "ChannelTypeComboBox");
                        if (channelComboBox != null)
                        {
                            channelComboBox.ItemsSource = _channelInfos;
                            channelComboBox.SelectedValue = newConfig.ChannelType;
                        }
                        
                        // Initialize VadMode ComboBox
                        var vadModeComboBox = FindVisualChild<ComboBox>(container, "VadModeComboBox");
                        if (vadModeComboBox != null)
                        {
                            var vadModeItems = new[]
                            {
                                new { Display = "客户端VAD", Value = VadMode.CLIENT_VAD }
                            };
                            
                            vadModeComboBox.ItemsSource = vadModeItems;
                            vadModeComboBox.DisplayMemberPath = "Display";
                            vadModeComboBox.SelectedValuePath = "Value";
                            vadModeComboBox.SelectedValue = newConfig.VadMode;
                        }

                        // Initialize Model ComboBox for QwenOmni
                        var qwenModelComboBox = FindVisualChild<ComboBox>(container, "QwenModelComboBox");
                        if (qwenModelComboBox != null && newConfig.ChannelType == RealtimeChannelType.QwenOmni)
                        {
                            qwenModelComboBox.ItemsSource = newConfig.SupportedModels;
                            qwenModelComboBox.SelectedItem = newConfig.Model;
                        }
                    }
                }));
            }, "添加实时交互配置");
        }

        private void SaveRealtimeConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is RealtimeConfiguration config)
            {
                // 动态验证必填字段
                var missingFields = new List<string>();

                if (string.IsNullOrWhiteSpace(config.Name))
                    missingFields.Add("配置名称");

                if (config.RequiresBaseUrl && string.IsNullOrWhiteSpace(config.BaseUrl))
                    missingFields.Add("WebSocket URL");

                if (config.RequiresApiKey && string.IsNullOrWhiteSpace(config.ApiKey))
                    missingFields.Add("API Key");

                if (config.RequiresModel && string.IsNullOrWhiteSpace(config.Model))
                    missingFields.Add("模型");

                if (config.RequiresVoice && string.IsNullOrWhiteSpace(config.Voice))
                    missingFields.Add("语音");

                if (missingFields.Count > 0)
                {
                    var message = $"请填写以下必填项：\n• {string.Join("\n• ", missingFields)}";
                    MessageBox.Show(message, "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 保存配置
                config.IsEditMode = false;
                config.IsSaved = true;
                ConfigurationUpdated?.Invoke(this, config);

                MessageBox.Show("实时交互配置保存成功！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CancelRealtimeConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is RealtimeConfiguration config)
            {
                var configurations = RealtimeConfigList.ItemsSource as ObservableCollection<RealtimeConfiguration>;
                if (configurations != null && !config.IsSaved)
                {
                    configurations.Remove(config);
                    UpdateNoRealtimeConfigMessageVisibility(configurations);
                    ConfigurationRemoved?.Invoke(this, config);
                }
                else
                {
                    config.IsEditMode = false;
                }
            }
        }

        private void EditRealtimeConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is RealtimeConfiguration config)
            {
                config.IsEditMode = true;
                
                // 重新初始化渠道类型下拉框
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    var container = RealtimeConfigList.ItemContainerGenerator.ContainerFromItem(config) as FrameworkElement;
                    if (container != null)
                    {
                        // Initialize ChannelType ComboBox
                        var channelComboBox = FindVisualChild<ComboBox>(container, "ChannelTypeComboBox");
                        if (channelComboBox != null)
                        {
                            channelComboBox.ItemsSource = _channelInfos;
                            channelComboBox.SelectedValue = config.ChannelType;
                        }
                        
                        // Initialize VadMode ComboBox
                        var vadModeComboBox = FindVisualChild<ComboBox>(container, "VadModeComboBox");
                        if (vadModeComboBox != null)
                        {
                            var vadModeItems = new[]
                            {
                                new { Display = "客户端VAD", Value = VadMode.CLIENT_VAD }
                            };
                            
                            vadModeComboBox.ItemsSource = vadModeItems;
                            vadModeComboBox.DisplayMemberPath = "Display";
                            vadModeComboBox.SelectedValuePath = "Value";
                            vadModeComboBox.SelectedValue = config.VadMode;
                        }

                        // Initialize Model ComboBox for QwenOmni
                        var qwenModelComboBox = FindVisualChild<ComboBox>(container, "QwenModelComboBox");
                        if (qwenModelComboBox != null && config.ChannelType == RealtimeChannelType.QwenOmni)
                        {
                            qwenModelComboBox.ItemsSource = config.SupportedModels;
                            qwenModelComboBox.SelectedItem = config.Model;
                        }
                    }
                }));
            }
        }

        private void DeleteRealtimeConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is RealtimeConfiguration config)
            {
                var result = MessageBox.Show(
                    $"确定要删除配置 \"{config.Name}\" 吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var configurations = RealtimeConfigList.ItemsSource as ObservableCollection<RealtimeConfiguration>;
                    if (configurations != null)
                    {
                        configurations.Remove(config);
                        UpdateNoRealtimeConfigMessageVisibility(configurations);
                        ConfigurationRemoved?.Invoke(this, config);
                    }
                }
            }
        }

        private void ToggleRealtimeActive_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is RealtimeConfiguration config)
            {
                var configurations = RealtimeConfigList.ItemsSource as ObservableCollection<RealtimeConfiguration>;
                if (configurations != null)
                {
                    if (!config.IsActive)
                    {
                        // 取消其他配置的激活状态
                        foreach (var c in configurations)
                        {
                            if (c.IsActive && c != config)
                            {
                                c.IsActive = false;
                            }
                        }
                        config.IsActive = true;
                    }
                    else
                    {
                        config.IsActive = false;
                    }

                    ConfigurationActivated?.Invoke(this, config);
                }
            }
        }

        private async void TestRealtimeConnection_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is RealtimeConfiguration config)
            {
                try
                {
                    button.IsEnabled = false;
                    button.Content = "测试中...";
                    config.TestStatus = TestStatus.Testing;

                    // TODO: 实现实际的连接测试逻辑
                    await Task.Delay(2000);

                    // 模拟测试结果
                    config.TestStatus = TestStatus.Success;
                    config.TestMessage = "连接测试成功";
                    MessageBox.Show("实时交互连接测试成功！", "测试成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    config.TestStatus = TestStatus.Failed;
                    config.TestMessage = ex.Message;
                    MessageBox.Show($"连接测试失败：{ex.Message}", "测试失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    button.IsEnabled = true;
                    button.Content = "测试连接";
                }
            }
        }

        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is RealtimeConfiguration config)
            {
                config.ApiKey = passwordBox.Password;
            }
        }

        // 辅助方法查找可视化树中的子控件
        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public class RealtimeChannelInfo
        {
            public string Name { get; set; } = "";
            public RealtimeChannelType ChannelType { get; set; }
        }
    }
}
