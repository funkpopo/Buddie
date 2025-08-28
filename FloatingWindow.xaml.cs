using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.ComponentModel;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Windows.Media.Effects;
using SystemDrawing = System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;

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
        
        // 关联的API配置
        public OpenApiConfiguration? ApiConfiguration { get; set; }
    }

    public partial class FloatingWindow : Window
    {
        #region Windows API for Click-Through
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);
        
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_LAYERED = 0x00080000;
        
        private bool isClickThrough = false;
        #endregion

        private NotifyIcon? trayIcon;
        private List<CardData> cards = new List<CardData>();
        private int currentCardIndex = 0;
        private AppSettings appSettings = new AppSettings();
        private readonly List<System.Windows.Media.Color> usedCardColors = new List<System.Windows.Media.Color>();
        private readonly Random colorRandom = new Random();

        public FloatingWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeCards();
            InitializeControls();
            UpdateCardDisplay();
            
            // 应用初始主题
            ApplyTheme();
            
            // 设置窗口加载完成后启用点击穿透
            this.SourceInitialized += FloatingWindow_SourceInitialized;
        }
        
        private void FloatingWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // 禁用点击穿透，允许鼠标操作界面
            EnableClickThrough(false);
        }
        
        /// <summary>
        /// 设置窗口点击穿透
        /// </summary>
        /// <param name="enable">是否启用点击穿透</param>
        private void EnableClickThrough(bool enable)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            
            if (enable)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
            
            isClickThrough = enable;
        }

        private void InitializeControls()
        {
            // 初始化设置控件
            SettingsControl.Initialize(appSettings);
            SettingsControl.DataContext = appSettings;
            
            // 订阅设置控件事件
            SettingsControl.SettingsClosed += (s, e) => EnableClickThrough(false);
            SettingsControl.TopMostChanged += (s, value) => this.Topmost = value;
            SettingsControl.ShowInTaskbarChanged += (s, value) => this.ShowInTaskbar = value;
            SettingsControl.DarkThemeChanged += async (s, value) => {
                appSettings.IsDarkTheme = value;
                ApplyTheme();
                // 自动保存主题设置到数据库
                try
                {
                    await appSettings.SaveToDatabaseAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save theme setting: {ex.Message}");
                }
            };
            SettingsControl.ResetSettingsRequested += (s, e) => ResetSettings();
            SettingsControl.ApiConfigurationChanged += async (s, e) => {
                // 自动保存配置更改到数据库
                try
                {
                    await appSettings.SaveToDatabaseAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save API configurations: {ex.Message}");
                }
                
                UpdateCardsFromApiConfigurations();
                UpdateCardDisplay();
            };
            
            // 订阅TTS配置事件
            SettingsControl.TtsConfigurationActivated += async (s, config) => {
                try
                {
                    await appSettings.ActivateTtsConfigurationAsync(config);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to activate TTS configuration: {ex.Message}");
                }
            };
            
            SettingsControl.TtsConfigurationAdded += (s, config) => {
                try
                {
                    // 新配置添加到集合中并保存到数据库
                    // 配置已经通过TtsConfigControl.Initialize绑定到appSettings.TtsConfigurations
                    // 新添加的配置在用户点击保存按钮时会触发TtsConfigurationUpdated事件
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to add TTS configuration: {ex.Message}");
                }
            };
            
            SettingsControl.TtsConfigurationUpdated += async (s, config) => {
                try
                {
                    // 当用户点击保存按钮后，保存单个配置
                    await appSettings.SaveTtsConfigurationAsync(config);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update TTS configuration: {ex.Message}");
                }
            };
            
            SettingsControl.TtsConfigurationRemoved += async (s, config) => {
                try
                {
                    if (config.IsSaved && config.Id > 0)
                    {
                        await appSettings.RemoveTtsConfigurationAsync(config);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to remove TTS configuration: {ex.Message}");
                }
            };
            
            // 初始化对话控件
            DialogControl.DataContext = appSettings;
            DialogControl.MessageSent += async (s, message) => {
                if (currentCardIndex < cards.Count && cards[currentCardIndex].ApiConfiguration != null)
                {
                    await DialogControl.SendMessageToApi(message, cards[currentCardIndex].ApiConfiguration!);
                }
                else
                {
                    DialogControl.AddMessageBubble("请先配置API才能进行对话。", false);
                    // 重置发送状态，确保按钮恢复为"发送"
                    DialogControl.ResetSendingState();
                }
            };
            DialogControl.DialogClosed += (s, e) => EnableClickThrough(false);
            
            // 初始化卡片控件
            cardControl.DialogRequested += (s, e) => {
                DialogControl.Toggle();
                EnableClickThrough(false);
            };
            cardControl.SettingsRequested += (s, e) => {
                SettingsControl.Toggle();
                EnableClickThrough(false);
            };
            cardControl.MouseEntered += (s, e) => EnableClickThrough(false);
            cardControl.MouseLeft += (s, e) => EnableClickThrough(false);
        }

        private void InitializeCards()
        {
            // 初始化卡片根据API配置
            UpdateCardsFromApiConfigurations();
            
            // 如果没有API配置，添加一个默认示例卡片
            if (cards.Count == 0)
            {
                cards.Add(new CardData
                {
                    FrontText = "示例配置",
                    FrontSubText = "请先添加API配置",
                    BackText = "无可用配置",
                    BackSubText = "点击设置按钮添加",
                    FrontBackground = new LinearGradientBrush(
                        Colors.LightBlue, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                    ),
                    BackBackground = new LinearGradientBrush(
                        Colors.LightCoral, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                    )
                });
            }
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemDrawing.SystemIcons.Application;
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

        private async void ExitApplication_Click(object? sender, EventArgs e)
        {
            // 保存所有设置到数据库
            try
            {
                await appSettings.SaveToDatabaseAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings on exit: {ex.Message}");
            }
            
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
            
            // 保存设置到数据库 - 使用同步方式确保保存完成
            try
            {
                var saveTask = appSettings.SaveToDatabaseAsync();
                saveTask.Wait(); // 等待保存完成
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 窗口关闭时保存设置失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }

        private bool isFlipped = false;
        private bool isAnimating = false;

        private void LeftFlipButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (isFlipped)
            {
                cardControl.FlipCard();
                isFlipped = false;
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
                cardControl.FlipCard();
                isFlipped = false;
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
            SwitchToCard(nextIndex);
        }

        private void SwitchToPreviousCard()
        {
            if (isAnimating || cards.Count <= 1)
                return;
                
            int prevIndex = (currentCardIndex - 1 + cards.Count) % cards.Count;
            SwitchToCard(prevIndex);
        }

        private void SwitchToCard(int newIndex)
        {
            if (newIndex == currentCardIndex || isAnimating)
                return;
                
            currentCardIndex = newIndex;
            isFlipped = false;
            UpdateCardDisplay();
            
            // 检查新卡片是否有关联的API配置，如果有则自动拉起对话界面
            if (currentCardIndex < cards.Count && cards[currentCardIndex].ApiConfiguration != null)
            {
                ShowDialogForCurrentCard();
            }
        }
        
        // 为当前卡片显示对话界面
        private void ShowDialogForCurrentCard()
        {
            DialogControl.Toggle();
            // 更新对话标题以显示当前使用的API配置
            UpdateDialogTitle();
        }
        
        // 更新对话界面标题
        private void UpdateDialogTitle()
        {
            if (currentCardIndex < cards.Count && cards[currentCardIndex].ApiConfiguration != null)
            {
                var apiConfig = cards[currentCardIndex].ApiConfiguration;
                // 配置信息仅在内部使用
            }
        }
        
        private void UpdateCardDisplay()
        {
            if (currentCardIndex < 0 || currentCardIndex >= cards.Count)
                return;
                
            var card = cards[currentCardIndex];
            cardControl.UpdateDisplay(card, currentCardIndex + 1, cards.Count);
        }
        
        // 根据API配置更新卡片
        private void UpdateCardsFromApiConfigurations()
        {
            cards.Clear();
            ResetCardColorSystem(); // 重置颜色系统以确保新卡片有不同的颜色
            
            foreach (var apiConfig in appSettings.ApiConfigurations)
            {
                var colorPair = GetRandomColorPair();
                cards.Add(new CardData
                {
                    FrontText = apiConfig.Name,
                    FrontSubText = apiConfig.ModelName,
                    BackText = "API配置",
                    BackSubText = $"URL: {apiConfig.ApiUrl}",
                    FrontBackground = new LinearGradientBrush(
                        colorPair.frontColor, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                    ),
                    BackBackground = new LinearGradientBrush(
                        colorPair.backColor, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                    ),
                    ApiConfiguration = apiConfig  // 关联API配置
                });
            }
            
            // 如果没有API配置，创建默认卡片
            if (cards.Count == 0)
            {
                var colorPair = GetRandomColorPair();
                cards.Add(new CardData
                {
                    FrontText = "欢迎使用",
                    FrontSubText = "点击设置添加AI配置",
                    BackText = "配置提示",
                    BackSubText = "在设置中添加OpenAI API配置后即可开始对话",
                    FrontBackground = new LinearGradientBrush(
                        colorPair.frontColor, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                    ),
                    BackBackground = new LinearGradientBrush(
                        colorPair.backColor, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)
                    )
                });
            }
            
            // 重置当前卡片索引
            currentCardIndex = Math.Min(currentCardIndex, cards.Count - 1);
            if (currentCardIndex < 0) currentCardIndex = 0;
        }
        
        // 获取随机颜色的辅助方法
        private System.Windows.Media.Color GetRandomColor()
        {
            var availableColors = new[] { 
                Colors.LightBlue, Colors.LightGreen, Colors.LightCoral, 
                Colors.Plum, Colors.LightSalmon, Colors.LightSeaGreen,
                Colors.Orange, Colors.Gold, Colors.LightPink, Colors.LightCyan,
                Colors.Lavender, Colors.LightYellow, Colors.PaleGreen,
                Colors.Wheat, Colors.SkyBlue, Colors.MediumOrchid,
                Colors.LightSteelBlue, Colors.PeachPuff, Colors.Khaki,
                Colors.Thistle, Colors.PowderBlue, Colors.MistyRose
            };
            
            // 过滤掉已使用的颜色
            var unusedColors = availableColors.Where(c => !usedCardColors.Contains(c)).ToList();
            
            // 如果所有颜色都已使用，重置颜色列表
            if (unusedColors.Count == 0)
            {
                usedCardColors.Clear();
                unusedColors = availableColors.ToList();
            }
            
            // 随机选择一个未使用的颜色
            var selectedColor = unusedColors[colorRandom.Next(unusedColors.Count)];
            usedCardColors.Add(selectedColor);
            
            return selectedColor;
        }
        
        // 获取配对的随机颜色（用于卡片正面和背面）
        private (System.Windows.Media.Color frontColor, System.Windows.Media.Color backColor) GetRandomColorPair()
        {
            var frontColor = GetRandomColor();
            var backColor = GetRandomColor();
            
            // 确保正面和背面颜色不同
            while (backColor == frontColor && usedCardColors.Count > 1)
            {
                // 从已用颜色中移除背面颜色，重新选择
                usedCardColors.Remove(backColor);
                backColor = GetRandomColor();
            }
            
            return (frontColor, backColor);
        }
        
        // 重置卡片颜色系统
        private void ResetCardColorSystem()
        {
            usedCardColors.Clear();
        }

        // 重置设置
        private void ResetSettings()
        {
            this.Topmost = true;
            this.ShowInTaskbar = true;
            appSettings.IsDarkTheme = false;
            ApplyTheme();
        }

        // 应用主题
        private void ApplyTheme()
        {
            bool isDarkTheme = appSettings.IsDarkTheme;
            
            if (isDarkTheme)
            {
                ApplyDarkTheme();
            }
            else
            {
                ApplyLightTheme();
            }
            
            // 应用主题到子控件
            SettingsControl.ApplyTheme(isDarkTheme);
            DialogControl.ApplyTheme(isDarkTheme);
        }

        private void ApplyDarkTheme()
        {
            // 主窗口背景始终保持透明
            this.Background = System.Windows.Media.Brushes.Transparent;
        }

        private void ApplyLightTheme()
        {
            // 主窗口背景始终保持透明
            this.Background = System.Windows.Media.Brushes.Transparent;
        }

        /// <summary>
        /// 从数据库加载设置
        /// </summary>
        public async Task LoadSettingsFromDatabaseAsync()
        {
            try
            {
                await appSettings.LoadFromDatabaseAsync();
                
                // 应用加载的设置
                this.Topmost = appSettings.IsTopmost;
                this.ShowInTaskbar = appSettings.ShowInTaskbar;
                ApplyTheme();
                
                // 重新初始化配置控件，确保加载的配置显示出来
                SettingsControl.RefreshApiConfigurations(appSettings.ApiConfigurations);
                SettingsControl.RefreshTtsConfigurations(appSettings.TtsConfigurations);
                
                // 更新卡片显示
                UpdateCardsFromApiConfigurations();
                UpdateCardDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings from database: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前对话记录
        /// </summary>
        private async Task SaveCurrentConversation()
        {
            try
            {
                // TODO: 实现对话记录保存逻辑
                // 这里需要从DialogControl获取当前对话内容并保存到数据库
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新卡片显示
        /// </summary>
        private void RefreshCards()
        {
            UpdateCardsFromApiConfigurations();
            UpdateCardDisplay();
        }

        /// <summary>
        /// 应用程序退出前保存设置
        /// </summary>
        public void SaveSettingsBeforeExit()
        {
            try
            {
                var saveTask = appSettings.SaveToDatabaseAsync();
                saveTask.Wait(TimeSpan.FromSeconds(5)); // 等待最多5秒
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSettingsBeforeExit failed: {ex.Message}");
                throw; // 重新抛出异常让调用者知道失败了
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            // 最后一次保存设置（防止强制关闭时丢失配置）
            try
            {
                var saveTask = appSettings.SaveToDatabaseAsync();
                saveTask.Wait(TimeSpan.FromSeconds(3)); // 最多等待3秒
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 应用程序最终关闭时保存设置失败: {ex.Message}");
            }
            
            trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}