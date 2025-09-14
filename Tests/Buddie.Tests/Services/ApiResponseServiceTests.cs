using System;
using System.Text.Json;
using Xunit;
using FluentAssertions;
using Buddie.Services;

namespace Buddie.Tests.Services
{
    public class ApiResponseServiceTests
    {
        #region Non-Streaming Response Tests

        [Fact]
        public void ParseNonStreamingResponse_OpenAI_ShouldParseCorrectly()
        {
            // Arrange
            var responseText = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""Hello, how can I help you?""
                        }
                    }
                ]
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.OpenAI);

            // Assert
            result.Should().Be("Hello, how can I help you?");
        }

        [Fact]
        public void ParseNonStreamingResponse_GoogleGemini_ShouldParseCorrectly()
        {
            // Arrange
            var responseText = @"{
                ""candidates"": [
                    {
                        ""content"": {
                            ""parts"": [
                                {
                                    ""text"": ""This is a Gemini response""
                                }
                            ]
                        }
                    }
                ]
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.GoogleGemini);

            // Assert
            result.Should().Be("This is a Gemini response");
        }

        [Fact]
        public void ParseNonStreamingResponse_AnthropicClaude_ShouldParseCorrectly()
        {
            // Arrange
            var responseText = @"{
                ""content"": [
                    {
                        ""text"": ""Claude's response here""
                    }
                ]
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.AnthropicClaude);

            // Assert
            result.Should().Be("Claude's response here");
        }

        [Fact]
        public void ParseNonStreamingResponse_InvalidJson_ShouldReturnErrorMessage()
        {
            // Arrange
            var responseText = "invalid json";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.OpenAI);

            // Assert
            result.Should().StartWith("API返回了无效的响应格式");
        }

        [Fact]
        public void ParseNonStreamingResponse_OpenAI_MissingContent_ShouldReturnErrorMessage()
        {
            // Arrange
            var responseText = @"{
                ""choices"": []
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.OpenAI);

            // Assert
            result.Should().Be("API响应格式错误");
        }

        [Fact]
        public void ParseNonStreamingResponse_CustomChannel_ShouldUseOpenAIFormat()
        {
            // Arrange
            var responseText = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": ""Custom channel response""
                        }
                    }
                ]
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.Custom);

            // Assert
            result.Should().Be("Custom channel response");
        }

        #endregion

        #region Streaming Response Tests

        [Fact]
        public void ParseStreamingDelta_OpenAI_ContentOnly_ShouldParseCorrectly()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""streaming content""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be("streaming content");
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_OpenAI_ReasoningOnly_ShouldParseCorrectly()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""reasoning_content"": ""thinking process""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().Be("thinking process");
        }

        [Fact]
        public void ParseStreamingDelta_OpenAI_BothContentAndReasoning_ShouldParseCorrectly()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""main content"",
                            ""reasoning_content"": ""reasoning content""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be("main content");
            reasoning.Should().Be("reasoning content");
        }

        [Fact]
        public void ParseStreamingDelta_GoogleGemini_ShouldParseCorrectly()
        {
            // Arrange
            var jsonData = @"{
                ""candidates"": [
                    {
                        ""content"": {
                            ""parts"": [
                                {
                                    ""text"": ""Gemini streaming text""
                                }
                            ]
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.GoogleGemini);

            // Assert
            content.Should().Be("Gemini streaming text");
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_AnthropicClaude_ShouldParseCorrectly()
        {
            // Arrange
            var jsonData = @"{
                ""type"": ""content_block_delta"",
                ""delta"": {
                    ""text"": ""Claude streaming text""
                }
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.AnthropicClaude);

            // Assert
            content.Should().Be("Claude streaming text");
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Claude_WrongType_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""type"": ""other_type"",
                ""delta"": {
                    ""text"": ""This should not be parsed""
                }
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.AnthropicClaude);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_InvalidJson_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = "invalid json data";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_EmptyChoices_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": []
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_MissingDelta_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""index"": 0
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Gemini_EmptyCandidates_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""candidates"": []
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.GoogleGemini);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Gemini_EmptyParts_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""candidates"": [
                    {
                        ""content"": {
                            ""parts"": []
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.GoogleGemini);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ParseNonStreamingResponse_NullContent_ShouldReturnNoResponseMessage()
        {
            // Arrange
            var responseText = @"{
                ""choices"": [
                    {
                        ""message"": {
                            ""content"": null
                        }
                    }
                ]
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.OpenAI);

            // Assert
            result.Should().Be("无响应内容");
        }

        [Fact]
        public void ParseNonStreamingResponse_Gemini_NullText_ShouldReturnNoResponseMessage()
        {
            // Arrange
            var responseText = @"{
                ""candidates"": [
                    {
                        ""content"": {
                            ""parts"": [
                                {
                                    ""text"": null
                                }
                            ]
                        }
                    }
                ]
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.GoogleGemini);

            // Assert
            result.Should().Be("无响应内容");
        }

        [Fact]
        public void ParseNonStreamingResponse_Claude_NullText_ShouldReturnNoResponseMessage()
        {
            // Arrange
            var responseText = @"{
                ""content"": [
                    {
                        ""text"": null
                    }
                ]
            }";

            // Act
            var result = ApiResponseService.ParseNonStreamingResponse(responseText, ChannelType.AnthropicClaude);

            // Assert
            result.Should().Be("无响应内容");
        }

        [Fact]
        public void ParseStreamingDelta_CustomChannel_ShouldUseOpenAIFormat()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""custom streaming""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.Custom);

            // Assert
            content.Should().Be("custom streaming");
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_ZhipuGLM_ShouldUseOpenAIFormat()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""ZhipuGLM response""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.ZhipuGLM);

            // Assert
            content.Should().Be("ZhipuGLM response");
        }

        [Fact]
        public void ParseStreamingDelta_TongyiQwen_ShouldUseOpenAIFormat()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""Tongyi Qwen response""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.TongyiQwen);

            // Assert
            content.Should().Be("Tongyi Qwen response");
        }

        [Fact]
        public void ParseStreamingDelta_SiliconFlow_ShouldUseOpenAIFormat()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""SiliconFlow response""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.SiliconFlow);

            // Assert
            content.Should().Be("SiliconFlow response");
        }

        #endregion

        #region Additional Streaming Edge Cases

        [Fact]
        public void ParseStreamingDelta_OpenAI_WithMultipleChoices_ShouldParseFirstChoice()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""first choice""
                        }
                    },
                    {
                        ""delta"": {
                            ""content"": ""second choice""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be("first choice");
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_OpenAI_WithPartialContent_ShouldParseCorrectly()
        {
            // Arrange - Simulating partial JSON chunks
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""Hello""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be("Hello");
        }

        [Fact]
        public void ParseStreamingDelta_OpenAI_WithEmptyContent_ShouldReturnEmptyString()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": """"
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeEmpty();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_OpenAI_WithNullContent_ShouldReturnNull()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": null
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Gemini_WithMultipleParts_ShouldParseFirstPart()
        {
            // Arrange
            var jsonData = @"{
                ""candidates"": [
                    {
                        ""content"": {
                            ""parts"": [
                                {
                                    ""text"": ""First part""
                                },
                                {
                                    ""text"": ""Second part""
                                }
                            ]
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.GoogleGemini);

            // Assert
            content.Should().Be("First part");
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Gemini_WithMissingText_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""candidates"": [
                    {
                        ""content"": {
                            ""parts"": [
                                {
                                    ""other_field"": ""value""
                                }
                            ]
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.GoogleGemini);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Claude_WithContentBlockStart_ShouldReturnNulls()
        {
            // Arrange - Claude sends different event types
            var jsonData = @"{
                ""type"": ""content_block_start"",
                ""content_block"": {
                    ""type"": ""text"",
                    ""text"": """"
                }
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.AnthropicClaude);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Claude_WithContentBlockStop_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""type"": ""content_block_stop"",
                ""index"": 0
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.AnthropicClaude);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Claude_WithMissingDelta_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""type"": ""content_block_delta"",
                ""index"": 0
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.AnthropicClaude);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_Claude_WithEmptyText_ShouldReturnEmptyString()
        {
            // Arrange
            var jsonData = @"{
                ""type"": ""content_block_delta"",
                ""delta"": {
                    ""text"": """"
                }
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.AnthropicClaude);

            // Assert
            content.Should().BeEmpty();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_WithUnicodeContent_ShouldParseCorrectly()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""Hello 世界 🌍""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be("Hello 世界 🌍");
        }

        [Fact]
        public void ParseStreamingDelta_WithEscapedCharacters_ShouldParseCorrectly()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""Line 1\nLine 2\t\""Quoted\""""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be("Line 1\nLine 2\t\"Quoted\"");
        }

        [Fact]
        public void ParseStreamingDelta_WithLargeContent_ShouldParseCorrectly()
        {
            // Arrange
            var largeText = new string('a', 10000);
            var jsonData = $@"{{
                ""choices"": [
                    {{
                        ""delta"": {{
                            ""content"": ""{largeText}""
                        }}
                    }}
                ]
            }}";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be(largeText);
        }

        [Fact]
        public void ParseStreamingDelta_WithFinishReason_ShouldStillParseContent()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""Final content""
                        },
                        ""finish_reason"": ""stop""
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().Be("Final content");
        }

        [Fact]
        public void ParseStreamingDelta_WithOnlyFinishReason_ShouldReturnNulls()
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {},
                        ""finish_reason"": ""stop""
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_OpenAI_WithToolCalls_ShouldReturnNulls()
        {
            // Arrange - OpenAI function calling response
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""tool_calls"": [
                                {
                                    ""index"": 0,
                                    ""function"": {
                                        ""name"": ""get_weather"",
                                        ""arguments"": ""{\""location\"":\""Boston\""}""
                                    }
                                }
                            ]
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Fact]
        public void ParseStreamingDelta_WithMalformedButValidJson_ShouldReturnNulls()
        {
            // Arrange - Valid JSON but unexpected structure
            var jsonData = @"{
                ""unexpected"": {
                    ""structure"": {
                        ""content"": ""This should not be parsed""
                    }
                }
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, ChannelType.OpenAI);

            // Assert
            content.Should().BeNull();
            reasoning.Should().BeNull();
        }

        [Theory]
        [InlineData(ChannelType.Custom)]
        [InlineData(ChannelType.ZhipuGLM)]
        [InlineData(ChannelType.TongyiQwen)]
        [InlineData(ChannelType.SiliconFlow)]
        public void ParseStreamingDelta_AllOpenAICompatibleChannels_ShouldUseOpenAIFormat(ChannelType channelType)
        {
            // Arrange
            var jsonData = @"{
                ""choices"": [
                    {
                        ""delta"": {
                            ""content"": ""Compatible response"",
                            ""reasoning_content"": ""Compatible reasoning""
                        }
                    }
                ]
            }";

            // Act
            var (content, reasoning) = ApiResponseService.ParseStreamingDelta(jsonData, channelType);

            // Assert
            content.Should().Be("Compatible response");
            reasoning.Should().Be("Compatible reasoning");
        }

        #endregion
    }
}