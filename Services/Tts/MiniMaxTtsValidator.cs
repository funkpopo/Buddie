using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using Buddie.Services.ExceptionHandling;
using Buddie.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// MiniMax TTS配置验证工具
    /// 用于诊断API配置问题
    /// </summary>
    public static class MiniMaxTtsValidator
    {
        /// <summary>
        /// 验证MiniMax TTS配置
        /// </summary>
        public static async Task<(bool IsValid, string Message)> ValidateConfigurationAsync(TtsConfiguration config)
        {
            return await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                var loggerFactory = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                var logger = (loggerFactory?.CreateLogger("MiniMaxTtsValidator")) ?? NullLogger.Instance;
                logger.LogInformation("=== MiniMax TTS 配置验证开始 ===");
                
                // 1. 基本配置检查
                var basicCheck = ValidateBasicConfiguration(config);
                if (!basicCheck.IsValid)
                {
                    return basicCheck;
                }
                
                // 2. 网络连接检查
                var networkCheck = await ValidateNetworkConnectionAsync(config.ApiUrl);
                if (!networkCheck.IsValid)
                {
                    return networkCheck;
                }
                
                // 3. API端点检查
                var apiCheck = await ValidateApiEndpointAsync(config);
                if (!apiCheck.IsValid)
                {
                    return apiCheck;
                }
                
                Debug.WriteLine("✅ MiniMax TTS 配置验证通过");
                return (true, "配置验证通过");
            },
            ExceptionHandlingService.HandlingStrategy.LogOnly,
            (false, "MiniMax TTS配置验证失败"),
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "MiniMaxTtsValidator",
                Operation = "TTS配置验证"
            });
        }
        
        /// <summary>
        /// 验证基本配置
        /// </summary>
        private static (bool IsValid, string Message) ValidateBasicConfiguration(TtsConfiguration config)
        {
            var loggerFactory = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = (loggerFactory?.CreateLogger("MiniMaxTtsValidator")) ?? NullLogger.Instance;
            logger.LogInformation("🔍 检查基本配置...");
            
            if (config == null)
                return (false, "配置对象为空");
                
            if (string.IsNullOrWhiteSpace(config.ApiUrl))
                return (false, "API URL不能为空");
                
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                return (false, "API Key不能为空");
                
            if (string.IsNullOrWhiteSpace(config.Model))
                return (false, "模型不能为空");
                
            if (string.IsNullOrWhiteSpace(config.Voice))
                return (false, "语音不能为空");
                
            // 检查API URL格式
            if (!Uri.TryCreate(config.ApiUrl, UriKind.Absolute, out var uri))
                return (false, "API URL格式无效");
                
            if (uri.Scheme != "https" && uri.Scheme != "http")
                return (false, "API URL必须是HTTP或HTTPS协议");
                
            // 检查是否是MiniMax的域名
            if (!config.ApiUrl.Contains("minimax"))
                return (false, "API URL似乎不是MiniMax的地址");
                
            // 检查语速范围
            if (config.Speed < 0.5 || config.Speed > 2.0)
                return (false, "语速必须在0.5-2.0范围内");
                
            logger.LogInformation("✅ 基本配置检查通过");
            return (true, "基本配置正确");
        }
        
        /// <summary>
        /// 验证网络连接
        /// </summary>
        private static async Task<(bool IsValid, string Message)> ValidateNetworkConnectionAsync(string apiUrl)
        {
            var loggerFactory = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = (loggerFactory?.CreateLogger("MiniMaxTtsValidator")) ?? NullLogger.Instance;
            logger.LogInformation("🔍 检查网络连接...");
            
            return await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                var uri = new Uri(apiUrl);
                var baseUrl = $"{uri.Scheme}://{uri.Host}";
                
                var httpClient = Buddie.App.GetService<System.Net.Http.IHttpClientFactory>().CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                
                var response = await httpClient.GetAsync(baseUrl);
                logger.LogInformation("网络连接测试: {Status}", response.StatusCode);
                
                // 任何HTTP响应都表示网络连接正常
                logger.LogInformation("✅ 网络连接正常");
                return (true, "网络连接正常");
            },
            ExceptionHandlingService.HandlingStrategy.LogOnly,
            (false, "网络连接失败"),
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "MiniMaxTtsValidator",
                Operation = "网络连接测试"
            });
        }
        
        /// <summary>
        /// 验证API端点
        /// </summary>
        private static async Task<(bool IsValid, string Message)> ValidateApiEndpointAsync(TtsConfiguration config)
        {
            var loggerFactory = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = (loggerFactory?.CreateLogger("MiniMaxTtsValidator")) ?? NullLogger.Instance;
            logger.LogInformation("🔍 检查API端点...");
            
            return await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                var httpClient = Buddie.App.GetService<System.Net.Http.IHttpClientFactory>().CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                // 设置请求头（使用原始 API Key，不记录）
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
                
                // 创建一个最小的测试请求
                var testRequest = new
                {
                    model = config.Model,
                    text = "测试",
                    voice_id = config.Voice,
                    audio_setting = new
                    {
                        audio_encoding = "mp3",
                        sample_rate = 32000,
                        bitrate = 128
                    },
                    stream = false
                };
                
                var json = JsonSerializer.Serialize(testRequest);
                logger.LogDebug("测试请求体: {Body}", json);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(config.ApiUrl, content);
                
                logger.LogInformation("API响应状态: {Status} {Reason}", (int)response.StatusCode, response.ReasonPhrase);
                
                var responseContent = await response.Content.ReadAsStringAsync();
                logger.LogDebug("API响应内容: {Body}", responseContent);
                
                if (response.IsSuccessStatusCode)
                {
                    // 检查响应是否为JSON格式
                    var trimmedContent = responseContent.TrimStart();
                    if (trimmedContent.StartsWith("{") || trimmedContent.StartsWith("["))
                    {
                        logger.LogInformation("✅ API端点响应正常");
                        return (true, "API端点可以正常访问");
                    }
                    else
                    {
                        logger.LogWarning("⚠️ API返回非JSON格式响应");
                        return (false, $"API返回非JSON格式响应: {responseContent}");
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    logger.LogError("❌ API Key无效");
                    return (false, "API Key无效，请检查您的密钥是否正确");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    logger.LogError("❌ 请求参数错误");
                    return (false, $"请求参数错误: {responseContent}");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogError("❌ API端点不存在");
                    return (false, "API端点不存在，请检查URL是否正确");
                }
                else
                {
                    logger.LogError("❌ API请求失败: {Status}", response.StatusCode);
                    return (false, $"API请求失败: {response.StatusCode} - {responseContent}");
                }
            },
            ExceptionHandlingService.HandlingStrategy.LogOnly,
            (false, "API端点检查失败"),
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "MiniMaxTtsValidator",
                Operation = "API端点检查"
            });
        }
        
        /// <summary>
        /// 显示详细的诊断信息
        /// </summary>
        public static void PrintDiagnosticInfo(TtsConfiguration config)
        {
            var loggerFactory = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = (loggerFactory?.CreateLogger("MiniMaxTtsValidator")) ?? NullLogger.Instance;
            logger.LogInformation("=== MiniMax TTS 诊断信息 ===");
            logger.LogInformation("配置名称: {Name}", config.Name);
            logger.LogInformation("API URL: {Url}", config.ApiUrl);
            logger.LogInformation("API Key (Masked): {Key}", ApiKeyProtection.Mask(config.ApiKey));
            logger.LogInformation("模型: {Model}", config.Model);
            logger.LogInformation("语音: {Voice}", config.Voice);
            logger.LogInformation("语速: {Speed}", config.Speed);
            logger.LogInformation("渠道类型: {Type}", config.ChannelType);
            logger.LogInformation("=========================");
        }
    }
}
