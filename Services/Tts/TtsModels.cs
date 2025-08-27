using System;
using System.Collections.Generic;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// TTS请求模型
    /// </summary>
    public class TtsRequest
    {
        public string Text { get; set; } = "";
        public TtsConfiguration Configuration { get; set; } = new TtsConfiguration();
        public Dictionary<string, object> AdditionalParameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// TTS响应模型
    /// </summary>
    public class TtsResponse
    {
        public byte[] AudioData { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "audio/wav";
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorDetails { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// TTS配置验证结果
    /// </summary>
    public class TtsValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public List<string> WarningMessages { get; set; } = new List<string>();

        public static TtsValidationResult Success()
        {
            return new TtsValidationResult { IsValid = true };
        }

        public static TtsValidationResult Failure(params string[] errors)
        {
            return new TtsValidationResult 
            { 
                IsValid = false, 
                ErrorMessages = new List<string>(errors) 
            };
        }

        public void AddError(string message)
        {
            ErrorMessages.Add(message);
            IsValid = false;
        }

        public void AddWarning(string message)
        {
            WarningMessages.Add(message);
        }
    }

    /// <summary>
    /// TTS异常类
    /// </summary>
    public class TtsException : Exception
    {
        public TtsChannelType ChannelType { get; }
        public string? ErrorDetails { get; }

        public TtsException(TtsChannelType channelType, string message) : base(message)
        {
            ChannelType = channelType;
        }

        public TtsException(TtsChannelType channelType, string message, string errorDetails) : base(message)
        {
            ChannelType = channelType;
            ErrorDetails = errorDetails;
        }

        public TtsException(TtsChannelType channelType, string message, Exception innerException) : base(message, innerException)
        {
            ChannelType = channelType;
        }
    }
}

