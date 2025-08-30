using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using SystemDrawing = System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;

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
        
        private bool _isClickThrough = false;
        #endregion

        private NotifyIcon? trayIcon;
        private readonly List<CardData> _cards = new List<CardData>();
        private int _currentCardIndex = 0;
        private readonly AppSettings _appSettings = new AppSettings();
        private readonly List<System.Windows.Media.Color> _usedCardColors = new List<System.Windows.Media.Color>();
        private readonly Random _colorRandom = new Random();
        
        // 用户交互状态管理
        private bool _isUserInteracting = false;
        private System.Windows.Threading.DispatcherTimer? _interactionTimer;
        private readonly TimeSpan _interactionDelay = TimeSpan.FromSeconds(2);

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
            
            // 初始化交互计时器
            InitializeInteractionTimer();
            
            // 初始化用户友好错误服务
            UserFriendlyErrorService.Initialize(ErrorNotificationContainer);
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
            
            _isClickThrough = enable;
        }

        private void InitializeControls()
        {
            // 初始化设置控件
            SettingsControl.Initialize(_appSettings);
            SettingsControl.DataContext = _appSettings;
            
            // 订阅设置控件事件
            SettingsControl.SettingsClosed += (s, e) => EnableClickThrough(false);
            SettingsControl.TopMostChanged += (s, value) => this.Topmost = value;
            SettingsControl.ShowInTaskbarChanged += (s, value) => this.ShowInTaskbar = value;
            SettingsControl.DarkThemeChanged += async (s, value) => {
                _appSettings.IsDarkTheme = value;
                ApplyTheme();
                // 自动保存主题设置到数据库
                await ExceptionHandlingService.Database.ExecuteSafelyAsync(
                    () => _appSettings.SaveToDatabaseAsync(),
                    "保存主题设置");
            };
            SettingsControl.ResetSettingsRequested += (s, e) => ResetSettings();
            SettingsControl.SettingsVisibilityChanged += (s, isVisible) => {
                // 更新卡片按钮状态
                cardControl.UpdateSettingsButtonState(isVisible);
            };
            SettingsControl.ApiConfigurationChanged += async (s, e) => {
                // 自动保存配置更改到数据库
                await ExceptionHandlingService.Database.ExecuteSafelyAsync(
                    () => _appSettings.SaveToDatabaseAsync(),
                    "保存API配置");
                
                UpdateCardsFromApiConfigurations();
                UpdateCardDisplay();
            };
            
            // 订阅TTS配置事件
            SettingsControl.TtsConfigurationActivated += async (s, config) => {
                await ExceptionHandlingService.Tts.ExecuteSafelyAsync(
                    () => _appSettings.ActivateTtsConfigurationAsync(config),
                    "激活TTS配置");
            };
            
            SettingsControl.TtsConfigurationAdded += (s, config) => {
                ExceptionHandlingService.UI.ExecuteSafely(() => {
                    // 新配置添加到集合中并保存到数据库
                    // 配置已经通过TtsConfigControl.Initialize绑定到_appSettings.TtsConfigurations
                    // 新添加的配置在用户点击保存按钮时会触发TtsConfigurationUpdated事件
                }, "TTS配置添加");
            };
            
            SettingsControl.TtsConfigurationUpdated += async (s, config) => {
                try
                {
                    // 当用户点击保存按钮后，保存单个配置
                    await _appSettings.SaveTtsConfigurationAsync(config);
                }
                catch (Exception)
                {
                    // Handle TTS configuration update error silently
                }
            };
            
            SettingsControl.TtsConfigurationRemoved += async (s, config) => {
                try
                {
                    if (config.IsSaved && config.Id > 0)
                    {
                        await _appSettings.RemoveTtsConfigurationAsync(config);
                    }
                }
                catch (Exception)
                {
                    // Handle TTS configuration removal error silently
                }
            };
            
            // 初始化对话控件
            DialogControl.DataContext = _appSettings;
            DialogControl.MessageSent += async (s, message) => {
                if (_currentCardIndex < _cards.Count && _cards[_currentCardIndex].ApiConfiguration != null)
                {
                    await DialogControl.SendMessageToApi(message, _cards[_currentCardIndex].ApiConfiguration!);
                }
                else
                {
                    DialogControl.AddMessageBubble("请先配置API才能进行对话。", false);
                    // 重置发送状态，确保按钮恢复为"发送"
                    DialogControl.ResetSendingState();
                }
            };
            DialogControl.DialogClosed += (s, e) => EnableClickThrough(false);
            DialogControl.DialogVisibilityChanged += (s, isVisible) => {
                // 更新卡片按钮状态
                cardControl.UpdateDialogButtonState(isVisible);
            };
            
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
            
            // 监听子控件的焦点事件以防止输入时透明化
            SubscribeToFocusEvents();
        }

        private void InitializeCards()
        {
            // 初始化卡片根据API配置
            UpdateCardsFromApiConfigurations();
            
            // 如果没有API配置，添加一个默认示例卡片
            if (_cards.Count == 0)
            {
                _cards.Add(new CardData
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
                await _appSettings.SaveToDatabaseAsync();
            }
            catch (Exception)
            {
                // Handle save error silently during exit
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
                var saveTask = _appSettings.SaveToDatabaseAsync();
                saveTask.Wait(); // 等待保存完成
            }
            catch (Exception)
            {
                // Handle save error silently during window close
            }
        }

        private bool _isFlipped = false;
        private bool _isAnimating = false;

        private void LeftFlipButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_isFlipped)
            {
                cardControl.FlipCard();
                _isFlipped = false;
            }
            else
            {
                SwitchToPreviousCard();
            }
        }

        private void RightFlipButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_isFlipped)
            {
                cardControl.FlipCard();
                _isFlipped = false;
            }
            else
            {
                SwitchToNextCard();
            }
        }

        private void SwitchToNextCard()
        {
            if (_isAnimating || _cards.Count <= 1)
                return;
                
            int nextIndex = (_currentCardIndex + 1) % _cards.Count;
            SwitchToCard(nextIndex);
        }

        private void SwitchToPreviousCard()
        {
            if (_isAnimating || _cards.Count <= 1)
                return;
                
            int prevIndex = (_currentCardIndex - 1 + _cards.Count) % _cards.Count;
            SwitchToCard(prevIndex);
        }

        private void SwitchToCard(int newIndex)
        {
            if (newIndex == _currentCardIndex || _isAnimating)
                return;
                
            _currentCardIndex = newIndex;
            _isFlipped = false;
            UpdateCardDisplay();
            
            // 检查新卡片是否有关联的API配置，如果有则自动拉起对话界面
            if (_currentCardIndex < _cards.Count && _cards[_currentCardIndex].ApiConfiguration != null)
            {
                ShowDialogForCurrentCard();
            }
        }
        
        // 为当前卡片显示对话界面
        private void ShowDialogForCurrentCard()
        {
            // 传递当前卡片的API配置给DialogControl
            if (_currentCardIndex >= 0 && _currentCardIndex < _cards.Count)
            {
                DialogControl.SetCurrentApiConfiguration(_cards[_currentCardIndex].ApiConfiguration);
            }
            DialogControl.Toggle();
        }
        
        private void UpdateCardDisplay()
        {
            if (_currentCardIndex < 0 || _currentCardIndex >= _cards.Count)
                return;
                
            var card = _cards[_currentCardIndex];
            cardControl.UpdateDisplay(card, _currentCardIndex + 1, _cards.Count);
            
            // 更新DialogControl的API配置
            DialogControl.SetCurrentApiConfiguration(card.ApiConfiguration);
        }
        
        // 根据API配置更新卡片
        private void UpdateCardsFromApiConfigurations()
        {
            _cards.Clear();
            ResetCardColorSystem(); // 重置颜色系统以确保新卡片有不同的颜色
            
            foreach (var apiConfig in _appSettings.ApiConfigurations)
            {
                var colorPair = GetRandomColorPair();
                _cards.Add(new CardData
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
            if (_cards.Count == 0)
            {
                var colorPair = GetRandomColorPair();
                _cards.Add(new CardData
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
            _currentCardIndex = Math.Min(_currentCardIndex, _cards.Count - 1);
            if (_currentCardIndex < 0) _currentCardIndex = 0;
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
            var unusedColors = availableColors.Where(c => !_usedCardColors.Contains(c)).ToList();
            
            // 如果所有颜色都已使用，重置颜色列表
            if (unusedColors.Count == 0)
            {
                _usedCardColors.Clear();
                unusedColors = availableColors.ToList();
            }
            
            // 随机选择一个未使用的颜色
            var selectedColor = unusedColors[_colorRandom.Next(unusedColors.Count)];
            _usedCardColors.Add(selectedColor);
            
            return selectedColor;
        }
        
        // 获取配对的随机颜色（用于卡片正面和背面）
        private (System.Windows.Media.Color frontColor, System.Windows.Media.Color backColor) GetRandomColorPair()
        {
            var frontColor = GetRandomColor();
            var backColor = GetRandomColor();
            
            // 确保正面和背面颜色不同
            while (backColor == frontColor && _usedCardColors.Count > 1)
            {
                // 从已用颜色中移除背面颜色，重新选择
                _usedCardColors.Remove(backColor);
                backColor = GetRandomColor();
            }
            
            return (frontColor, backColor);
        }
        
        // 重置卡片颜色系统
        private void ResetCardColorSystem()
        {
            _usedCardColors.Clear();
        }

        // 重置设置
        private void ResetSettings()
        {
            this.Topmost = true;
            this.ShowInTaskbar = true;
            _appSettings.IsDarkTheme = false;
            ApplyTheme();
        }

        // 应用主题
        private void ApplyTheme()
        {
            bool isDarkTheme = _appSettings.IsDarkTheme;
            
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
                await _appSettings.LoadFromDatabaseAsync();
                
                // 应用加载的设置
                this.Topmost = _appSettings.IsTopmost;
                this.ShowInTaskbar = _appSettings.ShowInTaskbar;
                ApplyTheme();
                
                // 重新初始化配置控件，确保加载的配置显示出来
                SettingsControl.RefreshApiConfigurations(_appSettings.ApiConfigurations);
                SettingsControl.RefreshTtsConfigurations(_appSettings.TtsConfigurations);
                
                // 更新卡片显示
                UpdateCardsFromApiConfigurations();
                UpdateCardDisplay();
            }
            catch (Exception)
            {
                // Handle load error silently
            }
        }

        /// <summary>
        /// 保存当前对话记录
        /// </summary>
        private async Task SaveCurrentConversation()
        {
            try
            {
                // 对话记录保存功能已在DialogControl中实现
                await Task.CompletedTask;
            }
            catch (Exception)
            {
                // Handle conversation save error silently
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
                var saveTask = _appSettings.SaveToDatabaseAsync();
                saveTask.Wait(TimeSpan.FromSeconds(5)); // 等待最多5秒
            }
            catch (Exception)
            {
                // Handle save error silently
                throw; // Re-throw for caller to handle
            }
        }


        protected override void OnClosed(EventArgs e)
        {
            // 最后一次保存设置（防止强制关闭时丢失配置）
            try
            {
                var saveTask = _appSettings.SaveToDatabaseAsync();
                saveTask.Wait(TimeSpan.FromSeconds(3)); // 最多等待3秒
            }
            catch (Exception)
            {
                // Handle save error silently during final close
            }
            
            trayIcon?.Dispose();
            _interactionTimer?.Stop();
            base.OnClosed(e);
        }
        
        #region 用户交互状态管理
        
        /// <summary>
        /// 初始化交互计时器
        /// </summary>
        private void InitializeInteractionTimer()
        {
            _interactionTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = _interactionDelay
            };
            _interactionTimer.Tick += InteractionTimer_Tick;
        }
        
        /// <summary>
        /// 交互计时器事件处理
        /// </summary>
        private void InteractionTimer_Tick(object? sender, EventArgs e)
        {
            _interactionTimer?.Stop();
            SetUserInteracting(false);
        }
        
        /// <summary>
        /// 设置用户交互状态
        /// </summary>
        private void SetUserInteracting(bool interacting)
        {
            _isUserInteracting = interacting;
            
            if (interacting)
            {
                // 用户开始交互，立即设置为不透明
                UpdateInterfaceOpacity(1.0);
                // 重置计时器
                _interactionTimer?.Stop();
                _interactionTimer?.Start();
            }
            else
            {
                // 用户停止交互，根据鼠标位置决定透明度
                var mousePosition = System.Windows.Forms.Control.MousePosition;
                var windowBounds = new System.Drawing.Rectangle(
                    (int)this.Left, (int)this.Top, 
                    (int)this.ActualWidth, (int)this.ActualHeight);
                
                if (!windowBounds.Contains(mousePosition))
                {
                    UpdateInterfaceOpacity(0.2);
                }
            }
        }
        
        /// <summary>
        /// 订阅焦点事件以监控用户交互
        /// </summary>
        private void SubscribeToFocusEvents()
        {
            // 监听整个窗口的焦点变化
            this.GotFocus += (s, e) => 
            {
                if (IsInputElementOrChild(e.OriginalSource as DependencyObject))
                {
                    SetUserInteracting(true);
                }
            };
            
            // 使用PreviewLostKeyboardFocus代替LostFocus
            this.PreviewLostKeyboardFocus += (s, e) => 
            {
                // 检查焦点是否转移到了其他输入元素
                if (!IsInputElementOrChild(e.NewFocus as DependencyObject))
                {
                    // 延迟一点时间，让用户有机会点击其他输入元素
                    var delayTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    delayTimer.Tick += (sender, args) =>
                    {
                        delayTimer.Stop();
                        if (!IsAnyInputElementFocused())
                        {
                            SetUserInteracting(false);
                        }
                    };
                    delayTimer.Start();
                }
            };
            
            // 监听鼠标点击事件
            this.PreviewMouseDown += (s, e) =>
            {
                if (IsInputElementOrChild(e.OriginalSource as DependencyObject))
                {
                    SetUserInteracting(true);
                }
            };
            
            // 监听键盘输入事件
            this.PreviewKeyDown += (s, e) =>
            {
                if (IsAnyInputElementFocused())
                {
                    SetUserInteracting(true);
                }
            };
        }
        
        /// <summary>
        /// 检查元素是否为输入元素
        /// </summary>
        private bool IsInputElement(DependencyObject? element)
        {
            if (element == null) return false;
            
            // 直接检查元素类型，避免递归调用
            return element is System.Windows.Controls.TextBox || 
                   element is System.Windows.Controls.RichTextBox || 
                   element is System.Windows.Controls.PasswordBox ||
                   element is System.Windows.Controls.ComboBox ||
                   element is System.Windows.Controls.Slider;
        }
        
        /// <summary>
        /// 检查是否有输入元素父级（带深度限制防止无限递归）
        /// </summary>
        private bool HasInputElementParent(DependencyObject? element, int maxDepth = 10)
        {
            if (element == null || maxDepth <= 0) return false;
            
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
            if (parent == null) return false;
            
            // 直接检查父元素类型，不调用IsInputElement避免递归
            if (parent is System.Windows.Controls.TextBox || 
                parent is System.Windows.Controls.RichTextBox || 
                parent is System.Windows.Controls.PasswordBox ||
                parent is System.Windows.Controls.ComboBox ||
                parent is System.Windows.Controls.Slider)
                return true;
                
            return HasInputElementParent(parent, maxDepth - 1);
        }
        
        /// <summary>
        /// 检查元素或其父级是否为输入元素
        /// </summary>
        private bool IsInputElementOrChild(DependencyObject? element)
        {
            return IsInputElement(element) || HasInputElementParent(element);
        }
        
        /// <summary>
        /// 检查当前是否有任何输入元素获得焦点
        /// </summary>
        private bool IsAnyInputElementFocused()
        {
            var focusedElement = Keyboard.FocusedElement as DependencyObject;
            return IsInputElementOrChild(focusedElement);
        }
        
        #endregion

        private void FloatingWindow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 鼠标进入应用窗口时，将展开的界面设置为完全不透明
            UpdateInterfaceOpacity(1.0);
        }

        private void FloatingWindow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 鼠标离开应用窗口时，将展开的界面透明度调整为0.2
            UpdateInterfaceOpacity(0.2);
        }

        /// <summary>
        /// 更新展开界面的透明度
        /// </summary>
        /// <param name="opacity">透明度值</param>
        private void UpdateInterfaceOpacity(double opacity)
        {
            // 如果用户正在交互，保持完全不透明
            if (_isUserInteracting && opacity < 1.0)
            {
                opacity = 1.0;
            }
            
            // 只影响当前可见的界面
            if (DialogControl.IsVisible)
            {
                DialogControl.SetOpacity(opacity);
            }
            if (SettingsControl.IsVisible)
            {
                SettingsControl.SetOpacity(opacity);
            }
        }
    }
}