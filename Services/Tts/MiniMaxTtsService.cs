using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// MiniMax TTS服务实现
    /// </summary>
    public class MiniMaxTtsService : TtsServiceBase
    {
        public override TtsChannelType SupportedChannelType => TtsChannelType.MiniMax;

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request)
        {
            var config = request.Configuration;
            
            // 设置请求头
            SetupHttpHeaders(config);

            // 构建请求体
            var requestBody = new
            {
                model = config.Model,
                text = request.Text,
                voice_id = config.Voice,
                speed = config.Speed,
                audio_format = "wav"
            };

            var json = JsonSerializer.Serialize(requestBody);
            Debug.WriteLine($"MiniMax 请求体: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(config.ApiUrl, content);
                var result = await ProcessHttpResponseAsync(response);

                if (!result.IsSuccess)
                {
                    throw new TtsException(SupportedChannelType, 
                        $"MiniMax TTS API请求失败: {result.ErrorMessage}", 
                        result.ErrorDetails ?? "");
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    $"MiniMax网络请求失败: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    "MiniMax请求超时", ex);
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
                // MiniMax特定验证
                if (string.IsNullOrWhiteSpace(configuration.Model))
                {
                    result.AddError("MiniMax TTS需要指定模型");
                }

                if (configuration.Speed < 0.5 || configuration.Speed > 2.0)
                {
                    result.AddWarning("MiniMax TTS推荐速度范围为0.5-2.0");
                }
            }

            return result;
        }
    }
}

