using System;
using System.Windows;

namespace Buddie.Services.ExceptionHandling
{
    public class WpfErrorNotifier : IErrorNotifier
    {
        public void NotifyError(string message, Exception? exception = null, ExceptionHandlingService.ExceptionContext? context = null)
        {
            try
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
            catch
            {
                Console.WriteLine($"Error: {message}");
                if (exception != null)
                {
                    Console.WriteLine(exception);
                }
            }
        }
    }
}

