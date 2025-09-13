using System;
using System.Windows;
using Buddie.Controls;

namespace Buddie.Services.ExceptionHandling
{
    public class WpfErrorNotifier : IErrorNotifier
    {
        public void NotifyError(string message, Exception? exception = null, ExceptionHandlingService.ExceptionContext? context = null)
        {
            try
            {
                // 委托给用户友好错误服务以展示内嵌通知
                Services.ExceptionHandling.UserFriendlyErrorService.ShowError(
                    exception ?? new Exception(message),
                    context?.Operation);
            }
            catch
            {
                // 降级到控制台输出
                Console.WriteLine($"Error: {message}");
                if (exception != null)
                {
                    Console.WriteLine(exception);
                }
            }
        }
    }
}
