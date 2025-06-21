using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Lyxie_desktop.Views; // 添加对LlmApiConfig所在命名空间的引用

namespace Lyxie_desktop.Services
{
    public class LlmApiService
    {
        private readonly HttpClient _httpClient;

        public LlmApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15); // 设置15秒超时
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

        // 释放资源
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
} 