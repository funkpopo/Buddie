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
using System.Windows.Interop;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;
using Buddie.Services;
using Buddie.Tests;
using Buddie.ViewModels;

namespace Buddie
{
    

    public partial class FloatingWindow : Window
    {
        private NotifyIcon? trayIcon;
        private readonly IClickThroughService _clickThroughService;
        private readonly FloatingWindowViewModel _vm;
        private readonly AppSettings _appSettings = new AppSettings();
        private readonly CardColorManager _colorManager = new CardColorManager();
        
        // 实时交互服务
        private readonly RealtimeInteractionService _realtimeService = Buddie.App.GetService<Buddie.Services.RealtimeInteractionService>();
        // Real-time interaction state is tracked in ViewModel
        
        // 用户交互状态管理通过ViewModel处理
        private System.Windows.Threading.DispatcherTimer? _interactionTimer;
        private readonly TimeSpan _interactionDelay = TimeSpan.FromSeconds(2);

        public FloatingWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            
            // ViewModel wiring
            _vm = new FloatingWindowViewModel(_appSettings, _realtimeService);
            this.DataContext = _vm;
            
            // Initialize ClickThroughService
            _clickThroughService = new ClickThroughService();
            _vm.SetClickThroughService(_clickThroughService);
            
            InitializeControls();
            
            // Initial cards sync to view is handled by ViewModel

            // Update card visuals on index/list changes
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FloatingWindowViewModel.CurrentCardIndex) ||
                    e.PropertyName == nameof(FloatingWindowViewModel.Cards))
                {
                    UpdateCardDisplayFromViewModel();
                }
                else if (e.PropertyName == nameof(FloatingWindowViewModel.IsDialogVisible))
                {
                    if (_vm.IsDialogVisible) DialogControl.Show(); else DialogControl.Hide();
                }
                else if (e.PropertyName == nameof(FloatingWindowViewModel.IsSettingsVisible))
                {
                    if (_vm.IsSettingsVisible) SettingsControl.Show(); else SettingsControl.Hide();
                }
                else if (e.PropertyName == nameof(FloatingWindowViewModel.IsWindowVisible))
                {
                    if (_vm.IsWindowVisible)
                    {
                        this.Show();
                        this.WindowState = WindowState.Normal;
                        this.Activate();
                    }
                    else
                    {
                        this.Hide();
                    }
                }
                else if (e.PropertyName == nameof(FloatingWindowViewModel.InterfaceOpacity))
                {
                    UpdateInterfaceOpacity(_vm.InterfaceOpacity);
                }
            };
            // Initial paint from VM state
            UpdateCardDisplayFromViewModel();
            
            // 应用初始主题
            ApplyTheme();
            
            // 测试颜色管理器功能
            TestColorManager();
            
            // 设置窗口加载完成后启用点击穿透
            this.SourceInitialized += FloatingWindow_SourceInitialized;
            
            // 初始化交互计时器
            InitializeInteractionTimer();
            
            // 初始化用户友好错误服务
            UserFriendlyErrorService.Initialize(ErrorNotificationContainer);
        }
        
        private void FloatingWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // 设置窗口句柄到ClickThroughService
            var hwnd = new WindowInteropHelper(this).Handle;
            _clickThroughService.SetWindowHandle(hwnd);
            
            // 禁用点击穿透，允许鼠标操作界面
            _clickThroughService.SetClickThrough(false);
        }

        private void UpdateCardDisplayFromViewModel()
        {
            var card = _vm.CurrentCard;
            if (card == null) return;

            var index = _vm.CurrentCardIndex + 1;
            var total = _vm.Cards.Count;
            cardControl.UpdateDisplay(card, index, total);
            DialogControl.SetCurrentApiConfiguration(card.ApiConfiguration);
        }

        // 实时交互切换由 ViewModel 的 ToggleRealtimeAsyncCommand 处理

        private void InitializeControls()
        {
            // 初始化设置控件（绑定到 SettingsViewModel）
            SettingsControl.Initialize(_appSettings);
            
            // 订阅设置控件事件
            SettingsControl.SettingsClosed += (s, e) => _clickThroughService.SetClickThrough(false);
            SettingsControl.ResetSettingsRequested += (s, e) => ResetSettings();
            SettingsControl.SettingsVisibilityChanged += (s, isVisible) => {
                _vm.IsSettingsVisible = isVisible;
            };
            SettingsControl.ApiConfigurationChanged += async (s, e) => {
                // 自动保存配置更改到数据库
                await ExceptionHandlingService.Database.ExecuteSafelyAsync(
                    () => _appSettings.SaveToDatabaseAsync(),
                    "保存API配置");
                
                _vm.BuildCardsFromAppSettings();
                UpdateCardDisplayFromViewModel();
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
            
            // 初始化对话控件（MVVM）
            var dialogVm = new Buddie.ViewModels.DialogViewModel(_appSettings);
            DialogControl.InitializeViewModel(dialogVm);
            DialogControl.DialogClosed += (s, e) => _clickThroughService.SetClickThrough(false);
            DialogControl.DialogVisibilityChanged += (s, isVisible) => { _vm.IsDialogVisible = isVisible; };
            
            // 初始化卡片控件 - 通过命令绑定触发动作
            cardControl.MouseEntered += (s, e) => _clickThroughService.SetClickThrough(false);
            cardControl.MouseLeft += (s, e) => _clickThroughService.SetClickThrough(false);
            
            // 监听子控件的焦点事件以防止输入时透明化
            SubscribeToFocusEvents();
        }

        // Cards are built and tracked via the ViewModel

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemDrawing.SystemIcons.Application;
            trayIcon.Text = "Buddie";
            trayIcon.Visible = true;

            // 创建右键菜单
            var contextMenu = new ContextMenuStrip();
            
            // 使用一个标志来追踪右键点击状态
            bool rightClickPressed = false;
            
            // 创建菜单项
            var showItem = new ToolStripMenuItem("显示");
            showItem.MouseDown += (sender, e) => {
                rightClickPressed = (e.Button == System.Windows.Forms.MouseButtons.Right);
            };
            showItem.Click += (sender, e) => {
                if (!rightClickPressed)
                {
                    _vm.ShowWindowCommand.Execute(null);
                }
                rightClickPressed = false;
            };
            contextMenu.Items.Add(showItem);
            
            var hideItem = new ToolStripMenuItem("隐藏");
            hideItem.MouseDown += (sender, e) => {
                rightClickPressed = (e.Button == System.Windows.Forms.MouseButtons.Right);
            };
            hideItem.Click += (sender, e) => {
                if (!rightClickPressed)
                {
                    _vm.HideWindowCommand.Execute(null);
                }
                rightClickPressed = false;
            };
            contextMenu.Items.Add(hideItem);
            
            contextMenu.Items.Add("-");
            
            var exitItem = new ToolStripMenuItem("退出");
            exitItem.MouseDown += (sender, e) => {
                rightClickPressed = (e.Button == System.Windows.Forms.MouseButtons.Right);
            };
            exitItem.Click += (sender, e) => {
                if (!rightClickPressed)
                {
                    _vm.ExitApplicationCommand.Execute(null);
                }
                rightClickPressed = false;
            };
            contextMenu.Items.Add(exitItem);
            // 初始化托盘图标到ViewModel
            _vm.InitializeTrayIcon(trayIcon);
            
            // 双击托盘图标显示窗口
            trayIcon.DoubleClick += (sender, e) => {
                _vm.ShowWindowCommand.Execute(null);
            };
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            // 阻止窗口关闭，改为隐藏到托盘
            e.Cancel = true;
            this.Hide();
            
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

        // Card navigation is handled via ViewModel commands and bound buttons.
        
        // Card visuals are updated from the ViewModel
        
        /// <summary>
        /// 测试颜色管理器功能
        /// </summary>
        private void TestColorManager()
        {
            try
            {
                var testResults = CardColorManagerTests.RunAllTests();
                System.Diagnostics.Debug.WriteLine("=== 颜色管理器测试结果 ===");
                System.Diagnostics.Debug.WriteLine(testResults);
                System.Diagnostics.Debug.WriteLine("=========================");
                
                // 在调试模式下显示测试结果
                #if DEBUG
                Console.WriteLine("颜色管理器测试结果:");
                Console.WriteLine(testResults);
                #endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"颜色管理器测试失败: {ex.Message}");
            }
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
                _vm.BuildCardsFromAppSettings();
                UpdateCardDisplayFromViewModel();
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
            _vm.BuildCardsFromAppSettings();
            UpdateCardDisplayFromViewModel();
        }
        
        /// <summary>
        /// 更新展开界面的透明度
        /// </summary>
        /// <param name="opacity">透明度值</param>
        private void UpdateInterfaceOpacity(double opacity)
        {
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
            _vm.SetUserInteracting(interacting);
            
            if (interacting)
            {
                // 用户开始交互，立即设置为不透明
                _vm.InterfaceOpacity = 1.0;
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
                    _vm.InterfaceOpacity = 0.2;
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

    }
}
