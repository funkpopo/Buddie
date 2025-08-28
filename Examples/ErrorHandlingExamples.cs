using System;
using System.Threading.Tasks;
using System.Windows;
using Buddie.Controls;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Examples
{
    /// <summary>
    /// 用户友好错误处理系统的使用示例
    /// 展示如何在应用中使用新的错误通知系统
    /// </summary>
    public static class ErrorHandlingExamples
    {
        /// <summary>
        /// 演示网络错误处理
        /// </summary>
        public static void DemoNetworkError()
        {
            // 模拟网络错误
            var networkException = new System.Net.Http.HttpRequestException("连接被远程主机强制关闭");
            
            // 使用新的错误服务显示用户友好的错误消息
            UserFriendlyErrorService.ShowError(networkException, "API请求", async () =>
            {
                // 重试逻辑
                await SimulateApiCall();
            });
        }

        /// <summary>
        /// 演示认证错误处理
        /// </summary>
        public static void DemoAuthenticationError()
        {
            var authException = new UnauthorizedAccessException("API密钥无效");
            
            UserFriendlyErrorService.ShowError(authException, "身份验证", () =>
            {
                // 跳转到设置页面
                MessageBox.Show("请在设置中重新配置API密钥", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        /// <summary>
        /// 演示配置错误处理
        /// </summary>
        public static void DemoConfigurationError()
        {
            var configException = new ArgumentException("模型名称不能为空");
            
            UserFriendlyErrorService.ShowError(configException, "配置验证");
        }

        /// <summary>
        /// 演示TTS错误处理
        /// </summary>
        public static void DemoTtsError()
        {
            var ttsException = new InvalidOperationException("语音合成服务暂时不可用");
            
            UserFriendlyErrorService.ShowError(ttsException, "TTS", async () =>
            {
                // 重试TTS
                await SimulateTtsCall();
            });
        }

        /// <summary>
        /// 演示成功消息
        /// </summary>
        public static void DemoSuccessMessage()
        {
            UserFriendlyErrorService.ShowSuccess("设置保存成功！");
        }

        /// <summary>
        /// 演示使用 ExecuteWithUserFriendlyErrorHandling 包装危险操作
        /// </summary>
        public static async Task DemoSafeExecution()
        {
            // 包装可能出错的操作
            await UserFriendlyErrorService.ExecuteWithUserFriendlyErrorHandling(async () =>
            {
                // 模拟可能失败的操作
                await SimulateRiskyOperation();
            }, "数据处理", async () =>
            {
                // 提供重试逻辑
                await SimulateRiskyOperation();
            });
        }

        // 辅助方法用于演示
        private static async Task SimulateApiCall()
        {
            await Task.Delay(1000);
            // 模拟可能成功的API调用
            if (new Random().Next(2) == 0)
            {
                throw new System.Net.Http.HttpRequestException("网络连接超时");
            }
        }

        private static async Task SimulateTtsCall()
        {
            await Task.Delay(500);
            UserFriendlyErrorService.ShowSuccess("语音合成完成");
        }

        private static async Task SimulateRiskyOperation()
        {
            await Task.Delay(100);
            if (new Random().Next(3) == 0)
            {
                throw new InvalidOperationException("模拟的随机错误");
            }
        }

        /// <summary>
        /// 注册自定义错误类型
        /// </summary>
        public static void RegisterCustomErrorTypes()
        {
            // 注册特定于应用的错误类型
            UserFriendlyErrorService.RegisterErrorContext("CustomBusinessException", new UserFriendlyErrorService.ErrorContextInfo
            {
                ErrorType = ErrorType.General,
                UserFriendlyTitle = "业务逻辑错误",
                UserFriendlyMessage = "操作违反了业务规则",
                SuggestedAction = "检查输入",
                ShowDetails = true
            });

            UserFriendlyErrorService.RegisterErrorContext("QuotaExceededException", new UserFriendlyErrorService.ErrorContextInfo
            {
                ErrorType = ErrorType.Network,
                UserFriendlyTitle = "配额已用完",
                UserFriendlyMessage = "API调用配额已达到限制",
                SuggestedAction = "等待重置",
                ShowDetails = false
            });
        }
    }

    /// <summary>
    /// 错误处理的最佳实践指南
    /// </summary>
    public static class ErrorHandlingBestPractices
    {
        /// <summary>
        /// 在DialogControl中使用错误处理的示例
        /// </summary>
        public static async Task<string> SafeApiCallExample(string message, OpenApiConfiguration config)
        {
            return await UserFriendlyErrorService.ExecuteWithUserFriendlyErrorHandling(async () =>
            {
                // 模拟API调用
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
                
                var response = await client.GetStringAsync(config.ApiUrl);
                return response;
            }, 
            defaultValue: "请求失败，请稍后重试", 
            context: "AI对话",
            retryOperation: async () =>
            {
                return await SafeApiCallExample(message, config);
            });
        }

        /// <summary>
        /// 错误处理的推荐做法
        /// </summary>
        public static void ShowBestPractices()
        {
            /*
            最佳实践：
            
            1. 始终为用户提供清晰、可操作的错误消息
            2. 根据错误类型使用合适的图标和颜色
            3. 提供重试选项（当合适时）
            4. 不要向用户暴露技术细节，除非他们主动要求
            5. 记录详细的错误信息用于调试
            6. 对于不同的错误严重程度使用不同的处理策略
            
            错误类型映射：
            - Network: 网络相关错误（连接、超时等）
            - Authentication: 身份验证和授权错误
            - Configuration: 配置和参数错误
            - Media: TTS、音频处理错误
            - Data: 数据解析、数据库错误
            - General: 其他通用错误
            */
        }
    }
}