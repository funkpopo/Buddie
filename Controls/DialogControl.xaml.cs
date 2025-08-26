using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Linq;
using System.IO;

namespace Buddie.Controls
{
    public partial class DialogControl : UserControl
    {
        public event EventHandler<string>? MessageSent;
        public event EventHandler? DialogClosed;
        
        private CancellationTokenSource? currentRequest;
        private bool isSending = false;
        private bool isSidebarVisible = false;
        private List<string> conversationHistory = new List<string>();

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

        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSidebar();
        }

        private void ToggleSidebar()
        {
            isSidebarVisible = !isSidebarVisible;
            
            // 创建宽度动画
            var animation = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            
            if (isSidebarVisible)
            {
                animation.From = 0;
                animation.To = 200;
                SidebarPanel.Visibility = Visibility.Visible;
                
                // 添加淡入效果
                var fadeAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300),
                    BeginTime = TimeSpan.FromMilliseconds(50)
                };
                SidebarPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
                
                LoadConversationHistory();
            }
            else
            {
                animation.From = 200;
                animation.To = 0;
                
                // 添加淡出效果
                var fadeAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                SidebarPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
                
                animation.Completed += (s, e) => SidebarPanel.Visibility = Visibility.Collapsed;
            }
            
            // 使用Transform来实现宽度动画效果
            animation.Completed += (s, e) => {
                if (isSidebarVisible)
                {
                    SidebarColumn.Width = new GridLength(200);
                }
                else
                {
                    SidebarColumn.Width = new GridLength(0);
                }
            };
            
            // 直接设置目标宽度，动画会平滑过渡
            SidebarColumn.Width = isSidebarVisible ? new GridLength(200) : new GridLength(0);
        }

        private void LoadConversationHistory()
        {
            HistoryPanel.Children.Clear();
            
            for (int i = 0; i < conversationHistory.Count; i++)
            {
                var conversation = conversationHistory[i];
                var historyCard = CreateHistoryCard(conversation, i + 1);
                HistoryPanel.Children.Add(historyCard);
            }
            
            // 更新计数显示
            HistoryCountLabel.Text = $"({conversationHistory.Count})";
        }

        private Border CreateHistoryCard(string conversation, int index)
        {
            var historyCard = new Border
            {
                Background = new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Colors.White, 
                    System.Windows.Media.Color.FromRgb(248, 249, 250), 
                    new System.Windows.Point(0, 0), 
                    new System.Windows.Point(0, 1)
                ),
                Margin = new Thickness(8, 4, 8, 4),
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(8),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    Direction = 315,
                    ShadowDepth = 2,
                    Opacity = 0.1,
                    BlurRadius = 4
                }
            };

            var cardContent = new StackPanel();

            // 添加序号和时间标签
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var indexLabel = new TextBlock
            {
                Text = $"#{index}",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Gray,
                Background = System.Windows.Media.Brushes.LightBlue,
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(0, 0, 6, 0)
            };

            var timeLabel = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(indexLabel);
            headerPanel.Children.Add(timeLabel);

            // 添加对话内容
            var contentText = new TextBlock
            {
                Text = conversation.Length > 60 ? conversation.Substring(0, 60) + "..." : conversation,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Black,
                LineHeight = 16
            };

            cardContent.Children.Add(headerPanel);
            cardContent.Children.Add(contentText);
            historyCard.Child = cardContent;

            // 添加悬停效果
            historyCard.MouseEnter += (s, e) => {
                var hoverAnimation = new DoubleAnimation
                {
                    To = 0.8,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                historyCard.BeginAnimation(UIElement.OpacityProperty, hoverAnimation);
                
                // 轻微放大效果
                var scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                historyCard.RenderTransform = scaleTransform;
                historyCard.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                
                var scaleAnimation = new DoubleAnimation
                {
                    To = 1.02,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnimation);
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnimation);
            };

            historyCard.MouseLeave += (s, e) => {
                var normalAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                historyCard.BeginAnimation(UIElement.OpacityProperty, normalAnimation);
                
                if (historyCard.RenderTransform is System.Windows.Media.ScaleTransform transform)
                {
                    var scaleBackAnimation = new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleBackAnimation);
                    transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleBackAnimation);
                }
            };

            // 添加点击事件以加载历史对话
            historyCard.MouseLeftButtonDown += (s, e) => {
                // 这里可以实现加载特定对话历史的功能
                DialogInput.Text = conversation;
                DialogInput.Focus();
                DialogInput.CaretIndex = DialogInput.Text.Length;
            };

            return historyCard;
        }

        private void CloseDialog_Click(object sender, RoutedEventArgs e)
        {
            // 如果正在发送消息，先取消请求
            if (isSending && currentRequest != null)
            {
                currentRequest.Cancel();
            }
            Hide();
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (isSending)
            {
                // 如果正在发送，执行中断操作
                if (currentRequest != null)
                {
                    currentRequest.Cancel();
                    AddMessageBubble("对话已中断", false);
                }
                // 重置发送状态
                SetSendingState(false);
                return;
            }

            var message = DialogInput.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                // 添加到历史记录
                conversationHistory.Add(message);
                
                // 更新UI状态
                SetSendingState(true);
                
                MessageSent?.Invoke(this, message);
                DialogInput.Clear();
            }
        }

        private void SetSendingState(bool sending)
        {
            isSending = sending;
            if (sending)
            {
                SendButton.Content = "中断";
                SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.OrangeRed);
            }
            else
            {
                SendButton.Content = "发送";
                SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
                currentRequest?.Dispose();
                currentRequest = null;
            }
        }

        public void ResetSendingState()
        {
            SetSendingState(false);
        }

        public void Show()
        {
            DialogInterface.Visibility = Visibility.Visible;
        }

        public void Toggle()
        {
            if (IsVisible)
            {
                // 如果已经可见，将界面移到最前面而不是隐藏
                BringToFront();
            }
            else
            {
                Show();
            }
        }

        public void BringToFront()
        {
            // 通过重新设置Panel.ZIndex将控件置于最前面
            var parent = this.Parent as Panel;
            if (parent != null)
            {
                var maxZ = 0;
                foreach (UIElement child in parent.Children)
                {
                    var z = Panel.GetZIndex(child);
                    if (z > maxZ) maxZ = z;
                }
                Panel.SetZIndex(this, maxZ + 1);
            }
        }

        public void Hide()
        {
            DialogInterface.Visibility = Visibility.Collapsed;
        }

        public new bool IsVisible => DialogInterface.Visibility == Visibility.Visible;

        public void AddMessage(string message, bool isUser = true)
        {
            var messageBubble = CreateMessageBubble(message, isUser);
            DialogMessagesPanel.Children.Add(messageBubble);
            DialogScrollViewer.ScrollToEnd();
        }

        public void AddMessageBubble(string message, bool isUser = true)
        {
            var messageBubble = CreateMessageBubble(message, isUser);
            DialogMessagesPanel.Children.Add(messageBubble);
            DialogScrollViewer.ScrollToEnd();
        }

        private Border CreateMessageBubble(string message, bool isUser)
        {
            var messageBlock = new TextBlock
            {
                Text = message,
                Padding = new Thickness(12, 8, 12, 8),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
                FontSize = 13,
                LineHeight = 18
            };

            // 创建气泡样式
            var isDarkTheme = (DialogInterface.Background as SolidColorBrush)?.Color == Color.FromRgb(30, 30, 30);
            
            if (isDarkTheme)
            {
                messageBlock.Foreground = Brushes.White;
                messageBlock.Background = isUser ? 
                    new SolidColorBrush(Color.FromRgb(0, 132, 255)) : 
                    new SolidColorBrush(Color.FromRgb(58, 58, 60));
            }
            else
            {
                messageBlock.Foreground = isUser ? Brushes.White : Brushes.Black;
                messageBlock.Background = isUser ? 
                    new SolidColorBrush(Color.FromRgb(0, 132, 255)) : 
                    new SolidColorBrush(Color.FromRgb(240, 240, 240));
            }

            // 设置圆角和阴影
            var border = new Border
            {
                Child = messageBlock,
                CornerRadius = new CornerRadius(18),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = isUser ? new Thickness(50, 5, 10, 5) : new Thickness(10, 5, 50, 5),
                Background = messageBlock.Background,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 1,
                    Opacity = 0.1,
                    BlurRadius = 3
                }
            };

            messageBlock.Background = Brushes.Transparent;

            return border;
        }

        public void AddMessageBubbleWithReasoning(string? content, string? reasoningContent = null)
        {
            var isDarkTheme = (DialogInterface.Background as SolidColorBrush)?.Color == Color.FromRgb(30, 30, 30);
            
            var messageContainer = new StackPanel
            {
                Margin = new Thickness(10, 5, 50, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 350
            };

            // 如果有思维内容，添加可折叠的思维过程
            if (!string.IsNullOrEmpty(reasoningContent))
            {
                var expander = new Expander
                {
                    Header = "💭 思维过程",
                    IsExpanded = false,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var reasoningBorder = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 270,
                        ShadowDepth = 1,
                        Opacity = 0.05,
                        BlurRadius = 2
                    }
                };

                var reasoningBlock = new TextBlock
                {
                    Text = reasoningContent,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    LineHeight = 16
                };

                if (isDarkTheme)
                {
                    expander.Foreground = Brushes.LightGray;
                    reasoningBorder.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    reasoningBlock.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                }
                else
                {
                    expander.Foreground = Brushes.DarkGray;
                    reasoningBorder.Background = new SolidColorBrush(Color.FromRgb(255, 253, 235));
                    reasoningBlock.Foreground = new SolidColorBrush(Color.FromRgb(101, 103, 107));
                }

                reasoningBorder.Child = reasoningBlock;
                expander.Content = reasoningBorder;
                messageContainer.Children.Add(expander);
            }

            // 添加实际回复内容
            if (!string.IsNullOrEmpty(content))
            {
                var contentBubble = CreateMessageBubble(content, false);
                contentBubble.Margin = new Thickness(0);
                messageContainer.Children.Add(contentBubble);
            }

            DialogMessagesPanel.Children.Add(messageContainer);
            DialogScrollViewer.ScrollToEnd();
        }

        public void ClearMessages()
        {
            DialogMessagesPanel.Children.Clear();
        }

        public void ApplyTheme(bool isDarkTheme)
        {
            if (isDarkTheme)
            {
                ApplyDarkTheme();
            }
            else
            {
                ApplyLightTheme();
            }
        }

        private void ApplyDarkTheme()
        {
            // 主界面背景
            DialogInterface.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 44, 52));
            
            // 标题栏背景和文字
            var titleElements = LogicalTreeHelper.GetChildren(DialogInterface);
            foreach (var element in titleElements)
            {
                if (element is Grid titleGrid)
                {
                    var titleBorder = titleGrid.Children.OfType<Border>().FirstOrDefault();
                    if (titleBorder != null)
                    {
                        titleBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 54, 62));
                        
                        // 更新标题栏内的所有文字颜色
                        UpdateTextElementsColor(titleBorder, System.Windows.Media.Brushes.White);
                    }
                }
            }
            
            // 侧边栏
            SidebarPanel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 39, 47));
            SidebarPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 64, 72));
            
            // 侧边栏标题文字
            UpdateTextElementsColor(SidebarPanel, System.Windows.Media.Brushes.White);
            
            // 输入框文字颜色
            DialogInput.Foreground = System.Windows.Media.Brushes.White;
            
            // 发送按钮样式调整
            SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
            SendButton.Foreground = System.Windows.Media.Brushes.White;
            
            // 更新对话消息的颜色
            UpdateMessageColors(true);
        }

        private void ApplyLightTheme()
        {
            // 主界面背景
            DialogInterface.Background = System.Windows.Media.Brushes.White;
            
            // 标题栏背景和文字
            var titleElements = LogicalTreeHelper.GetChildren(DialogInterface);
            foreach (var element in titleElements)
            {
                if (element is Grid titleGrid)
                {
                    var titleBorder = titleGrid.Children.OfType<Border>().FirstOrDefault();
                    if (titleBorder != null)
                    {
                        titleBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
                        
                        // 更新标题栏内的所有文字颜色
                        UpdateTextElementsColor(titleBorder, System.Windows.Media.Brushes.Black);
                    }
                }
            }
            
            // 侧边栏
            SidebarPanel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 251, 252));
            SidebarPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 228, 232));
            
            // 侧边栏标题文字
            UpdateTextElementsColor(SidebarPanel, System.Windows.Media.Brushes.Black);
            
            // 输入框文字颜色
            DialogInput.Foreground = System.Windows.Media.Brushes.Black;
            
            // 发送按钮样式调整
            SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
            SendButton.Foreground = System.Windows.Media.Brushes.White;
            
            // 更新对话消息的颜色
            UpdateMessageColors(false);
        }

        private void UpdateTextElementsColor(DependencyObject parent, System.Windows.Media.Brush color)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is TextBlock textBlock)
                {
                    textBlock.Foreground = color;
                }
                
                UpdateTextElementsColor(child, color);
            }
        }

        private void UpdateMessageColors(bool isDarkTheme)
        {
            foreach (UIElement child in DialogMessagesPanel.Children)
            {
                if (child is TextBlock messageBlock)
                {
                    if (isDarkTheme)
                    {
                        messageBlock.Foreground = System.Windows.Media.Brushes.White;
                        // 调整消息背景颜色以适应深色主题
                        var currentBackground = messageBlock.Background as System.Windows.Media.SolidColorBrush;
                        if (currentBackground != null)
                        {
                            if (currentBackground.Color == System.Windows.Media.Colors.LightBlue)
                            {
                                messageBlock.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 130, 180));
                            }
                            else if (currentBackground.Color == System.Windows.Media.Colors.LightGray)
                            {
                                messageBlock.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80));
                            }
                        }
                    }
                    else
                    {
                        messageBlock.Foreground = System.Windows.Media.Brushes.Black;
                        // 恢复浅色主题的消息背景颜色
                        var currentBackground = messageBlock.Background as System.Windows.Media.SolidColorBrush;
                        if (currentBackground != null)
                        {
                            if (currentBackground.Color == System.Windows.Media.Color.FromRgb(70, 130, 180))
                            {
                                messageBlock.Background = System.Windows.Media.Brushes.LightBlue;
                            }
                            else if (currentBackground.Color == System.Windows.Media.Color.FromRgb(80, 80, 80))
                            {
                                messageBlock.Background = System.Windows.Media.Brushes.LightGray;
                            }
                        }
                    }
                }
            }
        }

        private Border? currentStreamingBubble;
        private TextBlock? currentStreamingTextBlock;
        private StringBuilder streamingContent = new StringBuilder();
        private StringBuilder streamingReasoning = new StringBuilder();

        public async Task SendMessageToApi(string message, OpenApiConfiguration apiConfig)
        {
            AddMessageBubble(message, true);

            try
            {
                // 创建新的取消令牌
                currentRequest = new CancellationTokenSource();
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
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

                if (apiConfig.IsStreamingEnabled)
                {
                    await ProcessStreamingResponse(httpClient, apiConfig.ApiUrl, content);
                }
                else
                {
                    var response = await httpClient.PostAsync(apiConfig.ApiUrl, content, currentRequest?.Token ?? CancellationToken.None);
                    var responseText = await response.Content.ReadAsStringAsync();
                    
                    currentRequest?.Token.ThrowIfCancellationRequested();

                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(responseText);
                            var choices = jsonDoc.RootElement.GetProperty("choices");
                            if (choices.GetArrayLength() > 0)
                            {
                                var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
                                AddMessageBubble(messageContent ?? "无响应内容", false);
                            }
                            else
                            {
                                AddMessageBubble("API响应格式错误", false);
                            }
                        }
                        catch (JsonException)
                        {
                            AddMessageBubble($"API返回了无效的JSON格式: {responseText}", false);
                        }
                    }
                    else
                    {
                        AddMessageBubble($"API请求失败: {response.StatusCode}", false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，不显示错误消息
            }
            catch (Exception ex)
            {
                AddMessageBubble($"请求错误: {ex.Message}", false);
            }
            finally
            {
                // 恢复发送状态
                SetSendingState(false);
            }
        }

        private async Task ProcessStreamingResponse(HttpClient httpClient, string apiUrl, StringContent requestContent)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = requestContent
                };

                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, currentRequest?.Token ?? CancellationToken.None);
                
                if (!response.IsSuccessStatusCode)
                {
                    AddMessageBubble($"API请求失败: {response.StatusCode}", false);
                    return;
                }

                // 初始化流式消息显示
                InitializeStreamingMessage();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);
                
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    currentRequest?.Token.ThrowIfCancellationRequested();
                    
                    if (line.StartsWith("data:"))
                    {
                        var jsonData = line.Substring(5).Trim();
                        
                        if (jsonData == "[DONE]")
                        {
                            break;
                        }
                        
                        if (!string.IsNullOrEmpty(jsonData))
                        {
                            try
                            {
                                var jsonDoc = JsonDocument.Parse(jsonData);
                                var choices = jsonDoc.RootElement.GetProperty("choices");
                                
                                if (choices.GetArrayLength() > 0)
                                {
                                    var choice = choices[0];
                                    if (choice.TryGetProperty("delta", out var delta))
                                    {
                                        // 处理思维内容
                                        if (delta.TryGetProperty("reasoning_content", out var reasoningProp))
                                        {
                                            var reasoning = reasoningProp.GetString();
                                            if (!string.IsNullOrEmpty(reasoning))
                                            {
                                                streamingReasoning.Append(reasoning);
                                            }
                                        }
                                        
                                        // 处理实际内容
                                        if (delta.TryGetProperty("content", out var contentProp))
                                        {
                                            var messageText = contentProp.GetString();
                                            if (!string.IsNullOrEmpty(messageText))
                                            {
                                                streamingContent.Append(messageText);
                                                // 实时更新UI
                                                await Dispatcher.InvokeAsync(() => UpdateStreamingMessage());
                                            }
                                        }
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                // 忽略无效的JSON行
                                continue;
                            }
                        }
                    }
                }
                
                // 完成流式输出
                await Dispatcher.InvokeAsync(() => FinalizeStreamingMessage());
            }
            catch (OperationCanceledException)
            {
                // 被取消，清理当前流式消息
                await Dispatcher.InvokeAsync(() => FinalizeStreamingMessage());
            }
            catch (Exception ex)
            {
                AddMessageBubble($"流式处理错误: {ex.Message}", false);
            }
        }

        private void InitializeStreamingMessage()
        {
            streamingContent.Clear();
            streamingReasoning.Clear();
            
            // 创建空的消息气泡，准备接收流式内容
            currentStreamingBubble = CreateMessageBubble("", false);
            currentStreamingTextBlock = (currentStreamingBubble.Child as TextBlock);
            DialogMessagesPanel.Children.Add(currentStreamingBubble);
            DialogScrollViewer.ScrollToEnd();
        }

        private void UpdateStreamingMessage()
        {
            if (currentStreamingTextBlock != null)
            {
                currentStreamingTextBlock.Text = streamingContent.ToString();
                DialogScrollViewer.ScrollToEnd();
            }
        }

        private void FinalizeStreamingMessage()
        {
            if (currentStreamingBubble != null)
            {
                var finalContent = streamingContent.ToString().Trim();
                var finalReasoning = streamingReasoning.ToString().Trim();
                
                // 移除临时的流式消息
                DialogMessagesPanel.Children.Remove(currentStreamingBubble);
                
                // 添加最终格式化的消息
                if (!string.IsNullOrEmpty(finalContent) || !string.IsNullOrEmpty(finalReasoning))
                {
                    AddMessageBubbleWithReasoning(finalContent, finalReasoning);
                }
                else
                {
                    AddMessageBubble("AI没有返回有效内容", false);
                }
                
                currentStreamingBubble = null;
                currentStreamingTextBlock = null;
            }
        }
    }
}