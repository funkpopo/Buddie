using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Extensions.Http;

namespace Buddie.Services.Http
{
    public static class HttpPolicies
    {
        /// <summary>
        /// 获取 HTTP 重试策略（指数退避）。
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
            IServiceProvider sp,
            int retryCount = 3,
            int baseDelaySeconds = 2)
        {
            var logger = sp.GetService(typeof(ILogger<HttpPolicies>)) as ILogger<HttpPolicies>
                         ?? (sp.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger<HttpPolicies>()
                         ?? NullLogger<HttpPolicies>.Instance;

            return HttpPolicyExtensions
                .HandleTransientHttpError() // HttpRequestException, 5XX, 408
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(baseDelaySeconds, retryAttempt)),
                    onRetry: (outcome, timespan, attempt, context) =>
                    {
                        logger.LogWarning(
                            "HTTP retry {Attempt} after {Delay}s; status={StatusCode}",
                            attempt,
                            timespan.TotalSeconds,
                            (int?)outcome.Result?.StatusCode);
                    });
        }

        /// <summary>
        /// 获取 HTTP 熔断器策略。
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
            IServiceProvider sp,
            int failureThreshold = 5,
            int durationSeconds = 30)
        {
            var logger = sp.GetService(typeof(ILogger<HttpPolicies>)) as ILogger<HttpPolicies>
                         ?? (sp.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger<HttpPolicies>()
                         ?? NullLogger<HttpPolicies>.Instance;

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    failureThreshold,
                    TimeSpan.FromSeconds(durationSeconds),
                    onBreak: (result, timespan) =>
                    {
                        logger.LogError(
                            "Circuit broken for {Duration}s; lastStatus={StatusCode}",
                            timespan.TotalSeconds,
                            (int?)result.Result?.StatusCode);
                    },
                    onReset: () =>
                    {
                        logger.LogInformation("Circuit reset");
                    });
        }
    }
}

