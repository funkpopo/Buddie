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
using System.Runtime.InteropServices;

namespace Buddie.Controls
{
    public partial class DialogControl : UserControl
    {
        // NAudio-based audio player
        private WaveOutEvent? currentAudioPlayer;
        private AudioFileReader? currentAudioReader;

        public event EventHandler<string>? MessageSent;
        public event EventHandler? DialogClosed;
        
        private CancellationTokenSource? currentRequest;
        private bool isSending = false;
        private bool isSidebarVisible = false;
        private List<string> conversationHistory = new List<string>();
        private MarkdownPipeline markdownPipeline;
        private DatabaseService databaseService = new DatabaseService();
        private DbConversation? currentConversation;
        private List<DbMessage> currentMessages = new List<DbMessage>();

        public DialogControl()
        {
            InitializeComponent();
            // 初始化Markdown管道，启用常用的扩展
            markdownPipeline = new MarkdownPipelineBuilder()
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

        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            if (isSending)
            {
                // 如果正在发送，执行中断操作
                if (currentRequest != null)
                {
                    currentRequest.Cancel();
                    AddMessageBubble("对话已中断", false);
                }
                // 重置发送状态
                SetSendingState(false);
                return;
            }

            var message = DialogInput.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                // 添加到历史记录
                conversationHistory.Add(message);
                
                // 更新UI状态
                SetSendingState(true);
                
                MessageSent?.Invoke(this, message);
                DialogInput.Clear();
            }
        }

        private void SetSendingState(bool sending)
        {
            isSending = sending;
            if (sending)
            {
                SendButton.Content = "中断";
                SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.OrangeRed);
            }
            else
            {
                SendButton.Content = "发送";
                SendButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
                currentRequest?.Dispose();
                currentRequest = null;
            }
        }

        public void ResetSendingState()
        {
            SetSendingState(false);
        }

        public void Show()
        {
            DialogInterface.Visibility = Visibility.Visible;
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
            DialogInterface.Visibility = Visibility.Collapsed;
        }

        public new bool IsVisible => DialogInterface.Visibility == Visibility.Visible;

        public void AddMessage(string message, bool isUser = true)
        {
            var messageBubble = CreateMessageBubble(message, isUser);
            DialogMessagesPanel.Children.Add(messageBubble);
            DialogScrollViewer.ScrollToEnd();
        }

        public async void AddMessageBubble(string message, bool isUser = true)
        {
            var messageBubble = CreateMessageBubble(message, isUser);
            DialogMessagesPanel.Children.Add(messageBubble);
            DialogScrollViewer.ScrollToEnd();
            
            // 自动保存消息到数据库
            await SaveMessage(message, isUser);
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

            // 创建气泡样式
            var isDarkTheme = (DialogInterface.Background as SolidColorBrush)?.Color == Color.FromRgb(30, 30, 30);
            
            // 设置内容颜色
            if (isDarkTheme)
            {
                if (contentElement is TextBlock textBlock)
                {
                    textBlock.Foreground = Brushes.White;
                    textBlock.Background = isUser ? 
                        new SolidColorBrush(Color.FromRgb(0, 132, 255)) : 
                        new SolidColorBrush(Color.FromRgb(58, 58, 60));
                }
                else if (contentElement is RichTextBox richTextBox)
                {
                    richTextBox.Foreground = Brushes.White;
                    richTextBox.Background = new SolidColorBrush(Color.FromRgb(58, 58, 60));
                }
            }
            else
            {
                if (contentElement is TextBlock textBlock)
                {
                    textBlock.Foreground = isUser ? Brushes.White : Brushes.Black;
                    textBlock.Background = isUser ? 
                        new SolidColorBrush(Color.FromRgb(0, 132, 255)) : 
                        new SolidColorBrush(Color.FromRgb(240, 240, 240));
                }
                else if (contentElement is RichTextBox richTextBox)
                {
                    richTextBox.Foreground = Brushes.Black;
                    richTextBox.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                }
            }

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
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 8, 5)
                };

                // 创建TTS按钮（喇叭图标）
                var ttsButton = new Button
                {
                    Content = "🔊",
                    Width = 24,
                    Height = 24,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Margin = new Thickness(5, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = "朗读",
                    Tag = message // 存储消息内容供TTS使用
                };
                ttsButton.Click += TtsButton_Click;

                // 创建复制按钮
                var copyButton = new Button
                {
                    Content = "📋",
                    Width = 24,
                    Height = 24,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Margin = new Thickness(5, 0, 0, 0),
                    Cursor = Cursors.Hand,
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
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(8, 0, 0, 5)
                };

                // 创建复制按钮
                var copyButton = new Button
                {
                    Content = "📋",
                    Width = 24,
                    Height = 24,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Cursor = Cursors.Hand,
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
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = isUser ? new Thickness(50, 5, 10, 5) : new Thickness(10, 5, 50, 5),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
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
                tb.Background = Brushes.Transparent;
            }
            else if (contentElement is RichTextBox rtb)
            {
                border.Background = rtb.Background;
                rtb.Background = Brushes.Transparent;
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

        private RichTextBox CreateMarkdownRichTextBox(string markdownText)
        {
            var richTextBox = new RichTextBox
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

            try
            {
                // 将Markdown转换为HTML
                var html = Markdown.ToHtml(markdownText, markdownPipeline);
                
                // 将HTML转换为FlowDocument
                var flowDocument = ConvertHtmlToFlowDocument(html);
                richTextBox.Document = flowDocument;
            }
            catch (Exception)
            {
                // 如果转换失败，回退到普通文本
                var paragraph = new Paragraph(new Run(markdownText));
                richTextBox.Document = new FlowDocument(paragraph);
            }

            return richTextBox;
        }

        private FlowDocument ConvertHtmlToFlowDocument(string html)
        {
            var flowDocument = new FlowDocument();
            var paragraph = new Paragraph();

            // 简单的HTML到FlowDocument转换
            // 这是一个基本实现，可以根据需要扩展
            var lines = html.Split('\n');
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                if (trimmedLine.StartsWith("<h"))
                {
                    // 处理标题
                    var text = System.Text.RegularExpressions.Regex.Replace(trimmedLine, "<[^>]*>", "");
                    var headingRun = new Run(text)
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 16
                    };
                    paragraph.Inlines.Add(headingRun);
                    paragraph.Inlines.Add(new LineBreak());
                }
                else if (trimmedLine.StartsWith("<code>") || trimmedLine.Contains("<code>"))
                {
                    // 处理代码
                    var text = System.Text.RegularExpressions.Regex.Replace(trimmedLine, "<[^>]*>", "");
                    var codeRun = new Run(text)
                    {
                        FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                    };
                    paragraph.Inlines.Add(codeRun);
                }
                else if (trimmedLine.StartsWith("<pre>"))
                {
                    // 处理代码块
                    var text = System.Text.RegularExpressions.Regex.Replace(trimmedLine, "<[^>]*>", "");
                    var codeBlockRun = new Run(text)
                    {
                        FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
                        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                    };
                    paragraph.Inlines.Add(codeBlockRun);
                    paragraph.Inlines.Add(new LineBreak());
                }
                else if (trimmedLine.Contains("<strong>") || trimmedLine.Contains("<b>"))
                {
                    // 处理粗体文本
                    ProcessInlineFormatting(paragraph, trimmedLine, "strong", FontWeights.Bold);
                }
                else if (trimmedLine.Contains("<em>") || trimmedLine.Contains("<i>"))
                {
                    // 处理斜体文本
                    ProcessInlineFormatting(paragraph, trimmedLine, "em", FontWeights.Normal, FontStyles.Italic);
                }
                else if (trimmedLine.StartsWith("<li>"))
                {
                    // 处理列表项
                    var text = System.Text.RegularExpressions.Regex.Replace(trimmedLine, "<[^>]*>", "");
                    paragraph.Inlines.Add(new Run("• " + text));
                    paragraph.Inlines.Add(new LineBreak());
                }
                else if (trimmedLine.StartsWith("<p>") || !trimmedLine.StartsWith("<"))
                {
                    // 处理普通段落
                    var text = System.Text.RegularExpressions.Regex.Replace(trimmedLine, "<[^>]*>", "");
                    if (!string.IsNullOrEmpty(text))
                    {
                        paragraph.Inlines.Add(new Run(text));
                        paragraph.Inlines.Add(new LineBreak());
                    }
                }
            }

            if (paragraph.Inlines.Count > 0)
            {
                flowDocument.Blocks.Add(paragraph);
            }

            return flowDocument;
        }

        private void ProcessInlineFormatting(Paragraph paragraph, string html, string tag, FontWeight fontWeight, FontStyle fontStyle = default)
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
                if (fontStyle != default(FontStyle))
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

        public void AddMessageBubbleWithReasoning(string? content, string? reasoningContent = null)
        {
            var isDarkTheme = (DialogInterface.Background as SolidColorBrush)?.Color == Color.FromRgb(30, 30, 30);
            
            var messageContainer = new StackPanel
            {
                Margin = new Thickness(10, 5, 50, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 350
            };

            // 如果有思维内容，添加可折叠的思维过程
            if (!string.IsNullOrEmpty(reasoningContent))
            {
                var expander = new Expander
                {
                    Header = "💭 思维过程",
                    IsExpanded = false,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var reasoningBorder = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 270,
                        ShadowDepth = 1,
                        Opacity = 0.05,
                        BlurRadius = 2
                    }
                };

                var reasoningBlock = new TextBlock
                {
                    Text = reasoningContent,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    LineHeight = 16
                };

                if (isDarkTheme)
                {
                    expander.Foreground = Brushes.LightGray;
                    reasoningBorder.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                    reasoningBlock.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                }
                else
                {
                    expander.Foreground = Brushes.DarkGray;
                    reasoningBorder.Background = new SolidColorBrush(Color.FromRgb(255, 253, 235));
                    reasoningBlock.Foreground = new SolidColorBrush(Color.FromRgb(101, 103, 107));
                }

                reasoningBorder.Child = reasoningBlock;
                expander.Content = reasoningBorder;
                messageContainer.Children.Add(expander);
            }

            // 添加实际回复内容
            if (!string.IsNullOrEmpty(content))
            {
                var contentBubble = CreateMessageBubble(content, false);
                contentBubble.Margin = new Thickness(0);
                messageContainer.Children.Add(contentBubble);
            }

            DialogMessagesPanel.Children.Add(messageContainer);
            DialogScrollViewer.ScrollToEnd();
        }

        public void ClearMessages()
        {
            DialogMessagesPanel.Children.Clear();
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
            
            // 更新对话消息的颜色
            UpdateMessageColors(true);
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
            
            // 更新对话消息的颜色
            UpdateMessageColors(false);
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

        private void UpdateMessageColors(bool isDarkTheme)
        {
            foreach (UIElement child in DialogMessagesPanel.Children)
            {
                if (child is TextBlock messageBlock)
                {
                    if (isDarkTheme)
                    {
                        messageBlock.Foreground = System.Windows.Media.Brushes.White;
                        // 调整消息背景颜色以适应深色主题
                        var currentBackground = messageBlock.Background as System.Windows.Media.SolidColorBrush;
                        if (currentBackground != null)
                        {
                            if (currentBackground.Color == System.Windows.Media.Colors.LightBlue)
                            {
                                messageBlock.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 130, 180));
                            }
                            else if (currentBackground.Color == System.Windows.Media.Colors.LightGray)
                            {
                                messageBlock.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80));
                            }
                        }
                    }
                    else
                    {
                        messageBlock.Foreground = System.Windows.Media.Brushes.Black;
                        // 恢复浅色主题的消息背景颜色
                        var currentBackground = messageBlock.Background as System.Windows.Media.SolidColorBrush;
                        if (currentBackground != null)
                        {
                            if (currentBackground.Color == System.Windows.Media.Color.FromRgb(70, 130, 180))
                            {
                                messageBlock.Background = System.Windows.Media.Brushes.LightBlue;
                            }
                            else if (currentBackground.Color == System.Windows.Media.Color.FromRgb(80, 80, 80))
                            {
                                messageBlock.Background = System.Windows.Media.Brushes.LightGray;
                            }
                        }
                    }
                }
            }
        }

        private Border? currentStreamingBubble;
        private TextBlock? currentStreamingTextBlock;
        private StackPanel? currentStreamingContainer;
        private Expander? currentReasoningExpander;
        private TextBlock? currentReasoningTextBlock;
        private StringBuilder streamingContent = new StringBuilder();
        private StringBuilder streamingReasoning = new StringBuilder();
        private bool isReasoningPhase = true;

        public async Task SendMessageToApi(string message, OpenApiConfiguration apiConfig)
        {
            AddMessageBubble(message, true);

            try
            {
                // 创建新的取消令牌
                currentRequest = new CancellationTokenSource();
                
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiConfig.ApiKey}");

                var requestBody = new
                {
                    model = apiConfig.ModelName,
                    messages = new[]
                    {
                        new { role = "user", content = message }
                    },
                    stream = apiConfig.IsStreamingEnabled
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                if (apiConfig.IsStreamingEnabled)
                {
                    await ProcessStreamingResponse(httpClient, apiConfig.ApiUrl, content);
                }
                else
                {
                    var response = await httpClient.PostAsync(apiConfig.ApiUrl, content, currentRequest?.Token ?? CancellationToken.None);
                    var responseText = await response.Content.ReadAsStringAsync();
                    
                    currentRequest?.Token.ThrowIfCancellationRequested();

                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(responseText);
                            var choices = jsonDoc.RootElement.GetProperty("choices");
                            if (choices.GetArrayLength() > 0)
                            {
                                var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
                                AddMessageBubble(messageContent ?? "无响应内容", false);
                            }
                            else
                            {
                                AddMessageBubble("API响应格式错误", false);
                            }
                        }
                        catch (JsonException)
                        {
                            AddMessageBubble($"API返回了无效的JSON格式: {responseText}", false);
                        }
                    }
                    else
                    {
                        AddMessageBubble($"API请求失败: {response.StatusCode}", false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，不显示错误消息
            }
            catch (Exception ex)
            {
                AddMessageBubble($"请求错误: {ex.Message}", false);
            }
            finally
            {
                // 恢复发送状态
                SetSendingState(false);
            }
        }

        private async Task ProcessStreamingResponse(HttpClient httpClient, string apiUrl, StringContent requestContent)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = requestContent
                };

                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, currentRequest?.Token ?? CancellationToken.None);
                
                if (!response.IsSuccessStatusCode)
                {
                    AddMessageBubble($"API请求失败: {response.StatusCode}", false);
                    return;
                }

                // 初始化流式消息显示
                InitializeStreamingMessage();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);
                
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    currentRequest?.Token.ThrowIfCancellationRequested();
                    
                    if (line.StartsWith("data:"))
                    {
                        var jsonData = line.Substring(5).Trim();
                        
                        if (jsonData == "[DONE]")
                        {
                            break;
                        }
                        
                        if (!string.IsNullOrEmpty(jsonData))
                        {
                            try
                            {
                                var jsonDoc = JsonDocument.Parse(jsonData);
                                var choices = jsonDoc.RootElement.GetProperty("choices");
                                
                                if (choices.GetArrayLength() > 0)
                                {
                                    var choice = choices[0];
                                    if (choice.TryGetProperty("delta", out var delta))
                                    {
                                        // 处理思维内容
                                        if (delta.TryGetProperty("reasoning_content", out var reasoningProp))
                                        {
                                            var reasoning = reasoningProp.GetString();
                                            if (!string.IsNullOrEmpty(reasoning))
                                            {
                                                streamingReasoning.Append(reasoning);
                                                // 实时更新思维过程UI
                                                await Dispatcher.InvokeAsync(() => UpdateStreamingMessage());
                                            }
                                        }
                                        
                                        // 处理实际内容
                                        if (delta.TryGetProperty("content", out var contentProp))
                                        {
                                            var messageText = contentProp.GetString();
                                            if (!string.IsNullOrEmpty(messageText))
                                            {
                                                streamingContent.Append(messageText);
                                                // 实时更新UI
                                                await Dispatcher.InvokeAsync(() => UpdateStreamingMessage());
                                            }
                                        }
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                // 忽略无效的JSON行
                                continue;
                            }
                        }
                    }
                }
                
                // 完成流式输出
                await Dispatcher.InvokeAsync(() => FinalizeStreamingMessage());
            }
            catch (OperationCanceledException)
            {
                // 被取消，清理当前流式消息
                await Dispatcher.InvokeAsync(() => FinalizeStreamingMessage());
            }
            catch (Exception ex)
            {
                AddMessageBubble($"流式处理错误: {ex.Message}", false);
            }
        }

        private void InitializeStreamingMessage()
        {
            streamingContent.Clear();
            streamingReasoning.Clear();
            isReasoningPhase = true;
            
            // 创建消息容器
            currentStreamingContainer = new StackPanel
            {
                Margin = new Thickness(10, 5, 50, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 350
            };
            
            // 不在初始化时创建思维过程UI，而是在真正收到reasoning内容时创建
            currentReasoningExpander = null;
            currentReasoningTextBlock = null;
            
            // 创建内容消息气泡（暂时隐藏）
            currentStreamingBubble = CreateMessageBubble("", false);
            currentStreamingBubble.Margin = new Thickness(0);
            currentStreamingBubble.Visibility = Visibility.Collapsed;
            
            // 正确提取TextBlock - 需要处理可能的Grid结构
            if (currentStreamingBubble.Child is TextBlock directTextBlock)
            {
                currentStreamingTextBlock = directTextBlock;
            }
            else if (currentStreamingBubble.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is TextBlock gridTextBlock)
            {
                currentStreamingTextBlock = gridTextBlock;
            }
            
            currentStreamingContainer.Children.Add(currentStreamingBubble);
            
            DialogMessagesPanel.Children.Add(currentStreamingContainer);
            DialogScrollViewer.ScrollToEnd();
        }

        private void UpdateStreamingMessage()
        {
            bool updated = false;
            
            // 如果有思维内容且还没有创建思维过程UI，则创建它
            if (streamingReasoning.Length > 0 && currentReasoningExpander == null)
            {
                CreateReasoningUI();
            }
            
            // 更新思维过程（如果有新内容）
            if (currentReasoningTextBlock != null && streamingReasoning.Length > 0)
            {
                currentReasoningTextBlock.Text = streamingReasoning.ToString();
                updated = true;
            }
            
            // 更新实际内容
            if (currentStreamingTextBlock != null && streamingContent.Length > 0)
            {
                // 如果开始收到实际内容，切换到内容阶段
                if (isReasoningPhase)
                {
                    isReasoningPhase = false;
                    // 显示内容气泡
                    if (currentStreamingBubble != null)
                    {
                        currentStreamingBubble.Visibility = Visibility.Visible;
                    }
                    // 自动折叠思维过程
                    if (currentReasoningExpander != null)
                    {
                        currentReasoningExpander.IsExpanded = false;
                    }
                }
                
                currentStreamingTextBlock.Text = streamingContent.ToString();
                updated = true;
            }
            
            // 只有在内容更新时才滚动，避免不必要的滚动
            if (updated)
            {
                DialogScrollViewer.ScrollToEnd();
            }
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

        private void CreateReasoningUI()
        {
            if (currentStreamingContainer == null || currentReasoningExpander != null)
                return;
                
            var isDarkTheme = (DialogInterface.Background as SolidColorBrush)?.Color == Color.FromRgb(30, 30, 30);
            
            // 创建思维过程展开器（初始展开状态）
            currentReasoningExpander = new Expander
            {
                Header = "💭 思维过程",
                IsExpanded = true,
                Margin = new Thickness(0, 0, 0, 5)
            };
            
            var reasoningBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 1,
                    Opacity = 0.05,
                    BlurRadius = 2
                }
            };
            
            currentReasoningTextBlock = new TextBlock
            {
                Text = "",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                LineHeight = 16
            };
            
            if (isDarkTheme)
            {
                currentReasoningExpander.Foreground = Brushes.LightGray;
                reasoningBorder.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
                currentReasoningTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            }
            else
            {
                currentReasoningExpander.Foreground = Brushes.DarkGray;
                reasoningBorder.Background = new SolidColorBrush(Color.FromRgb(255, 253, 235));
                currentReasoningTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(101, 103, 107));
            }
            
            reasoningBorder.Child = currentReasoningTextBlock;
            currentReasoningExpander.Content = reasoningBorder;
            
            // 在内容气泡之前插入思维过程UI
            currentStreamingContainer.Children.Insert(0, currentReasoningExpander);
        }

        private async void FinalizeStreamingMessage()
        {
            if (currentStreamingContainer != null)
            {
                var finalContent = streamingContent.ToString().Trim();
                var finalReasoning = streamingReasoning.ToString().Trim();
                
                // 自动保存AI回复消息到数据库
                if (!string.IsNullOrEmpty(finalContent))
                {
                    await SaveMessage(finalContent, false, string.IsNullOrEmpty(finalReasoning) ? null : finalReasoning);
                }
                
                // 如果没有实际内容和思维内容，显示一个提示
                if (string.IsNullOrEmpty(finalContent) && string.IsNullOrEmpty(finalReasoning))
                {
                    DialogMessagesPanel.Children.Remove(currentStreamingContainer);
                    AddMessageBubbleWithoutSave("AI没有返回有效内容", false);
                }
                else if (string.IsNullOrEmpty(finalContent) && !string.IsNullOrEmpty(finalReasoning))
                {
                    // 只有思维内容，没有实际回复，隐藏内容气泡
                    if (currentStreamingBubble != null)
                    {
                        currentStreamingBubble.Visibility = Visibility.Collapsed;
                    }
                }
                else if (!string.IsNullOrEmpty(finalContent))
                {
                    // 有实际内容，重新创建气泡以确保TTS按钮的Tag正确设置
                    if (currentStreamingBubble != null)
                    {
                        // 移除当前的空内容气泡
                        currentStreamingContainer.Children.Remove(currentStreamingBubble);
                        
                        // 创建新的气泡（支持Markdown或普通文本）并添加到容器
                        var newBubble = CreateMessageBubble(finalContent, false);
                        newBubble.Margin = new Thickness(0);
                        currentStreamingContainer.Children.Add(newBubble);
                    }
                }
                
                // 如果没有思维内容但创建了思维UI，则移除它
                if (string.IsNullOrEmpty(finalReasoning) && currentReasoningExpander != null)
                {
                    currentStreamingContainer.Children.Remove(currentReasoningExpander);
                }
                // 确保思维过程在最终完成时是折叠的
                else if (currentReasoningExpander != null && !string.IsNullOrEmpty(finalReasoning))
                {
                    currentReasoningExpander.IsExpanded = false;
                }
                
                // 清理引用
                currentStreamingBubble = null;
                currentStreamingTextBlock = null;
                currentStreamingContainer = null;
                currentReasoningExpander = null;
                currentReasoningTextBlock = null;
                isReasoningPhase = true;
                
                // 滚动到底部显示完整内容
                DialogScrollViewer.ScrollToEnd();
            }
        }

        /// <summary>
        /// 添加消息气泡但不保存到数据库（用于内部提示消息）
        /// </summary>
        private void AddMessageBubbleWithoutSave(string message, bool isUser = false)
        {
            var messageBubble = CreateMessageBubble(message, isUser);
            DialogMessagesPanel.Children.Add(messageBubble);
            DialogScrollViewer.ScrollToEnd();
        }

        private async void TtsButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
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
                MessageBox.Show("未找到TTS配置，请先在设置中配置并激活TTS服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS: 调用失败 - {ex.Message}");
                MessageBox.Show($"TTS调用失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                button.Content = originalContent;
                button.IsEnabled = true;
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var messageText = button?.Tag as string;
            
            if (string.IsNullOrEmpty(messageText) || button == null)
                return;

            try
            {
                Clipboard.SetText(messageText);
                
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CallTtsApi(string text, TtsConfiguration ttsConfig)
        {
            System.Diagnostics.Debug.WriteLine($"TTS API: 开始调用，文本长度: {text.Length}, API URL: {ttsConfig.ApiUrl}");
            
            // 生成文本和配置的哈希值作为缓存键
            var textHash = GenerateHash($"{text}_{ttsConfig.Model}_{ttsConfig.Voice}_{ttsConfig.Speed}");
            var ttsConfigJson = JsonSerializer.Serialize(new 
            { 
                model = ttsConfig.Model, 
                voice = ttsConfig.Voice, 
                speed = ttsConfig.Speed 
            });

            // 先尝试从数据库获取缓存的音频
            var cachedAudio = await databaseService.GetTtsAudioAsync(textHash);
            if (cachedAudio != null)
            {
                System.Diagnostics.Debug.WriteLine("TTS API: 使用缓存音频");
                // 使用缓存的音频
                await PlayAudioFromBytes(cachedAudio.AudioData);
                return;
            }

            System.Diagnostics.Debug.WriteLine("TTS API: 未找到缓存，调用API生成新音频");
            
            // 如果没有缓存，调用API生成新的音频
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);
            
            if (!string.IsNullOrEmpty(ttsConfig.ApiKey))
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ttsConfig.ApiKey}");
                System.Diagnostics.Debug.WriteLine("TTS API: 已添加API Key到请求头");
            }

            var requestBody = new
            {
                model = ttsConfig.Model,
                input = text,
                voice = ttsConfig.Voice,
                speed = ttsConfig.Speed,
                response_format = "wav"  // 改回WAV格式，更可靠
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            System.Diagnostics.Debug.WriteLine($"TTS API: 发送请求到 {ttsConfig.ApiUrl}");
            var response = await httpClient.PostAsync(ttsConfig.ApiUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                System.Diagnostics.Debug.WriteLine($"TTS API: 收到音频数据，大小: {audioBytes.Length} bytes");
                
                // 保存到数据库缓存
                await databaseService.SaveTtsAudioAsync(textHash, audioBytes, ttsConfigJson);
                System.Diagnostics.Debug.WriteLine("TTS API: 音频已保存到缓存");
                
                // 播放音频
                await PlayAudioFromBytes(audioBytes);
                System.Diagnostics.Debug.WriteLine("TTS API: 音频播放完成");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"TTS API: 请求失败，状态码: {response.StatusCode}, 错误内容: {errorContent}");
                throw new Exception($"TTS API请求失败: {response.StatusCode} - {errorContent}");
            }
        }

        private string GenerateHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        private async Task PlayAudioFromBytes(byte[] audioBytes)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"播放音频: NAudio处理 {audioBytes.Length} bytes");
                
                // 停止之前的播放
                StopCurrentAudio();
                
                // 创建临时文件
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buddie");
                Directory.CreateDirectory(appDataPath);
                
                // 检查音频格式并使用适当的扩展名
                string extension = GetAudioExtension(audioBytes);
                var tempFile = Path.Combine(appDataPath, $"audio_{Guid.NewGuid()}{extension}");
                await File.WriteAllBytesAsync(tempFile, audioBytes);
                
                System.Diagnostics.Debug.WriteLine($"播放音频: 临时文件创建 {tempFile}");
                
                await Task.Run(() =>
                {
                    try
                    {
                        // 使用NAudio播放音频
                        currentAudioReader = new AudioFileReader(tempFile);
                        currentAudioPlayer = new WaveOutEvent();
                        
                        // 设置播放完成事件
                        currentAudioPlayer.PlaybackStopped += (sender, e) =>
                        {
                            try
                            {
                                currentAudioReader?.Dispose();
                                currentAudioPlayer?.Dispose();
                                currentAudioReader = null;
                                currentAudioPlayer = null;
                                
                                // 清理临时文件
                                CleanupTempFile(tempFile);
                                
                                if (e.Exception != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"NAudio播放异常: {e.Exception.Message}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("NAudio播放完成");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"播放完成清理异常: {ex.Message}");
                            }
                        };
                        
                        currentAudioPlayer.Init(currentAudioReader);
                        currentAudioPlayer.Play();
                        
                        System.Diagnostics.Debug.WriteLine("NAudio开始播放");
                        
                        // 等待播放完成
                        while (currentAudioPlayer?.PlaybackState == PlaybackState.Playing)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"NAudio播放异常: {ex.Message}");
                        
                        // 如果NAudio失败，回退到简单的播放方式
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("NAudio失败，尝试回退播放");
                            FallbackAudioPlayback(tempFile);
                        }
                        catch (Exception fallbackEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"回退播放也失败: {fallbackEx.Message}");
                            throw new Exception($"音频播放失败: NAudio: {ex.Message}, 回退: {fallbackEx.Message}");
                        }
                        finally
                        {
                            CleanupTempFile(tempFile);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放音频异常: {ex.Message}");
                throw new Exception($"音频播放失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 停止当前音频播放
        /// </summary>
        private void StopCurrentAudio()
        {
            try
            {
                currentAudioPlayer?.Stop();
                currentAudioReader?.Dispose();
                currentAudioPlayer?.Dispose();
                currentAudioReader = null;
                currentAudioPlayer = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止音频播放异常: {ex.Message}");
            }
        }
        /// <summary>
        /// 根据音频数据确定文件扩展名
        /// </summary>
        private string GetAudioExtension(byte[] audioBytes)
        {
            if (audioBytes.Length >= 12)
            {
                var header = System.Text.Encoding.ASCII.GetString(audioBytes, 0, 4);
                if (header == "RIFF")
                {
                    var format = System.Text.Encoding.ASCII.GetString(audioBytes, 8, 4);
                    if (format == "WAVE")
                    {
                        System.Diagnostics.Debug.WriteLine("检测到WAV格式");
                        return ".wav";
                    }
                }
            }
            
            if (audioBytes.Length >= 3)
            {
                var first3Bytes = System.Text.Encoding.ASCII.GetString(audioBytes, 0, 3);
                if (first3Bytes == "ID3")
                {
                    System.Diagnostics.Debug.WriteLine("检测到MP3格式（ID3标签）");
                    return ".mp3";
                }
            }
            
            if (audioBytes.Length >= 2)
            {
                // 检查MP3同步字节 (0xFF 0xFB/0xFA/0xF3/0xF2)
                if (audioBytes[0] == 0xFF && (audioBytes[1] & 0xE0) == 0xE0)
                {
                    System.Diagnostics.Debug.WriteLine("检测到MP3格式（同步字节）");
                    return ".mp3";
                }
            }
            
            // 默认假设为MP3（许多TTS服务返回MP3）
            System.Diagnostics.Debug.WriteLine("未识别格式，默认为MP3");
            return ".mp3";
        }
        /// <summary>
        /// 回退音频播放方法（当NAudio失败时使用）
        /// </summary>
        private void FallbackAudioPlayback(string audioFile)
        {
            try
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"回退播放方法失败: {ex.Message}");
                throw;
            }
        }
        
        private void CleanupTempFile(string tempFile)
        {
            // 异步清理临时文件
            _ = Task.Run(async () =>
            {
                try
                {
                    // 稍微延迟以确保文件没有被占用
                    await Task.Delay(1000);
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                        System.Diagnostics.Debug.WriteLine($"播放音频: 临时文件已删除 {tempFile}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"播放音频: 删除临时文件失败 {ex.Message}");
                }
            });
        }

        #region 对话历史功能

        /// <summary>
        /// 加载对话历史
        /// </summary>
        private async void LoadConversationHistory()
        {
            try
            {
                // 创建新的对话
                await StartNewConversation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load conversation history: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始新的对话
        /// </summary>
        public async Task StartNewConversation()
        {
            try
            {
                // 保存当前对话（如果有的话）
                if (currentConversation != null)
                {
                    await SaveCurrentConversation();
                }

                // 创建新对话
                currentConversation = new DbConversation
                {
                    Title = $"对话 {DateTime.Now:MM-dd HH:mm}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var conversationId = await databaseService.SaveConversationAsync(currentConversation);
                currentConversation.Id = conversationId;

                // 清空当前消息和界面
                currentMessages.Clear();
                ClearDialog();
                
                System.Diagnostics.Debug.WriteLine($"Started new conversation: {currentConversation.Id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start new conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载指定对话
        /// </summary>
        public async Task LoadConversation(int conversationId)
        {
            try
            {
                // 保存当前对话
                if (currentConversation != null)
                {
                    await SaveCurrentConversation();
                }

                // 加载指定对话
                var conversations = await databaseService.GetConversationsAsync();
                currentConversation = conversations.FirstOrDefault(c => c.Id == conversationId);

                if (currentConversation != null)
                {
                    // 加载对话消息
                    currentMessages = await databaseService.GetMessagesAsync(conversationId);
                    
                    // 重建对话界面
                    await RebuildConversationUI();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前对话
        /// </summary>
        public async Task SaveCurrentConversation()
        {
            try
            {
                if (currentConversation == null) return;

                // 更新对话标题（使用第一条用户消息的前20个字符）
                if (currentMessages.Count > 0)
                {
                    var firstUserMessage = currentMessages.FirstOrDefault(m => m.IsUser);
                    if (firstUserMessage != null && firstUserMessage.Content.Length > 0)
                    {
                        var title = firstUserMessage.Content.Length > 20 
                            ? firstUserMessage.Content.Substring(0, 20) + "..."
                            : firstUserMessage.Content;
                        currentConversation.Title = title;
                    }
                }

                currentConversation.UpdatedAt = DateTime.UtcNow;
                await databaseService.SaveConversationAsync(currentConversation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save current conversation: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存消息到数据库
        /// </summary>
        public async Task SaveMessage(string content, bool isUser, string? reasoningContent = null)
        {
            try
            {
                if (currentConversation == null)
                {
                    await StartNewConversation();
                }

                if (currentConversation != null)
                {
                    var message = new DbMessage
                    {
                        ConversationId = currentConversation.Id,
                        Content = content,
                        IsUser = isUser,
                        ReasoningContent = reasoningContent,
                        CreatedAt = DateTime.UtcNow
                    };

                    var messageId = await databaseService.SaveMessageAsync(message);
                    message.Id = messageId;
                    currentMessages.Add(message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save message: {ex.Message}");
            }
        }

        /// <summary>
        /// 重建对话界面
        /// </summary>
        private async Task RebuildConversationUI()
        {
            try
            {
                ClearDialog();

                foreach (var message in currentMessages.OrderBy(m => m.CreatedAt))
                {
                    if (message.IsUser)
                    {
                        AddMessageBubbleWithoutSave(message.Content, true);
                    }
                    else
                    {
                        // 对于AI消息，如果有思维内容，需要特殊处理
                        if (!string.IsNullOrEmpty(message.ReasoningContent))
                        {
                            // TODO: 重建带思维内容的消息气泡
                            AddMessageBubbleWithoutSave(message.Content, false);
                        }
                        else
                        {
                            AddMessageBubbleWithoutSave(message.Content, false);
                        }
                    }
                }

                DialogScrollViewer.ScrollToEnd();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to rebuild conversation UI: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空对话界面
        /// </summary>
        private void ClearDialog()
        {
            DialogMessagesPanel.Children.Clear();
        }

        /// <summary>
        /// 删除对话
        /// </summary>
        public async Task DeleteConversation(int conversationId)
        {
            try
            {
                await databaseService.DeleteConversationAsync(conversationId);
                
                // 如果删除的是当前对话，开始新对话
                if (currentConversation?.Id == conversationId)
                {
                    await StartNewConversation();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete conversation: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取所有对话列表
        /// </summary>
        public async Task<List<DbConversation>> GetAllConversations()
        {
            try
            {
                return await databaseService.GetConversationsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get conversations: {ex.Message}");
                return new List<DbConversation>();
            }
        }

        #endregion
        
        #region 侧边栏事件处理

        /// <summary>
        /// 切换侧边栏显示状态
        /// </summary>
        private async Task ToggleSidebar()
        {
            if (isSidebarVisible)
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
            try
            {
                isSidebarVisible = true;
                
                // 设置侧边栏宽度
                SidebarColumn.Width = new GridLength(150);
                HistorySidebar.Visibility = Visibility.Visible;
                
                // 刷新对话列表
                await RefreshConversationsList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show sidebar: {ex.Message}");
            }
        }

        /// <summary>
        /// 隐藏侧边栏
        /// </summary>
        private async Task HideSidebar()
        {
            try
            {
                isSidebarVisible = false;
                
                // 隐藏侧边栏
                SidebarColumn.Width = new GridLength(0);
                HistorySidebar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to hide sidebar: {ex.Message}");
            }
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// 刷新对话列表
        /// </summary>
        private async Task RefreshConversationsList()
        {
            try
            {
                var conversations = await GetAllConversations();
                ConversationsList.ItemsSource = conversations;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh conversations list: {ex.Message}");
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
                await LoadConversation(conversation.Id);
            }
        }

        private async void DeleteConversationButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is int conversationId)
            {
                var result = MessageBox.Show("确定要删除这个对话吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await DeleteConversation(conversationId);
                        await RefreshConversationsList();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除对话失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion
        
        /// <summary>
        /// 释放音频资源
        /// </summary>
        private void ReleaseAudioResources()
        {
            try
            {
                // 停止并释放音频播放资源
                StopCurrentAudio();
                
                // 关闭MediaFoundation
                try
                {
                    MediaFoundationApi.Shutdown();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MediaFoundation关闭异常: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"音频资源释放异常: {ex.Message}");
            }
        }
    }
}