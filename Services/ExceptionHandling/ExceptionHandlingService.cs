using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Buddie.Services.ExceptionHandling
{
    /// <summary>
    /// 统一异常处理服务，提供全局异常处理能力
    /// </summary>
    public static class ExceptionHandlingService
    {
        private static IErrorNotifier? _errorNotifier;
        private static ILogger? _logger;

        /// <summary>
        /// 在应用启动时配置错误通知器与日志。
        /// </summary>
        public static void Configure(IErrorNotifier notifier, ILoggerFactory loggerFactory)
        {
            _errorNotifier = notifier;
            _logger = loggerFactory.CreateLogger("ExceptionHandling");
        }
        /// <summary>
        /// 异常处理策略枚举
        /// </summary>
        public enum HandlingStrategy
        {
            Silent,              // 静默处理（记录日志但不显示给用户）
            ShowMessage,         // 显示消息给用户
            ShowMessageAndLog,   // 显示消息并记录日志
            Rethrow,            // 重新抛出异常
            LogOnly             // 仅记录日志
        }

        /// <summary>
        /// 异常上下文信息
        /// </summary>
        public class ExceptionContext
        {
            public string Operation { get; set; } = "";
            public string Component { get; set; } = "";
            public Dictionary<string, object> AdditionalData { get; set; } = new();
        }

        /// <summary>
        /// 同步执行带异常处理的操作
        /// </summary>
        public static T ExecuteSafely<T>(
            Func<T> operation,
            HandlingStrategy strategy = HandlingStrategy.ShowMessageAndLog,
            T? defaultValue = default(T),
            ExceptionContext? context = null)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                HandleException(ex, strategy, context);
                return defaultValue!;
            }
        }

        /// <summary>
        /// 同步执行带异常处理的操作（无返回值）
        /// </summary>
        public static void ExecuteSafely(
            Action operation,
            HandlingStrategy strategy = HandlingStrategy.ShowMessageAndLog,
            ExceptionContext? context = null)
        {
            try
            {
                operation();
            }
            catch (Exception ex)
            {
                HandleException(ex, strategy, context);
            }
        }

        /// <summary>
        /// 异步执行带异常处理的操作
        /// </summary>
        public static async Task<T> ExecuteSafelyAsync<T>(
            Func<Task<T>> operation,
            HandlingStrategy strategy = HandlingStrategy.ShowMessageAndLog,
            T? defaultValue = default(T),
            ExceptionContext? context = null)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                HandleException(ex, strategy, context);
                return defaultValue!;
            }
        }

        /// <summary>
        /// 异步执行带异常处理的操作（无返回值）
        /// </summary>
        public static async Task ExecuteSafelyAsync(
            Func<Task> operation,
            HandlingStrategy strategy = HandlingStrategy.ShowMessageAndLog,
            ExceptionContext? context = null)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                HandleException(ex, strategy, context);
            }
        }

        /// <summary>
        /// 核心异常处理逻辑
        /// </summary>
        private static void HandleException(
            Exception exception, 
            HandlingStrategy strategy,
            ExceptionContext? context)
        {
            // 构建异常信息
            var errorMessage = BuildErrorMessage(exception, context);
            var logMessage = BuildLogMessage(exception, context);

            // 根据策略处理异常
            switch (strategy)
            {
                case HandlingStrategy.Silent:
                    // 静默处理，什么都不做
                    break;

                case HandlingStrategy.ShowMessage:
                    ShowErrorMessage(errorMessage, exception, context);
                    break;

                case HandlingStrategy.ShowMessageAndLog:
                    ShowErrorMessage(errorMessage, exception, context);
                    LogException(exception, logMessage, context);
                    break;

                case HandlingStrategy.LogOnly:
                    LogException(exception, logMessage, context);
                    break;

                case HandlingStrategy.Rethrow:
                    LogException(exception, logMessage, context);
                    throw exception;
            }
        }

        /// <summary>
        /// 构建用户友好的错误消息
        /// </summary>
        private static string BuildErrorMessage(Exception exception, ExceptionContext? context)
        {
            var operation = context?.Operation ?? "操作";
            
            return exception switch
            {
                HttpRequestException httpEx => $"网络请求失败：{httpEx.Message}",
                TaskCanceledException => $"{operation}已取消或超时",
                JsonException => $"数据解析失败，请检查数据格式",
                UnauthorizedAccessException => $"访问权限不足",
                ArgumentException argEx => $"参数错误：{argEx.Message}",
                InvalidOperationException => $"当前操作无效，请稍后重试",
                NotSupportedException => $"当前操作不受支持",
                _ => $"{operation}失败：{exception.Message}"
            };
        }

        /// <summary>
        /// 构建详细的日志消息
        /// </summary>
        private static string BuildLogMessage(Exception exception, ExceptionContext? context)
        {
            var component = context?.Component ?? "Unknown";
            var operation = context?.Operation ?? "Unknown Operation";
            
            return $"[{component}] {operation} failed: {exception}";
        }

        /// <summary>
        /// 显示错误消息给用户
        /// </summary>
        private static void ShowErrorMessage(string message, Exception exception, ExceptionContext? context)
        {
            try
            {
                if (_errorNotifier != null)
                {
                    _errorNotifier.NotifyError(message, exception, context);
                }
                else
                {
                    if (Application.Current?.Dispatcher?.CheckAccess() == true)
                    {
                        MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                    }
                }
            }
            catch
            {
                // 如果消息框显示失败，至少输出到控制台
                Console.WriteLine($"Error: {message}");
            }
        }

        /// <summary>
        /// 记录异常日志
        /// </summary>
        private static void LogException(Exception exception, string logMessage, ExceptionContext? context)
        {
            try
            {
                if (_logger != null)
                {
                    _logger.LogError(exception, logMessage);
                    if (context?.AdditionalData?.Count > 0)
                    {
                        foreach (var kvp in context.AdditionalData)
                        {
                            _logger.LogDebug("AdditionalData {Key}={Value}", kvp.Key, kvp.Value);
                        }
                    }
                }
                else
                {
                    // 输出到控制台（在调试时可见）
                    Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {logMessage}");
                    Console.WriteLine($"Stack Trace: {exception.StackTrace}");
                    if (context?.AdditionalData?.Count > 0)
                    {
                        Console.WriteLine("Additional Data:");
                        foreach (var kvp in context.AdditionalData)
                        {
                            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                        }
                    }
                }
            }
            catch
            {
                // 如果日志记录失败，不要影响主流程
            }
        }

        /// <summary>
        /// 专门处理数据库操作异常
        /// </summary>
        public static class Database
        {
            public static T ExecuteSafely<T>(
                Func<T> operation,
                T? defaultValue = default(T),
                string operationName = "数据库操作")
            {
                return ExceptionHandlingService.ExecuteSafely(
                    operation,
                    HandlingStrategy.LogOnly, // 数据库操作通常静默处理
                    defaultValue,
                    new ExceptionContext 
                    { 
                        Component = "Database", 
                        Operation = operationName 
                    });
            }

            public static async Task<T> ExecuteSafelyAsync<T>(
                Func<Task<T>> operation,
                T? defaultValue = default(T),
                string operationName = "数据库操作")
            {
                return await ExceptionHandlingService.ExecuteSafelyAsync(
                    operation,
                    HandlingStrategy.LogOnly,
                    defaultValue,
                    new ExceptionContext 
                    { 
                        Component = "Database", 
                        Operation = operationName 
                    });
            }

            public static void ExecuteSafely(
                Action operation,
                string operationName = "数据库操作")
            {
                ExceptionHandlingService.ExecuteSafely(
                    operation,
                    HandlingStrategy.LogOnly,
                    new ExceptionContext 
                    { 
                        Component = "Database", 
                        Operation = operationName 
                    });
            }

            public static async Task ExecuteSafelyAsync(
                Func<Task> operation,
                string operationName = "数据库操作")
            {
                await ExceptionHandlingService.ExecuteSafelyAsync(
                    operation,
                    HandlingStrategy.LogOnly,
                    new ExceptionContext 
                    { 
                        Component = "Database", 
                        Operation = operationName 
                    });
            }
        }

        /// <summary>
        /// 专门处理网络请求异常
        /// </summary>
        public static class Network
        {
            public static T ExecuteSafely<T>(
                Func<T> operation,
                T? defaultValue = default(T),
                string operationName = "网络请求")
            {
                return ExceptionHandlingService.ExecuteSafely(
                    operation,
                    HandlingStrategy.ShowMessageAndLog,
                    defaultValue,
                    new ExceptionContext 
                    { 
                        Component = "Network", 
                        Operation = operationName 
                    });
            }

            public static async Task<T> ExecuteSafelyAsync<T>(
                Func<Task<T>> operation,
                T? defaultValue = default(T),
                string operationName = "网络请求")
            {
                return await ExceptionHandlingService.ExecuteSafelyAsync(
                    operation,
                    HandlingStrategy.ShowMessageAndLog,
                    defaultValue,
                    new ExceptionContext 
                    { 
                        Component = "Network", 
                        Operation = operationName 
                    });
            }
        }

        /// <summary>
        /// 专门处理TTS服务异常
        /// </summary>
        public static class Tts
        {
            public static async Task<T> ExecuteSafelyAsync<T>(
                Func<Task<T>> operation,
                T? defaultValue = default(T),
                string operationName = "TTS操作")
            {
                return await ExceptionHandlingService.ExecuteSafelyAsync(
                    operation,
                    HandlingStrategy.ShowMessageAndLog,
                    defaultValue,
                    new ExceptionContext 
                    { 
                        Component = "TTS", 
                        Operation = operationName 
                    });
            }

            public static async Task ExecuteSafelyAsync(
                Func<Task> operation,
                string operationName = "TTS操作")
            {
                await ExceptionHandlingService.ExecuteSafelyAsync(
                    operation,
                    HandlingStrategy.ShowMessageAndLog,
                    new ExceptionContext 
                    { 
                        Component = "TTS", 
                        Operation = operationName 
                    });
            }
        }

        /// <summary>
        /// 专门处理UI操作异常
        /// </summary>
        public static class UI
        {
            public static void ExecuteSafely(
                Action operation,
                string operationName = "UI操作")
            {
                ExceptionHandlingService.ExecuteSafely(
                    operation,
                    HandlingStrategy.LogOnly, // UI操作异常通常静默处理
                    new ExceptionContext 
                    { 
                        Component = "UI", 
                        Operation = operationName 
                    });
            }

            public static T ExecuteSafely<T>(
                Func<T> operation,
                T? defaultValue = default(T),
                string operationName = "UI操作")
            {
                return ExceptionHandlingService.ExecuteSafely(
                    operation,
                    HandlingStrategy.LogOnly,
                    defaultValue,
                    new ExceptionContext 
                    { 
                        Component = "UI", 
                        Operation = operationName 
                    });
            }
        }
    }
}
