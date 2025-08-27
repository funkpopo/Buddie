using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// Gemini TTS服务实现
    /// </summary>
    public class GeminiTtsService : TtsServiceBase
    {
        public override TtsChannelType SupportedChannelType => TtsChannelType.GeminiAPI;

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request)
        {
            var config = request.Configuration;
            
            // 设置请求头
            SetupHttpHeaders(config);

            // 构建请求体
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"Please convert this text to speech: {request.Text}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 1024
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            Debug.WriteLine($"Gemini 请求体: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(config.ApiUrl, content);
                var result = await ProcessHttpResponseAsync(response);

                if (!result.IsSuccess)
                {
                    throw new TtsException(SupportedChannelType, 
                        $"Gemini TTS API请求失败: {result.ErrorMessage}", 
                        result.ErrorDetails ?? "");
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    $"Gemini网络请求失败: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    "Gemini请求超时", ex);
            }
        }

        protected override void SetupHttpHeaders(TtsConfiguration config)
        {
            base.SetupHttpHeaders(config);
            
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
            }
        }

        public override TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            var result = base.ValidateConfiguration(configuration);

            if (result.IsValid)
            {
                // Gemini特定验证
                if (string.IsNullOrWhiteSpace(configuration.Model))
                {
                    result.AddWarning("建议为Gemini TTS指定模型");
                }
            }

            return result;
        }
    }
}

