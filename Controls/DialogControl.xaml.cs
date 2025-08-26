using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Input;

namespace Buddie.Controls
{
    public partial class DialogControl : UserControl
    {
        public event EventHandler<string>? MessageSent;
        public event EventHandler? DialogClosed;

        public DialogControl()
        {
            InitializeComponent();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.DragMove();
            }
        }

        private void CloseDialog_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            var message = DialogInput.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                MessageSent?.Invoke(this, message);
                DialogInput.Clear();
            }
        }

        public void Show()
        {
            DialogInterface.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            DialogInterface.Visibility = Visibility.Collapsed;
        }

        public new bool IsVisible => DialogInterface.Visibility == Visibility.Visible;

        public void AddMessage(string message, bool isUser = true)
        {
            var messageBlock = new TextBlock
            {
                Text = message,
                Margin = new Thickness(5),
                Background = isUser ? System.Windows.Media.Brushes.LightBlue : System.Windows.Media.Brushes.LightGray,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 350
            };

            DialogMessagesPanel.Children.Add(messageBlock);
            DialogScrollViewer.ScrollToEnd();
        }

        public void ClearMessages()
        {
            DialogMessagesPanel.Children.Clear();
        }

        public async Task SendMessageToApi(string message, OpenApiConfiguration apiConfig)
        {
            AddMessage(message, true);

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiConfig.ApiKey}");

                var requestBody = new
                {
                    model = apiConfig.ModelName,
                    messages = new[]
                    {
                        new { role = "user", content = message }
                    },
                    stream = apiConfig.IsStreamingEnabled
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(apiConfig.ApiUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(responseText);
                    var choices = jsonDoc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() > 0)
                    {
                        var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
                        AddMessage(messageContent ?? "无响应内容", false);
                    }
                    else
                    {
                        AddMessage("API响应格式错误", false);
                    }
                }
                else
                {
                    AddMessage($"API请求失败: {response.StatusCode}", false);
                }
            }
            catch (Exception ex)
            {
                AddMessage($"请求错误: {ex.Message}", false);
            }
        }
    }
}