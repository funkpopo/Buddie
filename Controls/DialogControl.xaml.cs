using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using NAudio.Wave;
using NAudio.MediaFoundation;
using Markdig;
using System.Windows.Documents;
using Buddie.Database;
using Buddie.Services.Tts;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Buddie.Services.ExceptionHandling;
using Buddie.Services;
using Buddie.ViewModels;
using Microsoft.Extensions.Logging;

namespace Buddie.Controls
{
    public partial class DialogControl : System.Windows.Controls.UserControl
    {
        private DialogViewModel? _viewModel;
        // NAudio-based audio player
        private WaveOutEvent? _currentAudioPlayer;
        private AudioFileReader? _currentAudioReader;

        // UI virtualization - ObservableCollection for messages
        public ObservableCollection<MessageDisplayModel> Messages { get; private set; }

        public event EventHandler<string>? MessageSent;
        public event EventHandler? DialogClosed;
        public event EventHandler<bool>? DialogVisibilityChanged;
        
        private CancellationTokenSource? _currentRequest;
        private bool _isSending = false;
        private bool _isSidebarVisible = false;
        private readonly List<string> _conversationHistory = new List<string>();
        private readonly MarkdownPipeline _markdownPipeline;
        private readonly DatabaseService _databaseService = Buddie.App.GetService<Buddie.Database.DatabaseService>();
        private DbConversation? _currentConversation;
        private List<DbMessage> _currentMessages = new List<DbMessage>();
        
        // 截图相关字段
        private byte[]? _currentScreenshot;
        private bool _hasScreenshot = false;
        
        // 本地图片相关字段
        private byte[]? _currentLocalImage;
        private bool _hasLocalImage = false;
        
        // 当前API配置
        private OpenApiConfiguration? _currentApiConfiguration;
        private readonly ILogger _logger = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) is ILoggerFactory lf ? lf.CreateLogger(typeof(DialogControl).FullName!) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        #region Windows API for Multi-Monitor Support
        
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint GW_OWNER = 4;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
        
        #endregion

        #region Screen Detection Methods
        
        /// <summary>
        /// 获取当前应用窗口所在的屏幕边界
        /// </summary>
        /// <returns>屏幕边界矩形，如果失败则返回主屏幕边界</returns>
        private Rectangle GetCurrentWindowScreen()
        {
            try
            {
                var mainWindow = Window.GetWindow(this);
                if (mainWindow != null)
                {
                    // 方法1：使用WPF窗口位置直接检测
                    var windowBounds = GetWindowScreenByPosition(mainWindow);
                    if (windowBounds.Width > 0 && windowBounds.Height > 0)
                    {
                        _logger.LogDebug("DialogControl: 通过窗口位置检测到屏幕 = {Bounds}", windowBounds);
                        return windowBounds;
                    }

                    // 方法2：使用Windows API检测（备用方法）
                    if (mainWindow.IsLoaded)
                    {
                        var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                        var hwnd = windowInteropHelper.EnsureHandle();
                        
                        _logger.LogDebug("DialogControl: 窗口句柄 = {Handle}", hwnd);
                        
                        if (hwnd != IntPtr.Zero)
                        {
                            var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                            _logger.LogDebug("DialogControl: 监视器句柄 = {Handle}", hMonitor);
                            
                            if (hMonitor != IntPtr.Zero)
                            {
                                var monitorInfo = new MONITORINFO();
                                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                                
                                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                                {
                                    var screenBounds = new Rectangle(
                                        monitorInfo.rcMonitor.Left,
                                        monitorInfo.rcMonitor.Top,
                                        monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
                                        monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top
                                    );
                                    
                                    _logger.LogDebug("DialogControl: 通过Windows API检测到屏幕 = {Bounds}", screenBounds);
                                    
                                    if (screenBounds.Width > 0 && screenBounds.Height > 0)
                                    {
                                        return screenBounds;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("DialogControl: 窗口尚未加载完成，使用主屏幕");
                    }
                }
                else
                {
                    _logger.LogWarning("DialogControl: 无法获取主窗口");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DialogControl: 获取当前窗口屏幕时出错 = {Message}", ex.Message);
                ExceptionHandlingService.ExecuteSafely(() => 
                {
                    throw ex;
                }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
                {
                    Component = "DialogControl",
                    Operation = "获取当前窗口屏幕"
                });
            }

            // 如果检测失败，回退到主屏幕
            _logger.LogInformation("DialogControl: 回退到主屏幕");
            return GetPrimaryScreenBounds();
        }
        
        /// <summary>
        /// 通过窗口位置检测屏幕边界
        /// </summary>
        private Rectangle GetWindowScreenByPosition(Window window)
        {
            try
            {
                // 获取窗口的中心点
                var windowLeft = window.Left;
                var windowTop = window.Top;
                var windowWidth = window.ActualWidth;
                var windowHeight = window.ActualHeight;
                
                // 如果窗口尺寸无效，使用窗口左上角
                if (windowWidth <= 0 || windowHeight <= 0)
                {
                    windowWidth = 100;
                    windowHeight = 100;
                }
                
                var centerX = (int)(windowLeft + windowWidth / 2);
                var centerY = (int)(windowTop + windowHeight / 2);
                
                _logger.LogDebug("DialogControl: 窗口位置=({Left},{Top}), 尺寸=({W},{H}), 中心点=({CX},{CY})", windowLeft, windowTop, windowWidth, windowHeight, centerX, centerY);
                
                // 查找包含窗口中心点的屏幕
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    _logger.LogTrace("DialogControl: 检查屏幕 {Device} = {Bounds}", screen.DeviceName, screen.Bounds);
                    
                    if (screen.Bounds.Contains(centerX, centerY))
                    {
                        _logger.LogDebug("DialogControl: 找到匹配的屏幕 {Device} = {Bounds}", screen.DeviceName, screen.Bounds);
                        return screen.Bounds;
                    }
                }
                
                // 如果中心点不在任何屏幕上，查找距离最近的屏幕
                var nearestScreen = System.Windows.Forms.Screen.AllScreens
                    .OrderBy(s => GetDistanceToScreen(centerX, centerY, s.Bounds))
                    .FirstOrDefault();
                    
                if (nearestScreen != null)
                {
                    _logger.LogDebug("DialogControl: 使用最近的屏幕 {Device} = {Bounds}", nearestScreen.DeviceName, nearestScreen.Bounds);
                    return nearestScreen.Bounds;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DialogControl: 通过窗口位置检测屏幕时出错 = {Message}", ex.Message);
            }
            
            return new Rectangle();
        }
        
        /// <summary>
        /// 计算点到屏幕边界的距离
        /// </summary>
        private double GetDistanceToScreen(int x, int y, Rectangle screenBounds)
        {
            var centerX = screenBounds.X + screenBounds.Width / 2;
            var centerY = screenBounds.Y + screenBounds.Height / 2;
            return Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
        }
        
        /// <summary>
        /// 获取主屏幕边界
        /// </summary>
        private Rectangle GetPrimaryScreenBounds()
        {
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            return primaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        }
        
        /// <summary>
        /// 获取指定屏幕的System.Windows.Forms.Screen对象
        /// </summary>
        /// <param name="screenBounds">屏幕边界</param>
        /// <returns>匹配的Screen对象，如果没找到则返回主屏幕</returns>
        private System.Windows.Forms.Screen GetScreenByBounds(Rectangle screenBounds)
        {
            try
            {
                // 查找匹配边界的屏幕
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (screen.Bounds.Equals(screenBounds))
                    {
                        return screen;
                    }
                }
                
                // 如果没有完全匹配的，找包含屏幕中心点的屏幕
                var centerX = screenBounds.X + screenBounds.Width / 2;
                var centerY = screenBounds.Y + screenBounds.Height / 2;
                var centerPoint = new System.Drawing.Point(centerX, centerY);
                
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (screen.Bounds.Contains(centerPoint))
                    {
                        return screen;
                    }
                }
            }
            catch (Exception ex)
            {
                // 使用ExceptionHandlingService处理异常
                ExceptionHandlingService.ExecuteSafely(() => 
                {
                    throw ex; // 重新抛出以便正确处理
                }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
                {
                    Component = "DialogControl",
                    Operation = "根据边界查找屏幕"
                });
            }

            // 回退到主屏幕
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            if (primaryScreen != null) return primaryScreen;
            
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            if (allScreens.Length > 0) return allScreens[0];
            
            // 这种情况理论上不应该发生，但为了安全起见
            throw new InvalidOperationException("系统中没有检测到任何屏幕");
        }
        
        #endregion

        // 附加属性用于绑定FlowDocument到RichTextBox
        public static readonly DependencyProperty BindableDocumentProperty =
            DependencyProperty.RegisterAttached(
                "BindableDocument",
                typeof(FlowDocument),
                typeof(DialogControl),
                new PropertyMetadata(null, OnBindableDocumentChanged));

        public static FlowDocument GetBindableDocument(DependencyObject obj)
        {
            return (FlowDocument)obj.GetValue(BindableDocumentProperty);
        }

        public static void SetBindableDocument(DependencyObject obj, FlowDocument value)
        {
            obj.SetValue(BindableDocumentProperty, value);
        }

        private static void OnBindableDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.RichTextBox richTextBox && e.NewValue is FlowDocument newDocument)
            {
                richTextBox.Document = newDocument;
            }
        }

        public DialogControl()
        {
            InitializeComponent();
            
            // Initialize messages collection
            Messages = new ObservableCollection<MessageDisplayModel>();
            
            // 初始化Markdown管道，启用常用的扩展
            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
            
            // 初始化NAudio MediaFoundation（用于MP3支持）
            MediaFoundationApi.Startup();
            
            // 加载对话历史
            LoadConversationHistory();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.DragMove();
            }
        }

        private async void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            await ToggleSidebar();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 释放音频资源
            ReleaseAudioResources();
            
            Hide();
            DialogClosed?.Invoke(this, EventArgs.Empty);
        }

        public void InitializeViewModel(DialogViewModel vm)
        {
            _viewModel = vm;
            this.DataContext = vm;
            // Share the same messages collection for virtualization binding
            vm.Messages = this.Messages;
            
            // Subscribe to VM HTTP events to drive UI and persistence
            vm.StreamingStarted += (s, e) =>
            {
                InitializeStreamingMessage();
            };
            vm.StreamingDelta += (s, e) =>
            {
                AppendStreamingDelta(e.Content, e.Reasoning);
            };
            vm.StreamingCompleted += (s, e) =>
            {
                FinalizeStreamingMessage();
                SetSendingState(false);
            };
            vm.ResponseReady += (s, e) =>
            {
                AddMessageBubble(e.Content, false);
                SetSendingState(false);
            };

            vm.SendRequested += (s, message) =>
            {
                if (DialogInput != null)
                {
                    DialogInput.Text = message ?? string.Empty;
                }
                SendMessage_Click(this, new RoutedEventArgs());
            };
            // Copy and TTS are now handled fully in ViewModel via commands
            vm.ScreenshotRequested += (s, e) =>
            {
                ScreenshotButton_Click(this, new RoutedEventArgs());
            };
            vm.ImageUploadRequested += (s, e) =>
            {
                ImageUploadButton_Click(this, new RoutedEventArgs());
            };
            vm.ToggleSidebarRequested += async (s, e) =>
            {
                await ToggleSidebar();
                // Also refresh conversations when opening
                if (_isSidebarVisible && _viewModel != null)
                {
                    await _viewModel.RefreshConversationsAsync();
                }
            };
            vm.CloseRequested += (s, e) =>
            {
                CloseButton_Click(this, new RoutedEventArgs());
            };
            vm.NewConversationRequested += async (s, e) =>
            {
                await StartNewConversation();
            };
            vm.RemoveScreenshotRequested += (s, e) =>
            {
                ClearScreenshot();
            };
            vm.OpenScreenshotRequested += (s, e) =>
            {
                ScreenshotThumbnail_Click(this, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
            };
            vm.DeleteConversationRequested += async (s, id) =>
            {
                await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
                {
                    var result = System.Windows.MessageBox.Show(
                        Buddie.Localization.LocalizationManager.GetString("Confirm_DeleteConversation_Message"),
                        Buddie.Localization.LocalizationManager.GetString("Confirm_DeleteConversation_Title"),
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await DeleteConversation(id);
                        await RefreshConversationsList();
                    }
                }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
                {
                    Component = "DialogControl",
                    Operation = "删除对话（VM）"
                });
            };
        }

        private async void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (_isSending)
            {
                // 如果正在发送，执行中断操作
                _viewModel?.CancelSend();
                AddMessageBubble("对话已中断", false);
                // 重置发送状态
                SetSendingState(false);
                return;
            }

            var message = DialogInput.Text.Trim();
            
            // 检查是否有内容需要发送（文字或图片）
            if (!string.IsNullOrEmpty(message) || _hasScreenshot || _hasLocalImage)
            {
                // 如果有截图或本地图片但没有文字，提供默认文字
                if (string.IsNullOrEmpty(message))
                {
                    if (_hasScreenshot)
                    {
                        message = "请分析这张截图。";
                    }
                    else if (_hasLocalImage)
                    {
                        message = "请分析这张图片。";
                    }
                }
                
                // 添加到历史记录
                _conversationHistory.Add(message);
                
                // 更新UI状态
                SetSendingState(true);
                
                // 先将用户消息显示并保存
                bool hasScreenshot = _hasScreenshot && _currentScreenshot != null;
                bool hasLocalImage = _hasLocalImage && _currentLocalImage != null;
                bool hasImage = hasScreenshot || hasLocalImage;
                bool configSupportsMultimodal = _currentApiConfiguration?.IsMultimodalEnabled ?? false;
                bool channelSupportsMultimodal = _currentApiConfiguration != null && Buddie.Services.MultimodalApiService.SupportsMultimodal(_currentApiConfiguration.ChannelType);
                byte[]? imageForDisplay = null;
                if (hasImage && configSupportsMultimodal && channelSupportsMultimodal)
                {
                    imageForDisplay = hasScreenshot ? _currentScreenshot : _currentLocalImage;
                }
                await AddMessageBubble(message, true, imageForDisplay);

                // 调用ViewModel发送HTTP请求
                _viewModel!.CurrentApiConfiguration = _currentApiConfiguration;
                _viewModel.ScreenshotImage = _currentScreenshot;
                _viewModel.LocalImage = _currentLocalImage;
                // 通过命令触发发送，避免直接调用私有方法
                _viewModel.SendMessageToApiCommand.Execute(message);
                
                DialogInput.Clear();
                
                // 发送后清理截图和本地图片
                if (_hasScreenshot || _hasLocalImage)
                {
                    _currentScreenshot = null;
                    _hasScreenshot = false;
                    _currentLocalImage = null;
                    _hasLocalImage = false;
                    ScreenshotPreviewContainer.Visibility = Visibility.Collapsed;
                    // 同步VM中的图像状态
                    if (_viewModel != null)
                    {
                        _viewModel.ScreenshotImage = null;
                        _viewModel.LocalImage = null;
                    }
                }
            }
        }

        private void SetSendingState(bool sending)
        {
            _isSending = sending;
            if (sending)
            {
                SendButton.Content = "中断";
                SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.OrangeRed);
            }
            else
            {
                SendButton.Content = "发送";
                SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
                _currentRequest?.Dispose();
                _currentRequest = null;
            }
        }

        /// &lt;summary&gt;
        /// 重置发送状态
        /// &lt;/summary&gt;
        public void ResetSendingState()
        {
            SetSendingState(false);
        }

        /// &lt;summary&gt;
        /// 显示对话界面
        /// &lt;/summary&gt;
        public void Show()
        {
            DialogInterface.Visibility = Visibility.Visible;
            DialogInterface.Opacity = 1.0; // 初始显示时完全不透明
            DialogVisibilityChanged?.Invoke(this, true);
        }

        /// &lt;summary&gt;
        /// 切换对话界面显示状态
        /// &lt;/summary&gt;
        public void Toggle()
        {
            if (IsVisible)
            {
                // 如果已经可见，隐藏界面
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// &lt;summary&gt;
        /// 将控件置于最前面
        /// &lt;/summary&gt;
        public void BringToFront()
        {
            // 通过重新设置Panel.ZIndex将控件置于最前面
            var parent = this.Parent as System.Windows.Controls.Panel;
            if (parent != null)
            {
                var maxZ = 0;
                foreach (UIElement child in parent.Children)
                {
                    var z = System.Windows.Controls.Panel.GetZIndex(child);
                    if (z > maxZ) maxZ = z;
                }
                System.Windows.Controls.Panel.SetZIndex(this, maxZ + 1);
            }
        }

        /// &lt;summary&gt;
        /// 隐藏对话界面
        /// &lt;/summary&gt;
        public void Hide()
        {
            DialogInterface.Visibility = Visibility.Collapsed;
            DialogVisibilityChanged?.Invoke(this, false);
        }

        public new bool IsVisible => DialogInterface.Visibility == Visibility.Visible;

        /// <summary>
        /// 设置当前API配置并更新UI
        /// </summary>
        /// <param name="apiConfiguration">API配置</param>
        public void SetCurrentApiConfiguration(OpenApiConfiguration? apiConfiguration)
        {
            _currentApiConfiguration = apiConfiguration;
            UpdateScreenshotButtonVisibility();
            if (_viewModel != null)
            {
                _viewModel.CurrentApiConfiguration = apiConfiguration;
            }
        }
        
        /// <summary>
        /// 根据当前API配置更新截图按钮和图片上传按钮的显示状态
        /// </summary>
        private void UpdateScreenshotButtonVisibility()
        {
            if (_currentApiConfiguration != null && _currentApiConfiguration.IsMultimodalEnabled)
            {
                ScreenshotButton.Visibility = Visibility.Visible;
                ImageUploadButton.Visibility = Visibility.Visible;
            }
            else
            {
                ScreenshotButton.Visibility = Visibility.Collapsed;
                ImageUploadButton.Visibility = Visibility.Collapsed;
                // 如果隐藏按钮时还有截图或本地图片，清除它们
                if (_hasScreenshot || _hasLocalImage)
                {
                    ClearScreenshot();
                }
            }
        }
        
        /// <summary>
        /// 清除截图
        /// </summary>
        private void ClearScreenshot()
        {
            _currentScreenshot = null;
            _hasScreenshot = false;
            _currentLocalImage = null;
            _hasLocalImage = false;
            ScreenshotPreviewContainer.Visibility = Visibility.Collapsed;
            if (_viewModel != null)
            {
                _viewModel.ScreenshotImage = null;
                _viewModel.LocalImage = null;
            }
        }

        /// <summary>
        /// 设置对话界面透明度
        /// </summary>
        /// <param name="opacity">透明度值</param>
        public void SetOpacity(double opacity)
        {
            DialogInterface.Opacity = opacity;
        }

        public void AddMessage(string message, bool isUser = true)
        {
            var messageModel = new MessageDisplayModel
            {
                Content = message,
                IsUser = isUser,
                Timestamp = DateTime.Now
            };
            
            Messages.Add(messageModel);
            ScrollToBottom();
        }

        public async void AddMessageBubble(string message, bool isUser = true)
        {
            await AddMessageBubble(message, isUser, null);
        }

        public async Task AddMessageBubble(string message, bool isUser, byte[]? imageData)
        {
            var messageModel = new MessageDisplayModel
            {
                Content = message,
                IsUser = isUser,
                Timestamp = DateTime.Now,
                ImageData = imageData,
                HasImage = imageData != null && imageData.Length > 0
            };
            
            // 检查AI回复是否包含Markdown内容
            if (!isUser && ContainsMarkdown(message))
            {
                messageModel.IsMarkdownContent = true;
                // 对长内容延迟渲染，避免阻塞UI
                if (message?.Length > 800)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        messageModel.RenderedDocument = ConvertMarkdownToFlowDocument(message);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    messageModel.RenderedDocument = ConvertMarkdownToFlowDocument(message);
                }
            }
            
            Messages.Add(messageModel);
            ScrollToBottom();
            
            // 自动保存消息到数据库（委托给 ViewModel）
            if (_viewModel != null)
            {
                await _viewModel.SaveMessageAsync(message, isUser, null, imageData);
            }
        }

        private Border CreateMessageBubble(string message, bool isUser)
        {
            FrameworkElement contentElement;
            
            if (!isUser && ContainsMarkdown(message))
            {
                // 对于AI回复，如果包含Markdown内容，使用RichTextBox渲染
                contentElement = CreateMarkdownRichTextBox(message);
            }
            else
            {
                // 普通文本使用TextBlock
                contentElement = new TextBlock
                {
                    Text = message,
                    Padding = new Thickness(12, 8, 12, 8),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300,
                    FontSize = 13,
                    LineHeight = 18
                };
            }

            // 应用当前主题样式
            ApplyMessageBubbleTheme(contentElement, isUser);

            // 检查是否有TTS配置和是否为AI回复
            var hasButtons = false;
            var appSettings = DataContext as AppSettings;
            var ttsConfig = appSettings?.GetActiveTtsConfiguration();
            
            // 为AI回复显示播放按钮
            if (!isUser && ttsConfig != null)
            {
                hasButtons = true;
            }

            FrameworkElement bubbleContent;
            if (hasButtons)
            {
                // 创建包含内容和按钮的Grid
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 内容区域
                contentElement.Margin = new Thickness(0, 0, 0, 5);
                Grid.SetRow(contentElement, 0);
                grid.Children.Add(contentElement);

                // 按钮区域
                var buttonPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 8, 5)
                };

                // 创建TTS按钮（喇叭图标）
                var ttsButton = new System.Windows.Controls.Button
                {
                    Content = "🔊",
                    Width = 24,
                    Height = 24,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Margin = new Thickness(5, 0, 0, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "朗读",
                    Tag = message // 存储消息内容供TTS使用
                };
                ttsButton.Click += TtsButton_Click;

                // 创建复制按钮
                var copyButton = new System.Windows.Controls.Button
                {
                    Content = "📋",
                    Width = 24,
                    Height = 24,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Margin = new Thickness(5, 0, 0, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "复制",
                    Tag = message // 存储消息内容供复制使用
                };
                copyButton.Click += CopyButton_Click;

                buttonPanel.Children.Add(copyButton);
                buttonPanel.Children.Add(ttsButton);

                Grid.SetRow(buttonPanel, 1);
                grid.Children.Add(buttonPanel);

                bubbleContent = grid;
            }
            else if (isUser)
            {
                // 用户消息，在左下角添加复制按钮
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 内容区域
                contentElement.Margin = new Thickness(0, 0, 0, 5);
                Grid.SetRow(contentElement, 0);
                grid.Children.Add(contentElement);

                // 按钮区域（左对齐）
                var buttonPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Margin = new Thickness(8, 0, 0, 5)
                };

                // 创建复制按钮
                var copyButton = new System.Windows.Controls.Button
                {
                    Content = "📋",
                    Width = 24,
                    Height = 24,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "复制",
                    Tag = message // 存储消息内容供复制使用
                };
                copyButton.Click += CopyButton_Click;

                buttonPanel.Children.Add(copyButton);

                Grid.SetRow(buttonPanel, 1);
                grid.Children.Add(buttonPanel);

                bubbleContent = grid;
            }
            else
            {
                bubbleContent = contentElement;
            }

            // 设置圆角和阴影
            var border = new Border
            {
                Child = bubbleContent,
                CornerRadius = new CornerRadius(18),
                HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left,
                Margin = isUser ? new Thickness(50, 5, 10, 5) : new Thickness(10, 5, 50, 5),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    Direction = 270,
                    ShadowDepth = 1,
                    Opacity = 0.1,
                    BlurRadius = 3
                }
            };

            // 设置背景颜色到Border
            if (contentElement is TextBlock tb)
            {
                border.Background = tb.Background;
                tb.Background = System.Windows.Media.Brushes.Transparent;
            }
            else if (contentElement is System.Windows.Controls.RichTextBox rtb)
            {
                border.Background = rtb.Background;
                rtb.Background = System.Windows.Media.Brushes.Transparent;
                rtb.BorderThickness = new Thickness(0);
            }

            return border;
        }

        private bool ContainsMarkdown(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // 检测常见的Markdown语法
            return text.Contains("**") ||        // 粗体
                   text.Contains("*") ||         // 斜体
                   text.Contains("```") ||       // 代码块
                   text.Contains("`") ||         // 内联代码
                   text.Contains("# ") ||        // 标题
                   text.Contains("## ") ||       // 标题
                   text.Contains("### ") ||      // 标题
                   text.Contains("- ") ||        // 列表
                   text.Contains("1. ") ||       // 有序列表
                   text.Contains("[") && text.Contains("]("); // 链接
        }

        /// <summary>
        /// 将Markdown内容转换为FlowDocument用于数据绑定
        /// </summary>
        private FlowDocument ConvertMarkdownToFlowDocument(string markdownText)
        {
            return ExceptionHandlingService.ExecuteSafely(() =>
            {
                // 将Markdown转换为HTML
                var html = Markdown.ToHtml(markdownText, _markdownPipeline);
                
                // 将HTML转换为FlowDocument
                return ConvertHtmlToFlowDocument(html);
            }, 
            ExceptionHandlingService.HandlingStrategy.LogOnly,
            new FlowDocument(new Paragraph(new Run(markdownText))), // 回退到普通文本
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "Markdown转换"
            });
        }

        private System.Windows.Controls.RichTextBox CreateMarkdownRichTextBox(string markdownText)
        {
            var richTextBox = new System.Windows.Controls.RichTextBox
            {
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 320,
                FontSize = 13,
                IsReadOnly = true,
                IsDocumentEnabled = true,
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            richTextBox.Document = ConvertMarkdownToFlowDocument(markdownText);
            return richTextBox;
        }

        private FlowDocument ConvertHtmlToFlowDocument(string html)
        {
            var flowDocument = new FlowDocument
            {
                FontSize = 13,
                LineHeight = 18,
                PagePadding = new Thickness(0)
            };

            // 预处理HTML，处理段落标签
            html = ProcessHtmlParagraphs(html);
            
            var lines = html.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            Paragraph? currentParagraph = null;
            bool inCodeBlock = false;
            bool inList = false;
            var codeBlockContent = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.Trim();

                // 处理代码块
                if (trimmedLine.StartsWith("<pre>") || trimmedLine.Contains("<pre>"))
                {
                    FinalizeParagraph(flowDocument, ref currentParagraph);
                    inCodeBlock = true;
                    codeBlockContent.Clear();
                    continue;
                }
                else if (trimmedLine.StartsWith("</pre>") || trimmedLine.Contains("</pre>"))
                {
                    if (inCodeBlock && codeBlockContent.Length > 0)
                    {
                        CreateCodeBlock(flowDocument, codeBlockContent.ToString());
                    }
                    inCodeBlock = false;
                    codeBlockContent.Clear();
                    continue;
                }
                else if (inCodeBlock)
                {
                    // 在代码块中，保留原始格式
                    var codeText = System.Text.RegularExpressions.Regex.Replace(line, "<[^>]*>", "");
                    if (codeBlockContent.Length > 0)
                        codeBlockContent.AppendLine();
                    codeBlockContent.Append(codeText);
                    continue;
                }

                // 处理空行 - 创建段落分隔
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    FinalizeParagraph(flowDocument, ref currentParagraph);
                    inList = false;
                    continue;
                }

                // 处理标题
                if (trimmedLine.StartsWith("<h"))
                {
                    FinalizeParagraph(flowDocument, ref currentParagraph);
                    CreateHeading(flowDocument, trimmedLine);
                    inList = false;
                    continue;
                }

                // 处理列表项
                if (trimmedLine.StartsWith("<li>"))
                {
                    if (!inList)
                    {
                        FinalizeParagraph(flowDocument, ref currentParagraph);
                        inList = true;
                    }
                    CreateListItem(flowDocument, trimmedLine);
                    continue;
                }
                else if (inList && !trimmedLine.StartsWith("<li>"))
                {
                    inList = false;
                }

                // 处理普通内容
                if (currentParagraph == null)
                {
                    currentParagraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
                }

                ProcessTextLine(currentParagraph, trimmedLine);
                
                // 如果不是最后一行，且下一行不为空，添加软换行
                if (i < lines.Length - 1 && !string.IsNullOrWhiteSpace(lines[i + 1]?.Trim()))
                {
                    var nextTrimmed = lines[i + 1].Trim();
                    if (!nextTrimmed.StartsWith("<h") && !nextTrimmed.StartsWith("<li>") && 
                        !nextTrimmed.StartsWith("<pre>") && !nextTrimmed.StartsWith("</pre>"))
                    {
                        currentParagraph.Inlines.Add(new LineBreak());
                    }
                }
            }

            // 完成最后的段落
            FinalizeParagraph(flowDocument, ref currentParagraph);

            return flowDocument;
        }

        private string ProcessHtmlParagraphs(string html)
        {
            // 将<p>标签转换为双换行符，</p>标签移除
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<p[^>]*>", "\n\n");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"</p>", "");
            
            // 清理多余的换行符
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\n{3,}", "\n\n");
            
            return html;
        }

        private void FinalizeParagraph(FlowDocument flowDocument, ref Paragraph? currentParagraph)
        {
            if (currentParagraph != null && currentParagraph.Inlines.Count > 0)
            {
                flowDocument.Blocks.Add(currentParagraph);
                currentParagraph = null;
            }
        }

        private void CreateCodeBlock(FlowDocument flowDocument, string codeContent)
        {
            var codeRun = new Run(codeContent)
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas, 'Courier New', monospace")
            };

            var codeBlock = new Paragraph(codeRun)
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas, 'Courier New', monospace"),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 8),
                BorderThickness = new Thickness(1)
            };

            // 根据当前主题设置样式
            var appSettings = DataContext as AppSettings;
            if (appSettings?.IsDarkTheme == true)
            {
                codeBlock.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40));
                codeBlock.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80));
                codeRun.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220));
            }
            else
            {
                codeBlock.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 248, 248));
                codeBlock.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 225, 225));
                codeRun.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 37, 41));
            }

            flowDocument.Blocks.Add(codeBlock);
        }

        private void CreateHeading(FlowDocument flowDocument, string headingHtml)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(headingHtml, "<[^>]*>", "");
            var headingSize = headingHtml.StartsWith("<h1") ? 18 : 
                             headingHtml.StartsWith("<h2") ? 16 : 
                             headingHtml.StartsWith("<h3") ? 14 : 13;
            
            var headingParagraph = new Paragraph(new Run(text))
            {
                FontWeight = FontWeights.Bold,
                FontSize = headingSize,
                Margin = new Thickness(0, 8, 0, 4)
            };
            flowDocument.Blocks.Add(headingParagraph);
        }

        private void CreateListItem(FlowDocument flowDocument, string listItemHtml)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(listItemHtml, "<[^>]*>", "");
            var listItem = new Paragraph(new Run("• " + text))
            {
                Margin = new Thickness(16, 1, 0, 1)
            };
            flowDocument.Blocks.Add(listItem);
        }

        private void ProcessTextLine(Paragraph paragraph, string html)
        {
            // 处理混合格式的文本行
            if (html.Contains("<strong>") || html.Contains("<b>") || 
                html.Contains("<em>") || html.Contains("<i>") || 
                html.Contains("<code>"))
            {
                ProcessComplexInlineFormatting(paragraph, html);
            }
            else
            {
                // 普通文本
                var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
                if (!string.IsNullOrEmpty(text))
                {
                    paragraph.Inlines.Add(new Run(text));
                }
            }
        }

        private void ProcessComplexInlineFormatting(Paragraph paragraph, string html)
        {
            // 移除段落标签
            html = System.Text.RegularExpressions.Regex.Replace(html, @"</?p[^>]*>", "");
            
            var segments = new List<(string text, bool isBold, bool isItalic, bool isCode)>();
            var currentIndex = 0;
            
            // 使用正则表达式找到所有格式化标签
            var formatPattern = @"<(strong|b|em|i|code)>(.*?)</\1>";
            var matches = System.Text.RegularExpressions.Regex.Matches(html, formatPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // 添加标签前的普通文本
                if (match.Index > currentIndex)
                {
                    var beforeText = html.Substring(currentIndex, match.Index - currentIndex);
                    beforeText = System.Text.RegularExpressions.Regex.Replace(beforeText, "<[^>]*>", "");
                    if (!string.IsNullOrEmpty(beforeText))
                    {
                        segments.Add((beforeText, false, false, false));
                    }
                }
                
                // 确定格式类型
                var tag = match.Groups[1].Value.ToLower();
                var content = match.Groups[2].Value;
                bool isBold = tag == "strong" || tag == "b";
                bool isItalic = tag == "em" || tag == "i";
                bool isCode = tag == "code";
                
                segments.Add((content, isBold, isItalic, isCode));
                currentIndex = match.Index + match.Length;
            }
            
            // 添加剩余的文本
            if (currentIndex < html.Length)
            {
                var remainingText = html.Substring(currentIndex);
                remainingText = System.Text.RegularExpressions.Regex.Replace(remainingText, "<[^>]*>", "");
                if (!string.IsNullOrEmpty(remainingText))
                {
                    segments.Add((remainingText, false, false, false));
                }
            }
            
            // 创建Run元素
            foreach (var segment in segments)
            {
                if (string.IsNullOrEmpty(segment.text)) continue;
                
                var run = new Run(segment.text);
                
                if (segment.isBold)
                    run.FontWeight = FontWeights.Bold;
                if (segment.isItalic)
                    run.FontStyle = FontStyles.Italic;
                if (segment.isCode)
                {
                    run.FontFamily = new System.Windows.Media.FontFamily("Consolas, 'Courier New', monospace");
                    
                    // 根据当前主题设置内联代码样式
                    var appSettings = DataContext as AppSettings;
                    if (appSettings?.IsDarkTheme == true)
                    {
                        run.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50));
                        run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 121, 198));
                    }
                    else
                    {
                        run.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
                        run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(199, 37, 78));
                    }
                }
                
                paragraph.Inlines.Add(run);
            }
        }

        private void ProcessInlineFormatting(Paragraph paragraph, string html, string tag, FontWeight fontWeight, System.Windows.FontStyle fontStyle = default)
        {
            var pattern = $"<{tag}>(.*?)</{tag}>";
            var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern);
            
            var lastIndex = 0;
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // 添加标签前的文本
                if (match.Index > lastIndex)
                {
                    var beforeText = html.Substring(lastIndex, match.Index - lastIndex);
                    beforeText = System.Text.RegularExpressions.Regex.Replace(beforeText, "<[^>]*>", "");
                    if (!string.IsNullOrEmpty(beforeText))
                        paragraph.Inlines.Add(new Run(beforeText));
                }

                // 添加格式化的文本
                var formattedRun = new Run(match.Groups[1].Value)
                {
                    FontWeight = fontWeight
                };
                if (fontStyle != default(System.Windows.FontStyle))
                    formattedRun.FontStyle = fontStyle;
                
                paragraph.Inlines.Add(formattedRun);
                lastIndex = match.Index + match.Length;
            }

            // 添加剩余的文本
            if (lastIndex < html.Length)
            {
                var remainingText = html.Substring(lastIndex);
                remainingText = System.Text.RegularExpressions.Regex.Replace(remainingText, "<[^>]*>", "");
                if (!string.IsNullOrEmpty(remainingText))
                    paragraph.Inlines.Add(new Run(remainingText));
            }
        }

        /// <summary>
        /// Scroll to the bottom of the message list
        /// </summary>
        private void ScrollToBottom()
        {
            if (Messages.Count > 0)
            {
                var lastMessage = Messages.LastOrDefault();
                if (lastMessage != null)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DialogMessagesList?.ScrollIntoView(lastMessage);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        public void AddMessageBubbleWithReasoning(string? content, string? reasoningContent = null)
        {
            var messageModel = new MessageDisplayModel
            {
                Content = content ?? "",
                IsUser = false,
                ReasoningContent = reasoningContent,
                Timestamp = DateTime.Now
            };
            
            Messages.Add(messageModel);
            ScrollToBottom();
        }

        public void ClearMessages()
        {
            Messages.Clear();
        }

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
            
            // With data binding and templates, theme changes are automatically handled
            // No need to manually refresh message bubbles
        }

        private void ApplyDarkTheme()
        {
            // 主界面背景
            DialogInterface.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 44, 52));
            
            // 标题栏背景和文字
            var titleElements = LogicalTreeHelper.GetChildren(DialogInterface);
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
            
            // 侧边栏
            HistorySidebar.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 39, 47));
            HistorySidebar.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 64, 72));
            
            // 侧边栏标题文字
            UpdateTextElementsColor(HistorySidebar, System.Windows.Media.Brushes.White);
            
            // 输入框文字颜色
            DialogInput.Foreground = System.Windows.Media.Brushes.White;
            
            // 发送按钮样式调整
            SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
            SendButton.Foreground = System.Windows.Media.Brushes.White;
        }

        private void ApplyLightTheme()
        {
            // 主界面背景
            DialogInterface.Background = System.Windows.Media.Brushes.White;
            
            // 标题栏背景和文字
            var titleElements = LogicalTreeHelper.GetChildren(DialogInterface);
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
            
            // 侧边栏
            HistorySidebar.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 251, 252));
            HistorySidebar.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 228, 232));
            
            // 侧边栏标题文字
            UpdateTextElementsColor(HistorySidebar, System.Windows.Media.Brushes.Black);
            
            // 输入框文字颜色
            DialogInput.Foreground = System.Windows.Media.Brushes.Black;
            
            // 发送按钮样式调整
            SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
            SendButton.Foreground = System.Windows.Media.Brushes.White;
        }

        private void UpdateTextElementsColor(DependencyObject parent, System.Windows.Media.Brush color)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is TextBlock textBlock)
                {
                    textBlock.Foreground = color;
                }
                
                UpdateTextElementsColor(child, color);
            }
        }

        // This method is no longer needed with data binding and templates
        // Theme changes are handled automatically in the XAML templates

        /// <summary>
        /// 应用主题样式到消息气泡内容元素
        /// </summary>
        private void ApplyMessageBubbleTheme(FrameworkElement contentElement, bool isUser)
        {
            var settings = DataContext as AppSettings;
            var isDarkTheme = settings?.IsDarkTheme ?? false;
            
            // 设置内容颜色
            if (isDarkTheme)
            {
                if (contentElement is TextBlock textBlock)
                {
                    textBlock.Foreground = System.Windows.Media.Brushes.White;
                    textBlock.Background = isUser ? 
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 132, 255)) : 
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 60));
                }
                else if (contentElement is System.Windows.Controls.RichTextBox richTextBox)
                {
                    richTextBox.Foreground = System.Windows.Media.Brushes.White;
                    richTextBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 60));
                }
            }
            else
            {
                if (contentElement is TextBlock textBlock)
                {
                    textBlock.Foreground = isUser ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black;
                    textBlock.Background = isUser ? 
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 132, 255)) : 
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
                }
                else if (contentElement is System.Windows.Controls.RichTextBox richTextBox)
                {
                    richTextBox.Foreground = System.Windows.Media.Brushes.Black;
                    richTextBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
                }
            }
        }

        // Obsolete methods removed - UI virtualization with data binding handles theming automatically
        
        private MessageDisplayModel? _currentStreamingMessage;
        private readonly StringBuilder _streamingContent = new StringBuilder();
        private readonly StringBuilder _streamingReasoning = new StringBuilder();

        public async Task SendMessageToApi(string message, OpenApiConfiguration apiConfig)
        {
            // 检查是否应该发送多模态消息
            // 条件：1. 有截图或本地图片数据 2. API配置支持多模态 3. 渠道支持多模态
            bool hasScreenshot = _hasScreenshot && _currentScreenshot != null;
            bool hasLocalImage = _hasLocalImage && _currentLocalImage != null;
            bool hasImage = hasScreenshot || hasLocalImage;
            bool configSupportsMultimodal = apiConfig.IsMultimodalEnabled;
            bool channelSupportsMultimodal = MultimodalApiService.SupportsMultimodal(apiConfig.ChannelType);
            
            bool isMultimodal = hasImage && configSupportsMultimodal && channelSupportsMultimodal;
            
            // 调试信息
            System.Diagnostics.Debug.WriteLine($"多模态检测: 有截图={hasScreenshot}, 有本地图片={hasLocalImage}, 配置支持={configSupportsMultimodal}, 渠道支持={channelSupportsMultimodal}, 最终结果={isMultimodal}");
            if (hasImage)
            {
                var imageData = hasScreenshot ? _currentScreenshot : _currentLocalImage;
                System.Diagnostics.Debug.WriteLine($"图片大小: {imageData?.Length ?? 0} bytes");
            }
            
            // 显示用户消息（包含图片信息）
            byte[]? imageForDisplay = null;
            if (isMultimodal)
            {
                imageForDisplay = hasScreenshot ? _currentScreenshot : _currentLocalImage;
            }
            await AddMessageBubble(message, true, imageForDisplay);

            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // 初始化请求
                _currentRequest = new CancellationTokenSource();
                
                // 创建HTTP客户端
                using var httpClient = CreateHttpClient(apiConfig);
                
                // 构建请求内容
                var requestContent = BuildRequestContent(message, isMultimodal, apiConfig);
                
                // 处理API响应
                await ProcessApiResponse(httpClient, apiConfig, requestContent);
                
                // 清除当前截图或本地图片（如果有）
                if (isMultimodal && (_currentScreenshot != null || _currentLocalImage != null))
                {
                    ClearScreenshot();
                }
                
                // 恢复发送状态
                SetSendingState(false);
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "发送API请求"
            });
        }
        

        
        /// <summary>
        /// 创建配置好的HTTP客户端
        /// </summary>
        private HttpClient CreateHttpClient(OpenApiConfiguration apiConfig)
        {
            var factory = App.GetService<System.Net.Http.IHttpClientFactory>();
            var httpClient = factory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            
            // 根据渠道类型设置不同的认证头
            switch (apiConfig.ChannelType)
            {
                case ChannelType.GoogleGemini:
                    // Gemini使用key参数而非Authorization头
                    // 在URL中处理
                    break;
                case ChannelType.AnthropicClaude:
                    httpClient.DefaultRequestHeaders.Add("x-api-key", apiConfig.ApiKey);
                    httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    break;
                default:
                    // OpenAI格式：包括OpenAI、智谱GLM、通义千问、硅基流动等
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiConfig.ApiKey}");
                    break;
            }
            
            return httpClient;
        }
        
        /// <summary>
        /// 构建API请求内容
        /// </summary>
        private StringContent BuildRequestContent(string actualMessage, bool isMultimodal, OpenApiConfiguration apiConfig)
        {
            object requestBody;
            
            if (isMultimodal)
            {
                // 获取图片数据（优先截图，其次本地图片）
                byte[]? imageData = null;
                if (_hasScreenshot && _currentScreenshot != null)
                {
                    imageData = _currentScreenshot;
                }
                else if (_hasLocalImage && _currentLocalImage != null)
                {
                    imageData = _currentLocalImage;
                }
                
                if (imageData != null)
                {
                    var imageBase64 = ConvertImageToBase64(imageData);
                    System.Diagnostics.Debug.WriteLine($"构建多模态请求，图片Base64长度: {imageBase64.Length}");
                    requestBody = MultimodalApiService.BuildMultimodalRequest(actualMessage, imageBase64, apiConfig);
                }
                else
                {
                    // 如果没有图片数据，回退到普通文本请求
                    requestBody = MultimodalApiService.BuildTextRequest(actualMessage, apiConfig);
                }
            }
            else
            {
                requestBody = MultimodalApiService.BuildTextRequest(actualMessage, apiConfig);
            }
            
            var json = JsonSerializer.Serialize(requestBody);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }
        

        
        /// <summary>
        /// 处理API响应
        /// </summary>
        private async Task ProcessApiResponse(HttpClient httpClient, OpenApiConfiguration apiConfig, StringContent requestContent)
        {
            // 构建最终的API URL（对于Gemini需要添加key参数）
            var finalApiUrl = BuildFinalApiUrl(apiConfig);
            
            if (apiConfig.IsStreamingEnabled)
            {
                await ProcessStreamingResponse(httpClient, finalApiUrl, requestContent, apiConfig);
            }
            else
            {
                await ProcessNonStreamingResponse(httpClient, finalApiUrl, requestContent, apiConfig);
            }
        }

        /// <summary>
        /// 构建最终的API URL
        /// </summary>
        private string BuildFinalApiUrl(OpenApiConfiguration apiConfig)
        {
            return apiConfig.ChannelType switch
            {
                ChannelType.GoogleGemini => $"{apiConfig.ApiUrl}?key={apiConfig.ApiKey}",
                _ => apiConfig.ApiUrl
            };
        }
        
        /// <summary>
        /// 处理非流式响应
        /// </summary>
        private async Task ProcessNonStreamingResponse(HttpClient httpClient, string apiUrl, StringContent requestContent, OpenApiConfiguration apiConfig)
        {
            var response = await httpClient.PostAsync(apiUrl, requestContent, _currentRequest?.Token ?? CancellationToken.None);
            var responseText = await response.Content.ReadAsStringAsync();
            
            _currentRequest?.Token.ThrowIfCancellationRequested();
            
            if (response.IsSuccessStatusCode)
            {
                var messageContent = ParseApiResponse(responseText, apiConfig);
                AddMessageBubble(messageContent, false);
            }
            else
            {
                AddMessageBubble($"API请求失败: {response.StatusCode}", false);
            }
        }
        
        /// <summary>
        /// 解析API响应内容
        /// </summary>
        private string ParseApiResponse(string responseText, OpenApiConfiguration apiConfig)
        {
            return ApiResponseService.ParseNonStreamingResponse(responseText, apiConfig.ChannelType);
        }

        private async Task ProcessStreamingResponse(HttpClient httpClient, string apiUrl, StringContent requestContent, OpenApiConfiguration apiConfig)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // 发送HTTP请求
                var response = await SendStreamingRequest(httpClient, apiUrl, requestContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    AddMessageBubble($"API请求失败: {response.StatusCode}", false);
                    return;
                }

                // 初始化流式消息显示
                InitializeStreamingMessage();

                // 处理流式数据
                await ProcessStreamData(response, apiConfig.ChannelType);
                
                // 完成流式输出
                await Dispatcher.InvokeAsync(() => FinalizeStreamingMessage());
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl", 
                Operation = "处理流式响应"
            });
        }
        
        /// <summary>
        /// 发送流式请求
        /// </summary>
        private async Task<HttpResponseMessage> SendStreamingRequest(HttpClient httpClient, string apiUrl, StringContent requestContent)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = requestContent
            };
            
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, 
                _currentRequest?.Token ?? CancellationToken.None);
        }
        
        /// <summary>
        /// 处理流式数据
        /// </summary>
        private async Task ProcessStreamData(HttpResponseMessage response, ChannelType channelType)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);
            
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                _currentRequest?.Token.ThrowIfCancellationRequested();
                
                if (line.StartsWith("data:"))
                {
                    ProcessStreamDataLine(line, channelType);
                }
            }
        }
        
        /// <summary>
        /// 处理单行流式数据
        /// </summary>
        private void ProcessStreamDataLine(string line, ChannelType channelType)
        {
            var jsonData = line.Substring(5).Trim();
            
            if (jsonData == "[DONE]")
            {
                return;
            }
            
            if (!string.IsNullOrEmpty(jsonData))
            {
                ParseAndUpdateStreamingContent(jsonData, channelType);
            }
        }
        
        /// <summary>
        /// 解析并更新流式内容
        /// </summary>
        private void ParseAndUpdateStreamingContent(string jsonData, ChannelType channelType)
        {
            ProcessDeltaContent(jsonData, channelType);
        }
        
        /// <summary>
        /// 处理增量内容
        /// </summary>
        private void ProcessDeltaContent(string jsonData, ChannelType channelType)
        {
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, channelType);
            
            // 处理思维内容
            if (!string.IsNullOrEmpty(reasoning))
            {
                _streamingReasoning.Append(reasoning);
                Dispatcher.InvokeAsync(() => UpdateStreamingMessage());
            }
            
            // 处理实际内容
            if (!string.IsNullOrEmpty(content))
            {
                _streamingContent.Append(content);
                Dispatcher.InvokeAsync(() => UpdateStreamingMessage());
            }
        }

        private void InitializeStreamingMessage()
        {
            _streamingContent.Clear();
            _streamingReasoning.Clear();
            
            // Create a new message model for streaming
            _currentStreamingMessage = new MessageDisplayModel
            {
                Content = "",
                IsUser = false,
                ReasoningContent = null,
                Timestamp = DateTime.Now
            };
            
            Messages.Add(_currentStreamingMessage);
            ScrollToBottom();
        }

        private void UpdateStreamingMessage()
        {
            if (_currentStreamingMessage == null) return;
            
            bool updated = false;
            
            // Update reasoning content if available
            if (_streamingReasoning.Length > 0)
            {
                _currentStreamingMessage.ReasoningContent = _streamingReasoning.ToString();
                updated = true;
            }
            
            // Update actual content
            if (_streamingContent.Length > 0)
            {
                _currentStreamingMessage.Content = _streamingContent.ToString();
                updated = true;
            }
            
            // Scroll to bottom if content was updated
            if (updated)
            {
                ScrollToBottom();
            }
        }

        private void AppendStreamingDelta(string? content, string? reasoning)
        {
            if (!string.IsNullOrEmpty(reasoning))
            {
                _streamingReasoning.Append(reasoning);
            }
            if (!string.IsNullOrEmpty(content))
            {
                _streamingContent.Append(content);
            }
            Dispatcher.InvokeAsync(() => UpdateStreamingMessage());
        }


        private List<string> ExtractCompleteSentences(StringBuilder buffer)
        {
            var sentences = new List<string>();
            var text = buffer.ToString();
            var lastSentenceEnd = -1;
            
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                
                // 检查句子结束标志
                if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！' || c == '？' || 
                    c == ',' || c == '，' || c == ';' || c == '；' || c == ':' || c == '：')
                {
                    // 提取句子
                    var sentence = text.Substring(lastSentenceEnd + 1, i - lastSentenceEnd).Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }
                    lastSentenceEnd = i;
                }
            }
            
            // 从缓冲区移除已提取的句子
            if (lastSentenceEnd >= 0 && lastSentenceEnd < text.Length - 1)
            {
                var remaining = text.Substring(lastSentenceEnd + 1);
                buffer.Clear();
                buffer.Append(remaining);
            }
            else if (lastSentenceEnd == text.Length - 1)
            {
                buffer.Clear();
            }
            
            return sentences;
        }


        private List<string> SplitTextIntoSentences(string text)
        {
            var sentences = new List<string>();
            var current = new StringBuilder();
            
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                current.Append(c);
                
                // 检查句子结束标志
                if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！' || c == '？')
                {
                    // 检查下一个字符是否是空格、换行或结束
                    if (i == text.Length - 1 || char.IsWhiteSpace(text[i + 1]))
                    {
                        var sentence = current.ToString().Trim();
                        if (sentence.Length > 0)
                        {
                            sentences.Add(sentence);
                        }
                        current.Clear();
                    }
                }
                // 如果内容太长，在空格处分割
                else if (current.Length > 50 && char.IsWhiteSpace(c))
                {
                    var sentence = current.ToString().Trim();
                    if (sentence.Length > 0)
                    {
                        sentences.Add(sentence);
                    }
                    current.Clear();
                }
            }
            
            // 添加剩余内容
            if (current.Length > 0)
            {
                var remaining = current.ToString().Trim();
                if (remaining.Length > 0)
                {
                    sentences.Add(remaining);
                }
            }
            
            return sentences;
        }

        // CreateReasoningUI method removed - UI virtualization with data binding handles reasoning display automatically
        
        private async void FinalizeStreamingMessage()
        {
            if (_currentStreamingMessage != null)
            {
                var finalContent = _streamingContent.ToString().Trim();
                var finalReasoning = _streamingReasoning.ToString().Trim();
                
                // Update the final message content
                _currentStreamingMessage.Content = finalContent;
                _currentStreamingMessage.ReasoningContent = string.IsNullOrEmpty(finalReasoning) ? null : finalReasoning;
                
                // 检查AI回复是否包含Markdown内容并转换
                if (ContainsMarkdown(finalContent))
                {
                    _currentStreamingMessage.IsMarkdownContent = true;
                    if (finalContent?.Length > 800)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _currentStreamingMessage.RenderedDocument = ConvertMarkdownToFlowDocument(finalContent);
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    else
                    {
                        _currentStreamingMessage.RenderedDocument = ConvertMarkdownToFlowDocument(finalContent);
                    }
                }
                
                // Auto-save AI reply message to database (delegate to ViewModel)
                if (!string.IsNullOrEmpty(finalContent) && _viewModel != null)
                {
                    await _viewModel.SaveMessageAsync(finalContent, false, string.IsNullOrEmpty(finalReasoning) ? null : finalReasoning);
                }
                
                // If no valid content was received, remove the message or update with placeholder
                if (string.IsNullOrEmpty(finalContent) && string.IsNullOrEmpty(finalReasoning))
                {
                    Messages.Remove(_currentStreamingMessage);
                    AddMessageBubbleWithoutSave("AI没有返回有效内容", false);
                }
                
                // Clear references
                _currentStreamingMessage = null;
                
                // Scroll to bottom to show complete content
                ScrollToBottom();
            }
        }

        /// <summary>
        /// 添加消息气泡但不保存到数据库（用于内部提示消息）
        /// </summary>
        private void AddMessageBubbleWithoutSave(string message, bool isUser = false)
        {
            var messageModel = new MessageDisplayModel
            {
                Content = message,
                IsUser = isUser,
                Timestamp = DateTime.Now
            };
            
            Messages.Add(messageModel);
            ScrollToBottom();
        }

        private async void TtsButton_Click(object sender, RoutedEventArgs e)
        {
            await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                var button = sender as System.Windows.Controls.Button;
                var messageText = button?.Tag as string;
                
                if (string.IsNullOrEmpty(messageText) || button == null)
                {
                    System.Diagnostics.Debug.WriteLine("TTS: 消息文本为空或按钮为null");
                    return;
                }

                var appSettings = DataContext as AppSettings;
                var ttsConfig = appSettings?.GetActiveTtsConfiguration();
                
                if (ttsConfig == null)
                {
                    System.Diagnostics.Debug.WriteLine("TTS: 未找到激活的TTS配置");
                    System.Windows.MessageBox.Show("未找到TTS配置，请先在设置中配置并激活TTS服务。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"TTS: 开始处理消息，长度: {messageText.Length}");
                
                // 改变按钮状态表示正在处理
                var originalContent = button.Content;
                button.Content = "⏳";
                button.IsEnabled = false;

                try
                {
                    await CallTtsApi(messageText, ttsConfig);
                    System.Diagnostics.Debug.WriteLine("TTS: 调用成功");
                }
                finally
                {
                    // 恢复按钮状态
                    button.Content = originalContent;
                    button.IsEnabled = true;
                }
            }, "TTS语音合成");
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                var button = sender as System.Windows.Controls.Button;
                var messageText = button?.Tag as string;
                
                if (string.IsNullOrEmpty(messageText) || button == null)
                    return;

                System.Windows.Clipboard.SetText(messageText);
                
                // 临时改变按钮显示表示复制成功
                var originalContent = button.Content;
                button.Content = "✅";
                
                // 1秒后恢复原始图标
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, args) =>
                {
                    button.Content = originalContent;
                    timer.Stop();
                };
                timer.Start();
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, context: new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "复制消息内容"
            });
        }

        private async Task CallTtsApi(string text, TtsConfiguration ttsConfig)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                System.Diagnostics.Debug.WriteLine($"TTS API: 开始调用，文本长度: {text.Length}, 渠道: {ttsConfig.ChannelType}");
                
                // 生成文本和配置的哈希值作为缓存键
                var textHash = GenerateHash($"{text}_{ttsConfig.Model}_{ttsConfig.Voice}_{ttsConfig.Speed}_{ttsConfig.ChannelType}");
                var ttsConfigJson = JsonSerializer.Serialize(new 
                { 
                    channelType = ttsConfig.ChannelType.ToString(),
                    model = ttsConfig.Model, 
                    voice = ttsConfig.Voice, 
                    speed = ttsConfig.Speed 
                });

                // 先尝试从数据库获取缓存的音频
                var cachedAudio = await _databaseService.GetTtsAudioAsync(textHash);
                if (cachedAudio != null)
                {
                    System.Diagnostics.Debug.WriteLine("TTS API: 使用缓存音频");
                    // 使用缓存的音频
                    await PlayAudioFromBytes(cachedAudio.AudioData);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("TTS API: 未找到缓存，使用TTS服务生成新音频");
                
                // 使用DI解析的解析器创建相应的TTS服务
                var resolver = Buddie.App.GetService<Buddie.Services.Tts.ITtsServiceResolver>();
                using var ttsService = resolver.Create(ttsConfig.ChannelType);
                
                // 创建TTS请求
                var ttsRequest = new TtsRequest
                {
                    Text = text,
                    Configuration = ttsConfig
                };

                // 调用TTS服务
                var ttsResponse = await ttsService.ConvertTextToSpeechAsync(ttsRequest);
                
                if (ttsResponse.IsSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"TTS API: 收到音频数据，大小: {ttsResponse.AudioData.Length} bytes");
                    
                    // 保存到数据库缓存
                    await _databaseService.SaveTtsAudioAsync(textHash, ttsResponse.AudioData, ttsConfigJson);
                    System.Diagnostics.Debug.WriteLine("TTS API: 音频已保存到缓存");
                    
                    // 触发缓存清理（使用AppSettings中的配置）
                    var appSettings = DataContext as AppSettings;
                    if (appSettings != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
                            {
                                await _databaseService.CleanupTtsCacheAsync(
                                    appSettings.TtsCacheCleanupDays,
                                    appSettings.MaxTtsCacheCount,
                                    appSettings.MaxTtsCacheSizeMB);
                            }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
                            {
                                Component = "DialogControl",
                                Operation = "TTS缓存清理"
                            });
                        });
                    }
                    
                    // 播放音频
                    await PlayAudioFromBytes(ttsResponse.AudioData, ttsResponse.ContentType);
                    System.Diagnostics.Debug.WriteLine("TTS API: 音频播放完成");
                }
                else
                {
                    throw new Exception($"TTS服务失败: {ttsResponse.ErrorMessage}");
                }
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl", 
                Operation = "TTS语音合成"
            });
        }

        private string GenerateHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        private async Task PlayAudioFromBytes(byte[] audioBytes, string? contentType = null)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                System.Diagnostics.Debug.WriteLine($"播放音频: NAudio处理 {audioBytes.Length} bytes");
                
                // 停止之前的播放
                StopCurrentAudio();
                
                // 创建临时文件
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buddie");
                Directory.CreateDirectory(appDataPath);
                
                // 根据ContentType或字节数据确定文件扩展名
                string extension = GetAudioExtension(audioBytes, contentType);
                var tempFile = Path.Combine(appDataPath, $"audio_{Guid.NewGuid()}{extension}");
                await File.WriteAllBytesAsync(tempFile, audioBytes);
                
                System.Diagnostics.Debug.WriteLine($"播放音频: 临时文件创建 {tempFile}");
                
                // 尝试使用NAudio播放
                var success = await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
                {
                    // 使用NAudio播放音频
                    _currentAudioReader = new AudioFileReader(tempFile);
                    _currentAudioPlayer = new WaveOutEvent();
                    
                    // 设置播放完成事件
                    _currentAudioPlayer.PlaybackStopped += async (sender, e) =>
                    {
                        await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
                        {
                            await CleanupAudioResourcesAsync(tempFile);
                            
                            if (e.Exception != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"NAudio播放异常: {e.Exception.Message}");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("NAudio播放完成");
                            }
                        }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
                        {
                            Component = "DialogControl",
                            Operation = "音频播放完成清理"
                        });
                    };
                    
                    _currentAudioPlayer.Init(_currentAudioReader);
                    _currentAudioPlayer.Play();
                    
                    System.Diagnostics.Debug.WriteLine("NAudio开始播放");
                    
                    // 异步等待播放完成
                    while (_currentAudioPlayer?.PlaybackState == PlaybackState.Playing)
                    {
                        await Task.Delay(100);
                    }
                    
                    return true;
                }, ExceptionHandlingService.HandlingStrategy.LogOnly, false, new ExceptionHandlingService.ExceptionContext
                {
                    Component = "DialogControl",
                    Operation = "NAudio音频播放", 
                    AdditionalData = new Dictionary<string, object>
                    {
                        ["AudioFileSize"] = audioBytes.Length,
                        ["ContentType"] = contentType ?? "unknown",
                        ["TempFile"] = tempFile
                    }
                });
                
                // 如果NAudio失败，尝试回退播放
                if (!success)
                {
                    await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine("NAudio失败，尝试回退播放");
                        FallbackAudioPlayback(tempFile);
                        
                        // 清理临时文件
                        await CleanupTempFileAsync(tempFile);
                        
                        await Task.CompletedTask;
                    }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
                    {
                        Component = "DialogControl",
                        Operation = "音频播放回退处理"
                    });
                }
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "音频播放",
                AdditionalData = new Dictionary<string, object>
                {
                    ["AudioSize"] = audioBytes.Length,
                    ["ContentType"] = contentType ?? "unknown"
                }
            });
        }
        /// <summary>
        /// 停止当前音频播放（异步）
        /// </summary>
        private async Task StopCurrentAudioAsync()
        {
            await CleanupAudioResourcesAsync();
        }
        
        /// <summary>
        /// 停止当前音频播放（同步版本，保持兼容性）
        /// </summary>
        private void StopCurrentAudio()
        {
            _ = StopCurrentAudioAsync();
        }
        /// <summary>
        /// 根据ContentType和音频数据确定文件扩展名
        /// </summary>
        private string GetAudioExtension(byte[] audioBytes, string? contentType = null)
        {
            // 首先尝试根据ContentType确定格式
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("wav"))
                {
                    System.Diagnostics.Debug.WriteLine($"根据ContentType检测到WAV格式: {contentType}");
                    return ".wav";
                }
                if (contentType.Contains("mp3") || contentType.Contains("mpeg"))
                {
                    System.Diagnostics.Debug.WriteLine($"根据ContentType检测到MP3格式: {contentType}");
                    return ".mp3";
                }
            }
            
            // 如果ContentType不明确，则分析字节数据
            if (audioBytes.Length >= 12)
            {
                var header = System.Text.Encoding.ASCII.GetString(audioBytes, 0, 4);
                if (header == "RIFF")
                {
                    var format = System.Text.Encoding.ASCII.GetString(audioBytes, 8, 4);
                    if (format == "WAVE")
                    {
                        System.Diagnostics.Debug.WriteLine("通过字节分析检测到WAV格式");
                        return ".wav";
                    }
                }
            }
            
            if (audioBytes.Length >= 3)
            {
                var first3Bytes = System.Text.Encoding.ASCII.GetString(audioBytes, 0, 3);
                if (first3Bytes == "ID3")
                {
                    System.Diagnostics.Debug.WriteLine("通过字节分析检测到MP3格式（ID3标签）");
                    return ".mp3";
                }
            }
            
            if (audioBytes.Length >= 2)
            {
                // 检查MP3同步字节 (0xFF 0xFB/0xFA/0xF3/0xF2)
                if (audioBytes[0] == 0xFF && (audioBytes[1] & 0xE0) == 0xE0)
                {
                    System.Diagnostics.Debug.WriteLine("通过字节分析检测到MP3格式（同步字节）");
                    return ".mp3";
                }
            }
            
            // 默认假设为MP3（因为很多TTS服务实际上返回MP3）
            System.Diagnostics.Debug.WriteLine("未识别格式，默认为MP3");
            return ".mp3";
        }
        /// <summary>
        /// 回退音频播放方法（当NAudio失败时使用）
        /// </summary>
        private void FallbackAudioPlayback(string audioFile)
        {
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                // 使用系统默认播放器
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = audioFile,
                    UseShellExecute = true,
                    Verb = "open",
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                
                using var process = System.Diagnostics.Process.Start(processInfo);
                process?.WaitForExit(30000); // 最多等待30秒
                System.Diagnostics.Debug.WriteLine("系统播放器播放完成");
            }, ExceptionHandlingService.HandlingStrategy.Rethrow, context: new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "回退音频播放",
                AdditionalData = new Dictionary<string, object>
                {
                    ["AudioFile"] = audioFile
                }
            });
        }
        
        private async Task CleanupAudioResourcesAsync(string? tempFile = null)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // 释放音频资源
                if (_currentAudioPlayer != null)
                {
                    _currentAudioPlayer.Stop();
                    await Task.Run(() => _currentAudioPlayer.Dispose());
                    _currentAudioPlayer = null;
                }
                
                if (_currentAudioReader != null)
                {
                    await Task.Run(() => _currentAudioReader.Dispose());
                    _currentAudioReader = null;
                }
                
                // 清理临时文件
                if (!string.IsNullOrEmpty(tempFile))
                {
                    await CleanupTempFileAsync(tempFile);
                }
                
                System.Diagnostics.Debug.WriteLine("音频资源清理完成");
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "清理音频资源"
            });
        }
        
        private async Task CleanupTempFileAsync(string tempFile)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // 稍微延迟以确保文件没有被占用
                await Task.Delay(1000);
                if (File.Exists(tempFile))
                {
                    await Task.Run(() => File.Delete(tempFile));
                    System.Diagnostics.Debug.WriteLine($"播放音频: 临时文件已删除 {tempFile}");
                }
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "清理临时文件"
            });
        }

        #region 对话历史功能

        /// <summary>
        /// 加载对话历史
        /// </summary>
        private async void LoadConversationHistory()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                // 创建新的对话
                await StartNewConversation();
            }, "加载对话历史");
        }

        /// <summary>
        /// 开始新的对话
        /// </summary>
        public async Task StartNewConversation()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                // 保存当前对话（如果有的话）
                if (_currentConversation != null)
                {
                    await SaveCurrentConversation();
                }

                // 创建新对话（暂不保存，等到有用户输入时再保存）
                _currentConversation = new DbConversation
                {
                    Title = $"对话 {DateTime.Now:MM-dd HH:mm}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Id = 0  // 标记为未保存状态
                };

                // 清空当前消息和界面
                _currentMessages.Clear();
                ClearDialog();
                
                System.Diagnostics.Debug.WriteLine($"Started new conversation: {_currentConversation.Id}");
            }, "开始新对话");
        }

        /// <summary>
        /// 加载指定对话
        /// </summary>
        public async Task LoadConversation(int conversationId)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                // 保存当前对话
                if (_currentConversation != null)
                {
                    await SaveCurrentConversation();
                }

                // 加载指定对话
                var conversations = await _databaseService.GetConversationsAsync();
                _currentConversation = conversations.FirstOrDefault(c => c.Id == conversationId);

                if (_currentConversation != null)
                {
                    // 加载对话消息
                    _currentMessages = await _databaseService.GetMessagesAsync(conversationId);
                    
                    // 重建对话界面
                    await RebuildConversationUI();
                }
            }, "加载指定对话");
        }

        /// <summary>
        /// 保存当前对话
        /// </summary>
        public async Task SaveCurrentConversation()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                if (_currentConversation == null) return;

                // 检查对话是否为空：没有任何用户输入内容
                var hasUserContent = _currentMessages.Any(m => m.IsUser && !string.IsNullOrWhiteSpace(m.Content));
                if (!hasUserContent)
                {
                    System.Diagnostics.Debug.WriteLine("Skipping save: conversation has no user content");
                    return;
                }

                // 更新对话标题（使用第一条用户消息的前20个字符）
                if (_currentMessages.Count > 0)
                {
                    var firstUserMessage = _currentMessages.FirstOrDefault(m => m.IsUser);
                    if (firstUserMessage != null && firstUserMessage.Content.Length > 0)
                    {
                        var title = firstUserMessage.Content.Length > 20 
                            ? firstUserMessage.Content.Substring(0, 20) + "..."
                            : firstUserMessage.Content;
                        _currentConversation.Title = title;
                    }
                }

                _currentConversation.UpdatedAt = DateTime.UtcNow;
                
                // 如果是首次保存（Id为0），则插入新记录并获取ID
                if (_currentConversation.Id == 0)
                {
                    var conversationId = await _databaseService.SaveConversationAsync(_currentConversation);
                    _currentConversation.Id = conversationId;
                    System.Diagnostics.Debug.WriteLine($"First save of conversation: {_currentConversation.Id}");
                }
                else
                {
                    // 更新已存在的对话
                    await _databaseService.SaveConversationAsync(_currentConversation);
                    System.Diagnostics.Debug.WriteLine($"Updated conversation: {_currentConversation.Id}");
                }
            }, "保存当前对话");
        }

        /// <summary>
        /// 保存消息到数据库
        /// </summary>
        public async Task SaveMessage(string content, bool isUser, string? reasoningContent = null, byte[]? imageData = null)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                if (_currentConversation == null)
                {
                    await StartNewConversation();
                }

                if (_currentConversation != null)
                {
                    // 如果是用户消息且对话还未保存，则先保存对话
                    if (isUser && _currentConversation.Id == 0)
                    {
                        // 先将用户消息添加到临时列表，以便SaveCurrentConversation能检测到有用户内容
                        var tempMessage = new DbMessage
                        {
                            Content = content,
                            IsUser = isUser,
                            CreatedAt = DateTime.UtcNow,
                            ImageData = imageData,
                            ImageContentType = imageData != null ? "image/png" : null
                        };
                        _currentMessages.Add(tempMessage);
                        
                        // 现在保存对话（会检查是否有用户内容）
                        await SaveCurrentConversation();
                        
                        // 移除临时消息，稍后会重新添加带有正确ConversationId的消息
                        _currentMessages.Remove(tempMessage);
                    }
                    
                    var message = new DbMessage
                    {
                        ConversationId = _currentConversation.Id,
                        Content = content,
                        IsUser = isUser,
                        ReasoningContent = reasoningContent,
                        CreatedAt = DateTime.UtcNow,
                        ImageData = imageData,
                        ImageContentType = imageData != null ? "image/png" : null
                    };

                    var messageId = await _databaseService.SaveMessageAsync(message);
                    message.Id = messageId;
                    _currentMessages.Add(message);
                }
            }, "保存消息");
        }

        /// <summary>
        /// 重建对话界面
        /// </summary>
        private async Task RebuildConversationUI() => await Task.CompletedTask; // Moved to ViewModel

        /// <summary>
        /// 清空对话界面
        /// </summary>
        private void ClearDialog()
        {
            Messages.Clear();
        }

        /// <summary>
        /// 删除对话
        /// </summary>
        public async Task DeleteConversation(int conversationId)
        {
            if (_viewModel != null)
            {
                await _viewModel.DeleteConversationByIdAsync(conversationId);
            }
        }

        /// <summary>
        /// 获取所有对话列表
        /// </summary>
        public async Task<List<DbConversation>> GetAllConversations()
        {
            if (_viewModel != null)
            {
                await _viewModel.RefreshConversationsAsync();
                return _viewModel.Conversations.ToList();
            }
            return new List<DbConversation>();
        }

        #endregion
        
        #region 侧边栏事件处理

        /// <summary>
        /// 切换侧边栏显示状态
        /// </summary>
        private async Task ToggleSidebar()
        {
            if (_isSidebarVisible)
            {
                await HideSidebar();
            }
            else
            {
                await ShowSidebar();
            }
        }

        /// <summary>
        /// 显示侧边栏
        /// </summary>
        private async Task ShowSidebar()
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                _isSidebarVisible = true;
                
                // 设置侧边栏宽度
                SidebarColumn.Width = new GridLength(150);
                HistorySidebar.Visibility = Visibility.Visible;
                
                // 刷新对话列表（改为调用VM）
                if (_viewModel != null)
                {
                    await _viewModel.RefreshConversationsAsync();
                }
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "显示侧边栏"
            });
        }

        /// <summary>
        /// 隐藏侧边栏
        /// </summary>
        private async Task HideSidebar()
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                _isSidebarVisible = false;
                
                // 隐藏侧边栏
                SidebarColumn.Width = new GridLength(0);
                HistorySidebar.Visibility = Visibility.Collapsed;
                
                await Task.CompletedTask;
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "隐藏侧边栏"
            });
        }

        /// <summary>
        /// 刷新对话列表
        /// </summary>
        private async Task RefreshConversationsList()
        {
            if (_viewModel != null)
            {
                await _viewModel.RefreshConversationsAsync();
            }
        }

        private async void NewConversationButton_Click(object sender, RoutedEventArgs e)
        {
            await StartNewConversation();
        }

        private async void ConversationItem_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            var conversation = border?.DataContext as DbConversation;
            
            if (conversation != null)
            {
                if (_viewModel != null)
                {
                    await _viewModel.LoadConversationAsync(conversation.Id);
                }
            }
        }

        private async void DeleteConversationButton_Click(object sender, RoutedEventArgs e)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                var button = sender as System.Windows.Controls.Button;
                if (button?.Tag is int conversationId)
                {
                    var result = System.Windows.MessageBox.Show(
                        Buddie.Localization.LocalizationManager.GetString("Confirm_DeleteConversation_Message"),
                        Buddie.Localization.LocalizationManager.GetString("Confirm_DeleteConversation_Title"),
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        if (_viewModel != null)
                        {
                            await _viewModel.DeleteConversationByIdAsync(conversationId);
                        }
                    }
                }
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "删除对话按钮处理"
            });
        }

        #endregion
        
        /// <summary>
        /// 释放音频资源
        /// </summary>
        private void ReleaseAudioResources()
        {
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                // 停止并释放音频播放资源
                StopCurrentAudio();
                
                // 关闭MediaFoundation
                ExceptionHandlingService.ExecuteSafely(() =>
                {
                    MediaFoundationApi.Shutdown();
                }, ExceptionHandlingService.HandlingStrategy.LogOnly, context: new ExceptionHandlingService.ExceptionContext
                {
                    Component = "DialogControl",
                    Operation = "关闭MediaFoundation"
                });
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, context: new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "释放音频资源"
            });
        }

        #region 截图功能

        /// <summary>
        /// 图片上传按钮点击事件
        /// </summary>
        private async void ImageUploadButton_Click(object sender, RoutedEventArgs e)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // 检查是否支持多模态
                var appSettings = DataContext as AppSettings;
                var activeConfig = appSettings?.ApiConfigurations.FirstOrDefault();
                
                if (activeConfig == null || !activeConfig.IsMultimodalEnabled)
                {
                    System.Windows.MessageBox.Show(
                        "当前API配置未启用多模态功能，请先在设置中启用多模态。", 
                        "提示", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Information);
                    return;
                }
                
                // 打开文件选择对话框
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择图片",
                    Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|所有文件|*.*",
                    Multiselect = false
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    
                    // 读取图片文件
                    var imageBytes = await File.ReadAllBytesAsync(filePath);
                    
                    // 检查文件大小（限制为10MB）
                    if (imageBytes.Length > 10 * 1024 * 1024)
                    {
                        System.Windows.MessageBox.Show(
                            "图片文件大小不能超过10MB", 
                            "文件过大", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                    
                    _currentLocalImage = imageBytes;
                    _hasLocalImage = true;
                    if (_viewModel != null)
                    {
                        _viewModel.LocalImage = imageBytes;
                        _viewModel.ScreenshotImage = null;
                    }
                    
                    // 清除之前的截图（如果有）
                    if (_hasScreenshot)
                    {
                        _currentScreenshot = null;
                        _hasScreenshot = false;
                        if (_viewModel != null) _viewModel.ScreenshotImage = null;
                    }
                    
                    // 显示图片预览
                    ShowLocalImagePreview(imageBytes);
                }
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "选择本地图片"
            });
        }
        
        /// <summary>
        /// 显示本地图片预览
        /// </summary>
        private void ShowLocalImagePreview(byte[] imageBytes)
        {
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                // 转换字节数组为BitmapImage
                var bitmapImage = new BitmapImage();
                using (var stream = new MemoryStream(imageBytes))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }
                
                // 显示预览UI
                if (ScreenshotPreviewContainer != null && ScreenshotThumbnail != null)
                {
                    ScreenshotThumbnail.Source = bitmapImage;
                    ScreenshotPreviewContainer.Visibility = Visibility.Visible;
                    
                    // 更新信息文本
                    if (ScreenshotInfo != null)
                    {
                        var fileSize = imageBytes.Length / 1024.0;
                        ScreenshotInfo.Text = $"图片大小: {fileSize:F1} KB";
                    }
                }
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, context: new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "显示本地图片预览"
            });
        }

        /// <summary>
        /// 截图按钮点击事件 - 简化版本
        /// </summary>
        private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // 检查是否支持多模态
                var appSettings = DataContext as AppSettings;
                var activeConfig = appSettings?.ApiConfigurations.FirstOrDefault();
                
                if (activeConfig == null || !activeConfig.IsMultimodalEnabled)
                {
                    System.Windows.MessageBox.Show(
                        "当前API配置未启用多模态功能，请先在设置中启用多模态。", 
                        "提示", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Information);
                    return;
                }
                
                // 直接使用简单的全屏截图
                var screenshotBytes = await CaptureFullScreenAsync();
                if (screenshotBytes != null)
                {
                    _currentScreenshot = screenshotBytes;
                    _hasScreenshot = true;
                    if (_viewModel != null)
                    {
                        _viewModel.ScreenshotImage = screenshotBytes;
                        _viewModel.LocalImage = null;
                    }
                    
                    // 清除之前的本地图片（如果有）
                    if (_hasLocalImage)
                    {
                        _currentLocalImage = null;
                        _hasLocalImage = false;
                        if (_viewModel != null) _viewModel.LocalImage = null;
                    }
                    
                    // 显示预览
                    ShowScreenshotPreview(screenshotBytes);
                }
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "截图操作"
            });
        }
        
        /// <summary>
        /// 全屏截图
        /// </summary>
        private async Task<byte[]?> CaptureFullScreenAsync()
        {
            // 在UI线程上获取屏幕边界信息
            Rectangle screenBounds = Rectangle.Empty;
            await Dispatcher.InvokeAsync(() =>
            {
                screenBounds = GetCurrentWindowScreen();
            });

            // 隐藏主窗口
            var mainWindow = Window.GetWindow(this);
            if (mainWindow != null)
            {
                mainWindow.WindowState = WindowState.Minimized;
                await Task.Delay(300); // 等待窗口最小化
            }

            byte[]? result = null;
            try
            {
                result = await Task.Run(() =>
                {
                    return ExceptionHandlingService.ExecuteSafely(() =>
                    {
                        // 使用预先获取的屏幕边界
                        _logger.LogDebug("DialogControl: 使用屏幕边界进行截图 = {Bounds}", screenBounds);
                        
                        // 创建位图
                        using var bitmap = new System.Drawing.Bitmap(screenBounds.Width, screenBounds.Height);
                        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                        
                        // 截取屏幕 - 使用正确的屏幕坐标
                        graphics.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size);
                        
                        // 转换为字节数组
                        using var memoryStream = new MemoryStream();
                        bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                        return memoryStream.ToArray();
                    }, ExceptionHandlingService.HandlingStrategy.LogOnly, (byte[]?)null, new ExceptionHandlingService.ExceptionContext
                    {
                        Component = "DialogControl",
                        Operation = "屏幕截取"
                    });
                });
            }
            finally
            {
                // 恢复主窗口
                if (mainWindow != null)
                {
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            }
            
            return result;
        }

        /// <summary>
        /// 删除截图按钮点击事件
        /// </summary>
        private void RemoveScreenshot_Click(object sender, RoutedEventArgs e)
        {
            ClearScreenshot();
        }

        /// <summary>
        /// 异步截取屏幕
        /// </summary>
        private async Task<byte[]?> CaptureScreenAsync()
        {
            // 在UI线程上获取屏幕边界信息
            Rectangle screenBounds = Rectangle.Empty;
            await Dispatcher.InvokeAsync(() =>
            {
                screenBounds = GetCurrentWindowScreen();
            });

            return await Task.Run(() =>
            {
                return ExceptionHandlingService.ExecuteSafely(() =>
                {
                    // 使用预先获取的屏幕边界
                    _logger.LogDebug("DialogControl: CaptureScreenAsync使用屏幕边界 = {Bounds}", screenBounds);
                    
                    // 创建位图
                    using var bitmap = new System.Drawing.Bitmap(screenBounds.Width, screenBounds.Height);
                    using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                    
                    // 截取屏幕 - 使用正确的屏幕坐标
                    graphics.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size);
                    
                    // 转换为字节数组
                    using var memoryStream = new MemoryStream();
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    return memoryStream.ToArray();
                }, ExceptionHandlingService.HandlingStrategy.LogOnly, (byte[]?)null, new ExceptionHandlingService.ExceptionContext
                {
                    Component = "DialogControl",
                    Operation = "屏幕截取"
                });
            });
        }

        /// <summary>
        /// 显示截图预览
        /// </summary>
        private void ShowScreenshotPreview(byte[] screenshotBytes)
        {
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                // 将字节数组转换为BitmapImage
                using var memoryStream = new MemoryStream(screenshotBytes);
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.DecodePixelWidth = 300; // 限制缩略图宽度
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                
                // 设置预览图片
                ScreenshotThumbnail.Source = bitmapImage;
                ScreenshotPreviewContainer.Visibility = Visibility.Visible;
                
                // 更新信息文本
                var sizeKB = screenshotBytes.Length / 1024;
                ScreenshotInfo.Text = $"大小: {sizeKB} KB";
            }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, context: new ExceptionHandlingService.ExceptionContext
            {
                Component = "DialogControl",
                Operation = "显示截图预览"
            });
        }
        
        /// <summary>
        /// 点击缩略图查看大图
        /// </summary>
        private void ScreenshotThumbnail_Click(object sender, MouseButtonEventArgs e)
        {
            if (_currentScreenshot != null)
            {
                ExceptionHandlingService.ExecuteSafely(() =>
                {
                    // 创建全尺寸图片
                    using var memoryStream = new MemoryStream(_currentScreenshot);
                    var fullImage = new BitmapImage();
                    fullImage.BeginInit();
                    fullImage.StreamSource = memoryStream;
                    fullImage.CacheOption = BitmapCacheOption.OnLoad;
                    fullImage.EndInit();
                    fullImage.Freeze();
                    
                    // 创建新窗口显示大图
                    var imageWindow = new Window
                    {
                        Title = "截图预览",
                        Width = 800,
                        Height = 600,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        Background = new SolidColorBrush(Colors.Black),
                        Content = new ScrollViewer
                        {
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = new System.Windows.Controls.Image
                            {
                                Source = fullImage,
                                Stretch = Stretch.Uniform
                            }
                        }
                    };
                    
                    imageWindow.ShowDialog();
                }, ExceptionHandlingService.HandlingStrategy.ShowMessageAndLog, context: new ExceptionHandlingService.ExceptionContext
                {
                    Component = "DialogControl",
                    Operation = "显示截图大图"
                });
            }
        }

        /// <summary>
        /// 将截图转换为Base64字符串
        /// </summary>
        private string ConvertImageToBase64(byte[] imageBytes)
        {
            return Convert.ToBase64String(imageBytes);
        }

        #endregion
    }
}
