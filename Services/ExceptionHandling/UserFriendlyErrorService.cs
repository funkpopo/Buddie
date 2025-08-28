using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Buddie.Controls;

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
                UserFriendlyTitle = "网络连接问题",
                UserFriendlyMessage = "无法连接到服务器，请检查网络连接",
                SuggestedAction = "重试",
                ShowDetails = true
            };

            ErrorContexts["TaskCanceledException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Network,
                UserFriendlyTitle = "请求超时",
                UserFriendlyMessage = "服务器响应超时，请稍后再试",
                SuggestedAction = "重试",
                ShowDetails = false
            };

            // 身份验证错误
            ErrorContexts["UnauthorizedAccessException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Authentication,
                UserFriendlyTitle = "权限不足",
                UserFriendlyMessage = "API密钥无效或权限不足，请检查配置",
                SuggestedAction = "检查设置",
                ShowDetails = true
            };

            // 配置错误
            ErrorContexts["ArgumentException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Configuration,
                UserFriendlyTitle = "配置错误",
                UserFriendlyMessage = "参数配置有误，请检查设置",
                SuggestedAction = "检查设置",
                ShowDetails = true
            };

            ErrorContexts["InvalidOperationException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Configuration,
                UserFriendlyTitle = "操作无效",
                UserFriendlyMessage = "当前操作无法执行，请检查配置或稍后重试",
                SuggestedAction = "重试",
                ShowDetails = true
            };

            // 数据解析错误
            ErrorContexts["JsonException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Data,
                UserFriendlyTitle = "数据格式错误",
                UserFriendlyMessage = "服务器返回的数据格式异常",
                SuggestedAction = "重试",
                ShowDetails = true
            };

            // TTS相关错误
            ErrorContexts["TtsException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Media,
                UserFriendlyTitle = "语音合成失败",
                UserFriendlyMessage = "语音合成服务出现问题",
                SuggestedAction = "重试",
                ShowDetails = true
            };

            // 数据库错误
            ErrorContexts["DatabaseException"] = new ErrorContextInfo
            {
                ErrorType = ErrorType.Data,
                UserFriendlyTitle = "数据保存失败",
                UserFriendlyMessage = "无法保存对话记录",
                SuggestedAction = null, // 数据库错误通常不需要用户操作
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
                    UserFriendlyTitle = "操作失败",
                    UserFriendlyMessage = "发生了意外错误，请稍后重试",
                    SuggestedAction = "重试",
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