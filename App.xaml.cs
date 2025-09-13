using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Buddie.Database;
using Buddie.Services.ExceptionHandling;

namespace Buddie
{
    public partial class App : Application
    {
        private IHost? _host;
        public static IServiceProvider Services => ((App)Current)._host!.Services;
        public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

        protected override async void OnStartup(StartupEventArgs e)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // Build Host (DI + Logging + HttpClient)
                _host = Host.CreateDefaultBuilder()
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddDebug();
                    })
                    .ConfigureServices(services =>
                    {
                        // 配置 HttpClient
                        services.AddHttpClient();
                        
                        // 为 TTS 服务配置专用的 HttpClient
                        services.AddHttpClient("TtsClient", client =>
                        {
                            client.Timeout = TimeSpan.FromMinutes(2);
                        });
                        
                        services.AddSingleton<IErrorNotifier, WpfErrorNotifier>();
                        
                        // Database services
                        services.AddSingleton<IDatabasePathProvider, DatabasePathProvider>();
                        services.AddSingleton<ISqliteConnectionPool, SqliteConnectionPool>();
                        services.AddSingleton<DatabaseService>();
                        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

                        // TTS services + resolver（基于Keyed DI，按渠道类型解析实现）
                        services.AddKeyedTransient<global::Buddie.Services.Tts.ITtsService, global::Buddie.Services.Tts.OpenAiTtsService>(global::Buddie.Services.Tts.TtsChannelType.OpenAI);
                        services.AddKeyedTransient<global::Buddie.Services.Tts.ITtsService, global::Buddie.Services.Tts.ElevenLabsTtsService>(global::Buddie.Services.Tts.TtsChannelType.ElevenLabs);
                        services.AddKeyedTransient<global::Buddie.Services.Tts.ITtsService, global::Buddie.Services.Tts.MiniMaxTtsService>(global::Buddie.Services.Tts.TtsChannelType.MiniMax);
                        services.AddSingleton<global::Buddie.Services.Tts.ITtsServiceResolver, global::Buddie.Services.Tts.DefaultTtsServiceResolver>();

                        // Realtime services
                        services.AddSingleton<Services.RealtimeInteractionService>();
                        services.AddSingleton<Services.EnhancedRealtimeInteractionService>();
                        services.AddSingleton<Services.RealtimeClient>();
                    })
                    .Build();

                await _host.StartAsync();

                // Wire ExceptionHandlingService with notifier + logger
                var notifier = _host.Services.GetRequiredService<IErrorNotifier>();
                var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
                ExceptionHandlingService.Configure(notifier, loggerFactory);

                // Set up global exception handlers
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                
                // Initialize database
                var dbInitializer = _host.Services.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();

                // Create and show main window
                var floatingWindow = new FloatingWindow();
                
                // Load settings from database
                await floatingWindow.LoadSettingsFromDatabaseAsync();
                
                floatingWindow.Show();
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
                notifier?.NotifyError($"发生严重错误:\n\n{exception.Message}\n\n应用程序将退出。", exception,
                    new ExceptionHandlingService.ExceptionContext { Component = "App", Operation = "UnhandledException" });
            }
        }
    }
}
