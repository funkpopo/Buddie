using System;
using System.Collections.Generic;
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

    public enum TtsChannelType
    {
        OpenAI,
        ElevenLabs,
        MiniMax,
        Azure,
        GeminiAPI
    }

    public class TtsPresetChannel
    {
        public string Name { get; set; } = "";
        public TtsChannelType ChannelType { get; set; }
        public string DefaultApiUrl { get; set; } = "";
        public string[] SupportedModels { get; set; } = Array.Empty<string>();
        public string[] SupportedVoices { get; set; } = Array.Empty<string>();
        public double DefaultSpeed { get; set; } = 1.0;
        public double MinSpeed { get; set; } = 0.25;
        public double MaxSpeed { get; set; } = 4.0;
        public string AuthHeaderFormat { get; set; } = "Bearer {0}";
        public string RequestFormat { get; set; } = "openai";
        public bool SupportsStreaming { get; set; } = false;
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

    public class TtsConfiguration : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private TtsChannelType _channelType = TtsChannelType.OpenAI;
        private string _apiUrl = "";
        private string _apiKey = "";
        private string _model = "";
        private string _voice = "";
        private double _speed = 1.0;
        private bool _isEditMode = true;
        private bool _isSaved = false;
        private bool _isActive = false;

        public TtsConfiguration()
        {
            // 设置默认值基于渠道类型
            UpdateDefaultsForChannel();
        }

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

        public TtsChannelType ChannelType
        {
            get => _channelType;
            set 
            { 
                if (SetProperty(ref _channelType, value))
                {
                    UpdateDefaultsForChannel();
                }
            }
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

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private void UpdateDefaultsForChannel()
        {
            var preset = TtsPresetChannels.GetPresetChannel(ChannelType);
            if (!string.IsNullOrEmpty(preset.DefaultApiUrl))
                ApiUrl = preset.DefaultApiUrl;
            if (preset.SupportedModels.Length > 0 && string.IsNullOrEmpty(Model))
                Model = preset.SupportedModels[0];
            if (preset.SupportedVoices.Length > 0 && string.IsNullOrEmpty(Voice))
                Voice = preset.SupportedVoices[0];
            Speed = preset.DefaultSpeed;
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
                Name = this.Name ?? "",
                ApiUrl = this.ApiUrl ?? "",
                ApiKey = this.ApiKey ?? "",
                Model = this.Model ?? "",
                Voice = this.Voice ?? "",
                Speed = this.Speed,
                IsStreamingEnabled = false, // 移除流式支持
                IsActive = this.IsActive,
                ChannelType = (int)this.ChannelType
            };
        }

        // Create from database model
        public static TtsConfiguration FromDbModel(DbTtsConfiguration dbModel)
        {
            return new TtsConfiguration
            {
                Id = dbModel.Id,
                Name = dbModel.Name,
                ChannelType = dbModel.ChannelType.HasValue ? (TtsChannelType)dbModel.ChannelType.Value : TtsChannelType.OpenAI,
                ApiUrl = dbModel.ApiUrl,
                ApiKey = dbModel.ApiKey,
                Model = dbModel.Model,
                Voice = dbModel.Voice,
                Speed = dbModel.Speed,
                IsActive = dbModel.IsActive,
                IsSaved = true,
                IsEditMode = false
            };
        }

        // 兼容旧版本的FromDbModel，用于数据迁移
        public static TtsConfiguration FromLegacyDbModel(DbTtsConfiguration dbModel)
        {
            return new TtsConfiguration
            {
                Id = dbModel.Id,
                Name = dbModel.Name,
                ChannelType = TtsChannelType.OpenAI, // 旧版本默认为OpenAI
                ApiUrl = dbModel.ApiUrl,
                ApiKey = dbModel.ApiKey,
                Model = dbModel.Model,
                Voice = dbModel.Voice,
                Speed = dbModel.Speed,
                IsActive = dbModel.IsActive,
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
        private ObservableCollection<TtsConfiguration> _ttsConfigurations = new ObservableCollection<TtsConfiguration>();
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

        public ObservableCollection<TtsConfiguration> TtsConfigurations
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
                    var config = TtsConfiguration.FromDbModel(dbConfig);
                    TtsConfigurations.Add(config);
                    System.Diagnostics.Debug.WriteLine($"Loaded TTS config: {dbConfig.Name}, IsActive: {dbConfig.IsActive} -> {config.IsActive}");
                }

                // 验证激活状态，确保最多只有一个配置处于激活状态
                var activeConfigs = TtsConfigurations.Where(c => c.IsActive).ToList();
                System.Diagnostics.Debug.WriteLine($"激活状态验证: 发现 {activeConfigs.Count} 个激活的TTS配置");
                
                if (activeConfigs.Count > 1)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 冲突检测：发现 {activeConfigs.Count} 个激活的TTS配置，需要修复冲突");
                    System.Diagnostics.Debug.WriteLine("激活的配置列表:");
                    for (int i = 0; i < activeConfigs.Count; i++)
                    {
                        System.Diagnostics.Debug.WriteLine($"  [{i}] {activeConfigs[i].Name} (ID: {activeConfigs[i].Id})");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"保留第一个配置: {activeConfigs[0].Name}，取消激活其余 {activeConfigs.Count - 1} 个配置");
                    for (int i = 1; i < activeConfigs.Count; i++)
                    {
                        activeConfigs[i].IsActive = false;
                        System.Diagnostics.Debug.WriteLine($"  取消激活: {activeConfigs[i].Name}");
                        if (activeConfigs[i].Id > 0)
                        {
                            await SaveTtsConfigurationAsync(activeConfigs[i]);
                            System.Diagnostics.Debug.WriteLine($"  已保存取消激活状态: {activeConfigs[i].Name}");
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"✅ 冲突已修复，当前激活配置: {activeConfigs[0].Name}");
                }
                else if (activeConfigs.Count == 1)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ 激活状态正确：唯一激活的TTS配置 - {activeConfigs[0].Name} (ID: {activeConfigs[0].Id})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️  当前没有激活的TTS配置");
                    if (TtsConfigurations.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("可用的TTS配置列表:");
                        foreach (var config in TtsConfigurations)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - {config.Name} (ID: {config.Id}, IsActive: {config.IsActive})");
                        }
                        System.Diagnostics.Debug.WriteLine("💡 建议：用户需要手动激活一个TTS配置，或检查数据库中的激活状态是否正确保存");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("💡 提示：当前没有任何TTS配置，用户需要创建并激活一个配置");
                    }
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
                    System.Diagnostics.Debug.WriteLine($"TTS Config: {config.Name}, IsSaved: {config.IsSaved}, Id: {config.Id}, IsActive: {config.IsActive}");
                    if (config.IsSaved) // Remove the Id > 0 condition to allow new configurations
                    {
                        try
                        {
                            var dbConfig = config.ToDbModel();
                            System.Diagnostics.Debug.WriteLine($"Converting to DB model - Name: {dbConfig.Name}, IsActive: {dbConfig.IsActive}");
                            var savedId = await _databaseService.SaveTtsConfigurationAsync(dbConfig);
                            config.Id = savedId; // Update the configuration with the new ID
                            System.Diagnostics.Debug.WriteLine($"Successfully saved TTS config: {config.Name} with ID {savedId}, IsActive: {config.IsActive}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to save TTS config {config.Name}: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                        }
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
        public async Task SaveTtsConfigurationAsync(TtsConfiguration config)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"💾 开始保存TTS配置: {config.Name} (ID: {config.Id}, IsActive: {config.IsActive})");
                var dbConfig = config.ToDbModel();
                System.Diagnostics.Debug.WriteLine($"💾 转换为数据库模型: Name={dbConfig.Name}, IsActive={dbConfig.IsActive}");
                var id = await _databaseService.SaveTtsConfigurationAsync(dbConfig);
                config.Id = id;
                config.IsSaved = true;
                System.Diagnostics.Debug.WriteLine($"✅ 成功保存TTS配置: {config.Name}, ID={id}, IsActive={config.IsActive}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 保存TTS配置失败: {config.Name}, 错误: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
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
        public async Task DeleteTtsConfigurationAsync(TtsConfiguration config)
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

        // Activate TTS configuration (deactivate others)
        public async Task ActivateTtsConfigurationAsync(TtsConfiguration configToActivate)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🚀 开始激活TTS配置: {configToActivate.Name} (ID: {configToActivate.Id})");
                
                // 先取消激活所有其他TTS配置
                var deactivatedConfigs = new List<TtsConfiguration>();
                foreach (var config in TtsConfigurations)
                {
                    if (config != configToActivate && config.IsActive)
                    {
                        config.IsActive = false;
                        deactivatedConfigs.Add(config);
                        System.Diagnostics.Debug.WriteLine($"⏹️ 取消激活TTS配置: {config.Name} (ID: {config.Id})");
                    }
                }

                // 激活指定的配置
                bool wasAlreadyActive = configToActivate.IsActive;
                if (!configToActivate.IsActive)
                {
                    configToActivate.IsActive = true;
                    System.Diagnostics.Debug.WriteLine($"✅ 激活TTS配置: {configToActivate.Name} (ID: {configToActivate.Id})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ℹ️ TTS配置已处于激活状态: {configToActivate.Name} (ID: {configToActivate.Id})");
                }

                // 立即保存所有状态变更到数据库
                var configsToSave = new List<TtsConfiguration>(deactivatedConfigs);
                if (configToActivate.IsSaved && configToActivate.Id > 0)
                {
                    configsToSave.Add(configToActivate);
                }

                System.Diagnostics.Debug.WriteLine($"💾 准备保存 {configsToSave.Count} 个TTS配置的状态变更到数据库");
                foreach (var config in configsToSave)
                {
                    if (config.IsSaved && config.Id > 0)
                    {
                        await SaveTtsConfigurationAsync(config);
                        System.Diagnostics.Debug.WriteLine($"💾 已保存TTS配置激活状态: {config.Name} (ID: {config.Id}) - IsActive: {config.IsActive}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ 跳过保存TTS配置: {config.Name} (IsSaved: {config.IsSaved}, ID: {config.Id})");
                    }
                }

                if (deactivatedConfigs.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"🎯 成功激活TTS配置: {configToActivate.Name}，已取消激活 {deactivatedConfigs.Count} 个其他配置");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"🎯 成功激活TTS配置: {configToActivate.Name}，无其他配置需要取消激活");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"激活TTS配置失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        // Remove TTS configuration
        public async Task RemoveTtsConfigurationAsync(TtsConfiguration configToRemove)
        {
            try
            {
                if (configToRemove.Id > 0)
                {
                    var service = new DatabaseService();
                    await service.DeleteTtsConfigurationAsync(configToRemove.Id);
                }
                
                TtsConfigurations.Remove(configToRemove);
                System.Diagnostics.Debug.WriteLine($"Removed TTS configuration: {configToRemove.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to remove TTS configuration: {ex.Message}");
                throw;
            }
        }

        // Get the currently active TTS configuration
        public TtsConfiguration? GetActiveTtsConfiguration()
        {
            return TtsConfigurations.FirstOrDefault(config => config.IsActive);
        }
    }

    public static class TtsPresetChannels
    {
        public static TtsPresetChannel[] GetPresetChannels()
        {
            return new TtsPresetChannel[]
            {
                new TtsPresetChannel
                {
                    Name = "OpenAI TTS",
                    ChannelType = TtsChannelType.OpenAI,
                    DefaultApiUrl = "https://api.openai.com/v1/audio/speech",
                    SupportedModels = new[] { "tts-1", "tts-1-hd" },
                    SupportedVoices = new[] { "alloy", "echo", "fable", "onyx", "nova", "shimmer" },
                    DefaultSpeed = 1.0,
                    MinSpeed = 0.25,
                    MaxSpeed = 4.0,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "openai",
                    SupportsStreaming = false
                },
                new TtsPresetChannel
                {
                    Name = "ElevenLabs",
                    ChannelType = TtsChannelType.ElevenLabs,
                    DefaultApiUrl = "https://api.elevenlabs.io/v1/text-to-speech/{voice_id}",
                    SupportedModels = new[] { "eleven_monolingual_v1", "eleven_multilingual_v1", "eleven_multilingual_v2" },
                    SupportedVoices = new[] { "21m00Tcm4TlvDq8ikWAM", "AZnzlk1XvdvUeBnXmlld", "EXAVITQu4vr4xnSDxMaL", "ErXwobaYiN019PkySvjV", "MF3mGyEYCl7XYWbV9V6O", "TxGEqnHWrfWFTfGW9XjX", "VR6AewLTigWG4xSOukaG", "pNInz6obpgDQGcFmaJgB", "yoZ06aMxZJJ28mfd3POQ" },
                    DefaultSpeed = 1.0,
                    MinSpeed = 0.25,
                    MaxSpeed = 2.0,
                    AuthHeaderFormat = "xi-api-key",
                    RequestFormat = "elevenlabs",
                    SupportsStreaming = true
                },
                new TtsPresetChannel
                {
                    Name = "MiniMax TTS",
                    ChannelType = TtsChannelType.MiniMax,
                    DefaultApiUrl = "https://api.minimax.chat/v1/text_to_speech",
                    SupportedModels = new[] { "speech-01", "speech-01-240228" },
                    SupportedVoices = new[] { "male-qn-qingse", "male-qn-jingying", "male-qn-badao", "male-qn-daxuesheng", "female-shaonv", "female-yujie", "female-chengshu", "female-tianmei", "presenter_male", "presenter_female", "audiobook_male_1", "audiobook_male_2", "audiobook_female_1", "audiobook_female_2" },
                    DefaultSpeed = 1.0,
                    MinSpeed = 0.5,
                    MaxSpeed = 2.0,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "minimax",
                    SupportsStreaming = false
                },
                new TtsPresetChannel
                {
                    Name = "Azure Cognitive Services",
                    ChannelType = TtsChannelType.Azure,
                    DefaultApiUrl = "https://{region}.tts.speech.microsoft.com/cognitiveservices/v1",
                    SupportedModels = new[] { "neural", "standard" },
                    SupportedVoices = new[] { "zh-CN-XiaoxiaoNeural", "zh-CN-YunxiNeural", "zh-CN-YunjianNeural", "zh-CN-XiaoyiNeural", "zh-CN-YunyangNeural", "zh-CN-XiaochenNeural", "zh-CN-XiaohanNeural", "zh-CN-XiaomengNeural", "zh-CN-XiaomoNeural", "zh-CN-XiaoqiuNeural", "zh-CN-XiaoruiNeural", "zh-CN-XiaoshuangNeural", "zh-CN-XiaoxuanNeural", "zh-CN-XiaoyanNeural", "zh-CN-XiaoyouNeural", "en-US-JennyNeural", "en-US-GuyNeural", "en-US-AriaNeural", "en-US-DavisNeural", "en-US-AmberNeural", "en-US-AnaNeural", "en-US-AshleyNeural", "en-US-BrandonNeural", "en-US-ChristopherNeural", "en-US-CoraNeural", "en-US-ElizabethNeural", "en-US-EricNeural", "en-US-JacobNeural", "en-US-JaneNeural", "en-US-JasonNeural", "en-US-MichelleNeural", "en-US-MonicaNeural", "en-US-NancyNeural", "en-US-RogerNeural", "en-US-SaraNeural", "en-US-SteffanNeural", "en-US-TonyNeural" },
                    DefaultSpeed = 1.0,
                    MinSpeed = 0.5,
                    MaxSpeed = 2.0,
                    AuthHeaderFormat = "Ocp-Apim-Subscription-Key",
                    RequestFormat = "azure",
                    SupportsStreaming = false
                },
                new TtsPresetChannel
                {
                    Name = "Gemini API",
                    ChannelType = TtsChannelType.GeminiAPI,
                    DefaultApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent",
                    SupportedModels = new[] { "gemini-pro" },
                    SupportedVoices = new[] { "default" },
                    DefaultSpeed = 1.0,
                    MinSpeed = 0.5,
                    MaxSpeed = 2.0,
                    AuthHeaderFormat = "Bearer {0}",
                    RequestFormat = "gemini",
                    SupportsStreaming = false
                }
            };
        }

        public static TtsPresetChannel GetPresetChannel(TtsChannelType channelType)
        {
            return GetPresetChannels().FirstOrDefault(c => c.ChannelType == channelType) 
                ?? GetPresetChannels().First(c => c.ChannelType == TtsChannelType.OpenAI);
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