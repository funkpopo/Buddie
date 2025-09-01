using System;
using System.Threading.Tasks;
using System.Threading;

namespace Buddie.Services
{
    public class RealtimeInteractionService : IDisposable
    {
        private EnhancedRealtimeInteractionService? _interactionService;
        private RealtimeConfiguration? _activeConfiguration;
        private bool _isRunning;

        public event Action<string>? OnTextReceived;
        public event Action<string>? OnStatusChanged;
        public event Action<Exception>? OnError;

        public bool IsRunning => _isRunning;
        public RealtimeConfiguration? ActiveConfiguration => _activeConfiguration;

        public async Task StartAsync(RealtimeConfiguration configuration)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("实时交互服务已在运行中");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            try
            {
                _activeConfiguration = configuration;
                _interactionService = new EnhancedRealtimeInteractionService(configuration);

                // 订阅事件
                _interactionService.OnTextResponse += text => OnTextReceived?.Invoke(text);
                _interactionService.OnStatusUpdate += status => OnStatusChanged?.Invoke(status);
                _interactionService.OnError += error => OnError?.Invoke(error);

                // 启动服务
                await _interactionService.StartAsync();
                _isRunning = true;
                
                OnStatusChanged?.Invoke($"实时交互已启动 (配置: {configuration.Name})");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _activeConfiguration = null;
                OnError?.Invoke(ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            try
            {
                if (_interactionService != null)
                {
                    await _interactionService.StopAsync();
                    _interactionService.Dispose();
                    _interactionService = null;
                }

                _isRunning = false;
                _activeConfiguration = null;
                OnStatusChanged?.Invoke("实时交互已停止");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                throw;
            }
        }

        public async Task SendTextMessageAsync(string message)
        {
            if (!_isRunning || _interactionService == null)
            {
                throw new InvalidOperationException("实时交互服务未运行");
            }

            await _interactionService.SendTextMessageAsync(message);
        }

        public async Task StopGenerationAsync()
        {
            if (_interactionService != null)
            {
                await _interactionService.StopGenerationAsync();
            }
        }

        public async Task TestConnectionAsync(RealtimeConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            OnStatusChanged?.Invoke("正在测试连接...");

            var result = await RealtimeConnectionDiagnostics.TestConnectionAsync(
                configuration.BaseUrl,
                configuration.ApiKey);

            if (result.IsSuccessful)
            {
                OnStatusChanged?.Invoke($"连接测试成功 (耗时: {result.ConnectionTime.TotalMilliseconds:F0}ms)");
            }
            else
            {
                OnError?.Invoke(new Exception(result.Message));
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _interactionService?.Dispose();
        }
    }
}