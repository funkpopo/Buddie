using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buddie.Services
{
    public enum TurnDetectionMode
    {
        ClientVad
    }

    public class RealtimeClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _voice;
        private readonly TurnDetectionMode _turnDetectionMode;
        private readonly ILogger _logger;

        public event Action<string>? OnTextDelta;
        public event Action<byte[]>? OnAudioDelta;
        public event Action? OnInterrupt;

        public RealtimeClient(
            string baseUrl,
            string apiKey,
            string model,
            string voice,
            TurnDetectionMode turnDetectionMode = TurnDetectionMode.ClientVad)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
            _model = model;
            _voice = voice;
            _turnDetectionMode = turnDetectionMode;
            var loggerFactory = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            _logger = (loggerFactory?.CreateLogger(typeof(RealtimeClient).FullName!)) ?? NullLogger.Instance;
        }

        public async Task ConnectAsync()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _webSocket = new ClientWebSocket();

                // 设置请求头
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                _webSocket.Options.SetRequestHeader("X-DashScope-Api-Key", _apiKey);

                var uri = new Uri(_baseUrl);
                await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

                // 发送初始化会话消息
                await SendSessionUpdateAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"WebSocket连接失败: {ex.Message}", ex);
            }
        }

        private async Task SendSessionUpdateAsync()
        {
            var sessionUpdate = new
            {
                type = "session.update",
                session = new
                {
                    modalities = new[] { "text", "audio" },
                    model = _model,
                    voice = _voice,
                    turn_detection = new
                    {
                        type = _turnDetectionMode.ToString().ToLower()
                    },
                    input_audio_format = "pcm16",
                    output_audio_format = "pcm16"
                }
            };

            await SendEventAsync(sessionUpdate);
        }

        public async Task SendEventAsync(object eventData)
        {
            if (_webSocket?.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket未连接");

            var json = JsonSerializer.Serialize(eventData);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource?.Token ?? CancellationToken.None);
        }

        public async Task SendAudioAsync(string base64Audio)
        {
            var audioEvent = new
            {
                event_id = $"event_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                type = "input_audio_buffer.append",
                audio = base64Audio
            };

            await SendEventAsync(audioEvent);
        }

        public async Task HandleMessagesAsync()
        {
            if (_webSocket == null || _cancellationTokenSource == null)
                throw new InvalidOperationException("WebSocket未初始化");

            var buffer = new byte[1024 * 4];

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessageAsync(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消操作，不需要处理
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"消息处理失败: {ex.Message}", ex);
            }
        }

        private Task ProcessMessageAsync(string message)
        {
            try
            {
                using var document = JsonDocument.Parse(message);
                var root = document.RootElement;

                if (root.TryGetProperty("type", out var typeElement))
                {
                    var eventType = typeElement.GetString();

                    switch (eventType)
                    {
                        case "response.text.delta":
                            if (root.TryGetProperty("delta", out var deltaElement))
                            {
                                var text = deltaElement.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    OnTextDelta?.Invoke(text);
                                }
                            }
                            break;

                        case "response.audio.delta":
                            if (root.TryGetProperty("delta", out var audioElement))
                            {
                                var base64Audio = audioElement.GetString();
                                if (!string.IsNullOrEmpty(base64Audio))
                                {
                                    var audioData = Convert.FromBase64String(base64Audio);
                                    OnAudioDelta?.Invoke(audioData);
                                }
                            }
                            break;

                        case "input_audio_buffer.speech_started":
                            OnInterrupt?.Invoke();
                            break;

                        case "session.created":
                        case "session.updated":
                        case "response.created":
                        case "response.done":
                            // 这些事件可以记录日志但不需要特殊处理
                            _logger.LogDebug("收到事件: {Event}", eventType);
                            break;

                        case "error":
                            if (root.TryGetProperty("error", out var errorElement))
                            {
                                var errorMessage = errorElement.GetProperty("message").GetString();
                                throw new InvalidOperationException($"服务器错误: {errorMessage}");
                            }
                            break;
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON解析错误: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "消息处理错误: {Message}", ex.Message);
                throw;
            }

            return Task.CompletedTask;
        }

        public async Task CloseAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "正常关闭",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "关闭WebSocket时出错: {Message}", ex.Message);
            }
        }

        public void Dispose()
        {
            CloseAsync().GetAwaiter().GetResult();
            _cancellationTokenSource?.Dispose();
            _webSocket?.Dispose();
        }
    }
}
