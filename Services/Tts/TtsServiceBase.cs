using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// TTS服务基类，提供公共功能
    /// </summary>
    public abstract class TtsServiceBase : ITtsService, IDisposable
    {
        private bool _disposed = false;
        protected readonly HttpClient _httpClient;
        private readonly IHttpClientFactory? _httpClientFactory;
        protected readonly ILogger _logger;

        protected TtsServiceBase()
        {
            // 尝试从DI容器获取IHttpClientFactory
            _httpClientFactory = App.Services?.GetService<IHttpClientFactory>();
            var loggerFactory = App.Services?.GetService<ILoggerFactory>();
            _logger = (loggerFactory?.CreateLogger(GetType().FullName ?? nameof(TtsServiceBase))) ?? NullLogger.Instance;
            
            if (_httpClientFactory != null)
            {
                // 使用IHttpClientFactory创建HttpClient
                _httpClient = _httpClientFactory.CreateClient("TtsClient");
            }
            else
            {
                // 降级到直接创建HttpClient（仅用于测试或没有DI的场景）
                _httpClient = new HttpClient();
            }
            
            _httpClient.Timeout = TimeSpan.FromMinutes(2);
        }

        public abstract TtsChannelType SupportedChannelType { get; }

        public virtual async Task<TtsResponse> ConvertTextToSpeechAsync(TtsRequest request)
        {
            return await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                
                _logger.LogInformation("TTS {Channel}: start, textLength={Length}", SupportedChannelType, request.Text.Length);

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
                
                _logger.LogInformation("TTS {Channel}: done, size={SizeBytes}B, elapsedMs={Elapsed}", SupportedChannelType, result.AudioData.Length, result.ProcessingTime.TotalMilliseconds);
                
                return result;
            }, 
            new TtsResponse { IsSuccess = false, ErrorMessage = "TTS服务调用失败" },
            $"TTS [{SupportedChannelType}] 转换文本到语音");
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 如果使用IHttpClientFactory创建的HttpClient，不需要手动Dispose
                    // IHttpClientFactory会管理HttpClient的生命周期
                    if (_httpClientFactory == null)
                    {
                        _httpClient?.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }
}

