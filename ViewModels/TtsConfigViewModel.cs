using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Buddie.ViewModels
{
    public partial class TtsConfigViewModel : ObservableObject
    {
        private readonly AppSettings _appSettings;

        [ObservableProperty]
        private ObservableCollection<TtsConfiguration> configurations = new();

        public TtsConfigViewModel(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _appSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppSettings.IsDarkTheme))
                {
                    OnPropertyChanged(nameof(IsDarkTheme));
                }
            };
        }

        public bool IsDarkTheme => _appSettings.IsDarkTheme;

        public event EventHandler<TtsConfiguration>? ConfigurationAdded;
        public event EventHandler<TtsConfiguration>? ConfigurationRemoved;
        public event EventHandler<TtsConfiguration>? ConfigurationUpdated;
        public event EventHandler<TtsConfiguration>? ConfigurationActivated;
        public event EventHandler<string>? ValidationFailed;

        [RelayCommand]
        private void Add()
        {
            var newConfig = new TtsConfiguration
            {
                Name = $"TTS配置 {Configurations.Count + 1}",
                ChannelType = TtsChannelType.OpenAI,
                IsEditMode = true,
                IsSaved = false,
                IsActive = false
            };
            Configurations.Add(newConfig);
            ConfigurationAdded?.Invoke(this, newConfig);
        }

        [RelayCommand]
        private void Edit(TtsConfiguration config)
        {
            if (config == null) return;
            config.IsEditMode = true;
        }

        [RelayCommand]
        private void Cancel(TtsConfiguration config)
        {
            if (config == null) return;
            if (!config.IsSaved)
            {
                Configurations.Remove(config);
                ConfigurationRemoved?.Invoke(this, config);
            }
            else
            {
                config.IsEditMode = false;
            }
        }

        [RelayCommand]
        private void Remove(TtsConfiguration config)
        {
            if (config == null) return;
            Configurations.Remove(config);
            ConfigurationRemoved?.Invoke(this, config);
        }

        [RelayCommand]
        private void Save(TtsConfiguration config)
        {
            if (config == null) return;

            var validationMessage = Validate(config);
            if (!string.IsNullOrEmpty(validationMessage))
            {
                ValidationFailed?.Invoke(this, validationMessage);
                return;
            }

            config.IsEditMode = false;
            config.IsSaved = true;
            ConfigurationUpdated?.Invoke(this, config);
        }

        [RelayCommand]
        private async void ToggleActivate(TtsConfiguration config)
        {
            if (config == null) return;

            if (config.IsActive)
            {
                // activate
                ConfigurationActivated?.Invoke(this, config);
            }
            else
            {
                // de-activate -> persist change if saved
                if (config.IsSaved && config.Id > 0)
                {
                    try
                    {
                        await _appSettings.SaveTtsConfigurationAsync(config);
                    }
                    catch
                    {
                        // ignore save errors here
                    }
                }
            }
        }

        [RelayCommand]
        private void Activate(TtsConfiguration config)
        {
            if (config == null) return;
            ConfigurationActivated?.Invoke(this, config);
        }

        private string Validate(TtsConfiguration config)
        {
            var missing = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(config.Name)) missing.Add("配置名称");
            if (config.RequiresApiUrl && string.IsNullOrWhiteSpace(config.ApiUrl)) missing.Add("API URL");
            if (config.RequiresApiKey && string.IsNullOrWhiteSpace(config.ApiKey)) missing.Add(config.ApiKeyLabel.TrimEnd(':'));
            if (config.RequiresModel && string.IsNullOrWhiteSpace(config.Model)) missing.Add(config.ModelLabel.TrimEnd(':'));
            if (config.RequiresVoice && string.IsNullOrWhiteSpace(config.Voice)) missing.Add(config.VoiceLabel.TrimEnd(':'));
            if (missing.Count > 0)
            {
                return $"请填写以下必填项：\n• {string.Join("\n• ", missing)}";
            }

            switch (config.ChannelType)
            {
                case TtsChannelType.ElevenLabs:
                    if (!config.ApiUrl.Contains("{voice_id}"))
                        return "ElevenLabs API URL 应包含 {voice_id} 占位符";
                    if (config.Voice.Length != 20)
                        return "ElevenLabs Voice ID 应为20位字符";
                    break;
                case TtsChannelType.OpenAI:
                    var validVoices = new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer" };
                    if (Array.IndexOf(validVoices, config.Voice) < 0)
                        return "请使用有效的 OpenAI 语音（alloy、echo、fable、onyx、nova、shimmer）";
                    break;
                case TtsChannelType.MiniMax:
                    if (string.IsNullOrWhiteSpace(config.ApiUrl) || !config.ApiUrl.Contains("minimax"))
                        return "请使用有效的 MiniMax API URL";
                    if (config.Speed < 0.5 || config.Speed > 2.0)
                        return "MiniMax TTS 语速范围为 0.5-2.0";
                    break;
            }
            return string.Empty;
        }
    }
}

