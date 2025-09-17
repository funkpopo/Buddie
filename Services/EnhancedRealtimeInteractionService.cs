using System;
using System.Threading.Tasks;
using System.Threading;

namespace Buddie.Services
{
    public class EnhancedRealtimeInteractionService : IDisposable
    {
        private QwenOmniRealtimeAdapter? _adapter;
        private RealtimeAudioCapture? _audioCapture;
        private RealtimeAudioPlayer? _audioPlayer;
        private LocalVadDetector? _vadDetector;
        private CancellationTokenSource? _cancellationTokenSource;
        
        private readonly RealtimeConfiguration _configuration;
        private bool _isRunning;

        public event Action<string>? OnTextResponse;
        public event Action<string>? OnStatusUpdate;
        public event Action<Exception>? OnError;

        public EnhancedRealtimeInteractionService(RealtimeConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("服务已在运行中");
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                
                OnStatusUpdate?.Invoke("初始化实时交互服务...");

                // 初始化适配器
                _adapter = new QwenOmniRealtimeAdapter(
                    _configuration.BaseUrl,
                    _configuration.ApiKey,
                    _configuration.Model,
                    _configuration.Voice,
                    TurnDetectionMode.ClientVad);

                // 订阅事件
                _adapter.OnTextDelta += text => OnTextResponse?.Invoke(text);
                _adapter.OnAudioDelta += OnAudioDelta;
                _adapter.OnInterrupt += OnInterrupt;

                // 连接到服务
                OnStatusUpdate?.Invoke("连接到实时交互服务...");
                await _adapter.ConnectAsync();

                // 启动音频播放
                OnStatusUpdate?.Invoke("启动音频播放...");
                _audioPlayer = new RealtimeAudioPlayer(
                    _configuration.SampleRate,
                    1,
                    16);
                _audioPlayer.StartPlayback();

                // 启动音频捕获
                OnStatusUpdate?.Invoke("启动音频捕获...");
                _audioCapture = new RealtimeAudioCapture(
                    _configuration.SampleRate,
                    1,
                    16);
                _audioCapture.OnAudioData += OnAudioCaptured;

                // 初始化VAD检测器（仅客户端VAD）
                _vadDetector = new LocalVadDetector(
                    _configuration.VadThreshold,
                    _configuration.VadMinSpeechFrames,
                    _configuration.VadMinSilenceFrames);
                _vadDetector.OnSpeechStart += OnSpeechStart;
                _vadDetector.OnSpeechEnd += OnSpeechEnd;

                _audioCapture.StartCapture();

                // 启动消息处理
                OnStatusUpdate?.Invoke("启动消息处理...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _adapter.StartMessageHandlingAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，不需要处理
                    }
                    catch (Exception ex)
                    {
                        OnError?.Invoke(ex);
                    }
                }, _cancellationTokenSource.Token);

                _isRunning = true;
                OnStatusUpdate?.Invoke("实时交互服务已启动");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                await StopAsync();
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            try
            {
                OnStatusUpdate?.Invoke("停止实时交互服务...");

                _isRunning = false;
                _cancellationTokenSource?.Cancel();

                _audioCapture?.StopCapture();
                _audioPlayer?.StopPlayback();

                if (_adapter != null)
                {
                    await _adapter.CloseAsync();
                }

                OnStatusUpdate?.Invoke("实时交互服务已停止");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private async void OnAudioCaptured(byte[] audioData)
        {
            try
            {
                if (!_isRunning || _adapter == null) return;

                // 进行语音检测（仅客户端VAD）
                if (_vadDetector != null)
                {
                    _vadDetector.ProcessAudioFrame(audioData);
                    
                    // 只有在检测到语音时才发送音频数据
                    if (_vadDetector.IsSpeaking)
                    {
                        await _adapter.SendAudioDataAsync(audioData);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private void OnAudioDelta(byte[] audioData)
        {
            try
            {
                _audioPlayer?.PlayAudioData(audioData);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private void OnInterrupt()
        {
            try
            {
                OnStatusUpdate?.Invoke("检测到语音输入，停止播放");
                _audioPlayer?.InterruptPlayback();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private void OnSpeechStart()
        {
            try
            {
                OnStatusUpdate?.Invoke("开始说话");
                _audioPlayer?.InterruptPlayback();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private void OnSpeechEnd()
        {
            try
            {
                OnStatusUpdate?.Invoke("结束说话");
                _audioPlayer?.ResumePlayback();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        public async Task SendTextMessageAsync(string message)
        {
            if (!_isRunning || _adapter == null)
            {
                throw new InvalidOperationException("服务未运行");
            }

            try
            {
                await _adapter.SendTextMessageAsync(message);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                throw;
            }
        }

        public async Task StopGenerationAsync()
        {
            if (_adapter != null)
            {
                try
                {
                    await _adapter.StopGenerationAsync();
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                    throw;
                }
            }
        }

        public bool IsRunning => _isRunning;

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            
            _adapter?.Dispose();
            _audioCapture?.Dispose();
            _audioPlayer?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
