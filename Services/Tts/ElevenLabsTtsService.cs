using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using ElevenLabs;
using ElevenLabs.TextToSpeech;
using ElevenLabs.Voices;
using Buddie.Services.ExceptionHandling;

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

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request)
        {
            return await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                var config = request.Configuration;
                
                if (string.IsNullOrEmpty(config.ApiKey))
                {
                    throw new TtsException(SupportedChannelType, "ElevenLabs API Key 不能为空");
                }

                Debug.WriteLine($"ElevenLabs TTS 请求 - Voice: {config.Voice}, Model: {config.Model}, Speed: {config.Speed}");
                Debug.WriteLine($"Text: {request.Text}");

                // 确定要使用的模型
                var modelToUse = !string.IsNullOrEmpty(config.Model) ? config.Model : "eleven_multilingual_v2";
                
                // 构建API URL
                var apiUrl = $"https://api.elevenlabs.io/v1/text-to-speech/{config.Voice}?output_format=mp3_44100_128";
                Debug.WriteLine($"API URL: {apiUrl}");

                // 创建请求体，包含完整的voice_settings，使用用户配置的语速
                var voiceSpeed = config.Speed > 0 ? config.Speed : 1.0;
                Debug.WriteLine($"使用语速设置: {voiceSpeed}");
                
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

                Debug.WriteLine($"请求体: {jsonContent}");

                // 发送HTTP请求
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
                
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var audioData = await response.Content.ReadAsByteArrayAsync();
                    Debug.WriteLine($"ElevenLabs TTS 响应成功，音频大小: {audioData.Length} bytes");

                    return new TtsResponse
                    {
                        IsSuccess = true,
                        AudioData = audioData,
                        ContentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg",
                        ErrorMessage = null,
                        ErrorDetails = null
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"ElevenLabs API 错误: {response.StatusCode} - {errorContent}");
                    throw new TtsException(SupportedChannelType, 
                        $"ElevenLabs API请求失败: {response.StatusCode}", errorContent);
                }
            }, 
            new TtsResponse 
            { 
                IsSuccess = false, 
                ErrorMessage = "ElevenLabs TTS服务调用失败" 
            },
            "ElevenLabs TTS API调用");
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
                    
                    Debug.WriteLine("ElevenLabs 客户端初始化成功");
                    
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
            // ElevenLabs-DotNet包会自动处理请求头设置
            // 这里不需要手动设置
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
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
                
                var response = await httpClient.GetAsync("https://api.elevenlabs.io/v1/voices");
                
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
                    
                    Debug.WriteLine($"获取到 {voiceIds.Count} 个语音");
                    return voiceIds.ToArray();
                }
                else
                {
                    Debug.WriteLine($"获取语音列表失败: {response.StatusCode}");
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
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
                
                var response = await httpClient.GetAsync($"https://api.elevenlabs.io/v1/voices/settings/default");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"ElevenLabs 默认语音设置: {jsonContent}");
                    return jsonContent;
                }
                else
                {
                    Debug.WriteLine($"获取默认语音设置失败: {response.StatusCode}");
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

