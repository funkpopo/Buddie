using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading.Tasks;
using Buddie.Database;

namespace Buddie
{
    public enum TestStatus
    {
        NotTested,
        Testing,
        Success,
        Failed
    }

    public enum ChannelType
    {
        Custom,
        GoogleGemini,
        ZhipuGLM,
        TongyiQwen,
        SiliconFlow,
        OpenAI,
        AnthropicClaude
    }

    public class PresetChannel
    {
        public string Name { get; set; } = "";
        public ChannelType ChannelType { get; set; }
        public string DefaultApiUrl { get; set; } = "";
        public string[] SupportedModels { get; set; } = Array.Empty<string>();
        public bool SupportsStreaming { get; set; } = true;
        public bool SupportsMultimodal { get; set; } = false;
        public bool SupportsThinking { get; set; } = false;
        public string AuthHeaderFormat { get; set; } = "Bearer {0}";
        public string RequestFormat { get; set; } = "openai";
    }

    public class OpenApiConfiguration : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _apiUrl = "";
        private string _apiKey = "";
        private string _modelName = "";
        private bool _isStreamingEnabled = true;
        private bool _isMultimodalEnabled = false;
        private bool _isEditMode = true;
        private bool _isSaved = false;
        private TestStatus _testStatus = TestStatus.NotTested;
        private string _testMessage = "";
        private ChannelType _channelType = ChannelType.Custom;
        private bool _supportsThinking = false;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ApiUrl
        {
            get => _apiUrl;
            set => SetProperty(ref _apiUrl, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string ModelName
        {
            get => _modelName;
            set => SetProperty(ref _modelName, value);
        }

        public bool IsStreamingEnabled
        {
            get => _isStreamingEnabled;
            set => SetProperty(ref _isStreamingEnabled, value);
        }

        public bool IsMultimodalEnabled
        {
            get => _isMultimodalEnabled;
            set => SetProperty(ref _isMultimodalEnabled, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public bool IsSaved
        {
            get => _isSaved;
            set => SetProperty(ref _isSaved, value);
        }

        public TestStatus TestStatus
        {
            get => _testStatus;
            set => SetProperty(ref _testStatus, value);
        }

        public string TestMessage
        {
            get => _testMessage;
            set => SetProperty(ref _testMessage, value);
        }

        public ChannelType ChannelType
        {
            get => _channelType;
            set => SetProperty(ref _channelType, value);
        }

        public bool SupportsThinking
        {
            get => _supportsThinking;
            set => SetProperty(ref _supportsThinking, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // Convert to database model
        public DbApiConfiguration ToDbModel()
        {
            return new DbApiConfiguration
            {
                Id = this.Id,
                Name = this.Name,
                ApiUrl = this.ApiUrl,
                ApiKey = this.ApiKey,
                ModelName = this.ModelName,
                IsStreamingEnabled = this.IsStreamingEnabled,
                IsMultimodalEnabled = this.IsMultimodalEnabled,
                ChannelType = (int)this.ChannelType,
                SupportsThinking = this.SupportsThinking
            };
        }

        // Create from database model
        public static OpenApiConfiguration FromDbModel(DbApiConfiguration dbModel)
        {
            return new OpenApiConfiguration
            {
                Id = dbModel.Id,
                Name = dbModel.Name,
                ApiUrl = dbModel.ApiUrl,
                ApiKey = dbModel.ApiKey,
                ModelName = dbModel.ModelName,
                IsStreamingEnabled = dbModel.IsStreamingEnabled,
                IsMultimodalEnabled = dbModel.IsMultimodalEnabled,
                ChannelType = (ChannelType)dbModel.ChannelType,
                SupportsThinking = dbModel.SupportsThinking,
                IsSaved = true,
                IsEditMode = false
            };
        }
    }

    public class OpenAiTtsConfiguration : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _apiUrl = "http://localhost:5050/v1/audio/speech";
        private string _apiKey = "";
        private string _model = "tts-1";
        private string _voice = "alloy";
        private double _speed = 1.0;
        private bool _isEditMode = true;
        private bool _isSaved = false;
        private bool _isStreamingEnabled = false;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ApiUrl
        {
            get => _apiUrl;
            set => SetProperty(ref _apiUrl, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string Voice
        {
            get => _voice;
            set => SetProperty(ref _voice, value);
        }

        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public bool IsSaved
        {
            get => _isSaved;
            set => SetProperty(ref _isSaved, value);
        }

        public bool IsStreamingEnabled
        {
            get => _isStreamingEnabled;
            set => SetProperty(ref _isStreamingEnabled, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // Convert to database model
        public DbTtsConfiguration ToDbModel()
        {
            return new DbTtsConfiguration
            {
                Id = this.Id,
                Name = this.Name,
                ApiUrl = this.ApiUrl,
                ApiKey = this.ApiKey,
                Model = this.Model,
                Voice = this.Voice,
                Speed = this.Speed,
                IsStreamingEnabled = this.IsStreamingEnabled
            };
        }

        // Create from database model
        public static OpenAiTtsConfiguration FromDbModel(DbTtsConfiguration dbModel)
        {
            return new OpenAiTtsConfiguration
            {
                Id = dbModel.Id,
                Name = dbModel.Name,
                ApiUrl = dbModel.ApiUrl,
                ApiKey = dbModel.ApiKey,
                Model = dbModel.Model,
                Voice = dbModel.Voice,
                Speed = dbModel.Speed,
                IsStreamingEnabled = dbModel.IsStreamingEnabled,
                IsSaved = true,
                IsEditMode = false
            };
        }
    }

    public class AppSettings : INotifyPropertyChanged
    {
        private bool _isTopmost = true;
        private bool _showInTaskbar = true;
        private bool _enableAnimation = true;
        private bool _isDarkTheme = false;
        private ObservableCollection<OpenApiConfiguration> _apiConfigurations = new ObservableCollection<OpenApiConfiguration>();
        private ObservableCollection<OpenAiTtsConfiguration> _ttsConfigurations = new ObservableCollection<OpenAiTtsConfiguration>();
        private DatabaseService _databaseService = new DatabaseService();

        public bool IsTopmost
        {
            get => _isTopmost;
            set => SetProperty(ref _isTopmost, value);
        }

        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set => SetProperty(ref _showInTaskbar, value);
        }

        public bool EnableAnimation
        {
            get => _enableAnimation;
            set => SetProperty(ref _enableAnimation, value);
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set => SetProperty(ref _isDarkTheme, value);
        }

        public ObservableCollection<OpenApiConfiguration> ApiConfigurations
        {
            get => _apiConfigurations;
            set => SetProperty(ref _apiConfigurations, value);
        }

        public ObservableCollection<OpenAiTtsConfiguration> TtsConfigurations
        {
            get => _ttsConfigurations;
            set => SetProperty(ref _ttsConfigurations, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // Load settings from database
        public async Task LoadFromDatabaseAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting to load settings from database...");
                
                // Load app settings
                var dbAppSettings = await _databaseService.GetAppSettingsAsync();
                if (dbAppSettings != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Loaded app settings: Topmost={dbAppSettings.IsTopmost}, DarkTheme={dbAppSettings.IsDarkTheme}");
                    IsTopmost = dbAppSettings.IsTopmost;
                    ShowInTaskbar = dbAppSettings.ShowInTaskbar;
                    EnableAnimation = dbAppSettings.EnableAnimation;
                    IsDarkTheme = dbAppSettings.IsDarkTheme;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No app settings found in database");
                }

                // Load API configurations
                var dbApiConfigs = await _databaseService.GetApiConfigurationsAsync();
                System.Diagnostics.Debug.WriteLine($"Found {dbApiConfigs.Count} API configurations in database");
                ApiConfigurations.Clear();
                foreach (var dbConfig in dbApiConfigs)
                {
                    ApiConfigurations.Add(OpenApiConfiguration.FromDbModel(dbConfig));
                    System.Diagnostics.Debug.WriteLine($"Loaded API config: {dbConfig.Name} - {dbConfig.ModelName}");
                }

                // Load TTS configurations
                var dbTtsConfigs = await _databaseService.GetTtsConfigurationsAsync();
                System.Diagnostics.Debug.WriteLine($"Found {dbTtsConfigs.Count} TTS configurations in database");
                TtsConfigurations.Clear();
                foreach (var dbConfig in dbTtsConfigs)
                {
                    TtsConfigurations.Add(OpenAiTtsConfiguration.FromDbModel(dbConfig));
                    System.Diagnostics.Debug.WriteLine($"Loaded TTS config: {dbConfig.Name}");
                }
                
                System.Diagnostics.Debug.WriteLine("Successfully loaded settings from database");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings from database: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Save settings to database
        public async Task SaveToDatabaseAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Starting to save settings to database...");
                
                // Save app settings
                var dbAppSettings = new DbAppSettings
                {
                    Id = 1, // Single settings record
                    IsTopmost = IsTopmost,
                    ShowInTaskbar = ShowInTaskbar,
                    EnableAnimation = EnableAnimation,
                    IsDarkTheme = IsDarkTheme
                };
                await _databaseService.SaveAppSettingsAsync(dbAppSettings);
                System.Diagnostics.Debug.WriteLine("App settings saved to database");

                // Save API configurations
                System.Diagnostics.Debug.WriteLine($"Saving {ApiConfigurations.Count} API configurations...");
                foreach (var config in ApiConfigurations)
                {
                    if (config.IsSaved) // Remove the Id > 0 condition to allow new configurations
                    {
                        var dbConfig = config.ToDbModel();
                        var savedId = await _databaseService.SaveApiConfigurationAsync(dbConfig);
                        config.Id = savedId; // Update the configuration with the new ID
                        System.Diagnostics.Debug.WriteLine($"Saved API config: {config.Name} with ID {savedId}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipped API config: {config.Name} (not marked as saved)");
                    }
                }

                // Save TTS configurations
                System.Diagnostics.Debug.WriteLine($"Saving {TtsConfigurations.Count} TTS configurations...");
                foreach (var config in TtsConfigurations)
                {
                    if (config.IsSaved) // Remove the Id > 0 condition to allow new configurations
                    {
                        var dbConfig = config.ToDbModel();
                        var savedId = await _databaseService.SaveTtsConfigurationAsync(dbConfig);
                        config.Id = savedId; // Update the configuration with the new ID
                        System.Diagnostics.Debug.WriteLine($"Saved TTS config: {config.Name} with ID {savedId}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipped TTS config: {config.Name} (not marked as saved)");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("Successfully saved all settings to database");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings to database: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Save individual API configuration
        public async Task SaveApiConfigurationAsync(OpenApiConfiguration config)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Saving individual API configuration: {config.Name} (ID: {config.Id})");
                var dbConfig = config.ToDbModel();
                var id = await _databaseService.SaveApiConfigurationAsync(dbConfig);
                config.Id = id;
                config.IsSaved = true;
                System.Diagnostics.Debug.WriteLine($"Successfully saved API configuration: {config.Name} with new ID: {id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save API configuration: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Save individual TTS configuration
        public async Task SaveTtsConfigurationAsync(OpenAiTtsConfiguration config)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Saving individual TTS configuration: {config.Name} (ID: {config.Id})");
                var dbConfig = config.ToDbModel();
                var id = await _databaseService.SaveTtsConfigurationAsync(dbConfig);
                config.Id = id;
                config.IsSaved = true;
                System.Diagnostics.Debug.WriteLine($"Successfully saved TTS configuration: {config.Name} with new ID: {id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save TTS configuration: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Delete API configuration
        public async Task DeleteApiConfigurationAsync(OpenApiConfiguration config)
        {
            try
            {
                if (config.Id > 0)
                {
                    await _databaseService.DeleteApiConfigurationAsync(config.Id);
                }
                ApiConfigurations.Remove(config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete API configuration: {ex.Message}");
                throw;
            }
        }

        // Delete TTS configuration
        public async Task DeleteTtsConfigurationAsync(OpenAiTtsConfiguration config)
        {
            try
            {
                if (config.Id > 0)
                {
                    await _databaseService.DeleteTtsConfigurationAsync(config.Id);
                }
                TtsConfigurations.Remove(config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete TTS configuration: {ex.Message}");
                throw;
            }
        }
    }

    public static class PresetChannels
    {
        public static PresetChannel[] GetPresetChannels()
        {
            return new PresetChannel[]
            {
                new PresetChannel
                {
                    Name = "自定义配置",
                    ChannelType = ChannelType.Custom,
                    DefaultApiUrl = "",
                    SupportedModels = Array.Empty<string>(),
                    SupportsStreaming = true,
                    SupportsMultimodal = false,
                    SupportsThinking = false,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "openai"
                },
                new PresetChannel
                {
                    Name = "Google Gemini",
                    ChannelType = ChannelType.GoogleGemini,
                    DefaultApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
                    SupportedModels = new[] { "gemini-pro", "gemini-pro-vision", "gemini-1.5-pro", "gemini-1.5-flash", "gemini-2.0-flash-thinking-exp" },
                    SupportsStreaming = true,
                    SupportsMultimodal = true,
                    SupportsThinking = true,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "gemini"
                },
                new PresetChannel
                {
                    Name = "智谱GLM",
                    ChannelType = ChannelType.ZhipuGLM,
                    DefaultApiUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions",
                    SupportedModels = new[] { "glm-4", "glm-4-0520", "glm-4-plus", "glm-4-air", "glm-4-airx", "glm-4-flash", "glm-4-long", "glm-4v", "glm-4v-plus", "codegeex-4" },
                    SupportsStreaming = true,
                    SupportsMultimodal = true,
                    SupportsThinking = false,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "openai"
                },
                new PresetChannel
                {
                    Name = "通义千问Qwen",
                    ChannelType = ChannelType.TongyiQwen,
                    DefaultApiUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
                    SupportedModels = new[] { "qwen-turbo", "qwen-plus", "qwen-max", "qwen-max-0428", "qwen-max-0403", "qwen-max-0107", "qwen-max-longcontext", "qwen2-72b-instruct", "qwen2-57b-a14b-instruct", "qwen2-7b-instruct", "qwen2-1.5b-instruct", "qwen2-0.5b-instruct", "qwen-vl-plus", "qwen-vl-max" },
                    SupportsStreaming = true,
                    SupportsMultimodal = true,
                    SupportsThinking = false,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "openai"
                },
                new PresetChannel
                {
                    Name = "硅基流动SiliconFlow",
                    ChannelType = ChannelType.SiliconFlow,
                    DefaultApiUrl = "https://api.siliconflow.cn/v1/chat/completions",
                    SupportedModels = new[] { "Qwen/Qwen2.5-7B-Instruct", "Qwen/Qwen2.5-14B-Instruct", "Qwen/Qwen2.5-32B-Instruct", "Qwen/Qwen2.5-72B-Instruct", "THUDM/glm-4-9b-chat", "01-ai/Yi-1.5-9B-Chat-16K", "01-ai/Yi-1.5-34B-Chat-16K", "meta-llama/Meta-Llama-3.1-8B-Instruct", "meta-llama/Meta-Llama-3.1-70B-Instruct", "mistralai/Mistral-7B-Instruct-v0.3", "google/gemma-2-9b-it", "google/gemma-2-27b-it" },
                    SupportsStreaming = true,
                    SupportsMultimodal = false,
                    SupportsThinking = false,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "openai"
                },
                new PresetChannel
                {
                    Name = "OpenAI",
                    ChannelType = ChannelType.OpenAI,
                    DefaultApiUrl = "https://api.openai.com/v1/chat/completions",
                    SupportedModels = new[] { "gpt-3.5-turbo", "gpt-3.5-turbo-16k", "gpt-4", "gpt-4-32k", "gpt-4-turbo", "gpt-4-turbo-preview", "gpt-4-vision-preview", "gpt-4o", "gpt-4o-mini", "o1-preview", "o1-mini" },
                    SupportsStreaming = true,
                    SupportsMultimodal = true,
                    SupportsThinking = true,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "openai"
                },
                new PresetChannel
                {
                    Name = "Anthropic Claude",
                    ChannelType = ChannelType.AnthropicClaude,
                    DefaultApiUrl = "https://api.anthropic.com/v1/messages",
                    SupportedModels = new[] { "claude-3-5-sonnet-20241022", "claude-3-5-sonnet-20240620", "claude-3-5-haiku-20241022", "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307", "claude-2.1", "claude-2.0", "claude-instant-1.2" },
                    SupportsStreaming = true,
                    SupportsMultimodal = true,
                    SupportsThinking = false,
                    AuthHeaderFormat = "x-api-key",
                    RequestFormat = "anthropic"
                }
            };
        }

        public static PresetChannel GetPresetChannel(ChannelType channelType)
        {
            return GetPresetChannels().FirstOrDefault(c => c.ChannelType == channelType) 
                ?? GetPresetChannels().First(c => c.ChannelType == ChannelType.Custom);
        }
    }
}