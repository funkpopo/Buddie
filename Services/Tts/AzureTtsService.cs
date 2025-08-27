using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// Azure Cognitive Services TTS服务实现
    /// </summary>
    public class AzureTtsService : TtsServiceBase
    {
        public override TtsChannelType SupportedChannelType => TtsChannelType.Azure;

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request)
        {
            var config = request.Configuration;
            
            // 设置请求头
            SetupHttpHeaders(config);

            // 构建SSML请求体
            var ssml = BuildSsmlRequest(request.Text, config);
            Debug.WriteLine($"Azure SSML: {ssml}");

            var content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

            try
            {
                var response = await _httpClient.PostAsync(config.ApiUrl, content);
                var result = await ProcessHttpResponseAsync(response);

                if (!result.IsSuccess)
                {
                    throw new TtsException(SupportedChannelType, 
                        $"Azure TTS API请求失败: {result.ErrorMessage}", 
                        result.ErrorDetails ?? "");
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    $"Azure网络请求失败: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new TtsException(SupportedChannelType, 
                    "Azure请求超时", ex);
            }
        }

        private string BuildSsmlRequest(string text, TtsConfiguration config)
        {
            var speedValue = config.Speed.ToString("F2");
            return $@"<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xml:lang='zh-CN'>
    <voice name='{config.Voice}'>
        <prosody rate='{speedValue}'>
            {System.Security.SecurityElement.Escape(text)}
        </prosody>
    </voice>
</speak>";
        }

        protected override void SetupHttpHeaders(TtsConfiguration config)
        {
            base.SetupHttpHeaders(config);
            
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", config.ApiKey);
            }
            
            _httpClient.DefaultRequestHeaders.Add("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Buddie");
        }

        public override TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            var result = base.ValidateConfiguration(configuration);

            if (result.IsValid)
            {
                // Azure特定验证
                if (configuration.ApiUrl.Contains("{region}"))
                {
                    result.AddError("Azure API URL中的{region}占位符需要替换为实际区域");
                }

                if (configuration.Speed < 0.5 || configuration.Speed > 2.0)
                {
                    result.AddWarning("Azure TTS推荐速度范围为0.5-2.0");
                }
            }

            return result;
        }
    }
}

