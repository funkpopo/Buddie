using System;
using System.Collections.Generic;
using System.Text.Json;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Services
{
    /// <summary>
    /// 多模态API请求构建服务，支持不同AI提供商的API格式
    /// </summary>
    public static class MultimodalApiService
    {
        /// <summary>
        /// 构建多模态请求体
        /// </summary>
        /// <param name="textContent">文本内容</param>
        /// <param name="imageBase64">Base64编码的图片数据</param>
        /// <param name="config">API配置</param>
        /// <returns>请求体对象</returns>
        public static object BuildMultimodalRequest(string textContent, string imageBase64, OpenApiConfiguration config)
        {
            return ExceptionHandlingService.ExecuteSafely(() =>
            {
                return config.ChannelType switch
                {
                    ChannelType.OpenAI => BuildOpenAIMultimodalRequest(textContent, imageBase64, config),
                    ChannelType.GoogleGemini => BuildGeminiMultimodalRequest(textContent, imageBase64, config),
                    ChannelType.ZhipuGLM => BuildZhipuMultimodalRequest(textContent, imageBase64, config),
                    ChannelType.TongyiQwen => BuildQwenMultimodalRequest(textContent, imageBase64, config),
                    ChannelType.AnthropicClaude => BuildClaudeMultimodalRequest(textContent, imageBase64, config),
                    ChannelType.Custom or ChannelType.SiliconFlow => BuildOpenAIMultimodalRequest(textContent, imageBase64, config), // 默认使用OpenAI格式
                    _ => BuildOpenAIMultimodalRequest(textContent, imageBase64, config)
                };
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, 
            BuildOpenAIMultimodalRequest(textContent, imageBase64, config), // 默认回退到OpenAI格式
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "MultimodalApiService",
                Operation = "构建多模态请求"
            });
        }

        /// <summary>
        /// 构建OpenAI格式的多模态请求
        /// </summary>
        private static object BuildOpenAIMultimodalRequest(string textContent, string imageBase64, OpenApiConfiguration config)
        {
            return new
            {
                model = config.ModelName,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = textContent },
                            new 
                            { 
                                type = "image_url", 
                                image_url = new { url = $"data:image/png;base64,{imageBase64}" }
                            }
                        }
                    }
                },
                stream = config.IsStreamingEnabled,
                max_tokens = 4096
            };
        }

        /// <summary>
        /// 构建Google Gemini格式的多模态请求
        /// </summary>
        private static object BuildGeminiMultimodalRequest(string textContent, string imageBase64, OpenApiConfiguration config)
        {
            return new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = textContent },
                            new 
                            { 
                                inline_data = new 
                                { 
                                    mime_type = "image/png",
                                    data = imageBase64
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 4096,
                    temperature = 0.7
                },
                safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                }
            };
        }

        /// <summary>
        /// 构建智谱GLM格式的多模态请求
        /// </summary>
        private static object BuildZhipuMultimodalRequest(string textContent, string imageBase64, OpenApiConfiguration config)
        {
            return new
            {
                model = config.ModelName,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = textContent },
                            new 
                            { 
                                type = "image_url", 
                                image_url = new { url = $"data:image/png;base64,{imageBase64}" }
                            }
                        }
                    }
                },
                stream = config.IsStreamingEnabled,
                max_tokens = 4096,
                temperature = 0.7
            };
        }

        /// <summary>
        /// 构建通义千问格式的多模态请求
        /// </summary>
        private static object BuildQwenMultimodalRequest(string textContent, string imageBase64, OpenApiConfiguration config)
        {
            return new
            {
                model = config.ModelName,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = textContent },
                            new 
                            { 
                                type = "image_url", 
                                image_url = new { url = $"data:image/png;base64,{imageBase64}" }
                            }
                        }
                    }
                },
                stream = config.IsStreamingEnabled,
                max_tokens = 4096,
                temperature = 0.7
            };
        }

        /// <summary>
        /// 构建Anthropic Claude格式的多模态请求
        /// </summary>
        private static object BuildClaudeMultimodalRequest(string textContent, string imageBase64, OpenApiConfiguration config)
        {
            return new
            {
                model = config.ModelName,
                max_tokens = 4096,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = textContent },
                            new 
                            { 
                                type = "image", 
                                source = new 
                                { 
                                    type = "base64",
                                    media_type = "image/png",
                                    data = imageBase64
                                }
                            }
                        }
                    }
                },
                stream = config.IsStreamingEnabled,
                temperature = 0.7
            };
        }

        /// <summary>
        /// 构建文本请求体（非多模态）
        /// </summary>
        public static object BuildTextRequest(string textContent, OpenApiConfiguration config)
        {
            return ExceptionHandlingService.ExecuteSafely(() =>
            {
                return config.ChannelType switch
                {
                    ChannelType.GoogleGemini => BuildGeminiTextRequest(textContent, config),
                    ChannelType.AnthropicClaude => BuildClaudeTextRequest(textContent, config),
                    _ => BuildOpenAITextRequest(textContent, config) // 默认使用OpenAI格式
                };
            }, ExceptionHandlingService.HandlingStrategy.LogOnly,
            BuildOpenAITextRequest(textContent, config), // 默认回退到OpenAI格式
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "MultimodalApiService",
                Operation = "构建文本请求"
            });
        }

        /// <summary>
        /// 构建OpenAI格式的文本请求
        /// </summary>
        private static object BuildOpenAITextRequest(string textContent, OpenApiConfiguration config)
        {
            return new
            {
                model = config.ModelName,
                messages = new[]
                {
                    new { role = "user", content = textContent }
                },
                stream = config.IsStreamingEnabled,
                max_tokens = 4096,
                temperature = 0.7
            };
        }

        /// <summary>
        /// 构建Gemini格式的文本请求
        /// </summary>
        private static object BuildGeminiTextRequest(string textContent, OpenApiConfiguration config)
        {
            return new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = textContent }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 4096,
                    temperature = 0.7
                }
            };
        }

        /// <summary>
        /// 构建Claude格式的文本请求
        /// </summary>
        private static object BuildClaudeTextRequest(string textContent, OpenApiConfiguration config)
        {
            return new
            {
                model = config.ModelName,
                max_tokens = 4096,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = textContent
                    }
                },
                stream = config.IsStreamingEnabled,
                temperature = 0.7
            };
        }

        /// <summary>
        /// 检查指定渠道是否支持多模态
        /// </summary>
        public static bool SupportsMultimodal(ChannelType channelType)
        {
            return channelType switch
            {
                ChannelType.OpenAI => true,
                ChannelType.GoogleGemini => true,
                ChannelType.ZhipuGLM => true,
                ChannelType.TongyiQwen => true,
                ChannelType.AnthropicClaude => true,
                ChannelType.Custom => true, // 假设自定义配置支持
                _ => false
            };
        }
    }
}
