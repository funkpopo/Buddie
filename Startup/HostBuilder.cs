using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Buddie.Database;
using Buddie.Services.ExceptionHandling;
using Buddie.Services.Http;

namespace Buddie.Startup
{
    /// <summary>
    /// 负责应用 Host 的构建与服务注册。
    /// </summary>
    public static class AppHostBuilder
    {
        public static async Task<IHost> BuildAndStartAsync()
        {
            var host = Host.CreateDefaultBuilder()
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
                    // 基础 HttpClient
                    services.AddHttpClient();

                    // TTS 专用 HttpClient + Polly 策略
                    var httpConfig = context.Configuration.GetSection("HttpClient");
                    var retryCount = httpConfig.GetValue<int>("Retry:Count", 3);
                    var baseDelay = httpConfig.GetValue<int>("Retry:BaseDelaySeconds", 2);
                    var circuitBreakerThreshold = httpConfig.GetValue<int>("CircuitBreaker:FailureThreshold", 5);
                    var circuitBreakerDuration = httpConfig.GetValue<int>("CircuitBreaker:DurationSeconds", 30);
                    var timeoutMinutes = httpConfig.GetValue<int>("TimeoutMinutes", 2);

                    services.AddHttpClient("TtsClient", client =>
                    {
                        client.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
                    })
                    .AddPolicyHandler((sp, request) => HttpPolicies.GetRetryPolicy(sp, retryCount, baseDelay))
                    .AddPolicyHandler((sp, request) => HttpPolicies.GetCircuitBreakerPolicy(sp, circuitBreakerThreshold, circuitBreakerDuration));

                    // 错误通知
                    services.AddSingleton<IErrorNotifier, WpfErrorNotifier>();

                    // 数据库服务
                    services.AddSingleton<IDatabasePathProvider, DatabasePathProvider>();
                    services.AddSingleton<ISqliteConnectionPool, SqliteConnectionPool>();
                    services.AddSingleton<DatabaseService>();
                    services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

                    // TTS 服务注册（Keyed DI）
                    services.AddKeyedTransient<global::Buddie.Services.Tts.ITtsService, global::Buddie.Services.Tts.OpenAiTtsService>(global::Buddie.Services.Tts.TtsChannelType.OpenAI);
                    services.AddKeyedTransient<global::Buddie.Services.Tts.ITtsService, global::Buddie.Services.Tts.ElevenLabsTtsService>(global::Buddie.Services.Tts.TtsChannelType.ElevenLabs);
                    services.AddKeyedTransient<global::Buddie.Services.Tts.ITtsService, global::Buddie.Services.Tts.MiniMaxTtsService>(global::Buddie.Services.Tts.TtsChannelType.MiniMax);
                    services.AddSingleton<global::Buddie.Services.Tts.ITtsServiceResolver, global::Buddie.Services.Tts.DefaultTtsServiceResolver>();

                    // 实时服务
                    services.AddSingleton<global::Buddie.Services.RealtimeInteractionService>();
                    services.AddSingleton<global::Buddie.Services.EnhancedRealtimeInteractionService>();
                    services.AddSingleton<global::Buddie.Services.RealtimeClient>();
                })
                .Build();

            await host.StartAsync();
            return host;
        }
    }
}

