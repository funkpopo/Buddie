using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Buddie.Controls;
using Buddie.Localization;

namespace Buddie.Services.ExceptionHandling
{
    /// <summary>
    /// 增强的用户友好错误处理服务
    /// 提供更好的用户体验和错误反馈
    /// </summary>
    public static class UserFriendlyErrorService
    {
        private static Panel? notificationContainer;
        private static readonly Dictionary<string, ErrorContextInfo> ErrorContexts = new();

        /// <summary>
        /// 错误上下文信息
        /// </summary>
        public class ErrorContextInfo
        {
            public ErrorType ErrorType { get; set; }
            public string UserFriendlyTitle { get; set; } = "";
            public string UserFriendlyMessage { get; set; } = "";
            public string? SuggestedAction { get; set; }
            public Action? RetryAction { get; set; }
            public bool ShowDetails { get; set; } = true;
        }

        /// <summary>
        /// 初始化错误服务，设置通知容器
        /// </summary>
        public static void Initialize(Panel container)
        {
            notificationContainer = container;
            RegisterDefaultErrorContexts();
        }

        /// <summary>
        /// 注册默认的错误上下文
        /// </summary>
        private static void RegisterDefaultErrorContexts()
        {
            // 网络相关错误
            ErrorContexts["HttpRequestException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Network,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = LocalizationManager.GetString("Error_Retry"),
                ShowDetails = true
            };

            ErrorContexts["TaskCanceledException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Network,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = LocalizationManager.GetString("Error_Retry"),
                ShowDetails = false
            };

            // 身份验证错误
            ErrorContexts["UnauthorizedAccessException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Authentication,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = null,
                ShowDetails = true
            };

            // 配置错误
            ErrorContexts["ArgumentException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Configuration,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = null,
                ShowDetails = true
            };

            ErrorContexts["InvalidOperationException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Configuration,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = LocalizationManager.GetString("Error_Retry"),
                ShowDetails = true
            };

            // 数据解析错误
            ErrorContexts["JsonException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Data,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = LocalizationManager.GetString("Error_Retry"),
                ShowDetails = true
            };

            // TTS相关错误
            ErrorContexts["TtsException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Media,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = LocalizationManager.GetString("Error_Retry"),
                ShowDetails = true
            };

            // 数据库错误
            ErrorContexts["DatabaseException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Data,
                UserFriendlyTitle = LocalizationManager.GetString("Error_Generic"),
                UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                SuggestedAction = null,
                ShowDetails = false
            };
        }

        /// <summary>
        /// 显示用户友好的错误通知
        /// </summary>
        public static void ShowError(Exception exception, string? context = null, Action? retryAction = null)
        {
            if (notificationContainer == null || Application.Current?.Dispatcher == null)
            {
                // 降级到传统MessageBox
                FallbackToMessageBox(exception, context);
                return;
            }

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var errorInfo = GetErrorInfo(exception, context);
                
                var notification = new ErrorNotificationControl();
                
                notification.ShowError(
                    errorInfo.ErrorType,
                    errorInfo.UserFriendlyTitle,
                    errorInfo.ShowDetails ? GetDetailedErrorMessage(exception) : null,
                    errorInfo.SuggestedAction,
                    retryAction ?? errorInfo.RetryAction
                );

                notification.Closed += (s, e) =>
                {
                    notificationContainer.Children.Remove(notification);
                };

                // 添加到容器顶部
                notificationContainer.Children.Insert(0, notification);
                
                // 限制同时显示的通知数量
                while (notificationContainer.Children.Count > 3)
                {
                    notificationContainer.Children.RemoveAt(notificationContainer.Children.Count - 1);
                }
            });
        }

        /// <summary>
        /// 显示简单的成功通知
        /// </summary>
        public static void ShowSuccess(string message)
        {
            if (notificationContainer == null || Application.Current?.Dispatcher == null)
                return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var notification = new ErrorNotificationControl();
                
                // 临时修改样式显示成功消息
                notification.ShowError(message);
                // 这里可以添加成功样式的设置

                notification.Closed += (s, e) =>
                {
                    notificationContainer.Children.Remove(notification);
                };

                notificationContainer.Children.Insert(0, notification);
            });
        }

        /// <summary>
        /// 获取错误信息
        /// </summary>
        private static ErrorContextInfo GetErrorInfo(Exception exception, string? context)
        {
            var exceptionType = exception.GetType().Name;
            
            // 首先尝试通过异常类型匹配
            if (ErrorContexts.TryGetValue(exceptionType, out var contextInfo))
            {
                return contextInfo;
            }
            
            // 根据上下文匹配
            if (!string.IsNullOrEmpty(context))
            {
                return context.ToLower() switch
                {
                    string c when c.Contains("tts") || c.Contains("语音") => ErrorContexts["TtsException"],
                    string c when c.Contains("api") || c.Contains("网络") => ErrorContexts["HttpRequestException"],
                    string c when c.Contains("database") || c.Contains("数据库") => ErrorContexts["DatabaseException"],
                    string c when c.Contains("config") || c.Contains("配置") => ErrorContexts["ArgumentException"],
                    _ => GetDefaultErrorInfo(exception)
                };
            }

            return GetDefaultErrorInfo(exception);
        }

        /// <summary>
        /// 获取默认错误信息
        /// </summary>
        private static ErrorContextInfo GetDefaultErrorInfo(Exception exception)
        {
            return exception switch
            {
                HttpRequestException => ErrorContexts["HttpRequestException"],
                TaskCanceledException => ErrorContexts["TaskCanceledException"],
                UnauthorizedAccessException => ErrorContexts["UnauthorizedAccessException"],
                ArgumentException => ErrorContexts["ArgumentException"],
                JsonException => ErrorContexts["JsonException"],
                InvalidOperationException => ErrorContexts["InvalidOperationException"],
                _ => new ErrorContextInfo
                {
                    ErrorType = ErrorType.General,
                    UserFriendlyTitle = LocalizationManager.GetString("Error_DefaultTitle"),
                    UserFriendlyMessage = LocalizationManager.GetString("Error_DefaultMessage"),
                    SuggestedAction = LocalizationManager.GetString("Error_Retry"),
                    ShowDetails = true
                }
            };
        }

        /// <summary>
        /// 获取详细错误信息
        /// </summary>
        private static string GetDetailedErrorMessage(Exception exception)
        {
            var details = $"错误类型: {exception.GetType().Name}\n";
            details += $"错误消息: {exception.Message}";
            
            if (exception.InnerException != null)
            {
                details += $"\n内部错误: {exception.InnerException.Message}";
            }
            
            return details;
        }

        /// <summary>
        /// 降级到传统MessageBox
        /// </summary>
        private static void FallbackToMessageBox(Exception exception, string? context)
        {
            var errorInfo = GetErrorInfo(exception, context);
            var message = $"{errorInfo.UserFriendlyTitle}\n\n{errorInfo.UserFriendlyMessage}";
            
            if (errorInfo.ShowDetails)
            {
                message += $"\n\n详细信息:\n{GetDetailedErrorMessage(exception)}";
            }

            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// 注册自定义错误上下文
        /// </summary>
        public static void RegisterErrorContext(string key, ErrorContextInfo contextInfo)
        {
            ErrorContexts[key] = contextInfo;
        }

        /// <summary>
        /// 执行带用户友好错误处理的操作
        /// </summary>
        public static async Task<T> ExecuteWithUserFriendlyErrorHandling<T>(
            Func<Task<T>> operation,
            T defaultValue,
            string? context = null,
            Func<Task<T>>? retryOperation = null)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                Action? retryAction = null;
                if (retryOperation != null)
                {
                    retryAction = async () =>
                    {
                        try
                        {
                            await retryOperation();
                        }
                        catch (Exception retryEx)
                        {
                            ShowError(retryEx, context);
                        }
                    };
                }

                ShowError(ex, context, retryAction);
                return defaultValue;
            }
        }

        /// <summary>
        /// 执行带用户友好错误处理的操作（无返回值）
        /// </summary>
        public static async Task ExecuteWithUserFriendlyErrorHandling(
            Func<Task> operation,
            string? context = null,
            Func<Task>? retryOperation = null)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                Action? retryAction = null;
                if (retryOperation != null)
                {
                    retryAction = async () =>
                    {
                        try
                        {
                            await retryOperation();
                        }
                        catch (Exception retryEx)
                        {
                            ShowError(retryEx, context);
                        }
                    };
                }

                ShowError(ex, context, retryAction);
            }
        }
    }
}
