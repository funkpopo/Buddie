using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Buddie.Services.ExceptionHandling;
using Buddie.Security;
using Microsoft.Extensions.Logging;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// MiniMax TTS服务实现
    /// 支持非流式语音合成，音频格式为WAV
    /// API文档: https://platform.minimaxi.com/document/%E5%90%8C%E6%AD%A5%E8%AF%AD%E9%9F%B3%E5%90%88%E6%88%90
    /// </summary>
    public class MiniMaxTtsService : TtsServiceBase
    {
        public override TtsChannelType SupportedChannelType => TtsChannelType.MiniMax;

        protected override async Task<TtsResponse> CallTtsApiAsync(TtsRequest request)
        {
            return await ExceptionHandlingService.Tts.ExecuteSafelyAsync(async () =>
            {
                var config = request.Configuration;
                
                // 打印诊断信息
                MiniMaxTtsValidator.PrintDiagnosticInfo(config);
                
                // 设置请求头
                SetupHttpHeaders(config);

                // 构建请求体，强制使用非流式和WAV格式
                var requestBody = CreateRequestBody(request);
                var json = JsonSerializer.Serialize(requestBody);
                
                // 构建完整的API URL，包含GroupId参数
                var apiUrl = BuildApiUrl(config);
                
                _logger.LogDebug("MiniMax request body: {Body}", json);
                _logger.LogInformation("MiniMax request URL: {Url}", apiUrl);

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // 使用非流式处理
                return await ProcessNonStreamingResponseAsync(config, content, apiUrl);
            },
            new TtsResponse 
            { 
                IsSuccess = false, 
                ErrorMessage = "MiniMax TTS服务调用失败" 
            },
            "MiniMax TTS API调用");
        }

        /// <summary>
        /// 创建MiniMax API请求体，强制使用WAV格式和非流式
        /// </summary>
        private object CreateRequestBody(TtsRequest request)
        {
            var config = request.Configuration;
            
            // 清理和验证文本内容
            var cleanText = CleanTextForTts(request.Text);
            
            // 确保文本不为空
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                throw new TtsException(SupportedChannelType, "文本内容不能为空");
            }
            
            // 根据MiniMax API文档创建请求体，强制使用WAV格式和非流式
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = config.Model ?? "speech-01-hd",
                ["text"] = cleanText,
                ["stream"] = false, // 强制非流式
                ["output_format"] = "hex", // 使用十六进制输出格式以获取WAV
                ["language_boost"] = "auto",
                ["voice_setting"] = new Dictionary<string, object>
                {
                    ["voice_id"] = config.Voice ?? "female-shaonv",
                    ["speed"] = config.Speed,
                    ["vol"] = 1,
                    ["pitch"] = 0
                },
                ["audio_setting"] = new Dictionary<string, object>
                {
                    ["sample_rate"] = 32000,
                    ["bitrate"] = 128000,
                    ["format"] = "wav", // 强制使用WAV格式
                    ["channel"] = 1
                }
            };

            // 确保所有必需字段都不为空
            _logger.LogDebug("MiniMax 参数检查: model={Model}, textLen={Len}, voice_id={Voice}, speed={Speed}, format={Fmt}, output_format={OutFmt}, stream={Stream}",
                requestBody["model"], cleanText?.Length ?? 0,
                ((Dictionary<string, object>)requestBody["voice_setting"])["voice_id"],
                ((Dictionary<string, object>)requestBody["voice_setting"])["speed"],
                ((Dictionary<string, object>)requestBody["audio_setting"])["format"],
                requestBody["output_format"], requestBody["stream"]);
            
            // 验证关键字段不为空
            if (string.IsNullOrEmpty(requestBody["model"]?.ToString()))
                _logger.LogWarning("MiniMax: model 字段为空");
            if (string.IsNullOrEmpty(requestBody["text"]?.ToString()))
                _logger.LogWarning("MiniMax: text 字段为空");
            var voiceSetting = (Dictionary<string, object>)requestBody["voice_setting"];
            if (string.IsNullOrEmpty(voiceSetting["voice_id"]?.ToString()))
                _logger.LogWarning("MiniMax: voice_id 字段为空");

            return requestBody;
        }

        /// <summary>
        /// 构建完整的API URL，包含必要的查询参数
        /// </summary>
        private string BuildApiUrl(TtsConfiguration config)
        {
            var baseUrl = config.ApiUrl ?? "https://api.minimaxi.com/v1/t2a_v2";
            
            // 从API Key中提取GroupId，或使用默认值
            // MiniMax的GroupId通常是API Key的一部分，或者可以从用户配置中获取
            // 这里我们先使用一个占位符，实际使用时需要用户提供正确的GroupId
            var groupId = ExtractGroupIdFromApiKey(config.ApiKey);
            
            return $"{baseUrl}?GroupId={groupId}";
        }
        
        /// <summary>
        /// 从API Key中提取GroupId，或返回默认值
        /// </summary>
        private string ExtractGroupIdFromApiKey(string? apiKey)
        {
            // 支持多种格式:
            // 1. "API_KEY|GROUP_ID" - 推荐格式
            // 2. "GROUP_ID:API_KEY" - 备选格式
            // 3. 纯API_KEY - 尝试从API配置中获取GroupId
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                // 格式1: API_KEY|GROUP_ID
                if (apiKey.Contains("|"))
                {
                    var parts = apiKey.Split('|');
                    if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        _logger.LogDebug("Extracted GroupId from API key (format1)");
                        return parts[1].Trim();
                    }
                }
                
                // 格式2: GROUP_ID:API_KEY
                if (apiKey.Contains(":"))
                {
                    var parts = apiKey.Split(':');
                    if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        _logger.LogDebug("Extracted GroupId from API key (format2)");
                        return parts[0].Trim();
                    }
                }
            }
            
            // 如果无法提取GroupId，抛出更明确的错误
            var errorMessage = "无法获取MiniMax GroupId。请在API Key中包含GroupId信息:\n" +
                             "格式1: YOUR_API_KEY|YOUR_GROUP_ID\n" +
                             "格式2: YOUR_GROUP_ID:YOUR_API_KEY\n" +
                             "您可以在MiniMax控制台找到您的GroupId";
            
            _logger.LogError("MiniMax: {Message}", errorMessage);
            throw new TtsException(SupportedChannelType, errorMessage);
        }

        /// <summary>
        /// 处理非流式响应
        /// </summary>
        private async Task<TtsResponse> ProcessNonStreamingResponseAsync(TtsConfiguration config, StringContent content, string apiUrl)
        {
            var response = await _httpClient.PostAsync(apiUrl, content);
            
            _logger.LogInformation("MiniMax status: {Status} {Reason}", (int)response.StatusCode, response.ReasonPhrase);
            _logger.LogDebug("MiniMax headers: {Headers}", response.Headers);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // 先检查响应内容是否为空或明显不是JSON
                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    return new TtsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "API返回空响应",
                        ErrorDetails = "响应内容为空"
                    };
                }
                
                // 检查响应是否以JSON开头
                var trimmedContent = responseContent.TrimStart();
                if (!trimmedContent.StartsWith("{") && !trimmedContent.StartsWith("["))
                {
                    // 截取前500字符用于日志与错误显示
                    var shortContent = responseContent.Length > 500 
                        ? responseContent.Substring(0, 500) + "..." 
                        : responseContent;
                    _logger.LogWarning("MiniMax returned non-JSON response: {Preview}", shortContent);
                    
                    return new TtsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"API返回非JSON格式响应: {shortContent}",
                        ErrorDetails = responseContent
                    };
                }
                
                try
                {
                    _logger.LogDebug("MiniMax response JSON: {Content}", responseContent);
                    
                    using var document = JsonDocument.Parse(responseContent);
                    var root = document.RootElement;
                    
                    // 检查是否有错误
                    if (root.TryGetProperty("base_resp", out var baseResp))
                    {
                        if (baseResp.TryGetProperty("status_code", out var statusCode) && 
                            statusCode.GetInt32() != 0)
                        {
                            var errorMsg = baseResp.TryGetProperty("status_msg", out var statusMsg) 
                                ? statusMsg.GetString() : "未知错误";
                            
                            _logger.LogError("MiniMax API error: status_code={StatusCode}, status_msg={Msg}", statusCode.GetInt32(), errorMsg);
                            
                            return new TtsResponse
                            {
                                IsSuccess = false,
                                ErrorMessage = $"MiniMax API错误: {errorMsg}",
                                ErrorDetails = responseContent
                            };
                        }
                    }
                    
                    // 获取音频数据
                    if (root.TryGetProperty("data", out var data) && 
                        data.TryGetProperty("audio", out var audioElement))
                    {
                        var audioHex = audioElement.GetString();
                        if (!string.IsNullOrEmpty(audioHex))
                        {
                            try
                            {
                                // MiniMax使用hex输出格式时返回十六进制编码的WAV音频数据
                                Debug.WriteLine($"MiniMax返回的音频数据长度: {audioHex.Length} 字符");
                                var audioData = ConvertHexToBytes(audioHex);
                                
                                // 验证是否为有效的WAV格式
                                var actualFormat = DetectAudioFormat(audioData);
                                Debug.WriteLine($"MiniMax返回的实际音频格式: {actualFormat}");
                                
                                if (actualFormat != "wav")
                                {
                                    Debug.WriteLine($"警告: 期望WAV格式，但检测到{actualFormat}格式");
                                }
                                
                                // 返回WAV音频数据，确保NAudio可以直接播放
                                return new TtsResponse
                                {
                                    IsSuccess = true,
                                    AudioData = audioData,
                                    ContentType = "audio/wav"
                                };
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"解析音频数据失败: {ex.Message}");
                                return new TtsResponse
                                {
                                    IsSuccess = false,
                                    ErrorMessage = $"音频数据解析失败: {ex.Message}",
                                    ErrorDetails = $"数据长度: {audioHex.Length}"
                                };
                            }
                        }
                    }
                    
                    return new TtsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "响应中未找到音频数据",
                        ErrorDetails = responseContent
                    };
                }
                catch (JsonException ex)
                {
                    return new TtsResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"解析响应失败: {ex.Message}",
                        ErrorDetails = responseContent
                    };
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"MiniMax HTTP错误: {response.StatusCode}");
                Debug.WriteLine($"错误响应内容: {errorContent}");
                
                return new TtsResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"HTTP请求失败: {response.StatusCode} - {response.ReasonPhrase}",
                    ErrorDetails = errorContent
                };
            }
        }

        /// <summary>
        /// 检测音频数据的实际格式
        /// </summary>
        private string DetectAudioFormat(byte[] audioData)
        {
            if (audioData.Length >= 12)
            {
                var header = System.Text.Encoding.ASCII.GetString(audioData, 0, 4);
                if (header == "RIFF")
                {
                    var format = System.Text.Encoding.ASCII.GetString(audioData, 8, 4);
                    if (format == "WAVE")
                    {
                        return "wav";
                    }
                }
            }
            
            if (audioData.Length >= 3)
            {
                var first3Bytes = System.Text.Encoding.ASCII.GetString(audioData, 0, 3);
                if (first3Bytes == "ID3")
                {
                    return "mp3";
                }
            }
            
            if (audioData.Length >= 2)
            {
                // 检查MP3同步字节 (0xFF 0xFB/0xFA/0xF3/0xF2)
                if (audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0)
                {
                    return "mp3";
                }
            }
            
            return "unknown";
        }

        /// <summary>
        /// 将十六进制字符串转换为字节数组
        /// </summary>
        private byte[] ConvertHexToBytes(string hex)
        {
            try
            {
                // 移除可能的前缀和空格
                hex = hex.Replace("0x", "").Replace(" ", "").Replace("\n", "").Replace("\r", "").Trim();
                
                if (string.IsNullOrEmpty(hex))
                {
                    throw new ArgumentException("十六进制字符串为空");
                }
                
                if (hex.Length % 2 != 0)
                {
                    throw new ArgumentException($"十六进制字符串长度必须为偶数，当前长度: {hex.Length}");
                }

                var bytes = new byte[hex.Length / 2];
                for (int i = 0; i < hex.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }
                
                _logger.LogDebug("Hex decoded: chars={Chars} bytes={Bytes}", hex.Length, bytes.Length);
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hex decode failed: {Msg}", ex.Message);
                throw new ArgumentException($"十六进制转换失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 清理文本内容，去除可能导致API错误的字符
        /// </summary>
        private string CleanTextForTts(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var cleanText = text;
            
            // 替换换行符为空格
            cleanText = cleanText.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            
            // 移除多余的空格
            cleanText = Regex.Replace(cleanText, @"\s+", " ").Trim();
            
            // 限制长度（MiniMax可能有文本长度限制）
            if (cleanText.Length > 1000)
            {
                cleanText = cleanText.Substring(0, 1000);
                _logger.LogDebug("Text truncated to 1000 characters");
            }
            
            _logger.LogTrace("Original text: {Text}", text);
            _logger.LogTrace("Cleaned text: {Text}", cleanText);
            
            return cleanText;
        }

        protected override void SetupHttpHeaders(TtsConfiguration config)
        {
            base.SetupHttpHeaders(config);
            
            // 清除可能存在的旧头
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            // 不要在这里设置Content-Type，它应该由StringContent自动处理
            
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                // 从API Key中提取纯API Key部分（去除GroupId）
                var pureApiKey = ExtractPureApiKey(config.ApiKey);
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {pureApiKey}");
                _logger.LogDebug("MiniMax set Authorization: Bearer {Masked}", ApiKeyProtection.Mask(pureApiKey));
            }
            else
            {
                _logger.LogWarning("MiniMax warning: API Key is empty");
            }
            
            _logger.LogDebug("MiniMax headers configured");
        }
        
        /// <summary>
        /// 从完整的API Key字符串中提取纯API Key部分
        /// </summary>
        private string ExtractPureApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return apiKey;
                
            // 格式1: API_KEY|GROUP_ID - 取第一部分
            if (apiKey.Contains("|"))
            {
                return apiKey.Split('|')[0].Trim();
            }
            
            // 格式2: GROUP_ID:API_KEY - 取第二部分
            if (apiKey.Contains(":"))
            {
                var parts = apiKey.Split(':');
                if (parts.Length >= 2)
                {
                    return parts[1].Trim();
                }
            }
            
            // 如果没有分隔符，假设整个字符串就是API Key
            return apiKey.Trim();
        }

        public override TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            var result = base.ValidateConfiguration(configuration);

            if (result.IsValid)
            {
                // MiniMax特定验证
                if (string.IsNullOrWhiteSpace(configuration.Model))
                {
                    result.AddError("MiniMax TTS需要指定模型");
                }

                // 验证速度范围 (0.5-2.0)
                if (configuration.Speed < 0.5 || configuration.Speed > 2.0)
                {
                    result.AddError("MiniMax TTS速度范围为0.5-2.0");
                }

                // 验证API URL格式
                if (!string.IsNullOrEmpty(configuration.ApiUrl) && 
                    !configuration.ApiUrl.Contains("minimax"))
                {
                    result.AddWarning("API URL似乎不是MiniMax的地址");
                }
                
                // 验证API Key和GroupId
                if (!string.IsNullOrEmpty(configuration.ApiKey))
                {
                    if (!configuration.ApiKey.Contains("|") && !configuration.ApiKey.Contains(":"))
                    {
                        result.AddWarning("MiniMax需要GroupId。支持格式: YOUR_API_KEY|YOUR_GROUP_ID 或 YOUR_GROUP_ID:YOUR_API_KEY");
                    }
                }
            }

            return result;
        }


    }
}

