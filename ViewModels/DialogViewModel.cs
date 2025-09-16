using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.MediaFoundation;
using NAudio.Wave;
using System.Text;
using System.Text.Json;
using System.Threading;
using Buddie.Database;
using Buddie.Services.ExceptionHandling;
using Buddie.Services.Tts;
using Buddie.Services;
using Buddie;

namespace Buddie.ViewModels
{
    public partial class DialogViewModel : ObservableObject
    {
        private readonly AppSettings _appSettings;
        private readonly DatabaseService _databaseService = Buddie.App.GetService<DatabaseService>();
        private readonly ITtsServiceResolver _ttsServiceResolver = Buddie.App.GetService<ITtsServiceResolver>();
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory = Buddie.App.GetService<System.Net.Http.IHttpClientFactory>();

        // Messages collection for virtualization binding
        [ObservableProperty]
        private ObservableCollection<MessageDisplayModel> messages = new();

        // Input text (optional two-way bind if needed)
        [ObservableProperty]
        private string inputText = string.Empty;

        // Current API configuration
        [ObservableProperty]
        private OpenApiConfiguration? currentApiConfiguration;

        // Multimodal image state (set by view)
        [ObservableProperty]
        private byte[]? screenshotImage;

        [ObservableProperty]
        private byte[]? localImage;

        // Sending state and cancellation
        private CancellationTokenSource? _currentRequest;
        private DbConversation? _currentConversation;
        private List<DbMessage> _currentMessages = new();

        // TTS playback resources
        private WaveOutEvent? _currentAudioPlayer;
        private AudioFileReader? _currentAudioReader;

        public DialogViewModel(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _appSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppSettings.IsDarkTheme))
                {
                    OnPropertyChanged(nameof(IsDarkTheme));
                }
                if (e.PropertyName == nameof(AppSettings.TtsConfigurations))
                {
                    OnPropertyChanged(nameof(HasActiveTtsConfiguration));
                }
            };

            // Ensure NAudio MediaFoundation is initialized for MP3
            ExceptionHandlingService.ExecuteSafely(() => MediaFoundationApi.Startup(),
                ExceptionHandlingService.HandlingStrategy.LogOnly,
                new ExceptionHandlingService.ExceptionContext { Component = nameof(DialogViewModel), Operation = "NAudio Startup" });
        }

        public bool IsDarkTheme => _appSettings.IsDarkTheme;
        public bool HasActiveTtsConfiguration => _appSettings.HasActiveTtsConfiguration;
        public bool HasScreenshot => ScreenshotImage != null;
        public bool HasLocalImage => LocalImage != null;
        
        // Conversation list for sidebar
        [ObservableProperty]
        private ObservableCollection<DbConversation> conversations = new();

        public event EventHandler<string>? SendRequested;
        public event EventHandler? ScreenshotRequested;
        public event EventHandler? ImageUploadRequested;
        public event EventHandler? ToggleSidebarRequested;
        public event EventHandler? CloseRequested;
        public event EventHandler? NewConversationRequested;
        public event EventHandler<string>? CopyRequested;
        public event EventHandler<int>? DeleteConversationRequested;
        public event EventHandler? RemoveScreenshotRequested;
        public event EventHandler? OpenScreenshotRequested;

        // HTTP/Streaming notifications for the view to update UI and persist
        public event EventHandler? StreamingStarted;
        public event EventHandler<StreamingDeltaEventArgs>? StreamingDelta;
        public event EventHandler<NonStreamingResponseEventArgs>? ResponseReady;
        public event EventHandler? StreamingCompleted;

        [RelayCommand]
        private void SendMessage(string message)
        {
            // Keep existing pipeline: raise to view for now
            SendRequested?.Invoke(this, message ?? string.Empty);
        }

        [RelayCommand]
        private void Screenshot()
        {
            ScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ImageUpload()
        {
            ImageUploadRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            ToggleSidebarRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void NewConversation()
        {
            NewConversationRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void CopyMessage(string? text)
        {
            text ??= string.Empty;
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                }
            }, ExceptionHandlingService.HandlingStrategy.LogOnly,
            new ExceptionHandlingService.ExceptionContext { Component = nameof(DialogViewModel), Operation = "复制消息" });
        }

        [RelayCommand]
        private async Task PlayTts(string? text)
        {
            text ??= string.Empty;
            await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                var ttsConfig = _appSettings.GetActiveTtsConfiguration();
                if (ttsConfig == null)
                {
                    var msg = Buddie.Localization.LocalizationManager.GetString("Dialog_Tts_MissingConfig_Message");
                    var title = Buddie.Localization.LocalizationManager.GetString("Info_Title");
                    MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (ttsConfig == null)
                {
                    MessageBox.Show("未找到TTS配置，请先在设置中配置并激活TTS服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                await CallTtsApi(text, ttsConfig);
            }, "TTS语音合成（VM）");
        }

        [RelayCommand]
        private void DeleteConversation(int conversationId)
        {
            DeleteConversationRequested?.Invoke(this, conversationId);
        }

        [RelayCommand]
        private void RemoveScreenshot()
        {
            RemoveScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void OpenScreenshot()
        {
            OpenScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task SendMessageToApiAsync(string message)
        {
            var apiConfig = CurrentApiConfiguration;
            if (apiConfig == null)
            {
                var msg = Buddie.Localization.LocalizationManager.GetString("Dialog_ConfigureApiFirst");
                ResponseReady?.Invoke(this, new NonStreamingResponseEventArgs(msg));
                return;
            }
            if (apiConfig == null)
            {
                // Let the view handle user feedback
                ResponseReady?.Invoke(this, new NonStreamingResponseEventArgs("请先配置API才能进行对话。"));
                return;
            }

            // Determine multimodal
            bool hasScreenshot = HasScreenshot;
            bool hasLocalImage = HasLocalImage;
            bool hasImage = hasScreenshot || hasLocalImage;
            bool configSupportsMultimodal = apiConfig.IsMultimodalEnabled;
            bool channelSupportsMultimodal = MultimodalApiService.SupportsMultimodal(apiConfig.ChannelType);
            bool isMultimodal = hasImage && configSupportsMultimodal && channelSupportsMultimodal;

            await ExceptionHandlingService.Network.ExecuteSafelyAsync(async () =>
            {
                _currentRequest?.Dispose();
                _currentRequest = new CancellationTokenSource();

                using var httpClient = CreateHttpClient(apiConfig);
                var requestContent = BuildRequestContent(message, isMultimodal, apiConfig);
                var finalApiUrl = BuildFinalApiUrl(apiConfig);

                if (apiConfig.IsStreamingEnabled)
                {
                    // Streaming path
                    var response = await SendStreamingRequest(httpClient, finalApiUrl, requestContent);
                    if (!response.IsSuccessStatusCode)
                    {
                        ResponseReady?.Invoke(this, new NonStreamingResponseEventArgs($"API请求失败: {response.StatusCode}"));
                        return false;
                    }

                    StreamingStarted?.Invoke(this, EventArgs.Empty);
                    await ProcessStreamData(response, apiConfig.ChannelType);
                    StreamingCompleted?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // Non-streaming path
                    var response = await httpClient.PostAsync(finalApiUrl, requestContent, _currentRequest?.Token ?? CancellationToken.None);
                    var responseText = await response.Content.ReadAsStringAsync();
                    _currentRequest?.Token.ThrowIfCancellationRequested();
                    if (response.IsSuccessStatusCode)
                    {
                        var messageContent = ApiResponseService.ParseNonStreamingResponse(responseText, apiConfig.ChannelType);
                        ResponseReady?.Invoke(this, new NonStreamingResponseEventArgs(messageContent));
                    }
                    else
                    {
                        ResponseReady?.Invoke(this, new NonStreamingResponseEventArgs($"API请求失败: {response.StatusCode}"));
                    }
                }
                return true;
            }, false, "发送API请求");
        }

        public void CancelSend()
        {
            try { _currentRequest?.Cancel(); } catch { }
        }

        private HttpClient CreateHttpClient(OpenApiConfiguration apiConfig)
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            switch (apiConfig.ChannelType)
            {
                case ChannelType.GoogleGemini:
                    break; // key in URL
                case ChannelType.AnthropicClaude:
                    httpClient.DefaultRequestHeaders.Add("x-api-key", apiConfig.ApiKey);
                    httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    break;
                default:
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiConfig.ApiKey}");
                    break;
            }
            return httpClient;
        }

        private StringContent BuildRequestContent(string actualMessage, bool isMultimodal, OpenApiConfiguration apiConfig)
        {
            object requestBody;
            if (isMultimodal)
            {
                byte[]? imageData = ScreenshotImage ?? LocalImage;
                if (imageData != null)
                {
                    var imageBase64 = ConvertImageToBase64(imageData);
                    requestBody = MultimodalApiService.BuildMultimodalRequest(actualMessage, imageBase64, apiConfig);
                }
                else
                {
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

        private string BuildFinalApiUrl(OpenApiConfiguration apiConfig)
        {
            return apiConfig.ChannelType switch
            {
                ChannelType.GoogleGemini => $"{apiConfig.ApiUrl}?key={apiConfig.ApiKey}",
                _ => apiConfig.ApiUrl
            };
        }

        private async Task<HttpResponseMessage> SendStreamingRequest(HttpClient httpClient, string apiUrl, StringContent requestContent)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = requestContent
            };
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _currentRequest?.Token ?? CancellationToken.None);
        }

        private async Task ProcessStreamData(HttpResponseMessage response, ChannelType channelType)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                _currentRequest?.Token.ThrowIfCancellationRequested();
                if (line.StartsWith("data:"))
                {
                    var jsonData = line.Substring(5).Trim();
                    if (jsonData == "[DONE]") continue;
                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, channelType);
                        if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(reasoning))
                        {
                            StreamingDelta?.Invoke(this, new StreamingDeltaEventArgs(content, reasoning));
                        }
                    }
                }
            }
        }

        private static string ConvertImageToBase64(byte[] imageBytes) => Convert.ToBase64String(imageBytes);

        private async Task CallTtsApi(string text, TtsConfiguration ttsConfig)
        {
            await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                // Cache key based on key TTS fields
                var textHash = GenerateHash($"{text}_{ttsConfig.Model}_{ttsConfig.Voice}_{ttsConfig.Speed}_{ttsConfig.ChannelType}");
                var ttsConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    channelType = ttsConfig.ChannelType.ToString(),
                    model = ttsConfig.Model,
                    voice = ttsConfig.Voice,
                    speed = ttsConfig.Speed
                });
                var cachedAudio = await _databaseService.GetTtsAudioAsync(textHash);
                if (cachedAudio != null)
                {
                    await PlayAudioFromBytes(cachedAudio.AudioData);
                    return;
                }

                using var service = _ttsServiceResolver.Create(ttsConfig.ChannelType);
                var request = new TtsRequest
                {
                    Text = text,
                    Configuration = ttsConfig
                };
                var response = await service.ConvertTextToSpeechAsync(request);
                if (response.IsSuccess && response.AudioData != null)
                {
                    await _databaseService.SaveTtsAudioAsync(textHash, response.AudioData, ttsConfigJson);
                    await PlayAudioFromBytes(response.AudioData, response.ContentType);
                }
                else
                {
                    throw new Exception(response.ErrorMessage ?? "TTS服务失败");
                }
            }, "TTS语音合成");
        }

        private async Task PlayAudioFromBytes(byte[] audioBytes, string? contentType = null)
        {
            await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                StopCurrentAudio();

                var tempPath = Path.GetTempPath();
                var extension = GetAudioExtension(audioBytes, contentType);
                var tempFile = Path.Combine(tempPath, $"buddie_tts_{Guid.NewGuid()}{extension}");
                await File.WriteAllBytesAsync(tempFile, audioBytes);

                try
                {
                    _currentAudioReader = new AudioFileReader(tempFile);
                    _currentAudioPlayer = new WaveOutEvent();
                    _currentAudioPlayer.PlaybackStopped += async (s, e) =>
                    {
                        try
                        {
                            await CleanupAudioResourcesAsync(tempFile);
                        }
                        catch { }
                    };
                    _currentAudioPlayer.Init(_currentAudioReader);
                    _currentAudioPlayer.Play();
                }
                catch
                {
                    await CleanupAudioResourcesAsync(tempFile);
                    throw;
                }
            }, "播放TTS音频");
        }

        private void StopCurrentAudio()
        {
            try
            {
                _currentAudioPlayer?.Stop();
                _currentAudioPlayer?.Dispose();
                _currentAudioPlayer = null;
            }
            catch { }
            finally
            {
                _currentAudioReader?.Dispose();
                _currentAudioReader = null;
            }
        }

        private async Task CleanupAudioResourcesAsync(string tempFile)
        {
            await Task.Yield();
            StopCurrentAudio();
            try
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch { }
        }

        private static string GetAudioExtension(byte[] audioBytes, string? contentType)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.Contains("mpeg") || contentType.Contains("mp3")) return ".mp3";
                if (contentType.Contains("wav")) return ".wav";
                if (contentType.Contains("ogg")) return ".ogg";
                if (contentType.Contains("aac")) return ".aac";
            }
            // Fallback to mp3
            return ".mp3";
        }

        private static string GenerateHash(string input)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        // ===== Conversation persistence (moved from code-behind) =====

        public async Task StartNewConversationAsync()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                if (_currentConversation != null)
                {
                    await SaveCurrentConversationAsync();
                }
                _currentConversation = new DbConversation
                {
                    Title = $"对话 {DateTime.Now:MM-dd HH:mm}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Id = 0
                };
                _currentMessages.Clear();
                Messages.Clear();
            }, operationName: "开始新对话");
        }

        public async Task LoadConversationAsync(int conversationId)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                if (_currentConversation != null)
                {
                    await SaveCurrentConversationAsync();
                }
                var list = await _databaseService.GetConversationsAsync();
                _currentConversation = list.FirstOrDefault(c => c.Id == conversationId);
                if (_currentConversation != null)
                {
                    _currentMessages = await _databaseService.GetMessagesAsync(conversationId);
                    Messages.Clear();
                    foreach (var m in _currentMessages.OrderBy(m => m.CreatedAt))
                    {
                        Messages.Add(new MessageDisplayModel
                        {
                            Content = m.Content,
                            IsUser = m.IsUser,
                            ReasoningContent = m.ReasoningContent,
                            Timestamp = m.CreatedAt,
                            ImageData = m.ImageData,
                            HasImage = m.HasImage
                        });
                    }
                }
            }, operationName: "加载指定对话");
        }

        public async Task SaveCurrentConversationAsync()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                if (_currentConversation == null) return;
                var hasUserContent = _currentMessages.Any(m => m.IsUser && !string.IsNullOrWhiteSpace(m.Content));
                if (!hasUserContent) return;

                if (_currentMessages.Count > 0)
                {
                    var firstUserMessage = _currentMessages.FirstOrDefault(m => m.IsUser);
                    if (firstUserMessage != null && firstUserMessage.Content.Length > 0)
                    {
                        var title = firstUserMessage.Content.Length > 20 ? firstUserMessage.Content.Substring(0, 20) + "..." : firstUserMessage.Content;
                        _currentConversation.Title = title;
                    }
                }
                _currentConversation.UpdatedAt = DateTime.UtcNow;
                if (_currentConversation.Id == 0)
                {
                    var conversationId = await _databaseService.SaveConversationAsync(_currentConversation);
                    _currentConversation.Id = conversationId;
                }
                else
                {
                    await _databaseService.SaveConversationAsync(_currentConversation);
                }
            }, operationName: "保存当前对话");
        }

        public async Task SaveMessageAsync(string content, bool isUser, string? reasoningContent = null, byte[]? imageData = null)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                if (_currentConversation == null)
                {
                    await StartNewConversationAsync();
                }
                if (_currentConversation != null)
                {
                    if (isUser && _currentConversation.Id == 0)
                    {
                        var tempMessage = new DbMessage
                        {
                            Content = content,
                            IsUser = isUser,
                            CreatedAt = DateTime.UtcNow,
                            ImageData = imageData,
                            ImageContentType = imageData != null ? "image/png" : null
                        };
                        _currentMessages.Add(tempMessage);
                        await SaveCurrentConversationAsync();
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
                    _currentConversation!.UpdatedAt = DateTime.UtcNow;
                    await _databaseService.SaveConversationAsync(_currentConversation!);
                }
            }, operationName: "保存消息");
        }

        public async Task DeleteConversationByIdAsync(int conversationId)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                await _databaseService.DeleteConversationAsync(conversationId);
                if (_currentConversation?.Id == conversationId)
                {
                    await StartNewConversationAsync();
                }
                await RefreshConversationsAsync();
            }, operationName: "删除对话");
        }

        public async Task RefreshConversationsAsync()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                var list = await _databaseService.GetConversationsAsync();
                Conversations.Clear();
                foreach (var c in list.OrderByDescending(c => c.UpdatedAt))
                {
                    Conversations.Add(c);
                }
            }, operationName: "刷新对话列表");
        }

        public class StreamingDeltaEventArgs : EventArgs
        {
            public string? Content { get; }
            public string? Reasoning { get; }
            public StreamingDeltaEventArgs(string? content, string? reasoning)
            {
                Content = content;
                Reasoning = reasoning;
            }
        }

        public class NonStreamingResponseEventArgs : EventArgs
        {
            public string Content { get; }
            public NonStreamingResponseEventArgs(string content)
            {
                Content = content;
            }
        }
    }
}
