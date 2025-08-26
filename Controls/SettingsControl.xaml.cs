using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Buddie.Controls
{
    public partial class SettingsControl : UserControl
    {
        public event EventHandler? SettingsClosed;
        public event EventHandler? ResetSettingsRequested;
        public event EventHandler<bool>? TopMostChanged;
        public event EventHandler<bool>? ShowInTaskbarChanged;
        public event EventHandler<bool>? DarkThemeChanged;

        public SettingsControl()
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

        public void Initialize(AppSettings appSettings)
        {
            // 初始化子控件
            ApiConfigControl.Initialize(appSettings.ApiConfigurations);
            TtsConfigControl.Initialize(appSettings.TtsConfigurations);
            
            // 订阅子控件事件
            ApiConfigControl.ConfigurationAdded += (s, config) => {
                // 通知主窗口更新卡片
                OnApiConfigurationChanged();
            };
            
            ApiConfigControl.ConfigurationRemoved += (s, config) => {
                // 通知主窗口更新卡片
                OnApiConfigurationChanged();
            };
            
            ApiConfigControl.ConfigurationUpdated += (s, config) => {
                // 通知主窗口更新卡片
                OnApiConfigurationChanged();
            };
            
            TtsConfigControl.ConfigurationAdded += (s, config) => {
                // TTS配置添加
            };
            
            TtsConfigControl.ConfigurationRemoved += (s, config) => {
                // TTS配置移除
            };
            
            TtsConfigControl.ConfigurationUpdated += (s, config) => {
                // TTS配置更新
            };
        }

        public event EventHandler? ApiConfigurationChanged;
        
        private void OnApiConfigurationChanged()
        {
            ApiConfigurationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            SettingsClosed?.Invoke(this, EventArgs.Empty);
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            ResetSettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void TopMostCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (TopMostCheckBox.IsChecked.HasValue)
                TopMostChanged?.Invoke(this, TopMostCheckBox.IsChecked.Value);
        }

        private void ShowInTaskbarCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowInTaskbarCheckBox.IsChecked.HasValue)
                ShowInTaskbarChanged?.Invoke(this, ShowInTaskbarCheckBox.IsChecked.Value);
        }

        private void DarkThemeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (DarkThemeCheckBox.IsChecked.HasValue)
                DarkThemeChanged?.Invoke(this, DarkThemeCheckBox.IsChecked.Value);
        }

        public void Show()
        {
            SettingsInterface.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            SettingsInterface.Visibility = Visibility.Collapsed;
        }

        public new bool IsVisible => SettingsInterface.Visibility == Visibility.Visible;

        public void Dispose()
        {
            ApiConfigControl?.Dispose();
        }
    }
}