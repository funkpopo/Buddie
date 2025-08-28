using System;
using System.Windows;
using System.Windows.Threading;
using Buddie.Database;

namespace Buddie
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Set up global exception handlers
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                
                // Initialize database
                DatabaseManager.InitializeDatabase();

                // Create and show main window
                var floatingWindow = new FloatingWindow();
                
                // Load settings from database
                await floatingWindow.LoadSettingsFromDatabaseAsync();
                
                floatingWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Application startup failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"应用程序启动失败: {ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 最后一次保存应用程序设置
            try
            {
                var mainWindow = Current.MainWindow as FloatingWindow;
                mainWindow?.SaveSettingsBeforeExit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 应用程序退出时保存设置失败: {ex.Message}");
            }
            
            // 清理TTS音频缓存
            try
            {
                DatabaseManager.CleanupTtsAudioCache();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cleanup TTS audio cache: {ex.Message}");
            }
            
            base.OnExit(e);
        }
        
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled exception in dispatcher: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {e.Exception.StackTrace}");
            
            MessageBox.Show(
                $"发生未处理的异常:\n\n{e.Exception.Message}\n\n详细信息已记录到调试输出。", 
                "应用程序错误", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
            
            // Mark the exception as handled to prevent app crash
            e.Handled = true;
        }
        
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Unhandled domain exception: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {exception.StackTrace}");
                
                MessageBox.Show(
                    $"发生严重错误:\n\n{exception.Message}\n\n应用程序将退出。", 
                    "严重错误", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
    }
}