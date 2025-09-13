using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Buddie.Services
{
    public class RealtimeAudioCapture : IDisposable
    {
        private WaveInEvent? _waveIn;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bitsPerSample;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<byte[]>? OnAudioData;

        public RealtimeAudioCapture(int sampleRate = 24000, int channels = 1, int bitsPerSample = 16)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = bitsPerSample;
        }

        public void StartCapture()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channels),
                    BufferMilliseconds = 100 // 100ms buffer
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _waveIn.StartRecording();
                System.Diagnostics.Debug.WriteLine("音频捕获已启动");
            }
            catch (Exception ex)
            {
                throw new Exception($"启动音频捕获失败: {ex.Message}", ex);
            }
        }

        public void StopCapture()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _waveIn?.StopRecording();
                System.Diagnostics.Debug.WriteLine("音频捕获已停止");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止音频捕获时出错: {ex.Message}");
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (!_cancellationTokenSource?.Token.IsCancellationRequested ?? false)
                {
                    var audioData = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, audioData, e.BytesRecorded);
                    OnAudioData?.Invoke(audioData);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"音频数据处理错误: {ex.Message}");
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"录音停止异常: {e.Exception.Message}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("录音正常停止");
            }
        }

        public void Dispose()
        {
            StopCapture();
            _waveIn?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }

    public class RealtimeAudioPlayer : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> _audioQueue;
        private readonly AutoResetEvent _audioAvailableEvent;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _waveProvider;
        private Thread? _playbackThread;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bitsPerSample;
        private volatile bool _isDisposed;

        // 中断标志
        private volatile bool _interruptPlayback;

        public RealtimeAudioPlayer(int sampleRate = 24000, int channels = 1, int bitsPerSample = 16)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = bitsPerSample;
            _audioQueue = new ConcurrentQueue<byte[]>();
            _audioAvailableEvent = new AutoResetEvent(false);
        }

        public void StartPlayback()
        {
            try
            {
                var waveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channels);
                _waveProvider = new BufferedWaveProvider(waveFormat)
                {
                    BufferLength = _sampleRate * _channels * (_bitsPerSample / 8) * 10, // 10秒缓冲
                    DiscardOnBufferOverflow = true
                };

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_waveProvider);
                _waveOut.Play();

                _playbackThread = new Thread(PlaybackWorker)
                {
                    IsBackground = true,
                    Name = "RealtimeAudioPlayback"
                };
                _playbackThread.Start();

                System.Diagnostics.Debug.WriteLine("音频播放已启动");
            }
            catch (Exception ex)
            {
                throw new Exception($"启动音频播放失败: {ex.Message}", ex);
            }
        }

        public void StopPlayback()
        {
            try
            {
                _isDisposed = true;
                _audioAvailableEvent.Set();
                
                _playbackThread?.Join(1000);
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveProvider = null;
                
                System.Diagnostics.Debug.WriteLine("音频播放已停止");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止音频播放时出错: {ex.Message}");
            }
        }

        public void PlayAudioData(byte[] audioData)
        {
            if (_isDisposed || _interruptPlayback) return;

            try
            {
                _audioQueue.Enqueue(audioData);
                _audioAvailableEvent.Set();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"音频数据排队错误: {ex.Message}");
            }
        }

        public void InterruptPlayback()
        {
            System.Diagnostics.Debug.WriteLine("检测到语音输入，停止音频播放");
            _interruptPlayback = true;
            ClearAudioQueue();
        }

        public void ResumePlayback()
        {
            System.Diagnostics.Debug.WriteLine("恢复音频播放");
            _interruptPlayback = false;
        }

        private void ClearAudioQueue()
        {
            // 清空音频队列
            while (_audioQueue.TryDequeue(out _)) { }
            
            // 清空缓冲区
            _waveProvider?.ClearBuffer();
        }

        private void PlaybackWorker()
        {
            while (!_isDisposed)
            {
                try
                {
                    _audioAvailableEvent.WaitOne();

                    if (_isDisposed) break;

                    // 检查中断标志
                    if (_interruptPlayback)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    // 处理所有可用的音频数据
                    while (_audioQueue.TryDequeue(out var audioData) && !_interruptPlayback)
                    {
                        if (_waveProvider != null)
                        {
                            _waveProvider.AddSamples(audioData, 0, audioData.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"音频播放工作线程错误: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        public void Dispose()
        {
            StopPlayback();
            _audioAvailableEvent?.Dispose();
        }
    }
}