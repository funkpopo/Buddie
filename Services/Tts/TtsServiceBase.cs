using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
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
        private static readonly IDictionary<string, string> _contentTypeToExtension = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["audio/mpeg"] = ".mp3",
            ["audio/mp3"] = ".mp3",
            ["audio/wav"] = ".wav",
            ["audio/x-wav"] = ".wav",
            ["audio/wave"] = ".wav",
            ["audio/ogg"] = ".ogg",
            ["audio/opus"] = ".opus",
            ["audio/webm"] = ".webm",
            ["audio/aac"] = ".aac",
            ["audio/mp4"] = ".m4a",
            ["audio/x-m4a"] = ".m4a",
            ["audio/flac"] = ".flac"
        };

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

        public virtual async Task<TtsResponse> ConvertTextToSpeechAsync(TtsRequest request, CancellationToken cancellationToken = default)
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
                var result = await CallTtsApiAsync(request, cancellationToken);
                
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
        protected abstract Task<TtsResponse> CallTtsApiAsync(TtsRequest request, CancellationToken cancellationToken = default);

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
                _logger.LogError("TTS HTTP失败: {Status} {Reason}. Body: {Preview}", (int)response.StatusCode, response.ReasonPhrase, errorContent?.Length > 500 ? errorContent.Substring(0, 500) + "..." : errorContent);
                return new TtsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"HTTP请求失败: {response.StatusCode}",
                    ErrorDetails = errorContent
                };
            }
        }

        /// <summary>
        /// 根据Content-Type和可选的音频字节推断合适的文件扩展名。
        /// </summary>
        public static string GetAudioExtension(string? contentType, byte[]? audioBytes = null)
        {
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                // 去掉可能的参数部分，例如: audio/ogg; codecs=opus
                var typeOnly = contentType.Split(';')[0].Trim();
                if (_contentTypeToExtension.TryGetValue(typeOnly, out var ext))
                {
                    return ext;
                }

                // 宽松匹配常见关键词
                var lower = contentType.ToLowerInvariant();
                if (lower.Contains("mp3") || lower.Contains("mpeg")) return ".mp3";
                if (lower.Contains("wav")) return ".wav";
                if (lower.Contains("ogg")) return ".ogg";
                if (lower.Contains("opus")) return ".opus";
                if (lower.Contains("aac")) return ".aac";
                if (lower.Contains("webm")) return ".webm";
                if (lower.Contains("flac")) return ".flac";
                if (lower.Contains("mp4") || lower.Contains("m4a")) return ".m4a";
            }

            // 简单的字节特征检测（可选）
            if (audioBytes != null)
            {
                try
                {
                    if (audioBytes.Length >= 12)
                    {
                        var header = System.Text.Encoding.ASCII.GetString(audioBytes, 0, 4);
                        if (header == "RIFF")
                        {
                            var format = System.Text.Encoding.ASCII.GetString(audioBytes, 8, 4);
                            if (format == "WAVE") return ".wav";
                        }
                    }

                    if (audioBytes.Length >= 3)
                    {
                        var id3 = System.Text.Encoding.ASCII.GetString(audioBytes, 0, 3);
                        if (id3 == "ID3") return ".mp3";
                    }

                    if (audioBytes.Length >= 2)
                    {
                        if (audioBytes[0] == 0xFF && (audioBytes[1] & 0xE0) == 0xE0) return ".mp3";
                    }
                }
                catch { /* best-effort */ }
            }

            // 默认回退
            return ".mp3";
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

