using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using ElevenLabs;
using ElevenLabs.TextToSpeech;
using ElevenLabs.Voices;
using Buddie.Services.ExceptionHandling;
using Microsoft.Extensions.Logging;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// ElevenLabs TTS服务实现 - 使用ElevenLabs-DotNet包
    /// </summary>
    public class ElevenLabsTtsService : TtsServiceBase
    {
        private ElevenLabsClient? _elevenLabsClient;
        private string? _currentApiKey;

        public override TtsChannelType SupportedChannelType => TtsChannelType.ElevenLabs;

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            var config = request.Configuration;

            // 统一通过基类的HttpClient+Headers策略
            SetupHttpHeaders(config);

            _logger.LogInformation("ElevenLabs TTS: voice={Voice}, model={Model}, speed={Speed}", config.Voice, config.Model, config.Speed);
            _logger.LogTrace("Text: {Text}", request.Text);

            var modelToUse = !string.IsNullOrEmpty(config.Model) ? config.Model : "eleven_multilingual_v2";

            // 优先使用配置的ApiUrl（需包含{voice_id}），否则回退到默认格式
            var apiUrl = !string.IsNullOrWhiteSpace(config.ApiUrl) && config.ApiUrl.Contains("{voice_id}")
                ? config.ApiUrl.Replace("{voice_id}", config.Voice ?? string.Empty)
                : $"https://api.elevenlabs.io/v1/text-to-speech/{config.Voice}?output_format=mp3_44100_128";
            _logger.LogDebug("ElevenLabs request URL: {Url}", apiUrl);

            var voiceSpeed = config.Speed > 0 ? config.Speed : 1.0;
            var requestBody = new
            {
                text = request.Text,
                model_id = modelToUse,
                voice_settings = new
                {
                    stability = 0.5,
                    speed = voiceSpeed,
                    similarity_boost = 0.75
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            _logger.LogTrace("ElevenLabs request body: {Body}", jsonContent);

            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(apiUrl, content, cancellationToken);
            return await ProcessHttpResponseAsync(response);
        }

        private void InitializeClient(TtsConfiguration config)
        {
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                if (_elevenLabsClient == null || _currentApiKey != config.ApiKey)
                {
                    if (string.IsNullOrEmpty(config.ApiKey))
                    {
                        throw new TtsException(SupportedChannelType, "ElevenLabs API Key 不能为空");
                    }

                    // 释放旧客户端
                    _elevenLabsClient?.Dispose();

                    var auth = new ElevenLabsAuthentication(config.ApiKey);
                    _elevenLabsClient = new ElevenLabsClient(auth);
                    _currentApiKey = config.ApiKey;
                    
                    _logger.LogInformation("ElevenLabs client initialized");
                    
                    // 异步获取并记录默认语音设置（不阻塞主流程）
                    Task.Run(async () =>
                    {
                        await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
                        {
                            await GetVoiceDefaultSettingsInfoAsync(config);
                        }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
                        {
                            Component = "ElevenLabsTtsService",
                            Operation = "获取默认语音设置"
                        });
                    });
                }
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, context: new ExceptionHandlingService.ExceptionContext
            {
                Component = "ElevenLabsTtsService",
                Operation = "初始化客户端"
            });
        }

        protected override void SetupHttpHeaders(TtsConfiguration config)
        {
            // 统一清理并设置所需头
            _httpClient.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
            }
        }

        public override TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            var result = new TtsValidationResult { IsValid = true };

            // 基本验证
            if (string.IsNullOrWhiteSpace(configuration.Name))
            {
                result.AddError("配置名称不能为空");
            }

            if (string.IsNullOrWhiteSpace(configuration.ApiKey))
            {
                result.AddError("xi-api-key 不能为空");
            }

            if (string.IsNullOrWhiteSpace(configuration.Voice))
            {
                result.AddError("Voice ID 不能为空");
            }
            else if (configuration.Voice.Length != 20)
            {
                result.AddWarning("ElevenLabs Voice ID 通常为20位字符");
            }

            // 模型验证
            if (!string.IsNullOrWhiteSpace(configuration.Model))
            {
                var supportedModels = new[] { 
                    "eleven_multilingual_v2", "eleven_turbo_v2_5", "eleven_flash_v2_5", 
                    "eleven_monolingual_v1", "eleven_multilingual_v1", "eleven_turbo_v2", 
                    "eleven_multilingual_v2_turbo" 
                };
                
                if (!supportedModels.Contains(configuration.Model))
                {
                    result.AddWarning($"模型 '{configuration.Model}' 可能不受支持");
                }
            }

            // 语速验证
            if (configuration.Speed < 0.5 || configuration.Speed > 1.2)
            {
                result.AddWarning($"语速应在 0.5 到 1.2 之间，当前值: {configuration.Speed}");
            }

            return result;
        }

        /// <summary>
        /// 获取可用的语音列表
        /// </summary>
        public async Task<string[]> GetAvailableVoiceIdsAsync(TtsConfiguration config)
        {
            return await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
                
                var response = await _httpClient.GetAsync("https://api.elevenlabs.io/v1/voices");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var voicesData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                    
                    var voiceIds = new List<string>();
                    if (voicesData.TryGetProperty("voices", out var voicesArray))
                    {
                        foreach (var voice in voicesArray.EnumerateArray())
                        {
                            if (voice.TryGetProperty("voice_id", out var voiceId))
                            {
                                voiceIds.Add(voiceId.GetString() ?? "");
                            }
                        }
                    }
                    
                    _logger.LogInformation("Voices fetched: {Count}", voiceIds.Count);
                    return voiceIds.ToArray();
                }
                else
                {
                    _logger.LogWarning("Get voices failed: {Status}", response.StatusCode);
                    return Array.Empty<string>();
                }
            }, 
            ExceptionHandlingService.HandlingStrategy.LogOnly,
            Array.Empty<string>(),
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "ElevenLabsTtsService",
                Operation = "获取语音列表"
            });
        }

        /// <summary>
        /// 获取语音的默认设置（仅用于信息展示）
        /// </summary>
        public async Task<string> GetVoiceDefaultSettingsInfoAsync(TtsConfiguration config)
        {
            return await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
                
                var response = await _httpClient.GetAsync($"https://api.elevenlabs.io/v1/voices/settings/default");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("Default voice settings: {Body}", jsonContent);
                    return jsonContent;
                }
                else
                {
                    _logger.LogWarning("Get default voice settings failed: {Status}", response.StatusCode);
                    return string.Empty;
                }
            },
            ExceptionHandlingService.HandlingStrategy.LogOnly,
            string.Empty,
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "ElevenLabsTtsService",
                Operation = "获取默认语音设置"
            });
        }

        public override void Dispose()
        {
            _elevenLabsClient?.Dispose();
            _elevenLabsClient = null;
            _currentApiKey = null;
            base.Dispose();
        }
    }
}

