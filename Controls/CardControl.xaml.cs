using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

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

        public CardControl()
        {
            InitializeComponent();
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

        public void UpdateCardInfo(string info)
        {
            CardInfo.Text = info;
        }

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
    }
}