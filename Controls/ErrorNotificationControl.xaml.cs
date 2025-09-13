using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Buddie.Localization;

namespace Buddie.Controls
{
    public partial class ErrorNotificationControl : UserControl
    {
        private bool _isDetailsVisible = false;
        private Action? _retryAction;
        private DoubleAnimation? _fadeAnimation;

        public event EventHandler? Closed;
        public event EventHandler? ActionRequested;

        public ErrorNotificationControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 显示错误通知
        /// </summary>
        public void ShowError(string message, string? details = null, string? actionText = null, Action? action = null)
        {
            MainMessage.Text = message;
            
            if (!string.IsNullOrEmpty(details))
            {
                DetailsMessage.Text = details;
                DetailsButton.Visibility = Visibility.Visible;
            }
            else
            {
                DetailsButton.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrEmpty(actionText) && action != null)
            {
                ActionButton.Content = actionText;
                ActionButton.Visibility = Visibility.Visible;
                _retryAction = action;
            }
            else
            {
                ActionButton.Visibility = Visibility.Collapsed;
            }

            // 设置错误类型对应的图标和颜色
            SetErrorStyle(ErrorType.General);
            
            // 淡入动画
            FadeIn();
        }

        /// <summary>
        /// 显示特定类型的错误
        /// </summary>
        public void ShowError(ErrorType errorType, string message, string? details = null, string? actionText = null, Action? action = null)
        {
            ShowError(message, details, actionText, action);
            SetErrorStyle(errorType);
        }

        /// <summary>
        /// 设置错误样式
        /// </summary>
        private void SetErrorStyle(ErrorType errorType)
        {
            switch (errorType)
            {
                case ErrorType.Network:
                    IconText.Text = "🌐";
                    NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8B, 0x2C, 0x2C)); // 深红色
                    break;
                case ErrorType.Authentication:
                    IconText.Text = "🔒";
                    NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8B, 0x45, 0x13)); // 橙棕色
                    break;
                case ErrorType.Configuration:
                    IconText.Text = "⚙️";
                    NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2F, 0x4F, 0x4F)); // 深青色
                    break;
                case ErrorType.Media:
                    IconText.Text = "🎵";
                    NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x48, 0x3D, 0x8B)); // 深紫色
                    break;
                case ErrorType.Data:
                    IconText.Text = "💾";
                    NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2F, 0x4F, 0x2F)); // 深绿色
                    break;
                case ErrorType.General:
                default:
                    IconText.Text = "⚠️";
                    NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x30)); // 默认深灰色
                    break;
            }
        }

        /// <summary>
        /// 淡入动画
        /// </summary>
        private void FadeIn()
        {
            _fadeAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            NotificationBorder.BeginAnimation(UIElement.OpacityProperty, _fadeAnimation);
            
            // 3秒后自动淡出（除非用户交互）
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (!_isDetailsVisible) // 如果用户没有查看详情，则自动关闭
                {
                    FadeOut();
                }
            };
            timer.Start();
        }

        /// <summary>
        /// 淡出动画
        /// </summary>
        private void FadeOut()
        {
            _fadeAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            _fadeAnimation.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
                Closed?.Invoke(this, EventArgs.Empty);
            };

            NotificationBorder.BeginAnimation(UIElement.OpacityProperty, _fadeAnimation);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            FadeOut();
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            _isDetailsVisible = !_isDetailsVisible;
            
            if (_isDetailsVisible)
            {
                DetailsPanel.Visibility = Visibility.Visible;
                DetailsButton.Content = LocalizationManager.GetString("Error_HideDetails");
            }
            else
            {
                DetailsPanel.Visibility = Visibility.Collapsed;
                DetailsButton.Content = LocalizationManager.GetString("Error_ViewDetails");
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            _retryAction?.Invoke();
            ActionRequested?.Invoke(this, EventArgs.Empty);
            FadeOut();
        }
    }

    /// <summary>
    /// 错误类型枚举
    /// </summary>
    public enum ErrorType
    {
        General,
        Network,
        Authentication,
        Configuration,
        Media,
        Data
    }
}
