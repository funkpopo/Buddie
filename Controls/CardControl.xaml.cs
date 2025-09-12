using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Data;

namespace Buddie.Controls
{
    public partial class CardControl : UserControl
    {
        public static readonly DependencyProperty DialogCommandProperty = DependencyProperty.Register(
            nameof(DialogCommand), typeof(ICommand), typeof(CardControl), new PropertyMetadata(null));

        public static readonly DependencyProperty BuddieCommandProperty = DependencyProperty.Register(
            nameof(BuddieCommand), typeof(ICommand), typeof(CardControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SettingsCommandProperty = DependencyProperty.Register(
            nameof(SettingsCommand), typeof(ICommand), typeof(CardControl), new PropertyMetadata(null));

        public ICommand? DialogCommand
        {
            get => (ICommand?)GetValue(DialogCommandProperty);
            set => SetValue(DialogCommandProperty, value);
        }

        public ICommand? BuddieCommand
        {
            get => (ICommand?)GetValue(BuddieCommandProperty);
            set => SetValue(BuddieCommandProperty, value);
        }

        public ICommand? SettingsCommand
        {
            get => (ICommand?)GetValue(SettingsCommandProperty);
            set => SetValue(SettingsCommandProperty, value);
        }

        public event EventHandler? DialogButtonClicked;
        public event EventHandler? BuddieButtonClicked;
        public event EventHandler? SettingsButtonClicked;
        public event EventHandler? LeftFlipButtonClicked;
        public event EventHandler? RightFlipButtonClicked;
        
        public event EventHandler? DialogRequested;
        public event EventHandler? BuddieRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler? MouseEntered;
        public event EventHandler? MouseLeft;

        // 界面状态跟踪
        private bool _isDialogOpen = false;
        private bool _isBuddieOpen = false;
        private bool _isSettingsOpen = false;

        public CardControl()
        {
            InitializeComponent();
            
            // 确保卡片初始显示正面
            CardFront.Visibility = Visibility.Visible;
            CardBack.Visibility = Visibility.Collapsed;
            
            // 监听DataContext变化以获取主题设置
            DataContextChanged += OnDataContextChanged;
            
            // 初始化时应用主题
            UpdateTheme();
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

        // Button clicks are now bound to ICommand via dependency properties in XAML.


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
            
            // 确保显示正面
            CardFront.Visibility = Visibility.Visible;
            CardBack.Visibility = Visibility.Collapsed;
            
            // Update card info
            UpdateCardInfo($"{currentIndex}/{totalCount}");
            
            // Update stack visibility based on total count
            UpdateStackVisibility(totalCount);
        }

        /// <summary>
        /// 根据卡片总数更新堆叠显示
        /// </summary>
        /// <param name="totalCount">卡片总数</param>
        private void UpdateStackVisibility(int totalCount)
        {
            if (totalCount <= 1)
            {
                // 只有一张卡片时隐藏堆叠
                StackCard2.Visibility = Visibility.Collapsed;
                StackCard3.Visibility = Visibility.Collapsed;
            }
            else if (totalCount == 2)
            {
                // 两张卡片时显示第二层
                StackCard2.Visibility = Visibility.Visible;
                StackCard3.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 三张或以上显示全部堆叠
                StackCard2.Visibility = Visibility.Visible;
                StackCard3.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 执行卡片切换动画（无旋转效果）
        /// </summary>
        /// <param name="newCardData">新卡片数据</param>
        /// <param name="currentIndex">当前索引</param>
        /// <param name="totalCount">总数</param>
        /// <param name="direction">翻动方向：1为向右，-1为向左</param>
        /// <param name="onCompleted">动画完成回调</param>
        public void SwitchWithFlipAnimation(CardData newCardData, int currentIndex, int totalCount, int direction, Action? onCompleted = null)
        {
            // 简单的淡出淡入效果
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                // 更新卡片内容
                UpdateDisplay(newCardData, currentIndex, totalCount);
                
                // 淡入新内容
                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0.3,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                fadeInAnimation.Completed += (s2, e2) =>
                {
                    // 确保完全不透明
                    this.Opacity = 1.0;
                    onCompleted?.Invoke();
                };

                // 执行淡入动画
                this.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };

            // 执行淡出动画
            this.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
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

        /// &lt;summary&gt;
        /// 更新Buddie按钮状态
        /// &lt;/summary&gt;
        /// &lt;param name="isOpen"&gt;Buddie是否打开&lt;/param&gt;
        public void UpdateBuddieButtonState(bool isOpen)
        {
            _isBuddieOpen = isOpen;
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

            // Buddie按钮：展开时显示绿色
            if (_isBuddieOpen)
            {
                BuddieButton.Background = System.Windows.Media.Brushes.Green;
                BuddieButton.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                BuddieButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 255, 255)); // #80FFFFFF
                BuddieButton.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 51, 51, 51)); // #333333
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

        #region 主题适配

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 当DataContext变化时更新主题
            if (e.NewValue is AppSettings appSettings)
            {
                appSettings.PropertyChanged += AppSettings_PropertyChanged;
            }
            
            if (e.OldValue is AppSettings oldAppSettings)
            {
                oldAppSettings.PropertyChanged -= AppSettings_PropertyChanged;
            }
            
            UpdateTheme();
        }

        private void AppSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppSettings.IsDarkTheme))
            {
                UpdateTheme();
            }
        }

        /// <summary>
        /// 更新主题适配
        /// </summary>
        public void UpdateTheme()
        {
            bool isDarkTheme = GetCurrentTheme();
            UpdateTextTheme(isDarkTheme);
            UpdateCardInfoTheme(isDarkTheme);
            UpdateButtonColors(); // 重新应用按钮颜色以适配主题
        }

        private bool GetCurrentTheme()
        {
            if (DataContext is AppSettings appSettings)
            {
                return appSettings.IsDarkTheme;
            }
            return false; // 默认浅色主题
        }

        private void UpdateTextTheme(bool isDarkTheme)
        {
            // 更新阴影效果以适配主题
            var shadowColor = isDarkTheme ? Colors.White : Colors.Black;
            var shadowOpacity = isDarkTheme ? 0.6 : 0.8;

            // 更新正面文字阴影
            if (FrontTextShadow != null)
            {
                FrontTextShadow.Color = shadowColor;
                FrontTextShadow.Opacity = shadowOpacity;
            }

            if (FrontSubTextShadow != null)
            {
                FrontSubTextShadow.Color = shadowColor;
                FrontSubTextShadow.Opacity = shadowOpacity * 0.875; // 稍微透明一些
            }

            // 更新背面文字阴影
            if (BackTextShadow != null)
            {
                BackTextShadow.Color = shadowColor;
                BackTextShadow.Opacity = shadowOpacity;
            }

            if (BackSubTextShadow != null)
            {
                BackSubTextShadow.Color = shadowColor;
                BackSubTextShadow.Opacity = shadowOpacity * 0.875;
            }
        }

        private void UpdateCardInfoTheme(bool isDarkTheme)
        {
            if (isDarkTheme)
            {
                // 深色主题：深色背景，浅色文字
                CardInfoContainer.Background = new SolidColorBrush(Color.FromArgb(128, 45, 45, 48)); // #802D2D30
                CardInfo.Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 220, 220)); // #DCDCDC
            }
            else
            {
                // 浅色主题：浅色背景，深色文字
                CardInfoContainer.Background = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)); // #80FFFFFF
                CardInfo.Foreground = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51)); // #333333
            }
        }

        #endregion
    }
}
