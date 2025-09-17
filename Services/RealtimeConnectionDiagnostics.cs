using System;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Buddie.Services
{
    public class RealtimeConnectionDiagnostics
    {
        public class DiagnosticResult
        {
            public bool IsSuccessful { get; set; }
            public string Message { get; set; } = "";
            public TimeSpan ConnectionTime { get; set; }
            public Exception? Exception { get; set; }
        }

        public static async Task<DiagnosticResult> TestConnectionAsync(
            string baseUrl,
            string apiKey,
            int timeoutMs = 10000)
        {
            var startTime = DateTime.UtcNow;
            var result = new DiagnosticResult();

            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var webSocket = new ClientWebSocket();

                // 设置请求头
                webSocket.Options.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                webSocket.Options.SetRequestHeader("X-DashScope-Api-Key", apiKey);

                // 尝试连接
                var uri = new Uri(baseUrl);
                await webSocket.ConnectAsync(uri, cts.Token);

                result.ConnectionTime = DateTime.UtcNow - startTime;

                if (webSocket.State == WebSocketState.Open)
                {
                    // 发送测试消息
                    var testMessage = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "session.update",
                        session = new
                        {
                            modalities = new[] { "text", "audio" },
                            model = "qwen-omni-turbo-realtime",
                            voice = "Chelsie"
                        }
                    });

                    var bytes = Encoding.UTF8.GetBytes(testMessage);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        cts.Token);

                    // 等待响应
                    var buffer = new byte[1024 * 4];
                    var receiveResult = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cts.Token);

                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        var responseMessage = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                        
                        // 检查响应是否包含错误
                        if (responseMessage.Contains("error"))
                        {
                            result.IsSuccessful = false;
                            result.Message = "服务器返回错误响应";
                        }
                        else
                        {
                            result.IsSuccessful = true;
                            result.Message = "连接测试成功";
                        }
                    }
                    else
                    {
                        result.IsSuccessful = false;
                        result.Message = "收到意外的消息类型";
                    }

                    // 正常关闭连接
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "测试完成",
                        CancellationToken.None);
                }
                else
                {
                    result.IsSuccessful = false;
                    result.Message = $"WebSocket连接状态异常: {webSocket.State}";
                }
            }
            catch (OperationCanceledException)
            {
                result.IsSuccessful = false;
                result.Message = "连接超时";
                result.ConnectionTime = DateTime.UtcNow - startTime;
            }
            catch (WebSocketException ex)
            {
                result.IsSuccessful = false;
                result.Message = $"WebSocket连接错误: {ex.Message}";
                result.Exception = ex;
                result.ConnectionTime = DateTime.UtcNow - startTime;
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.Message = $"连接测试失败: {ex.Message}";
                result.Exception = ex;
                result.ConnectionTime = DateTime.UtcNow - startTime;
            }

            return result;
        }

        public static async Task<DiagnosticResult> TestAudioStreamingAsync(
            string baseUrl,
            string apiKey,
            byte[] testAudioData,
            int timeoutMs = 15000)
        {
            var startTime = DateTime.UtcNow;
            var result = new DiagnosticResult();

            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var adapter = new QwenOmniRealtimeAdapter(
                    baseUrl,
                    apiKey,
                    "qwen-omni-turbo-realtime",
                    "Chelsie",
                    TurnDetectionMode.ClientVad);

                var audioReceived = false;
                var textReceived = false;

                adapter.OnAudioDelta += (audio) => audioReceived = true;
                adapter.OnTextDelta += (text) => textReceived = true;

                await adapter.ConnectAsync();

                // 启动消息处理（在后台任务中）
                var messageTask = Task.Run(async () =>
                {
                    try
                    {
                        await adapter.StartMessageHandlingAsync();
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，忽略
                    }
                }, cts.Token);

                // 等待一段时间确保连接稳定
                await Task.Delay(1000, cts.Token);

                // 发送测试音频数据
                await adapter.SendAudioDataAsync(testAudioData);

                // 等待响应
                var waitTime = 0;
                while (waitTime < 10000 && !audioReceived && !textReceived && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, cts.Token);
                    waitTime += 100;
                }

                result.ConnectionTime = DateTime.UtcNow - startTime;

                if (audioReceived || textReceived)
                {
                    result.IsSuccessful = true;
                    result.Message = $"音频流测试成功 (音频: {audioReceived}, 文本: {textReceived})";
                }
                else
                {
                    result.IsSuccessful = false;
                    result.Message = "未收到服务器响应";
                }

                await adapter.CloseAsync();
            }
            catch (OperationCanceledException)
            {
                result.IsSuccessful = false;
                result.Message = "音频流测试超时";
                result.ConnectionTime = DateTime.UtcNow - startTime;
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.Message = $"音频流测试失败: {ex.Message}";
                result.Exception = ex;
                result.ConnectionTime = DateTime.UtcNow - startTime;
            }

            return result;
        }
    }
}