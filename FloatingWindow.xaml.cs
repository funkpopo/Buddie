using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Buddie
{
    public class CardData
    {
        public string FrontText { get; set; } = "";
        public string FrontSubText { get; set; } = "";
        public string BackText { get; set; } = "";
        public string BackSubText { get; set; } = "";
        public System.Windows.Media.Brush FrontBackground { get; set; } = System.Windows.Media.Brushes.LightBlue;
        public System.Windows.Media.Brush BackBackground { get; set; } = System.Windows.Media.Brushes.LightCoral;
    }

    public partial class FloatingWindow : Window
    {
        private NotifyIcon? trayIcon;
        private List<CardData> cards = new List<CardData>();
        private int currentCardIndex = 0;
        private AppSettings appSettings = new AppSettings();
        private readonly HttpClient httpClient = new HttpClient();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> testCancellationTokens = new();

        public FloatingWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeCards();
            InitializeApiConfigurations();
            UpdateCardDisplay();
        }

        private void InitializeCards()
        {
            cards.Add(new CardData
            {
                FrontText = "卡片 1",
                FrontSubText = "这是第一张卡片",
                BackText = "卡片 1 背面",
                BackSubText = "第一张卡片的背面",
                FrontBackground = new LinearGradientBrush(
                    Colors.LightBlue, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                ),
                BackBackground = new LinearGradientBrush(
                    Colors.LightCoral, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                )
            });
            
            cards.Add(new CardData
            {
                FrontText = "卡片 2",
                FrontSubText = "这是第二张卡片",
                BackText = "卡片 2 背面",
                BackSubText = "第二张卡片的背面",
                FrontBackground = new LinearGradientBrush(
                    Colors.LightGreen, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                ),
                BackBackground = new LinearGradientBrush(
                    Colors.Orange, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                )
            });
            
            cards.Add(new CardData
            {
                FrontText = "卡片 3",
                FrontSubText = "这是第三张卡片",
                BackText = "卡片 3 背面",
                BackSubText = "第三张卡片的背面",
                FrontBackground = new LinearGradientBrush(
                    Colors.Plum, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                ),
                BackBackground = new LinearGradientBrush(
                    Colors.Gold, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                )
            });
            
            cards.Add(new CardData
            {
                FrontText = "卡片 4",
                FrontSubText = "这是第四张卡片",
                BackText = "卡片 4 背面",
                BackSubText = "第四张卡片的背面",
                FrontBackground = new LinearGradientBrush(
                    Colors.LightSalmon, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                ),
                BackBackground = new LinearGradientBrush(
                    Colors.LightSeaGreen, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                )
            });
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Text = "Buddie";
            trayIcon.Visible = true;

            // 创建右键菜单
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("显示", null, ShowWindow_Click);
            contextMenu.Items.Add("隐藏", null, HideWindow_Click);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("退出", null, ExitApplication_Click);
            
            trayIcon.ContextMenuStrip = contextMenu;
            
            // 双击托盘图标显示窗口
            trayIcon.DoubleClick += (sender, e) => {
                ShowWindow();
            };
        }

        private void ShowWindow_Click(object? sender, EventArgs e)
        {
            ShowWindow();
        }

        private void HideWindow_Click(object? sender, EventArgs e)
        {
            HideWindow();
        }

        private void ExitApplication_Click(object? sender, EventArgs e)
        {
            trayIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void HideWindow()
        {
            this.Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // 阻止窗口关闭，改为隐藏到托盘
            e.Cancel = true;
            HideWindow();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            HideWindow();
        }

        private bool isFlipped = false;
        private bool isAnimating = false;

        private void LeftFlipButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (isFlipped)
            {
                FlipCard();
            }
            else
            {
                SwitchToPreviousCard();
            }
        }

        private void RightFlipButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (isFlipped)
            {
                FlipCard();
            }
            else
            {
                SwitchToNextCard();
            }
        }

        private void SwitchToNextCard()
        {
            if (isAnimating || cards.Count <= 1)
                return;
                
            int nextIndex = (currentCardIndex + 1) % cards.Count;
            SwitchToCard(nextIndex, true);
        }

        private void SwitchToPreviousCard()
        {
            if (isAnimating || cards.Count <= 1)
                return;
                
            int prevIndex = (currentCardIndex - 1 + cards.Count) % cards.Count;
            SwitchToCard(prevIndex, false);
        }

        private void SwitchToCard(int newIndex, bool isNext)
        {
            if (newIndex == currentCardIndex || isAnimating)
                return;
                
            var leftButton = FindName("LeftFlipButton") as System.Windows.Controls.Button;
            var rightButton = FindName("RightFlipButton") as System.Windows.Controls.Button;
            var scaleTransform = FindName("CardScaleTransform") as ScaleTransform;
            
            if (leftButton == null || rightButton == null || scaleTransform == null)
                return;
                
            // 设置动画状态并禁用按钮
            isAnimating = true;
            leftButton.IsEnabled = false;
            rightButton.IsEnabled = false;
            
            // 创建翻转动画 - 使用与FlipCard相同的效果
            var scaleDownAnimation = new DoubleAnimation();
            scaleDownAnimation.From = 1.0;
            scaleDownAnimation.To = 0.0;
            scaleDownAnimation.Duration = TimeSpan.FromMilliseconds(200);
            scaleDownAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };

            var scaleUpAnimation = new DoubleAnimation();
            scaleUpAnimation.From = 0.0;
            scaleUpAnimation.To = 1.0;
            scaleUpAnimation.Duration = TimeSpan.FromMilliseconds(200);
            scaleUpAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 第一阶段：横向缩小到0
            scaleDownAnimation.Completed += (s, e) =>
            {
                // 在中间点切换卡片
                currentCardIndex = newIndex;
                isFlipped = false;
                UpdateCardDisplay();
                
                // 开始第二阶段：从0放大到1
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpAnimation);
            };

            // 动画完成后更新状态并重新启用按钮
            scaleUpAnimation.Completed += (s, e) =>
            {
                isAnimating = false;
                leftButton.IsEnabled = true;
                rightButton.IsEnabled = true;
            };

            // 开始第一阶段动画
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDownAnimation);
        }
        
        private void UpdateCardDisplay()
        {
            if (currentCardIndex < 0 || currentCardIndex >= cards.Count)
                return;
                
            var card = cards[currentCardIndex];
            var cardFront = FindName("CardFront") as Border;
            var cardBack = FindName("CardBack") as Border;
            var cardInfo = FindName("CardInfo") as TextBlock;
            
            if (cardFront != null && cardBack != null)
            {
                // 更新正面
                var frontPanel = cardFront.Child as StackPanel;
                if (frontPanel?.Children.Count >= 2)
                {
                    if (frontPanel.Children[0] is TextBlock frontTitle)
                        frontTitle.Text = card.FrontText;
                    if (frontPanel.Children[1] is TextBlock frontSub)
                        frontSub.Text = card.FrontSubText;
                }
                cardFront.Background = card.FrontBackground;
                
                // 更新背面
                var backPanel = cardBack.Child as StackPanel;
                if (backPanel?.Children.Count >= 2)
                {
                    if (backPanel.Children[0] is TextBlock backTitle)
                        backTitle.Text = card.BackText;
                    if (backPanel.Children[1] is TextBlock backSub)
                        backSub.Text = card.BackSubText;
                }
                cardBack.Background = card.BackBackground;
                
                // 显示正面，隐藏背面
                cardFront.Visibility = Visibility.Visible;
                cardBack.Visibility = Visibility.Collapsed;
            }
            
            // 更新卡片信息
            if (cardInfo != null)
            {
                cardInfo.Text = $"卡片 {currentCardIndex + 1} / {cards.Count}";
            }
        }
        
        private void FlipCard()
        {
            // 如果正在动画中，忽略点击
            if (isAnimating)
                return;

            var cardFront = FindName("CardFront") as Border;
            var cardBack = FindName("CardBack") as Border;
            var cardContainer = FindName("CardContainer") as Border;
            var leftButton = FindName("LeftFlipButton") as System.Windows.Controls.Button;
            var rightButton = FindName("RightFlipButton") as System.Windows.Controls.Button;
            var scaleTransform = FindName("CardScaleTransform") as ScaleTransform;

            if (cardFront == null || cardBack == null || cardContainer == null || 
                leftButton == null || rightButton == null || scaleTransform == null)
                return;

            // 设置动画状态，禁用按钮
            isAnimating = true;
            leftButton.IsEnabled = false;
            rightButton.IsEnabled = false;

            // 保存当前状态，避免在动画过程中状态混乱
            bool currentlyShowingFront = !isFlipped;

            // 创建翻转动画 - 分两个阶段（模拟3D翻转效果）
            var scaleDownAnimation = new DoubleAnimation();
            scaleDownAnimation.From = 1.0;
            scaleDownAnimation.To = 0.0;
            scaleDownAnimation.Duration = TimeSpan.FromMilliseconds(250);
            scaleDownAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };

            var scaleUpAnimation = new DoubleAnimation();
            scaleUpAnimation.From = 0.0;
            scaleUpAnimation.To = 1.0;
            scaleUpAnimation.Duration = TimeSpan.FromMilliseconds(250);
            scaleUpAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 第一阶段：横向缩小到0
            scaleDownAnimation.Completed += (s, e) =>
            {
                // 在中间点切换卡片面
                if (currentlyShowingFront)
                {
                    cardFront.Visibility = Visibility.Collapsed;
                    cardBack.Visibility = Visibility.Visible;
                }
                else
                {
                    cardBack.Visibility = Visibility.Collapsed;
                    cardFront.Visibility = Visibility.Visible;
                }
                
                // 开始第二阶段：从0放大到1
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpAnimation);
            };

            // 动画完成后更新状态并重新启用按钮
            scaleUpAnimation.Completed += (s, e) =>
            {
                isFlipped = !isFlipped;
                isAnimating = false;
                leftButton.IsEnabled = true;
                rightButton.IsEnabled = true;
            };

            // 开始第一阶段动画
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDownAnimation);
        }

        // 卡片悬停事件处理
        private void CardContainer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var hoverButtons = FindName("HoverButtons") as StackPanel;
            if (hoverButtons != null)
            {
                hoverButtons.Visibility = Visibility.Visible;
            }
        }

        private void CardContainer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var hoverButtons = FindName("HoverButtons") as StackPanel;
            if (hoverButtons != null)
            {
                hoverButtons.Visibility = Visibility.Collapsed;
            }
        }

        // 对话按钮点击事件
        private void DialogButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialogInterface = FindName("DialogInterface") as Border;
            var settingsInterface = FindName("SettingsInterface") as Border;
            
            if (dialogInterface != null && settingsInterface != null)
            {
                // 如果对话界面已经显示，则关闭它
                if (dialogInterface.Visibility == Visibility.Visible)
                {
                    dialogInterface.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 隐藏设置界面，显示对话界面
                    settingsInterface.Visibility = Visibility.Collapsed;
                    dialogInterface.Visibility = Visibility.Visible;
                    
                    // 聚焦到输入框
                    var dialogInput = FindName("DialogInput") as System.Windows.Controls.TextBox;
                    dialogInput?.Focus();
                }
            }
        }

        // 设置按钮点击事件
        private void SettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialogInterface = FindName("DialogInterface") as Border;
            var settingsInterface = FindName("SettingsInterface") as Border;
            
            if (dialogInterface != null && settingsInterface != null)
            {
                // 如果设置界面已经显示，则关闭它
                if (settingsInterface.Visibility == Visibility.Visible)
                {
                    settingsInterface.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 隐藏对话界面，显示设置界面
                    dialogInterface.Visibility = Visibility.Collapsed;
                    settingsInterface.Visibility = Visibility.Visible;
                }
            }
        }

        // 关闭对话界面
        private void CloseDialog_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialogInterface = FindName("DialogInterface") as Border;
            if (dialogInterface != null)
            {
                dialogInterface.Visibility = Visibility.Collapsed;
            }
        }

        // 关闭设置界面
        private void CloseSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var settingsInterface = FindName("SettingsInterface") as Border;
            if (settingsInterface != null)
            {
                settingsInterface.Visibility = Visibility.Collapsed;
            }
        }

        // 发送消息
        private void SendMessage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialogInput = FindName("DialogInput") as System.Windows.Controls.TextBox;
            var dialogContent = FindName("DialogContent") as TextBlock;
            
            if (dialogInput != null && dialogContent != null && !string.IsNullOrWhiteSpace(dialogInput.Text))
            {
                string message = dialogInput.Text.Trim();
                string currentTime = DateTime.Now.ToString("HH:mm:ss");
                
                // 添加用户消息到对话内容
                dialogContent.Text += $"\n[{currentTime}] 您: {message}";
                
                // 模拟回复
                dialogContent.Text += $"\n[{currentTime}] 系统: 收到您的消息: {message}";
                
                // 清空输入框
                dialogInput.Text = "";
                
                // 滚动到底部
                var scrollViewer = dialogContent.Parent as ScrollViewer;
                scrollViewer?.ScrollToEnd();
            }
        }

        // 置顶设置更改
        private void TopMostCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            var checkBox = sender as System.Windows.Controls.CheckBox;
            if (checkBox != null)
            {
                this.Topmost = checkBox.IsChecked ?? true;
            }
        }

        // 任务栏显示设置更改
        private void ShowInTaskbarCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            var checkBox = sender as System.Windows.Controls.CheckBox;
            if (checkBox != null)
            {
                this.ShowInTaskbar = checkBox.IsChecked ?? true;
            }
        }

        // 重置设置
        private void ResetSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var topMostCheckBox = FindName("TopMostCheckBox") as System.Windows.Controls.CheckBox;
            var showInTaskbarCheckBox = FindName("ShowInTaskbarCheckBox") as System.Windows.Controls.CheckBox;
            
            if (topMostCheckBox != null)
            {
                topMostCheckBox.IsChecked = true;
                this.Topmost = true;
            }
            
            if (showInTaskbarCheckBox != null)
            {
                showInTaskbarCheckBox.IsChecked = true;
                this.ShowInTaskbar = true;
            }
            
            // 显示重置完成的提示
            var dialogContent = FindName("DialogContent") as TextBlock;
            if (dialogContent != null)
            {
                string currentTime = DateTime.Now.ToString("HH:mm:ss");
                dialogContent.Text += $"\n[{currentTime}] 系统: 设置已重置为默认值。";
            }
        }

        // 添加新卡片
        private void AddButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // 创建一个新的卡片数据
            int newCardNumber = cards.Count + 1;
            var newCard = new CardData
            {
                FrontText = $"新卡片 {newCardNumber}",
                FrontSubText = $"这是第 {newCardNumber} 张卡片",
                BackText = $"新卡片 {newCardNumber} 背面",
                BackSubText = $"第 {newCardNumber} 张卡片的背面",
                FrontBackground = new LinearGradientBrush(
                    GetRandomColor(), Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                ),
                BackBackground = new LinearGradientBrush(
                    GetRandomColor(), Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                )
            };
            
            // 添加到卡片列表
            cards.Add(newCard);
            
            // 切换到新添加的卡片
            currentCardIndex = cards.Count - 1;
            isFlipped = false;
            UpdateCardDisplay();
            
            // 在对话框中显示添加成功的消息
            var dialogContent = FindName("DialogContent") as TextBlock;
            if (dialogContent != null)
            {
                string currentTime = DateTime.Now.ToString("HH:mm:ss");
                dialogContent.Text += $"\n[{currentTime}] 系统: 已添加新卡片 '{newCard.FrontText}'。";
                
                // 滚动到底部
                var scrollViewer = dialogContent.Parent as ScrollViewer;
                scrollViewer?.ScrollToEnd();
            }
        }
        
        // 获取随机颜色的辅助方法
        private System.Windows.Media.Color GetRandomColor()
        {
            var random = new Random();
            var colors = new[] { 
                Colors.LightBlue, Colors.LightGreen, Colors.LightCoral, 
                Colors.Plum, Colors.LightSalmon, Colors.LightSeaGreen,
                Colors.Orange, Colors.Gold, Colors.LightPink, Colors.LightCyan
            };
            return colors[random.Next(colors.Length)];
        }

        private void InitializeApiConfigurations()
        {
            var apiConfigList = FindName("ApiConfigList") as ItemsControl;
            var noConfigMessage = FindName("NoConfigMessage") as Border;
            var ttsConfigList = FindName("TtsConfigList") as ItemsControl;
            var noTtsConfigMessage = FindName("NoTtsConfigMessage") as Border;
            
            if (apiConfigList != null)
            {
                apiConfigList.ItemsSource = appSettings.ApiConfigurations;
                UpdateNoConfigMessageVisibility();
            }

            if (ttsConfigList != null)
            {
                ttsConfigList.ItemsSource = appSettings.TtsConfigurations;
                UpdateNoTtsConfigMessageVisibility();
            }
            
            // 绑定整个设置到设置界面
            var settingsInterface = FindName("SettingsInterface") as Border;
            if (settingsInterface != null)
            {
                settingsInterface.DataContext = appSettings;
            }
        }

        private void UpdateNoTtsConfigMessageVisibility()
        {
            var noTtsConfigMessage = FindName("NoTtsConfigMessage") as Border;
            if (noTtsConfigMessage != null)
            {
                noTtsConfigMessage.Visibility = appSettings.TtsConfigurations.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void UpdateNoConfigMessageVisibility()
        {
            var noConfigMessage = FindName("NoConfigMessage") as Border;
            if (noConfigMessage != null)
            {
                noConfigMessage.Visibility = appSettings.ApiConfigurations.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        private void AddApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var newConfig = new OpenApiConfiguration
            {
                Name = $"配置 {appSettings.ApiConfigurations.Count + 1}",
                ApiUrl = "https://api.openai.com/v1/chat/completions",
                ModelName = "gpt-3.5-turbo",
                IsStreamingEnabled = true,
                IsMultimodalEnabled = false,
                IsEditMode = true,
                IsSaved = false
            };
            
            appSettings.ApiConfigurations.Add(newConfig);
            UpdateNoConfigMessageVisibility();
        }

        private void SaveApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                // 验证必填字段
                if (string.IsNullOrWhiteSpace(config.Name) || 
                    string.IsNullOrWhiteSpace(config.ApiUrl) || 
                    string.IsNullOrWhiteSpace(config.ModelName))
                {
                    System.Windows.MessageBox.Show(
                        "请填写完整的配置信息（配置名称、API URL、模型名称为必填项）", 
                        "验证错误", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                    return;
                }

                config.IsEditMode = false;
                config.IsSaved = true;
                
                // 显示成功消息
                var dialogContent = FindName("DialogContent") as TextBlock;
                if (dialogContent != null)
                {
                    string currentTime = DateTime.Now.ToString("HH:mm:ss");
                    dialogContent.Text += $"\n[{currentTime}] 系统: API配置 \"{config.Name}\" 已保存。";
                    
                    var scrollViewer = dialogContent.Parent as ScrollViewer;
                    scrollViewer?.ScrollToEnd();
                }
            }
        }

        private void CancelApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                if (!config.IsSaved)
                {
                    // 新配置，直接删除
                    appSettings.ApiConfigurations.Remove(config);
                    UpdateNoConfigMessageVisibility();
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
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                config.IsEditMode = true;
            }
        }

        private void RemoveApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                var result = System.Windows.MessageBox.Show(
                    $"确定要删除配置 \"{config.Name}\" 吗？", 
                    "确认删除", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    appSettings.ApiConfigurations.Remove(config);
                    UpdateNoConfigMessageVisibility();
                }
            }
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is OpenApiConfiguration config)
            {
                config.ApiKey = passwordBox.Password;
            }
        }

        private void TtsApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox?.DataContext is OpenAiTtsConfiguration config)
            {
                config.ApiKey = passwordBox.Password;
            }
        }

        private void AddTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var newConfig = new OpenAiTtsConfiguration
            {
                Name = $"TTS配置 {appSettings.TtsConfigurations.Count + 1}",
                ApiUrl = "http://localhost:5050/v1/audio/speech",
                Model = "tts-1",
                Voice = "alloy",
                Speed = 1.0,
                IsEditMode = true,
                IsSaved = false
            };
            
            appSettings.TtsConfigurations.Add(newConfig);
            UpdateNoTtsConfigMessageVisibility();
        }

        private void SaveTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenAiTtsConfiguration config)
            {
                // 验证必填字段
                if (string.IsNullOrWhiteSpace(config.Name) || 
                    string.IsNullOrWhiteSpace(config.ApiUrl) || 
                    string.IsNullOrWhiteSpace(config.Model) ||
                    string.IsNullOrWhiteSpace(config.Voice))
                {
                    System.Windows.MessageBox.Show(
                        "请填写完整的配置信息（配置名称、API URL、模型、语音为必填项）", 
                        "验证错误", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                    return;
                }

                config.IsEditMode = false;
                config.IsSaved = true;
                
                // 显示成功消息
                var dialogContent = FindName("DialogContent") as TextBlock;
                if (dialogContent != null)
                {
                    string currentTime = DateTime.Now.ToString("HH:mm:ss");
                    dialogContent.Text += $"\n[{currentTime}] 系统: TTS配置 \"{config.Name}\" 已保存。";
                    
                    var scrollViewer = dialogContent.Parent as ScrollViewer;
                    scrollViewer?.ScrollToEnd();
                }
            }
        }

        private void CancelTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenAiTtsConfiguration config)
            {
                if (!config.IsSaved)
                {
                    // 新配置，直接删除
                    appSettings.TtsConfigurations.Remove(config);
                    UpdateNoTtsConfigMessageVisibility();
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
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenAiTtsConfiguration config)
            {
                config.IsEditMode = true;
            }
        }

        private void RemoveTtsConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenAiTtsConfiguration config)
            {
                var result = System.Windows.MessageBox.Show(
                    $"确定要删除TTS配置 \"{config.Name}\" 吗？", 
                    "确认删除", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    appSettings.TtsConfigurations.Remove(config);
                    UpdateNoTtsConfigMessageVisibility();
                }
            }
        }

        private void TtsEditSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // 这个方法主要用于实时更新显示的速度值，数据绑定会自动处理实际值的更新
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var speedValueText = FindName("SpeedValueText") as TextBlock;
            if (speedValueText != null)
            {
                speedValueText.Text = $"{e.NewValue:F1}x";
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var volumeValueText = FindName("VolumeValueText") as TextBlock;
            if (volumeValueText != null)
            {
                volumeValueText.Text = $"{e.NewValue:F0}%";
            }
        }

        private void TestTts_Click(object sender, RoutedEventArgs e)
        {
            var testTextBox = FindName("TestTextBox") as System.Windows.Controls.TextBox;
            var dialogContent = FindName("DialogContent") as TextBlock;
            
            if (testTextBox != null && dialogContent != null)
            {
                string testText = testTextBox.Text;
                string currentTime = DateTime.Now.ToString("HH:mm:ss");
                
                if (appSettings.TtsConfigurations.Count > 0)
                {
                    var firstTtsConfig = appSettings.TtsConfigurations.First();
                    dialogContent.Text += $"\n[{currentTime}] OpenAI TTS测试: 使用配置 \"{firstTtsConfig.Name}\" (模型: {firstTtsConfig.Model}，语音: {firstTtsConfig.Voice})，播放文本 \"{testText}\"";
                }
                else
                {
                    dialogContent.Text += $"\n[{currentTime}] TTS测试: 暂无可用的TTS配置，请先添加配置。播放文本 \"{testText}\"";
                }
                
                var scrollViewer = dialogContent.Parent as ScrollViewer;
                scrollViewer?.ScrollToEnd();
            }
        }

        private async void TestApiConfig_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is OpenApiConfiguration config)
            {
                await TestApiConfigurationAsync(config);
            }
        }

        private async void TestAllApiConfigs_Click(object sender, RoutedEventArgs e)
        {
            var tasks = new List<Task>();
            foreach (var config in appSettings.ApiConfigurations)
            {
                tasks.Add(TestApiConfigurationAsync(config));
            }
            await Task.WhenAll(tasks);
        }

        private async Task TestApiConfigurationAsync(OpenApiConfiguration config)
        {
            if (config == null)
            {
                return;
            }
            
            if (string.IsNullOrWhiteSpace(config.ApiUrl) || 
                string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.ModelName))
            {
                config.TestStatus = TestStatus.Failed;
                config.TestMessage = "配置信息不完整";
                return;
            }

            string configId = $"{config.Name}_{DateTime.Now.Ticks}";
            
            // 如果已经有测试在进行，先取消
            if (testCancellationTokens.TryGetValue(configId, out var existingCts))
            {
                existingCts.Cancel();
                testCancellationTokens.TryRemove(configId, out _);
            }

            var cts = new CancellationTokenSource();
            testCancellationTokens.TryAdd(configId, cts);

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

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (responseObj.TryGetProperty("choices", out var choices) && 
                        choices.GetArrayLength() > 0)
                    {
                        config.TestStatus = TestStatus.Success;
                        config.TestMessage = "连接成功";
                        LogTestResult(config, "API测试成功", true);
                    }
                    else
                    {
                        config.TestStatus = TestStatus.Failed;
                        config.TestMessage = "响应格式异常";
                        LogTestResult(config, "API响应格式异常", false);
                    }
                }
                else
                {
                    config.TestStatus = TestStatus.Failed;
                    config.TestMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    LogTestResult(config, $"API测试失败: {response.StatusCode}", false);
                }
            }
            catch (OperationCanceledException)
            {
                config.TestStatus = TestStatus.Failed;
                config.TestMessage = "测试被取消";
            }
            catch (HttpRequestException ex)
            {
                config.TestStatus = TestStatus.Failed;
                config.TestMessage = $"网络错误: {ex.Message}";
                LogTestResult(config, $"网络连接失败: {ex.Message}", false);
            }
            catch (Exception ex)
            {
                config.TestStatus = TestStatus.Failed;
                config.TestMessage = $"错误: {ex.Message}";
                LogTestResult(config, $"测试异常: {ex.Message}", false);
            }
            finally
            {
                testCancellationTokens.TryRemove(configId, out _);
                cts.Dispose();
            }
        }

        private void LogTestResult(OpenApiConfiguration config, string message, bool success)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogContent = FindName("DialogContent") as TextBlock;
                if (dialogContent != null)
                {
                    string currentTime = DateTime.Now.ToString("HH:mm:ss");
                    string status = success ? "✓" : "✗";
                    dialogContent.Text += $"\n[{currentTime}] {status} {config.Name}: {message}";
                    
                    var scrollViewer = dialogContent.Parent as ScrollViewer;
                    scrollViewer?.ScrollToEnd();
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            // 取消所有正在进行的测试
            foreach (var cts in testCancellationTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            testCancellationTokens.Clear();
            
            httpClient?.Dispose();
            trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}