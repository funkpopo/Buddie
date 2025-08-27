using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Buddie.Database
{
    public class DbApiConfiguration : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _apiUrl = "";
        private string _apiKey = "";
        private string _modelName = "";
        private bool _isStreamingEnabled = true;
        private bool _isMultimodalEnabled = false;
        private int _channelType = 0; // ChannelType enum as int
        private bool _supportsThinking = false;
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime _updatedAt = DateTime.UtcNow;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ApiUrl
        {
            get => _apiUrl;
            set => SetProperty(ref _apiUrl, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string ModelName
        {
            get => _modelName;
            set => SetProperty(ref _modelName, value);
        }

        public bool IsStreamingEnabled
        {
            get => _isStreamingEnabled;
            set => SetProperty(ref _isStreamingEnabled, value);
        }

        public bool IsMultimodalEnabled
        {
            get => _isMultimodalEnabled;
            set => SetProperty(ref _isMultimodalEnabled, value);
        }

        public int ChannelType
        {
            get => _channelType;
            set => SetProperty(ref _channelType, value);
        }

        public bool SupportsThinking
        {
            get => _supportsThinking;
            set => SetProperty(ref _supportsThinking, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class DbTtsConfiguration : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _apiUrl = "";
        private string _apiKey = "";
        private string _model = "";
        private string _voice = "";
        private double _speed = 1.0;
        private bool _isStreamingEnabled = false;
        private bool _isActive = false;
        private int? _channelType = null; // TtsChannelType enum as nullable int for backward compatibility
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime _updatedAt = DateTime.UtcNow;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ApiUrl
        {
            get => _apiUrl;
            set => SetProperty(ref _apiUrl, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string Voice
        {
            get => _voice;
            set => SetProperty(ref _voice, value);
        }

        public double Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public bool IsStreamingEnabled
        {
            get => _isStreamingEnabled;
            set => SetProperty(ref _isStreamingEnabled, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public int? ChannelType
        {
            get => _channelType;
            set => SetProperty(ref _channelType, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class DbConversation : INotifyPropertyChanged
    {
        private int _id;
        private string _title = "";
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime _updatedAt = DateTime.UtcNow;
        private int _messageCount = 0;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        public int MessageCount
        {
            get => _messageCount;
            set => SetProperty(ref _messageCount, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class DbMessage : INotifyPropertyChanged
    {
        private int _id;
        private int _conversationId;
        private string _content = "";
        private bool _isUser = true;
        private string? _reasoningContent;
        private DateTime _createdAt = DateTime.UtcNow;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int ConversationId
        {
            get => _conversationId;
            set => SetProperty(ref _conversationId, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public bool IsUser
        {
            get => _isUser;
            set => SetProperty(ref _isUser, value);
        }

        public string? ReasoningContent
        {
            get => _reasoningContent;
            set => SetProperty(ref _reasoningContent, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class DbTtsAudio : INotifyPropertyChanged
    {
        private int _id;
        private string _textHash = "";
        private byte[] _audioData = Array.Empty<byte>();
        private string _ttsConfigJson = "";
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime _lastAccessedAt = DateTime.UtcNow;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string TextHash
        {
            get => _textHash;
            set => SetProperty(ref _textHash, value);
        }

        public byte[] AudioData
        {
            get => _audioData;
            set => SetProperty(ref _audioData, value);
        }

        public string TtsConfigJson
        {
            get => _ttsConfigJson;
            set => SetProperty(ref _ttsConfigJson, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime LastAccessedAt
        {
            get => _lastAccessedAt;
            set => SetProperty(ref _lastAccessedAt, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class DbAppSettings : INotifyPropertyChanged
    {
        private int _id;
        private bool _isTopmost = true;
        private bool _showInTaskbar = true;
        private bool _enableAnimation = true;
        private bool _isDarkTheme = false;
        private DateTime _updatedAt = DateTime.UtcNow;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public bool IsTopmost
        {
            get => _isTopmost;
            set => SetProperty(ref _isTopmost, value);
        }

        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set => SetProperty(ref _showInTaskbar, value);
        }

        public bool EnableAnimation
        {
            get => _enableAnimation;
            set => SetProperty(ref _enableAnimation, value);
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set => SetProperty(ref _isDarkTheme, value);
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set => SetProperty(ref _updatedAt, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}