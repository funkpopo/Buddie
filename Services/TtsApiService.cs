using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NAudio.Wave;
using Lyxie_desktop.Interfaces;
using Lyxie_desktop.Models;
using Lyxie_desktop.Helpers;

namespace Lyxie_desktop.Services
{
    public class TtsApiService : ITtsApiService
    {
        private readonly HttpClient _httpClient;
        private WaveOutEvent? _waveOut;
        private MemoryStream? _audioStream;
        private TtsPlaybackState _currentState = TtsPlaybackState.Idle;
        private float _volume = 1.0f;
        private readonly object _stateLock = new object();

        public TtsPlaybackState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
            private set
            {
                lock (_stateLock)
                {
                    _currentState = value;
                }
                StateChanged?.Invoke(value);
            }
        }

        public bool IsPlaying => CurrentState == TtsPlaybackState.Playing;
        public bool IsPaused => CurrentState == TtsPlaybackState.Paused;

        public event TtsStateChangedCallback? StateChanged;
        public event TtsErrorCallback? ErrorOccurred;

        public TtsApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<(bool Success, string Message)> TestApiAsync(TtsApiConfig config)
        {
            try
            {
                if (!config.IsValid())
                {
                    return (false, "配置信息不完整");
                }

                var testText = "测试";
                var audioData = await SynthesizeAudioAsync(config, testText);
                
                if (audioData != null && audioData.Length > 0)
                {
                    return (true, "TTS API连接成功");
                }
                else
                {
                    return (false, "TTS API返回的音频数据为空");
                }
            }
            catch (Exception ex)
            {
                return (false, $"TTS API测试失败: {ex.Message}");
            }
        }

        public async Task<string?> SpeakAsync(TtsApiConfig config, string text, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (!config.IsValid())
                {
                    ErrorOccurred?.Invoke("TTS配置不完整");
                    return null;
                }

                CurrentState = TtsPlaybackState.Loading;

                // 合成音频
                var audioData = await SynthesizeAudioAsync(config, text, cancellationToken);
                if (audioData == null || audioData.Length == 0)
                {
                    CurrentState = TtsPlaybackState.Error;
                    ErrorOccurred?.Invoke("音频合成失败");
                    return null;
                }

                // 缓存音频文件
                var tempPath = AppDataHelper.GetTempPath();
                // 使用哈希值作为文件名避免重复，并确保唯一性
                var fileName = $"{System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text))}.{config.AudioFormat.ToString().ToLower()}";
                var filePath = Path.Combine(tempPath, fileName);
                await File.WriteAllBytesAsync(filePath, audioData, cancellationToken);

                // 停止当前播放
                Stop();

                // 从内存流播放
                _audioStream = new MemoryStream(audioData);
                
                IWaveProvider waveProvider = CreateWaveProvider(config, _audioStream);

                // 应用音量控制
                var volumeProvider = new VolumeWaveProvider16(waveProvider)
                {
                    Volume = _volume
                };

                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Init(volumeProvider);
                
                CurrentState = TtsPlaybackState.Playing;
                _waveOut.Play();

                return filePath;
            }
            catch (OperationCanceledException)
            {
                CurrentState = TtsPlaybackState.Stopped;
                return null;
            }
            catch (Exception ex)
            {
                CurrentState = TtsPlaybackState.Error;
                ErrorOccurred?.Invoke($"播放失败: {ex.Message}");
                return null;
            }
        }

        public async Task PlayFromFileAsync(string filePath, TtsApiConfig config)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    ErrorOccurred?.Invoke("音频缓存文件不存在");
                    return;
                }

                Stop();

                // 从文件流播放
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                _audioStream = new MemoryStream();
                await fileStream.CopyToAsync(_audioStream);
                fileStream.Close();
                _audioStream.Position = 0;

                IWaveProvider waveProvider = CreateWaveProvider(config, _audioStream);

                var volumeProvider = new VolumeWaveProvider16(waveProvider)
                {
                    Volume = _volume
                };

                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Init(volumeProvider);

                CurrentState = TtsPlaybackState.Playing;
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                CurrentState = TtsPlaybackState.Error;
                ErrorOccurred?.Invoke($"从文件播放失败: {ex.Message}");
            }
        }

        private IWaveProvider CreateWaveProvider(TtsApiConfig config, Stream stream)
        {
            try
            {
                stream.Position = 0;
                if (config.AudioFormat == AudioFormat.Mp3)
                {
                    return new Mp3FileReader(stream);
                }
                else
                {
                    return new WaveFileReader(stream);
                }
            }
            catch (Exception)
            {
                stream.Position = 0;
                var waveFormat = new WaveFormat(16000, 16, 1); // 默认格式
                return new RawSourceWaveStream(stream, waveFormat);
            }
        }

        private async Task<byte[]?> SynthesizeAudioAsync(TtsApiConfig config, string text, CancellationToken cancellationToken = default)
        {
            try
            {
                switch (config.Provider)
                {
                    case TtsProvider.Azure:
                        return await SynthesizeAzureTtsAsync(config, text, cancellationToken);
                    case TtsProvider.OpenAI:
                        return await SynthesizeOpenAITtsAsync(config, text, cancellationToken);
                    case TtsProvider.ElevenLabs:
                        return await SynthesizeElevenLabsTtsAsync(config, text, cancellationToken);
                    case TtsProvider.Custom:
                        return await SynthesizeCustomTtsAsync(config, text, cancellationToken);
                    default:
                        throw new NotSupportedException($"不支持的TTS提供商: {config.Provider}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS合成错误: {ex.Message}");
                throw;
            }
        }

        private async Task<byte[]?> SynthesizeAzureTtsAsync(TtsApiConfig config, string text, CancellationToken cancellationToken)
        {
            // 构建SSML
            var ssml = $@"
<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{config.Language}'>
    <voice name='{config.VoiceModel}'>
        <prosody rate='{config.Speed}' pitch='{config.Pitch:+#;-#;+0}Hz' volume='{config.Volume * 100}'>
            {System.Security.SecurityElement.Escape(text)}
        </prosody>
    </voice>
</speak>";

            var request = new HttpRequestMessage(HttpMethod.Post, config.ApiUrl);
            request.Headers.Add("Ocp-Apim-Subscription-Key", config.ApiKey);
            request.Headers.Add("User-Agent", "Lyxie-TTS");
            request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
            request.Headers.Add("X-Microsoft-OutputFormat", GetAzureAudioFormat(config.AudioFormat));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<byte[]?> SynthesizeOpenAITtsAsync(TtsApiConfig config, string text, CancellationToken cancellationToken)
        {
            var requestData = new
            {
                model = config.SynthesisModel,
                input = text,
                voice = config.VoiceModel,
                response_format = GetOpenAIAudioFormat(config.AudioFormat),
                speed = config.Speed
            };

            var request = new HttpRequestMessage(HttpMethod.Post, config.ApiUrl);
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
            request.Content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<byte[]?> SynthesizeElevenLabsTtsAsync(TtsApiConfig config, string text, CancellationToken cancellationToken)
        {
            var requestUrl = $"{config.ApiUrl.TrimEnd('/')}/{config.VoiceModel}";
            
            var requestData = new
            {
                text = text,
                model_id = config.SynthesisModel
            };

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Add("xi-api-key", config.ApiKey);
            request.Content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<byte[]?> SynthesizeCustomTtsAsync(TtsApiConfig config, string text, CancellationToken cancellationToken)
        {
            var requestData = new
            {
                text = text,
                voice = config.VoiceModel,
                language = config.Language,
                speed = config.Speed,
                pitch = config.Pitch,
                volume = config.Volume,
                format = config.AudioFormat.ToString().ToLower()
            };

            var request = new HttpRequestMessage(HttpMethod.Post, config.ApiUrl);
            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
            }
            request.Content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        private string GetAzureAudioFormat(AudioFormat format)
        {
            return format switch
            {
                AudioFormat.Mp3 => "audio-16khz-128kbitrate-mono-mp3",
                AudioFormat.Wav => "riff-16khz-16bit-mono-pcm",
                AudioFormat.Ogg => "ogg-16khz-16bit-mono-opus",
                _ => "audio-16khz-128kbitrate-mono-mp3"
            };
        }

        private string GetOpenAIAudioFormat(AudioFormat format)
        {
            return format switch
            {
                AudioFormat.Mp3 => "mp3",
                AudioFormat.Wav => "wav", 
                AudioFormat.Ogg => "opus",
                _ => "mp3"
            };
        }

        public void Pause()
        {
            try
            {
                if (_waveOut != null && IsPlaying)
                {
                    _waveOut.Pause();
                    CurrentState = TtsPlaybackState.Paused;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"暂停失败: {ex.Message}");
            }
        }

        public void Resume()
        {
            try
            {
                if (_waveOut != null && IsPaused)
                {
                    _waveOut.Play();
                    CurrentState = TtsPlaybackState.Playing;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"恢复播放失败: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }

                if (_audioStream != null)
                {
                    _audioStream.Dispose();
                    _audioStream = null;
                }

                CurrentState = TtsPlaybackState.Stopped;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"停止播放失败: {ex.Message}");
            }
        }

        public void SetVolume(float volume)
        {
            _volume = Math.Max(0.0f, Math.Min(1.0f, volume));
        }

        public float GetVolume()
        {
            return _volume;
        }

        public void ClearQueue()
        {
            Stop();
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            CurrentState = TtsPlaybackState.Idle;
            
            if (e.Exception != null)
            {
                ErrorOccurred?.Invoke($"播放中断: {e.Exception.Message}");
            }

            if (_audioStream != null)
            {
                _audioStream.Dispose();
                _audioStream = null;
            }
        }

        public void Dispose()
        {
            Stop();
            _httpClient?.Dispose();
        }
    }
} 