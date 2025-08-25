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

        public FloatingWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeCards();
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

        protected override void OnClosed(EventArgs e)
        {
            trayIcon?.Dispose();
            base.OnClosed(e);
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
            
            if (leftButton == null || rightButton == null)
                return;
                
            // 直接更新卡片内容，不使用位移动画
            currentCardIndex = newIndex;
            isFlipped = false;
            UpdateCardDisplay();
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
            
            // 更新卡片指示器
            UpdateCardIndicator();
        }
        
        private void UpdateCardIndicator()
        {
            var indicator = FindName("CardIndicator") as StackPanel;
            if (indicator == null)
                return;
                
            indicator.Children.Clear();
            
            for (int i = 0; i < cards.Count; i++)
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Margin = new Thickness(2),
                    Fill = i == currentCardIndex ? System.Windows.Media.Brushes.White : new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255))
                };
                indicator.Children.Add(dot);
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
            var enableAnimationCheckBox = FindName("EnableAnimationCheckBox") as System.Windows.Controls.CheckBox;
            
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
            
            if (enableAnimationCheckBox != null)
            {
                enableAnimationCheckBox.IsChecked = true;
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
    }
}