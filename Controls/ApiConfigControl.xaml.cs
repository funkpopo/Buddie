using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Controls
{
    public partial class ApiConfigControl : UserControl
    {
        private readonly HttpClient httpClient = new HttpClient();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> testCancellationTokens = new();
        private bool _isInitializing = false;

        public event EventHandler<OpenApiConfiguration>? ConfigurationAdded;
        public event EventHandler<OpenApiConfiguration>? ConfigurationRemoved;
        public event EventHandler<OpenApiConfiguration>? ConfigurationUpdated;

        public ApiConfigControl()
        {
            InitializeComponent();
            InitializeChannelTypeComboBox();
        }

        private void InitializeChannelTypeComboBox()
        {
            // This will be handled in the Loaded event of each ComboBox instance
        }

        public void Initialize(ObservableCollection<OpenApiConfiguration> configurations)
        {
            ApiConfigList.ItemsSource = configurations;
            UpdateNoConfigMessageVisibility(configurations);
        }

        private void UpdateNoConfigMessageVisibility(ObservableCollection<OpenApiConfiguration> configurations)
        {
            if (NoConfigMessage != null)
            {
                NoConfigMessage.Visibility = configurations.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void AddApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var configurations = ApiConfigList.ItemsSource as ObservableCollection<OpenApiConfiguration>;
            if (configurations == null) return;

            var newConfig = new OpenApiConfiguration
            {
                Name = $"配置 {configurations.Count + 1}",
                ApiUrl = "https://api.openai.com/v1/chat/completions",
                ModelName = "", // 空的模型名称，让用户手动填写
                IsStreamingEnabled = true,
                IsMultimodalEnabled = false, // 默认关闭多模态
                SupportsThinking = false,
                ChannelType = ChannelType.OpenAI,
                IsEditMode = true,
                IsSaved = false
            };
            
            configurations.Add(newConfig);
            UpdateNoConfigMessageVisibility(configurations);
            ConfigurationAdded?.Invoke(this, newConfig);
        }

        private void SaveApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                // 验证必填字段
                if (string.IsNullOrWhiteSpace(config.Name) || 
                    string.IsNullOrWhiteSpace(config.ApiUrl) || 
                    string.IsNullOrWhiteSpace(config.ModelName))
                {
                    MessageBox.Show(
                        "请填写完整的配置信息（配置名称、API URL、模型名称为必填项）", 
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

        private void CancelApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                var configurations = ApiConfigList.ItemsSource as ObservableCollection<OpenApiConfiguration>;
                if (configurations == null) return;

                if (!config.IsSaved)
                {
                    // 新配置，直接删除
                    configurations.Remove(config);
                    UpdateNoConfigMessageVisibility(configurations);
                    ConfigurationRemoved?.Invoke(this, config);
                }
                else
                {
                    // 已保存的配置，恢复到显示模式
                    config.IsEditMode = false;
                }
            }
        }

        private void EditApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                config.IsEditMode = true;
            }
        }

        private void RemoveApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                var result = MessageBox.Show(
                    $"确定要删除配置 \"{config.Name}\" 吗？", 
                    "确认删除", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var configurations = ApiConfigList.ItemsSource as ObservableCollection<OpenApiConfiguration>;
                    if (configurations != null)
                    {
                        configurations.Remove(config);
                        UpdateNoConfigMessageVisibility(configurations);
                        ConfigurationRemoved?.Invoke(this, config);
                    }
                }
            }
        }

        private async void TestApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                await TestApiConfigurationAsync(config);
            }
        }

        private async void TestAllApiConfigs_Click(object sender, RoutedEventArgs e)
        {
            var configurations = ApiConfigList.ItemsSource as ObservableCollection<OpenApiConfiguration>;
            if (configurations == null) return;

            var tasks = new List<Task>();
            foreach (var config in configurations)
            {
                tasks.Add(TestApiConfigurationAsync(config));
            }
            await Task.WhenAll(tasks);
        }

        private async Task TestApiConfigurationAsync(OpenApiConfiguration config)
        {
            if (config == null) return;
            
            if (string.IsNullOrWhiteSpace(config.ApiUrl) || 
                string.IsNullOrWhiteSpace(config.ApiKey) || 
                string.IsNullOrWhiteSpace(config.ModelName))
            {
                config.TestStatus = TestStatus.Failed;
                config.TestMessage = "配置信息不完整";
                return;
            }

            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                string configId = $"{config.Name}_{DateTime.Now.Ticks}";
                
                // 如果已经有测试在进行，先取消
                if (testCancellationTokens.TryGetValue(configId, out var existingCts))
                {
                    existingCts.Cancel();
                    testCancellationTokens.TryRemove(configId, out _);
                }

                var cts = new CancellationTokenSource();
                testCancellationTokens.TryAdd(configId, cts);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    config.TestStatus = TestStatus.Testing;
                    config.TestMessage = "正在测试...";

                    var requestData = new
                    {
                        model = config.ModelName,
                        messages = new[]
                        {
                            new { role = "user", content = "ping" }
                        },
                        max_tokens = 10
                    };

                    var json = JsonSerializer.Serialize(requestData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var request = new HttpRequestMessage(HttpMethod.Post, config.ApiUrl);
                    request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                    request.Content = content;

                    var response = await httpClient.SendAsync(request, cts.Token);
                    stopwatch.Stop();
                    var delayMs = stopwatch.ElapsedMilliseconds;

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var isValidResponse = ExceptionHandlingService.ExecuteSafely(() =>
                        {
                            var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                            return responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0;
                        }, 
                        ExceptionHandlingService.HandlingStrategy.LogOnly, 
                        false,
                        new ExceptionHandlingService.ExceptionContext
                        {
                            Component = "ApiConfigControl",
                            Operation = "解析API测试响应"
                        });

                        if (isValidResponse)
                        {
                            config.TestStatus = TestStatus.Success;
                            config.TestMessage = $"连接成功 ({delayMs}ms)";
                        }
                        else
                        {
                            config.TestStatus = TestStatus.Failed;
                            config.TestMessage = $"响应格式异常 ({delayMs}ms)";
                        }
                    }
                    else
                    {
                        config.TestStatus = TestStatus.Failed;
                        config.TestMessage = $"HTTP {(int)response.StatusCode} ({delayMs}ms)";
                    }
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    config.TestStatus = TestStatus.Failed;
                    config.TestMessage = "测试被取消";
                }
                finally
                {
                    testCancellationTokens.TryRemove(configId, out _);
                    cts.Dispose();
                }
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "ApiConfigControl",
                Operation = "API配置测试"
            });
        }

        private void TextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        public void Dispose()
        {
            // 取消所有正在进行的测试
            foreach (var cts in testCancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            testCancellationTokens.Clear();
            
            httpClient?.Dispose();
        }

        private void ChannelTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is not ComboBoxItem selectedItem)
                return;
                
            var selectedChannel = selectedItem.Tag as PresetChannel;
            if (selectedChannel == null || comboBox.DataContext is not OpenApiConfiguration config)
                return;

            config.ChannelType = selectedChannel.ChannelType;
            
            // Only update API URL and other properties during user interaction, not during initialization
            if (!_isInitializing && selectedChannel.ChannelType != ChannelType.Custom)
            {
                config.ApiUrl = selectedChannel.DefaultApiUrl;
                config.IsStreamingEnabled = selectedChannel.SupportsStreaming;
                // 不自动设置多模态状态，保持用户当前选择
                config.SupportsThinking = selectedChannel.SupportsThinking;
                
                // 不填充模型名称，让用户手动输入
            }
        }

        private void ChannelTypeComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox != null)
            {
                // Set initialization flag to prevent API URL overwriting
                _isInitializing = true;
                
                // Initialize ComboBox items
                comboBox.Items.Clear();
                var channels = PresetChannels.GetPresetChannels();
                foreach (var channel in channels)
                {
                    var comboBoxItem = new ComboBoxItem
                    {
                        Content = channel.Name,
                        Tag = channel
                    };
                    comboBox.Items.Add(comboBoxItem);
                }

                // Set selected item based on current ChannelType
                if (comboBox.DataContext is OpenApiConfiguration config)
                {
                    var selectedChannel = channels.FirstOrDefault(c => c.ChannelType == config.ChannelType);
                    if (selectedChannel != null)
                    {
                        var item = comboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (i.Tag as PresetChannel)?.ChannelType == selectedChannel.ChannelType);
                        comboBox.SelectedItem = item;
                    }
                }
                
                // Clear initialization flag
                _isInitializing = false;
            }
        }
    }
}
