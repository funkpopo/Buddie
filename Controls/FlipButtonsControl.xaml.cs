using System;
using System.Windows;
using System.Windows.Controls;

namespace Buddie.Controls
{
    public partial class FlipButtonsControl : UserControl
    {
        public event EventHandler? LeftFlipButtonClicked;
        public event EventHandler? RightFlipButtonClicked;

        public FlipButtonsControl()
        {
            InitializeComponent();
        }

        private void LeftFlipButton_Click(object sender, RoutedEventArgs e)
        {
            LeftFlipButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        private void RightFlipButton_Click(object sender, RoutedEventArgs e)
        {
            RightFlipButtonClicked?.Invoke(this, EventArgs.Empty);
        }

        public void SetLeftButtonEnabled(bool enabled)
        {
            LeftFlipButton.IsEnabled = enabled;
        }

        public void SetRightButtonEnabled(bool enabled)
        {
            RightFlipButton.IsEnabled = enabled;
        }

        public void SetButtonsEnabled(bool leftEnabled, bool rightEnabled)
        {
            LeftFlipButton.IsEnabled = leftEnabled;
            RightFlipButton.IsEnabled = rightEnabled;
        }
    }
}