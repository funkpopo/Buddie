using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Input;

namespace Buddie.Controls
{
    public partial class CardControl : UserControl
    {
        public event EventHandler? DialogButtonClicked;
        public event EventHandler? SettingsButtonClicked;
        public event EventHandler? AddButtonClicked;
        public event EventHandler? LeftFlipButtonClicked;
        public event EventHandler? RightFlipButtonClicked;
        
        public event EventHandler? DialogRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler? MouseEntered;
        public event EventHandler? MouseLeft;

        // 界面状态跟踪
        private bool _isDialogOpen = false;
        private bool _isSettingsOpen = false;

        public CardControl()
        {
            InitializeComponent();
        }

        private void CardContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.DragMove();
            }
        }

        private void CardContainer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HoverButtons.Visibility = Visibility.Visible;
            MouseEntered?.Invoke(this, EventArgs.Empty);
        }

        private void CardContainer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HoverButtons.Visibility = Visibility.Collapsed;
            MouseLeft?.Invoke(this, EventArgs.Empty);
        }

        private void DialogButton_Click(object sender, RoutedEventArgs e)
        {
            DialogButtonClicked?.Invoke(this, EventArgs.Empty);
            DialogRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsButtonClicked?.Invoke(this, EventArgs.Empty);
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void LeftFlipButton_Click(object sender, RoutedEventArgs e)
        {
            LeftFlipButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void RightFlipButton_Click(object sender, RoutedEventArgs e)
        {
            RightFlipButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        /// &lt;summary&gt;
        /// 翻转卡片，切换正面和背面显示
        /// &lt;/summary&gt;
        public void FlipCard()
        {
            var isFlipped = CardBack.Visibility == Visibility.Visible;

            var rotationAnimation = new DoubleAnimation
            {
                From = 0,
                To = 180,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            rotationAnimation.Completed += (s, e) =>
            {
                if (isFlipped)
                {
                    CardBack.Visibility = Visibility.Collapsed;
                    CardFront.Visibility = Visibility.Visible;
                }
                else
                {
                    CardFront.Visibility = Visibility.Collapsed;
                    CardBack.Visibility = Visibility.Visible;
                }

                var backRotationAnimation = new DoubleAnimation
                {
                    From = 180,
                    To = 360,
                    Duration = TimeSpan.FromMilliseconds(600),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };

                backRotationAnimation.Completed += (s2, e2) =>
                {
                    CardRotateTransform.Angle = 0;
                };

                CardRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, backRotationAnimation);
            };

            CardRotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, rotationAnimation);
        }

        /// &lt;summary&gt;
        /// 更新卡片信息显示
        /// &lt;/summary&gt;
        /// &lt;param name="info"&gt;要显示的信息文本&lt;/param&gt;
        public void UpdateCardInfo(string info)
        {
            CardInfo.Text = info;
        }

        /// &lt;summary&gt;
        /// 更新卡片显示内容
        /// &lt;/summary&gt;
        /// &lt;param name="cardData"&gt;卡片数据&lt;/param&gt;
        /// &lt;param name="currentIndex"&gt;当前卡片索引&lt;/param&gt;
        /// &lt;param name="totalCount"&gt;卡片总数&lt;/param&gt;
        public void UpdateDisplay(CardData cardData, int currentIndex, int totalCount)
        {
            // Update card content
            FrontText.Text = cardData.FrontText;
            FrontSubText.Text = cardData.FrontSubText;
            BackText.Text = cardData.BackText;
            BackSubText.Text = cardData.BackSubText;
            
            // Update card backgrounds
            CardFront.Background = cardData.FrontBackground;
            CardBack.Background = cardData.BackBackground;
            
            // Update card info
            UpdateCardInfo($"{currentIndex}/{totalCount}");
        }

        // 更新按钮状态显示
        /// &lt;summary&gt;
        /// 更新对话按钮状态
        /// &lt;/summary&gt;
        /// &lt;param name="isOpen"&gt;对话是否打开&lt;/param&gt;
        public void UpdateDialogButtonState(bool isOpen)
        {
            _isDialogOpen = isOpen;
            UpdateButtonColors();
        }

        /// &lt;summary&gt;
        /// 更新设置按钮状态
        /// &lt;/summary&gt;
        /// &lt;param name="isOpen"&gt;设置是否打开&lt;/param&gt;
        public void UpdateSettingsButtonState(bool isOpen)
        {
            _isSettingsOpen = isOpen;
            UpdateButtonColors();
        }

        private void UpdateButtonColors()
        {
            // 对话按钮：展开时显示橘色
            if (_isDialogOpen)
            {
                DialogButton.Background = System.Windows.Media.Brushes.Orange;
                DialogButton.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                DialogButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 255, 255)); // #80FFFFFF
                DialogButton.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 51, 51, 51)); // #333333
            }

            // 设置按钮：展开时显示蓝色
            if (_isSettingsOpen)
            {
                SettingsButton.Background = System.Windows.Media.Brushes.Blue;
                SettingsButton.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                SettingsButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 255, 255)); // #80FFFFFF
                SettingsButton.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 51, 51, 51)); // #333333
            }
        }
    }
}