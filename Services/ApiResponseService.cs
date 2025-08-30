using System;
using System.Text.Json;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Services
{
    /// <summary>
    /// API响应解析服务，支持不同AI提供商的响应格式
    /// </summary>
    public static class ApiResponseService
    {
        /// <summary>
        /// 解析非流式API响应
        /// </summary>
        /// <param name="responseText">响应文本</param>
        /// <param name="channelType">渠道类型</param>
        /// <returns>解析出的消息内容</returns>
        public static string ParseNonStreamingResponse(string responseText, ChannelType channelType)
        {
            return ExceptionHandlingService.ExecuteSafely(() =>
            {
                return channelType switch
                {
                    ChannelType.GoogleGemini => ParseGeminiResponse(responseText),
                    ChannelType.AnthropicClaude => ParseClaudeResponse(responseText),
                    _ => ParseOpenAIResponse(responseText) // 默认使用OpenAI格式
                };
            }, ExceptionHandlingService.HandlingStrategy.LogOnly,
            $"API返回了无效的响应格式: {responseText}",
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "ApiResponseService",
                Operation = "解析非流式响应"
            });
        }

        /// <summary>
        /// 解析流式响应中的增量内容
        /// </summary>
        /// <param name="jsonData">JSON数据</param>
        /// <param name="channelType">渠道类型</param>
        /// <returns>解析出的增量内容(content, reasoning)</returns>
        public static (string? content, string? reasoning) ParseStreamingDelta(string jsonData, ChannelType channelType)
        {
            return ExceptionHandlingService.ExecuteSafely(() =>
            {
                return channelType switch
                {
                    ChannelType.GoogleGemini => ParseGeminiStreamingDelta(jsonData),
                    ChannelType.AnthropicClaude => ParseClaudeStreamingDelta(jsonData),
                    _ => ParseOpenAIStreamingDelta(jsonData) // 默认使用OpenAI格式
                };
            }, ExceptionHandlingService.HandlingStrategy.LogOnly,
            (null, null),
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "ApiResponseService",
                Operation = "解析流式增量"
            });
        }

        /// <summary>
        /// 解析OpenAI格式的响应
        /// </summary>
        private static string ParseOpenAIResponse(string responseText)
        {
            var jsonDoc = JsonDocument.Parse(responseText);
            var choices = jsonDoc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message))
                {
                    return message.GetProperty("content").GetString() ?? "无响应内容";
                }
            }
            return "API响应格式错误";
        }

        /// <summary>
        /// 解析Google Gemini格式的响应
        /// </summary>
        private static string ParseGeminiResponse(string responseText)
        {
            var jsonDoc = JsonDocument.Parse(responseText);
            if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var part = parts[0];
                    if (part.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "无响应内容";
                    }
                }
            }
            return "Gemini API响应格式错误";
        }

        /// <summary>
        /// 解析Anthropic Claude格式的响应
        /// </summary>
        private static string ParseClaudeResponse(string responseText)
        {
            var jsonDoc = JsonDocument.Parse(responseText);
            if (jsonDoc.RootElement.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
            {
                var contentItem = content[0];
                if (contentItem.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "无响应内容";
                }
            }
            return "Claude API响应格式错误";
        }

        /// <summary>
        /// 解析OpenAI格式的流式增量
        /// </summary>
        private static (string? content, string? reasoning) ParseOpenAIStreamingDelta(string jsonData)
        {
            var jsonDoc = JsonDocument.Parse(jsonData);
            var choices = jsonDoc.RootElement.GetProperty("choices");
            
            if (choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta))
                {
                    string? content = null;
                    string? reasoning = null;

                    // 处理思维内容
                    if (delta.TryGetProperty("reasoning_content", out var reasoningProp))
                    {
                        reasoning = reasoningProp.GetString();
                    }
                    
                    // 处理实际内容
                    if (delta.TryGetProperty("content", out var contentProp))
                    {
                        content = contentProp.GetString();
                    }

                    return (content, reasoning);
                }
            }
            return (null, null);
        }

        /// <summary>
        /// 解析Google Gemini格式的流式增量
        /// </summary>
        private static (string? content, string? reasoning) ParseGeminiStreamingDelta(string jsonData)
        {
            var jsonDoc = JsonDocument.Parse(jsonData);
            if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var part = parts[0];
                    if (part.TryGetProperty("text", out var text))
                    {
                        return (text.GetString(), null);
                    }
                }
            }
            return (null, null);
        }

        /// <summary>
        /// 解析Anthropic Claude格式的流式增量
        /// </summary>
        private static (string? content, string? reasoning) ParseClaudeStreamingDelta(string jsonData)
        {
            var jsonDoc = JsonDocument.Parse(jsonData);
            if (jsonDoc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "content_block_delta")
            {
                if (jsonDoc.RootElement.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("text", out var text))
                {
                    return (text.GetString(), null);
                }
            }
            return (null, null);
        }
    }
}
