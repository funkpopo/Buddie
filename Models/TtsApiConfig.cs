using System;
using System.ComponentModel;

namespace Lyxie_desktop.Models
{
    /// <summary>
    /// TTS API配置类
    /// </summary>
    public class TtsApiConfig : INotifyPropertyChanged
    {
        private string _name = "默认TTS配置";
        private TtsProvider _provider = TtsProvider.Azure;
        private string _apiUrl = "";
        private string _apiKey = "";
        private string _voiceModel = "";
        private string _synthesisModel = "";
        private string _language = "zh-CN";
        private float _speed = 1.0f;
        private float _pitch = 0.0f;
        private float _volume = 1.0f;
        private AudioFormat _audioFormat = AudioFormat.Mp3;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public TtsProvider Provider
        {
            get => _provider;
            set
            {
                _provider = value;
                OnPropertyChanged();
                UpdateDefaultsForProvider();
            }
        }

        public string ApiUrl
        {
            get => _apiUrl;
            set
            {
                _apiUrl = value;
                OnPropertyChanged();
            }
        }

        public string ApiKey
        {
            get => _apiKey;
            set
            {
                _apiKey = value;
                OnPropertyChanged();
            }
        }

        public string VoiceModel
        {
            get => _voiceModel;
            set
            {
                _voiceModel = value;
                OnPropertyChanged();
            }
        }

        public string SynthesisModel
        {
            get => _synthesisModel;
            set
            {
                _synthesisModel = value;
                OnPropertyChanged();
            }
        }

        public string Language
        {
            get => _language;
            set
            {
                _language = value;
                OnPropertyChanged();
            }
        }

        public float Speed
        {
            get => _speed;
            set
            {
                _speed = Math.Max(0.25f, Math.Min(4.0f, value));
                OnPropertyChanged();
            }
        }

        public float Pitch
        {
            get => _pitch;
            set
            {
                _pitch = Math.Max(-20.0f, Math.Min(20.0f, value));
                OnPropertyChanged();
            }
        }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0.0f, Math.Min(2.0f, value));
                OnPropertyChanged();
            }
        }

        public AudioFormat AudioFormat
        {
            get => _audioFormat;
            set
            {
                _audioFormat = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateDefaultsForProvider()
        {
            switch (_provider)
            {
                case TtsProvider.Azure:
                    if (string.IsNullOrEmpty(_apiUrl))
                        _apiUrl = "https://[region].tts.speech.microsoft.com/cognitiveservices/v1";
                    if (string.IsNullOrEmpty(_voiceModel))
                        _voiceModel = "zh-CN-XiaoxiaoNeural";
                    break;
                case TtsProvider.OpenAI:
                    if (string.IsNullOrEmpty(_apiUrl))
                        _apiUrl = "https://api.openai.com/v1/audio/speech";
                    if (string.IsNullOrEmpty(_voiceModel))
                        _voiceModel = "alloy";
                    if (string.IsNullOrEmpty(_synthesisModel))
                        _synthesisModel = "tts-1";
                    break;
                case TtsProvider.ElevenLabs:
                    if (string.IsNullOrEmpty(_apiUrl))
                        _apiUrl = "https://api.elevenlabs.io/v1/text-to-speech";
                    if (string.IsNullOrEmpty(_voiceModel))
                        _voiceModel = "JBFqnCBsd6RMkjVDRZzb";
                    if (string.IsNullOrEmpty(_synthesisModel))
                        _synthesisModel = "eleven_multilingual_v2";
                    break;
                case TtsProvider.Custom:
                    // 自定义API，用户需要手动配置
                    break;
            }
        }

        /// <summary>
        /// 验证配置是否完整
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(ApiUrl) &&
                   !string.IsNullOrWhiteSpace(ApiKey) &&
                   !string.IsNullOrWhiteSpace(VoiceModel);
        }

        /// <summary>
        /// 创建配置副本
        /// </summary>
        public TtsApiConfig Clone()
        {
            return new TtsApiConfig
            {
                Name = this.Name,
                Provider = this.Provider,
                ApiUrl = this.ApiUrl,
                ApiKey = this.ApiKey,
                VoiceModel = this.VoiceModel,
                SynthesisModel = this.SynthesisModel,
                Language = this.Language,
                Speed = this.Speed,
                Pitch = this.Pitch,
                Volume = this.Volume,
                AudioFormat = this.AudioFormat
            };
        }
    }

    /// <summary>
    /// TTS服务提供商
    /// </summary>
    public enum TtsProvider
    {
        Azure = 0,
        OpenAI = 1,
        ElevenLabs = 2,
        Custom = 3
    }

    /// <summary>
    /// 音频格式
    /// </summary>
    public enum AudioFormat
    {
        Mp3 = 0,
        Wav = 1,
        Ogg = 2
    }

    /// <summary>
    /// TTS播放状态
    /// </summary>
    public enum TtsPlaybackState
    {
        Idle = 0,
        Loading = 1,
        Playing = 2,
        Paused = 3,
        Stopped = 4,
        Error = 5
    }
} 