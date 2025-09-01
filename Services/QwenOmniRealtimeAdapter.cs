using System;
using System.Threading.Tasks;

namespace Buddie.Services
{
    public class QwenOmniRealtimeAdapter : IDisposable
    {
        private RealtimeClient? _client;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _voice;
        private readonly TurnDetectionMode _turnDetectionMode;

        public event Action<string>? OnTextDelta;
        public event Action<byte[]>? OnAudioDelta;  
        public event Action? OnInterrupt;

        public QwenOmniRealtimeAdapter(
            string baseUrl,
            string apiKey,
            string model,
            string voice,
            TurnDetectionMode turnDetectionMode = TurnDetectionMode.CLIENT_VAD)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
            _model = model;
            _voice = voice;
            _turnDetectionMode = turnDetectionMode;
        }

        public async Task ConnectAsync()
        {
            _client = new RealtimeClient(_baseUrl, _apiKey, _model, _voice, _turnDetectionMode);
            
            // 订阅事件
            _client.OnTextDelta += text => OnTextDelta?.Invoke(text);
            _client.OnAudioDelta += audio => OnAudioDelta?.Invoke(audio);
            _client.OnInterrupt += () => OnInterrupt?.Invoke();

            await _client.ConnectAsync();
        }

        public async Task StartMessageHandlingAsync()
        {
            if (_client == null)
                throw new InvalidOperationException("客户端未连接");

            await _client.HandleMessagesAsync();
        }

        public async Task SendAudioDataAsync(byte[] audioData)
        {
            if (_client == null)
                throw new InvalidOperationException("客户端未连接");

            var base64Audio = Convert.ToBase64String(audioData);
            await _client.SendAudioAsync(base64Audio);
        }

        public async Task SendTextMessageAsync(string text)
        {
            if (_client == null)
                throw new InvalidOperationException("客户端未连接");

            var textEvent = new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "user",
                    content = new[]
                    {
                        new
                        {
                            type = "input_text",
                            text = text
                        }
                    }
                }
            };

            await _client.SendEventAsync(textEvent);

            // 触发响应生成
            var responseEvent = new
            {
                type = "response.create"
            };

            await _client.SendEventAsync(responseEvent);
        }

        public async Task StopGenerationAsync()
        {
            if (_client == null)
                throw new InvalidOperationException("客户端未连接");

            var cancelEvent = new
            {
                type = "response.cancel"
            };

            await _client.SendEventAsync(cancelEvent);
        }

        public async Task CloseAsync()
        {
            if (_client != null)
            {
                await _client.CloseAsync();
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}