using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;

namespace Buddie.Controls
{
    public partial class SettingsControl : UserControl
    {
        public event EventHandler? SettingsClosed;
        public event EventHandler? ResetSettingsRequested;
        public event EventHandler<bool>? TopMostChanged;
        public event EventHandler<bool>? ShowInTaskbarChanged;
        public event EventHandler<bool>? DarkThemeChanged;
        public event EventHandler<TtsConfiguration>? TtsConfigurationActivated;
        public event EventHandler<TtsConfiguration>? TtsConfigurationAdded;
        public event EventHandler<TtsConfiguration>? TtsConfigurationUpdated;
        public event EventHandler<TtsConfiguration>? TtsConfigurationRemoved;

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
                TtsConfigurationAdded?.Invoke(this, config);
            };
            
            TtsConfigControl.ConfigurationRemoved += (s, config) => {
                // TTS配置移除
                TtsConfigurationRemoved?.Invoke(this, config);
            };
            
            TtsConfigControl.ConfigurationUpdated += (s, config) => {
                // TTS配置更新
                TtsConfigurationUpdated?.Invoke(this, config);
            };
            
            TtsConfigControl.ConfigurationActivated += (s, config) => {
                // TTS配置激活
                TtsConfigurationActivated?.Invoke(this, config);
            };
        }
        
        public void RefreshTtsConfigurations(ObservableCollection<TtsConfiguration> ttsConfigurations)
        {
            // 重新初始化TTS配置控件，用于数据库加载后刷新
            TtsConfigControl.Initialize(ttsConfigurations);
        }
        
        public void RefreshApiConfigurations(ObservableCollection<OpenApiConfiguration> apiConfigurations)
        {
            // 重新初始化API配置控件，用于数据库加载后刷新
            ApiConfigControl.Initialize(apiConfigurations);
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
            SettingsInterface.Visibility = Visibility.Collapsed;
        }

        public new bool IsVisible => SettingsInterface.Visibility == Visibility.Visible;

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
            SettingsInterface.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 44, 52));
            
            // 标题栏背景和文字
            var titleElements = System.Windows.LogicalTreeHelper.GetChildren(SettingsInterface);
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
            
            // TabControl背景
            var tabControl = this.FindName("MainTabControl") as TabControl;
            if (tabControl != null)
            {
                tabControl.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 44, 52));
                
                // 更新TabControl内的所有文字颜色
                UpdateTextElementsColor(tabControl, System.Windows.Media.Brushes.White);
            }
            
            // 更新所有设置界面的文字颜色
            UpdateTextElementsColor(SettingsInterface, System.Windows.Media.Brushes.White);
        }

        private void ApplyLightTheme()
        {
            // 主界面背景
            SettingsInterface.Background = System.Windows.Media.Brushes.White;
            
            // 标题栏背景和文字
            var titleElements = System.Windows.LogicalTreeHelper.GetChildren(SettingsInterface);
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
            
            // TabControl背景
            var tabControl = this.FindName("MainTabControl") as TabControl;
            if (tabControl != null)
            {
                tabControl.Background = System.Windows.Media.Brushes.Transparent;
                
                // 更新TabControl内的所有文字颜色
                UpdateTextElementsColor(tabControl, System.Windows.Media.Brushes.Black);
            }
            
            // 更新所有设置界面的文字颜色
            UpdateTextElementsColor(SettingsInterface, System.Windows.Media.Brushes.Black);
        }

        private void UpdateTextElementsColor(DependencyObject parent, System.Windows.Media.Brush color)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is TextBlock textBlock)
                {
                    // 为描述性文字设置特殊颜色
                    if (textBlock.Name == "TopMostDescription" || 
                        textBlock.Name == "TaskbarDescription" || 
                        textBlock.Name == "DarkModeDescription")
                    {
                        if (color == System.Windows.Media.Brushes.White)
                        {
                            textBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160));
                        }
                        else
                        {
                            textBlock.Foreground = System.Windows.Media.Brushes.Gray;
                        }
                    }
                    else
                    {
                        textBlock.Foreground = color;
                    }
                }
                
                UpdateTextElementsColor(child, color);
            }
        }

        public void Dispose()
        {
            ApiConfigControl?.Dispose();
        }
    }
}