using System;

namespace Buddie.Services
{
    public class VadRealtimeEventModels
    {
        public class RealtimeEvent
        {
            public string Type { get; set; } = "";
            public string EventId { get; set; } = "";
        }

        public class SessionUpdateEvent : RealtimeEvent
        {
            public SessionConfig Session { get; set; } = new();
        }

        public class SessionConfig
        {
            public string[] Modalities { get; set; } = { "text", "audio" };
            public string Model { get; set; } = "";
            public string Voice { get; set; } = "";
            public TurnDetectionConfig TurnDetection { get; set; } = new();
            public string InputAudioFormat { get; set; } = "pcm16";
            public string OutputAudioFormat { get; set; } = "pcm16";
        }

        public class TurnDetectionConfig
        {
            public string Type { get; set; } = "server_vad";
            public double? Threshold { get; set; }
            public int? PrefixPaddingMs { get; set; }
            public int? SilenceDurationMs { get; set; }
        }

        public class InputAudioBufferAppendEvent : RealtimeEvent
        {
            public string Audio { get; set; } = "";
        }

        public class ResponseCreateEvent : RealtimeEvent
        {
            public ResponseConfig? Response { get; set; }
        }

        public class ResponseConfig
        {
            public string[]? Modalities { get; set; }
            public string? Instructions { get; set; }
            public string? Voice { get; set; }
            public double? Temperature { get; set; }
            public int? MaxTokens { get; set; }
        }

        public class ConversationItemCreateEvent : RealtimeEvent
        {
            public ConversationItem Item { get; set; } = new();
        }

        public class ConversationItem
        {
            public string Type { get; set; } = "message";
            public string Role { get; set; } = "user";
            public MessageContent[] Content { get; set; } = Array.Empty<MessageContent>();
        }

        public class MessageContent
        {
            public string Type { get; set; } = "";
            public string? Text { get; set; }
            public string? Audio { get; set; }
        }

        public class ResponseTextDeltaEvent : RealtimeEvent
        {
            public string ResponseId { get; set; } = "";
            public string ItemId { get; set; } = "";
            public int OutputIndex { get; set; }
            public int ContentIndex { get; set; }
            public string Delta { get; set; } = "";
        }

        public class ResponseAudioDeltaEvent : RealtimeEvent
        {
            public string ResponseId { get; set; } = "";
            public string ItemId { get; set; } = "";
            public int OutputIndex { get; set; }
            public int ContentIndex { get; set; }
            public string Delta { get; set; } = "";
        }

        public class InputAudioBufferSpeechStartedEvent : RealtimeEvent
        {
            public string AudioStartMs { get; set; } = "";
            public string ItemId { get; set; } = "";
        }

        public class InputAudioBufferSpeechStoppedEvent : RealtimeEvent
        {
            public string AudioEndMs { get; set; } = "";
            public string ItemId { get; set; } = "";
        }

        public class ErrorEvent : RealtimeEvent
        {
            public ErrorDetails Error { get; set; } = new();
        }

        public class ErrorDetails
        {
            public string Message { get; set; } = "";
            public string Code { get; set; } = "";
            public object? Param { get; set; }
        }
    }
}