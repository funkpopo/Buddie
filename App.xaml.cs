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
                System.Diagnostics.Debug.WriteLine("Application starting up...");
                
                // Set up global exception handlers
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                
                // Initialize database
                System.Diagnostics.Debug.WriteLine("Initializing database...");
                DatabaseManager.InitializeDatabase();
                System.Diagnostics.Debug.WriteLine("Database initialization completed");

                // Create and show main window
                var floatingWindow = new FloatingWindow();
                System.Diagnostics.Debug.WriteLine("FloatingWindow created");
                
                // Load settings from database
                System.Diagnostics.Debug.WriteLine("Loading settings from database...");
                await floatingWindow.LoadSettingsFromDatabaseAsync();
                System.Diagnostics.Debug.WriteLine("Settings loading completed");
                
                floatingWindow.Show();
                System.Diagnostics.Debug.WriteLine("FloatingWindow shown");
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
            System.Diagnostics.Debug.WriteLine("Application shutting down...");
            
            // 清理TTS音频缓存
            try
            {
                System.Diagnostics.Debug.WriteLine("Cleaning up TTS audio cache...");
                DatabaseManager.CleanupTtsAudioCache();
                System.Diagnostics.Debug.WriteLine("TTS audio cache cleanup completed");
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