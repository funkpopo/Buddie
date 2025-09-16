using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Buddie.Database;
using Buddie.Services.ExceptionHandling;
using Buddie.Startup;

namespace Buddie
{
    public partial class App : Application
    {
        private IHost? _host;
        private IConfiguration? _configuration;
        private ILogger<App>? _logger;
        private static IServiceProvider? _services; // Static backing field for test scenarios

        public static IServiceProvider? Services
        {
            get
            {
                // Try to get from test-assigned services first
                if (_services != null)
                    return _services;

                // Otherwise, get from running app
                return Current != null ? ((App)Current)._host?.Services : null;
            }
            set => _services = value; // Allow test to set services (only for testing purposes)
        }

        public static T GetService<T>() where T : notnull => Services!.GetRequiredService<T>();
        public static IConfiguration Configuration => ((App)Current)._configuration!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // 构建并启动 Host（DI/Logging/HttpClient/Config）
                _host = await AppHostBuilder.BuildAndStartAsync();
                _configuration = _host.Services.GetRequiredService<IConfiguration>();
                _logger = _host.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();
                _logger.LogInformation("Host started successfully");

                // Wire ExceptionHandlingService with notifier + logger
                _logger.LogDebug("Getting IErrorNotifier...");
                var notifier = _host.Services.GetRequiredService<IErrorNotifier>();
                _logger.LogDebug("Got IErrorNotifier successfully");
                
                _logger.LogDebug("Getting ILoggerFactory...");
                var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
                _logger.LogDebug("Got ILoggerFactory successfully");
                
                _logger.LogInformation("Configuring ExceptionHandlingService...");
                ExceptionHandlingService.Configure(notifier, loggerFactory);
                _logger.LogInformation("ExceptionHandlingService configured successfully");

                // Set up global exception handlers
                _logger.LogInformation("Setting up global exception handlers...");
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                _logger.LogInformation("Global exception handlers set up successfully");
                
                // Initialize database
                _logger.LogDebug("Getting IDatabaseInitializer...");
                var dbInitializer = _host.Services.GetRequiredService<IDatabaseInitializer>();
                _logger.LogDebug("Got IDatabaseInitializer successfully");
                
                _logger.LogInformation("Initializing database...");
                await dbInitializer.InitializeAsync();
                _logger.LogInformation("Database initialized successfully");

                // Create and show main window
                _logger.LogInformation("Creating FloatingWindow...");
                var floatingWindow = new FloatingWindow();
                _logger.LogInformation("FloatingWindow created successfully");
                
                // Load settings from database
                _logger.LogInformation("Loading settings from database...");
                await floatingWindow.LoadSettingsFromDatabaseAsync();
                _logger.LogInformation("Settings loaded successfully");
                
                _logger.LogInformation("Showing FloatingWindow...");
                floatingWindow.Show();
                _logger.LogInformation("FloatingWindow shown successfully");
            },
            ExceptionHandlingService.HandlingStrategy.ShowMessage,
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "App",
                Operation = "应用程序启动"
            });

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop host gracefully
            if (_host != null)
            {
                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
            }
            // 最后一次保存应用程序设置
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                var mainWindow = Current.MainWindow as FloatingWindow;
                mainWindow?.SaveSettingsBeforeExit();
            },
            ExceptionHandlingService.HandlingStrategy.Silent, // 静默处理应用退出时的错误
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "App",
                Operation = "保存设置"
            });
            
            // 清理TTS音频缓存
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                var initializer = _host?.Services.GetService<IDatabaseInitializer>();
                if (initializer != null)
                {
                    initializer.CleanupTtsAudioCacheAsync().GetAwaiter().GetResult();
                }
            },
            ExceptionHandlingService.HandlingStrategy.Silent,
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "App",
                Operation = "清理TTS缓存"
            });
            
            base.OnExit(e);
        }
        
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var notifier = _host?.Services.GetService<IErrorNotifier>();
            var tmpl1 = Buddie.Localization.LocalizationManager.GetString("App_DispatcherUnhandledException_Format");
            var localized1 = string.Format(tmpl1, e.Exception.Message);
            notifier?.NotifyError(localized1, e.Exception,
                new ExceptionHandlingService.ExceptionContext { Component = "App", Operation = "DispatcherUnhandledException" });
            // Mark the exception as handled to prevent app crash
            e.Handled = true;
            return;
            notifier?.NotifyError($"发生未处理的异常:\n\n{e.Exception.Message}", e.Exception,
                new ExceptionHandlingService.ExceptionContext { Component = "App", Operation = "DispatcherUnhandledException" });

            // Mark the exception as handled to prevent app crash
            e.Handled = true;
        }
        
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                var notifier = _host?.Services.GetService<IErrorNotifier>();
                var tmpl2 = Buddie.Localization.LocalizationManager.GetString("App_UnhandledException_Format");
                var localized2 = string.Format(tmpl2, exception.Message);
                notifier?.NotifyError(localized2, exception,
                    new ExceptionHandlingService.ExceptionContext { Component = "App", Operation = "UnhandledException" });
                return;
                notifier?.NotifyError($"发生严重错误:\n\n{exception.Message}\n\n应用程序将退出。", exception,
                    new ExceptionHandlingService.ExceptionContext { Component = "App", Operation = "UnhandledException" });
            }
        }
        
    }
}
