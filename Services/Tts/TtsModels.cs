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
        
        /// <summary>
        /// 是否启用流式输出 (MiniMax不支持流式，此属性已废弃)
        /// </summary>
        [Obsolete("MiniMax TTS不支持流式输出，此属性将始终返回false")]
        public bool IsStreamingEnabled
        {
            get => false; // MiniMax强制非流式
            set => AdditionalParameters["stream"] = false; // 强制设为false
        }
        
        /// <summary>
        /// 音频格式 (wav, mp3, flac, aac等) - MiniMax强制使用WAV格式
        /// </summary>
        public string AudioFormat
        {
            get => AdditionalParameters.TryGetValue("audio_format", out var value) && value is string format ? format : "wav";
            set => AdditionalParameters["audio_format"] = value;
        }
        
        /// <summary>
        /// 采样率 (8000, 16000, 24000, 32000, 48000)
        /// </summary>
        public int SampleRate
        {
            get => AdditionalParameters.TryGetValue("sample_rate", out var value) && value is int rate ? rate : 32000;
            set => AdditionalParameters["sample_rate"] = value;
        }
        
        /// <summary>
        /// 比特率 (64, 128, 192, 256, 320)
        /// </summary>
        public int Bitrate
        {
            get => AdditionalParameters.TryGetValue("bitrate", out var value) && value is int bitrate ? bitrate : 128;
            set => AdditionalParameters["bitrate"] = value;
        }
        
        /// <summary>
        /// 音量 (0.1-3.0，默认1.0)
        /// </summary>
        public double Volume
        {
            get => AdditionalParameters.TryGetValue("volume", out var value) && value is double volume ? volume : 1.0;
            set => AdditionalParameters["volume"] = Math.Max(0.1, Math.Min(3.0, value));
        }
        
        /// <summary>
        /// 音调 (-12到12，默认0)
        /// </summary>
        public int Pitch
        {
            get => AdditionalParameters.TryGetValue("pitch", out var value) && value is int pitch ? pitch : 0;
            set => AdditionalParameters["pitch"] = Math.Max(-12, Math.Min(12, value));
        }
        
        /// <summary>
        /// 情感设置 (neutral, happy, sad, angry, excited等)
        /// </summary>
        public string Emotion
        {
            get => AdditionalParameters.TryGetValue("emotion", out var value) && value is string emotion ? emotion : "";
            set => AdditionalParameters["emotion"] = value ?? "";
        }
        
        /// <summary>
        /// 语言设置 (auto, zh, en, jp等)
        /// </summary>
        public string Language
        {
            get => AdditionalParameters.TryGetValue("language", out var value) && value is string language ? language : "";
            set => AdditionalParameters["language"] = value ?? "";
        }
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

