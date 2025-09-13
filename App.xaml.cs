using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Buddie.Database;
using Buddie.Services.ExceptionHandling;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http;

namespace Buddie
{
    public partial class App : Application
    {
        private IHost? _host;
        private IConfiguration? _configuration;
        private ILogger<App>? _logger;
        public static IServiceProvider Services => ((App)Current)._host!.Services;
        public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();
        public static IConfiguration Configuration => ((App)Current)._configuration!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
            {
                // Build Host (DI + Logging + HttpClient + Configuration)
                _host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                              .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureLogging((context, logging) =>
                    {
                        logging.ClearProviders();
                        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                        logging.AddDebug();
                        logging.AddConsole();
                    })
                    .ConfigureServices((context, services) =>
                    {
                        _configuration = context.Configuration;
                        
                        // 配置 HttpClient
                        services.AddHttpClient();
                        
                        // 为 TTS 服务配置专用的 HttpClient with Polly
                        var httpConfig = _configuration.GetSection("HttpClient");
                        var retryCount = httpConfig.GetValue<int>("Retry:Count", 3);
                        var baseDelay = httpConfig.GetValue<int>("Retry:BaseDelaySeconds", 2);
                        var circuitBreakerThreshold = httpConfig.GetValue<int>("CircuitBreaker:FailureThreshold", 5);
                        var circuitBreakerDuration = httpConfig.GetValue<int>("CircuitBreaker:DurationSeconds", 30);
                        var timeoutMinutes = httpConfig.GetValue<int>("TimeoutMinutes", 2);
                        
                        services.AddHttpClient("TtsClient", client =>
                        {
                            client.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
                        })
                        .AddPolicyHandler((sp, request) => GetRetryPolicy(sp, retryCount, baseDelay))
                        .AddPolicyHandler((sp, request) => GetCircuitBreakerPolicy(sp, circuitBreakerThreshold, circuitBreakerDuration));
                        
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
        
        /// <summary>
        /// 配置 HTTP 重试策略
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(IServiceProvider sp, int retryCount = 3, int baseDelaySeconds = 2)
        {
            var logger = sp.GetService<ILogger<App>>()
                         ?? sp.GetService<ILoggerFactory>()?.CreateLogger("HttpPolicies")
                         ?? NullLogger<App>.Instance;
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 处理 HttpRequestException, 5XX and 408
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(baseDelaySeconds, retryAttempt)), // 指数退避
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        logger.LogWarning("HTTP retry {Attempt} after {Delay}s; status={StatusCode}", attempt, timespan.TotalSeconds, (int?)outcome.Result?.StatusCode);
                    });
        }
        
        /// <summary>
        /// 配置熔断器策略
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(IServiceProvider sp, int failureThreshold = 5, int durationSeconds = 30)
        {
            var logger = sp.GetService<ILogger<App>>()
                         ?? sp.GetService<ILoggerFactory>()?.CreateLogger("HttpPolicies")
                         ?? NullLogger<App>.Instance;
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    failureThreshold,
                    TimeSpan.FromSeconds(durationSeconds),
                    onBreak: (result, timespan) =>
                    {
                        logger.LogError("Circuit broken for {Duration}s; lastStatus={StatusCode}", timespan.TotalSeconds, (int?)result.Result?.StatusCode);
                    },
                    onReset: () =>
                    {
                        logger.LogInformation("Circuit reset");
                    });
        }
    }
}
