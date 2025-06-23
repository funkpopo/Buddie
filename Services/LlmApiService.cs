using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lyxie_desktop.Views; // 添加对LlmApiConfig所在命名空间的引用
using Lyxie_desktop.Interfaces;

namespace Lyxie_desktop.Services
{
    public class LlmApiService : ILlmApiService
    {
        private readonly HttpClient _httpClient;

        public LlmApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // 增加超时时间以支持流式响应
        }

        /// <summary>
        /// 测试LLM API是否可用，发送简单的"ping"消息
        /// </summary>
        /// <param name="config">LLM API配置</param>
        /// <returns>测试结果和消息</returns>
        public async Task<(bool Success, string Message)> TestApiAsync(LlmApiConfig config)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(config.ApiUrl))
                    return (false, "API URL不能为空");

                if (string.IsNullOrWhiteSpace(config.ApiKey))
                    return (false, "API Key不能为空");

                // 克隆HttpClient以便能设置不同的请求头
                using var httpClient = new HttpClient();
                httpClient.Timeout = _httpClient.Timeout;
                
                // 设置请求头
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

                // 构建简单的请求消息
                var requestData = new
                {
                    model = config.ModelName,
                    messages = new[]
                    {
                        new { role = "user", content = "ping" }
                    },
                    temperature = config.Temperature,
                    max_tokens = Math.Min(20, config.MaxTokens) // 测试时限制token数量
                };

                // 转换为JSON
                var jsonContent = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 发送请求
                var response = await httpClient.PostAsync(config.ApiUrl, content);

                // 检查响应状态
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return (true, "API连接成功");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, $"API返回错误状态码 {(int)response.StatusCode}: {errorContent}");
                }
            }
            catch (HttpRequestException ex)
            {
                return (false, $"网络请求错误: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                return (false, "请求超时");
            }
            catch (Exception ex)
            {
                return (false, $"未知错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送流式消息请求
        /// </summary>
        public async Task<bool> SendStreamingMessageAsync(
            LlmApiConfig config,
            string message,
            StreamingDataCallback onDataReceived,
            StreamingErrorCallback? onError = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(config.ApiUrl) || string.IsNullOrWhiteSpace(config.ApiKey))
                {
                    onError?.Invoke("API配置不完整");
                    return false;
                }

                // 构建流式请求数据
                var requestData = new
                {
                    model = config.ModelName,
                    messages = new[]
                    {
                        new { role = "user", content = message }
                    },
                    temperature = config.Temperature,
                    max_tokens = config.MaxTokens,
                    stream = true // 启用流式响应
                };

                var jsonContent = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 创建请求消息
                var request = new HttpRequestMessage(HttpMethod.Post, config.ApiUrl)
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                request.Headers.Add("Accept", "text/event-stream");

                // 发送请求并获取响应流
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    onError?.Invoke($"API错误 {(int)response.StatusCode}: {errorContent}");
                    return false;
                }

                // 读取流式数据
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                var fullContent = new StringBuilder();
                string? line;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 跳过空行
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // 处理SSE格式数据 (data: {...})
                    if (line.StartsWith("data: "))
                    {
                        var jsonData = line.Substring(6).Trim();
                        
                        // 检查是否为结束标记
                        if (jsonData == "[DONE]")
                        {
                            onDataReceived.Invoke("", true);
                            break;
                        }

                        try
                        {
                            // 解析JSON数据
                            var jsonObject = JObject.Parse(jsonData);
                            var choices = jsonObject["choices"] as JArray;
                            
                            if (choices != null && choices.Count > 0)
                            {
                                var firstChoice = choices[0];
                                var delta = firstChoice["delta"];
                                
                                if (delta != null && delta["content"] != null)
                                {
                                    var contentChunk = delta["content"]?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(contentChunk))
                                    {
                                        fullContent.Append(contentChunk);
                                        onDataReceived.Invoke(contentChunk, false);
                                    }
                                }
                                
                                // 检查是否完成
                                var finishReason = firstChoice["finish_reason"];
                                if (finishReason != null && !string.IsNullOrEmpty(finishReason.ToString()))
                                {
                                    onDataReceived.Invoke("", true);
                                    break;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"解析流式数据错误: {ex.Message}");
                            // 继续处理下一行，不中断整个流程
                        }
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                // 用户取消，正常情况
                return false;
            }
            catch (Exception ex)
            {
                onError?.Invoke($"流式请求错误: {ex.Message}");
                return false;
            }
        }

        // 释放资源
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 