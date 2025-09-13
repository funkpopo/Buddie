using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using Buddie.Services.ExceptionHandling;
using Microsoft.Extensions.Logging;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// OpenAI TTS服务实现
    /// </summary>
    public class OpenAiTtsService : TtsServiceBase
    {
        public override TtsChannelType SupportedChannelType => TtsChannelType.OpenAI;

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request)
        {
            return await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                var config = request.Configuration;
                
                // 设置请求头
                SetupHttpHeaders(config);

                // 构建请求体
                var requestBody = new
                {
                    model = config.Model,
                    input = request.Text,
                    voice = config.Voice,
                    speed = config.Speed,
                    response_format = "wav"
                };

                var json = JsonSerializer.Serialize(requestBody);
                _logger.LogDebug("OpenAI request body: {Body}", json);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync(config.ApiUrl, content);
                var result = await ProcessHttpResponseAsync(response);

                if (!result.IsSuccess)
                {
                    throw new TtsException(SupportedChannelType, 
                        $"OpenAI TTS API请求失败: {result.ErrorMessage}", 
                        result.ErrorDetails ?? "");
                }

                return result;
            },
            new TtsResponse 
            { 
                IsSuccess = false, 
                ErrorMessage = "OpenAI TTS服务调用失败"
            },
            "OpenAI TTS API调用");
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
                // OpenAI特定验证
                if (string.IsNullOrWhiteSpace(configuration.Model))
                {
                    result.AddError("OpenAI TTS需要指定模型");
                }

                if (configuration.Speed < 0.25 || configuration.Speed > 4.0)
                {
                    result.AddWarning("OpenAI TTS推荐速度范围为0.25-4.0");
                }
            }

            return result;
        }
    }
}

