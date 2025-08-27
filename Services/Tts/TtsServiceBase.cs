using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// TTS服务基类，提供公共功能
    /// </summary>
    public abstract class TtsServiceBase : ITtsService
    {
        protected readonly HttpClient _httpClient;

        protected TtsServiceBase()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(2);
        }

        public abstract TtsChannelType SupportedChannelType { get; }

        public virtual async Task<TtsResponse> ConvertTextToSpeechAsync(TtsRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                Debug.WriteLine($"TTS [{SupportedChannelType}]: 开始处理请求，文本长度: {request.Text.Length}");

                // 验证配置
                var validation = ValidateConfiguration(request.Configuration);
                if (!validation.IsValid)
                {
                    var errorMsg = string.Join("; ", validation.ErrorMessages);
                    throw new TtsException(SupportedChannelType, $"配置验证失败: {errorMsg}");
                }

                // 验证文本
                if (string.IsNullOrWhiteSpace(request.Text))
                {
                    throw new TtsException(SupportedChannelType, "文本内容不能为空");
                }

                // 调用具体实现
                var result = await CallTtsApiAsync(request);
                
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                
                Debug.WriteLine($"TTS [{SupportedChannelType}]: 处理完成，音频大小: {result.AudioData.Length} bytes，耗时: {result.ProcessingTime.TotalMilliseconds}ms");
                
                return result;
            }
            catch (TtsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"TTS [{SupportedChannelType}]: 处理失败 - {ex.Message}");
                throw new TtsException(SupportedChannelType, $"TTS调用失败: {ex.Message}", ex);
            }
        }

        public virtual TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            var result = new TtsValidationResult { IsValid = true };

            if (configuration == null)
            {
                result.AddError("TTS配置不能为空");
                return result;
            }

            if (string.IsNullOrWhiteSpace(configuration.ApiUrl))
            {
                result.AddError("API URL不能为空");
            }

            if (string.IsNullOrWhiteSpace(configuration.ApiKey))
            {
                result.AddError("API密钥不能为空");
            }

            if (string.IsNullOrWhiteSpace(configuration.Voice))
            {
                result.AddError("语音不能为空");
            }

            return result;
        }

        /// <summary>
        /// 调用具体的TTS API实现
        /// </summary>
        protected abstract Task<TtsResponse> CallTtsApiAsync(TtsRequest request);

        /// <summary>
        /// 设置通用的HTTP请求头
        /// </summary>
        protected virtual void SetupHttpHeaders(TtsConfiguration config)
        {
            _httpClient.DefaultRequestHeaders.Clear();
        }

        /// <summary>
        /// 处理HTTP响应
        /// </summary>
        protected virtual async Task<TtsResponse> ProcessHttpResponseAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                var audioData = await response.Content.ReadAsByteArrayAsync();
                return new TtsResponse
                {
                    IsSuccess = true,
                    AudioData = audioData,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "audio/wav"
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return new TtsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"HTTP请求失败: {response.StatusCode}",
                    ErrorDetails = errorContent
                };
            }
        }

        public virtual void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

