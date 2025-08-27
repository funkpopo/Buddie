using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// ElevenLabs TTS服务实现
    /// </summary>
    public class ElevenLabsTtsService : TtsServiceBase
    {
        public override TtsChannelType SupportedChannelType => TtsChannelType.ElevenLabs;

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request)
        {
            var config = request.Configuration;
            
            // 设置请求头
            SetupHttpHeaders(config);

            // 构建API URL，替换voice_id占位符
            var apiUrl = config.ApiUrl.Replace("{voice_id}", config.Voice);
            Debug.WriteLine($"ElevenLabs API URL: {apiUrl}");

            // 构建请求体
            var requestBody = new
            {
                text = request.Text,  // 确保text字段不为空
                model_id = !string.IsNullOrEmpty(config.Model) ? config.Model : "eleven_turbo_v2_5",
                voice_settings = new
                {
                    stability = 0.5,
                    similarity_boost = 0.5,
                    style = 0.0,
                    use_speaker_boost = true
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            Debug.WriteLine($"ElevenLabs 请求体: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(apiUrl, content);
                var result = await ProcessHttpResponseAsync(response);

                if (!result.IsSuccess)
                {
                    throw new TtsException(SupportedChannelType, 
                        $"ElevenLabs API请求失败: {result.ErrorMessage}", 
                        result.ErrorDetails ?? "");
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    $"ElevenLabs网络请求失败: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    "ElevenLabs请求超时", ex);
            }
        }

        protected override void SetupHttpHeaders(TtsConfiguration config)
        {
            base.SetupHttpHeaders(config);
            
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("xi-api-key", config.ApiKey);
            }
            
            _httpClient.DefaultRequestHeaders.Add("Accept", "audio/mpeg");
        }

        public override TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            var result = base.ValidateConfiguration(configuration);

            if (result.IsValid)
            {
                // ElevenLabs特定验证
                if (!configuration.ApiUrl.Contains("{voice_id}"))
                {
                    result.AddWarning("API URL应包含{voice_id}占位符");
                }

                // 验证语音ID格式（ElevenLabs使用特定格式的ID）
                if (configuration.Voice.Length != 20)
                {
                    result.AddWarning("ElevenLabs语音ID通常为20字符长度");
                }
            }

            return result;
        }
    }
}

