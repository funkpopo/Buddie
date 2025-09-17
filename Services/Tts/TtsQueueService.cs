using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Buddie.Database;
using Buddie.Services.ExceptionHandling;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace Buddie.Services.Tts
{
    public class TtsQueueItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _text = "";
        private string _displayText = "";
        private TtsConfiguration? _configuration;
        private TtsQueueItemStatus _status = TtsQueueItemStatus.Pending;
        private byte[]? _audioData;
        private string? _contentType;
        private double _progress;
        private TimeSpan _duration;
        private TimeSpan _currentPosition;
        private string? _errorMessage;
        private DateTime _createdAt = DateTime.UtcNow;
        private DateTime? _startedAt;
        private DateTime? _completedAt;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        public string DisplayText
        {
            get => _displayText;
            set { _displayText = value; OnPropertyChanged(); }
        }

        public TtsConfiguration? Configuration
        {
            get => _configuration;
            set { _configuration = value; OnPropertyChanged(); }
        }

        public TtsQueueItemStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public byte[]? AudioData
        {
            get => _audioData;
            set { _audioData = value; OnPropertyChanged(); }
        }

        public string? ContentType
        {
            get => _contentType;
            set { _contentType = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(); }
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set { _currentPosition = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        public DateTime? StartedAt
        {
            get => _startedAt;
            set { _startedAt = value; OnPropertyChanged(); }
        }

        public DateTime? CompletedAt
        {
            get => _completedAt;
            set { _completedAt = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum TtsQueueItemStatus
    {
        Pending,        // 等待合成
        Synthesizing,   // 正在合成
        Synthesized,    // 合成完成，等待播放
        Playing,        // 正在播放
        Paused,         // 已暂停
        Completed,      // 已完成
        Failed,         // 失败
        Cancelled       // 已取消
    }

    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    public interface ITtsQueueService : IDisposable
    {
        event EventHandler<TtsQueueItem>? ItemAdded;
        event EventHandler<TtsQueueItem>? ItemStatusChanged;
        event EventHandler<TtsQueueItem>? ItemCompleted;
        event EventHandler<TtsQueueItem>? ItemFailed;
        event EventHandler<PlaybackState>? PlaybackStateChanged;
        event EventHandler<double>? ProgressChanged;

        IReadOnlyList<TtsQueueItem> QueueItems { get; }
        TtsQueueItem? CurrentItem { get; }
        PlaybackState PlaybackState { get; }
        bool IsProcessing { get; }
        double Volume { get; set; }
        float PlaybackSpeed { get; set; }

        Task<TtsQueueItem> EnqueueAsync(string text, TtsConfiguration configuration, bool autoPlay = true);
        Task<List<TtsQueueItem>> EnqueueBatchAsync(IEnumerable<string> texts, TtsConfiguration configuration, bool autoPlay = true);
        Task PlayAsync(TtsQueueItem? item = null);
        Task PauseAsync();
        Task ResumeAsync();
        Task StopAsync();
        Task SkipToNextAsync();
        Task SkipToPreviousAsync();
        Task SeekAsync(TimeSpan position);
        Task ClearQueueAsync();
        Task RemoveItemAsync(string itemId);
        Task<bool> MoveItemAsync(string itemId, int newIndex);
        Task PreloadNextAsync(int count = 2);
    }

    public class TtsQueueService : ITtsQueueService
    {
        private readonly ITtsServiceResolver _ttsServiceResolver;
        private readonly IAudioPlaybackService _audioPlaybackService;
        private readonly DatabaseService _databaseService;
        private readonly ILogger<TtsQueueService> _logger;
        private readonly List<TtsQueueItem> _queue = new();
        private readonly SemaphoreSlim _queueSemaphore = new(1, 1);
        private readonly SemaphoreSlim _playbackSemaphore = new(1, 1);
        private CancellationTokenSource? _processingCts;
        private CancellationTokenSource? _playbackCts;
        private Task? _processingTask;
        private Timer? _progressTimer;
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioReader;
        private TtsQueueItem? _currentItem;
        private PlaybackState _playbackState = PlaybackState.Stopped;
        private bool _autoPlay = true;
        private float _volume = 1.0f;
        private float _playbackSpeed = 1.0f;
        private bool _disposed;

        public event EventHandler<TtsQueueItem>? ItemAdded;
        public event EventHandler<TtsQueueItem>? ItemStatusChanged;
        public event EventHandler<TtsQueueItem>? ItemCompleted;
        public event EventHandler<TtsQueueItem>? ItemFailed;
        public event EventHandler<PlaybackState>? PlaybackStateChanged;
        public event EventHandler<double>? ProgressChanged;

        public IReadOnlyList<TtsQueueItem> QueueItems => _queue.AsReadOnly();
        public TtsQueueItem? CurrentItem => _currentItem;
        public PlaybackState PlaybackState => _playbackState;
        public bool IsProcessing => _processingTask != null && !_processingTask.IsCompleted;

        public double Volume
        {
            get => _volume;
            set
            {
                _volume = (float)Math.Max(0, Math.Min(1, value));
                if (_waveOut != null)
                {
                    _waveOut.Volume = _volume;
                }
            }
        }

        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                _playbackSpeed = Math.Max(0.5f, Math.Min(2.0f, value));
                // Note: Changing playback speed during playback requires more complex audio processing
            }
        }

        public TtsQueueService(
            ITtsServiceResolver ttsServiceResolver,
            IAudioPlaybackService audioPlaybackService,
            DatabaseService databaseService,
            ILogger<TtsQueueService> logger)
        {
            _ttsServiceResolver = ttsServiceResolver;
            _audioPlaybackService = audioPlaybackService;
            _databaseService = databaseService;
            _logger = logger;
        }

        public async Task<TtsQueueItem> EnqueueAsync(string text, TtsConfiguration configuration, bool autoPlay = true)
        {
            ThrowIfDisposed();

            var item = new TtsQueueItem
            {
                Text = text,
                DisplayText = text.Length > 100 ? text.Substring(0, 100) + "..." : text,
                Configuration = configuration,
                Status = TtsQueueItemStatus.Pending
            };

            await _queueSemaphore.WaitAsync();
            try
            {
                _queue.Add(item);
                _autoPlay = autoPlay;
                ItemAdded?.Invoke(this, item);
                _logger.LogInformation("Enqueued TTS item {Id} with text length {Length}", item.Id, text.Length);
            }
            finally
            {
                _queueSemaphore.Release();
            }

            // Start processing if not already running
            if (!IsProcessing)
            {
                StartProcessing();
            }

            return item;
        }

        public async Task<List<TtsQueueItem>> EnqueueBatchAsync(IEnumerable<string> texts, TtsConfiguration configuration, bool autoPlay = true)
        {
            ThrowIfDisposed();

            var items = new List<TtsQueueItem>();

            await _queueSemaphore.WaitAsync();
            try
            {
                foreach (var text in texts)
                {
                    var item = new TtsQueueItem
                    {
                        Text = text,
                        DisplayText = text.Length > 100 ? text.Substring(0, 100) + "..." : text,
                        Configuration = configuration,
                        Status = TtsQueueItemStatus.Pending
                    };

                    _queue.Add(item);
                    items.Add(item);
                    ItemAdded?.Invoke(this, item);
                }

                _autoPlay = autoPlay;
                _logger.LogInformation("Enqueued {Count} TTS items in batch", items.Count);
            }
            finally
            {
                _queueSemaphore.Release();
            }

            // Start processing if not already running
            if (!IsProcessing)
            {
                StartProcessing();
            }

            // Start preloading in background
            _ = PreloadNextAsync(Math.Min(3, items.Count));

            return items;
        }

        private void StartProcessing()
        {
            _processingCts?.Cancel();
            _processingCts = new CancellationTokenSource();
            _processingTask = ProcessQueueAsync(_processingCts.Token);
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started TTS queue processing");

            while (!cancellationToken.IsCancellationRequested)
            {
                TtsQueueItem? itemToProcess = null;

                await _queueSemaphore.WaitAsync(cancellationToken);
                try
                {
                    itemToProcess = _queue.FirstOrDefault(q => q.Status == TtsQueueItemStatus.Pending);
                }
                finally
                {
                    _queueSemaphore.Release();
                }

                if (itemToProcess == null)
                {
                    // No more pending items, wait a bit and check again
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                await SynthesizeItemAsync(itemToProcess, cancellationToken);

                // If auto-play is enabled and nothing is playing, start playback
                if (_autoPlay && _playbackState == PlaybackState.Stopped)
                {
                    await PlayNextAsync();
                }
            }

            _logger.LogInformation("Stopped TTS queue processing");
        }

        private async Task SynthesizeItemAsync(TtsQueueItem item, CancellationToken cancellationToken)
        {
            try
            {
                UpdateItemStatus(item, TtsQueueItemStatus.Synthesizing);
                item.StartedAt = DateTime.UtcNow;

                // Check cache first
                var textHash = ComputeTextHash(item.Text + JsonSerializer.Serialize(item.Configuration));
                var cachedAudio = await _databaseService.GetTtsAudioAsync(textHash);

                if (cachedAudio != null)
                {
                    _logger.LogInformation("Found cached audio for item {Id}", item.Id);
                    item.AudioData = cachedAudio.AudioData;
                    item.ContentType = "audio/mpeg"; // Default, should be stored in cache
                    UpdateItemStatus(item, TtsQueueItemStatus.Synthesized);
                    item.CompletedAt = DateTime.UtcNow;
                    return;
                }

                // Synthesize using TTS service
                var ttsService = _ttsServiceResolver.Create(item.Configuration!.ChannelType);
                var request = new TtsRequest
                {
                    Text = item.Text,
                    Configuration = item.Configuration!
                };

                var response = await ttsService.ConvertTextToSpeechAsync(request, cancellationToken);

                if (response.IsSuccess && response.AudioData != null)
                {
                    item.AudioData = response.AudioData;
                    item.ContentType = response.ContentType;

                    // Save to cache
                    await _databaseService.SaveTtsAudioAsync(textHash, response.AudioData, JsonSerializer.Serialize(item.Configuration));

                    UpdateItemStatus(item, TtsQueueItemStatus.Synthesized);
                    item.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("Successfully synthesized item {Id}", item.Id);
                }
                else
                {
                    throw new TtsException(item.Configuration.ChannelType, response.ErrorMessage ?? "Unknown error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to synthesize item {Id}", item.Id);
                item.ErrorMessage = ex.Message;
                UpdateItemStatus(item, TtsQueueItemStatus.Failed);
                item.CompletedAt = DateTime.UtcNow;
                ItemFailed?.Invoke(this, item);
            }
        }

        public async Task PlayAsync(TtsQueueItem? item = null)
        {
            ThrowIfDisposed();

            await _playbackSemaphore.WaitAsync();
            try
            {
                if (item != null)
                {
                    // Play specific item
                    _currentItem = item;
                }
                else if (_currentItem == null)
                {
                    // Find next synthesized item to play
                    await _queueSemaphore.WaitAsync();
                    try
                    {
                        _currentItem = _queue.FirstOrDefault(q => q.Status == TtsQueueItemStatus.Synthesized);
                    }
                    finally
                    {
                        _queueSemaphore.Release();
                    }
                }

                if (_currentItem?.AudioData != null)
                {
                    await PlayItemAsync(_currentItem);
                }
            }
            finally
            {
                _playbackSemaphore.Release();
            }
        }

        private async Task PlayItemAsync(TtsQueueItem item)
        {
            try
            {
                _playbackCts?.Cancel();
                _playbackCts = new CancellationTokenSource();

                UpdateItemStatus(item, TtsQueueItemStatus.Playing);
                UpdatePlaybackState(PlaybackState.Playing);

                // Use the audio playback service
                await _audioPlaybackService.PlayAsync(item.AudioData!, item.ContentType, _playbackCts.Token);

                UpdateItemStatus(item, TtsQueueItemStatus.Completed);
                ItemCompleted?.Invoke(this, item);

                // Play next item if available
                await PlayNextAsync();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Playback cancelled for item {Id}", item.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to play item {Id}", item.Id);
                item.ErrorMessage = ex.Message;
                UpdateItemStatus(item, TtsQueueItemStatus.Failed);
                ItemFailed?.Invoke(this, item);
            }
            finally
            {
                UpdatePlaybackState(PlaybackState.Stopped);
            }
        }

        private async Task PlayNextAsync()
        {
            await _queueSemaphore.WaitAsync();
            try
            {
                var nextItem = _queue.FirstOrDefault(q => q.Status == TtsQueueItemStatus.Synthesized);
                if (nextItem != null)
                {
                    _currentItem = nextItem;
                    _ = Task.Run(() => PlayItemAsync(nextItem));
                }
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        public async Task PauseAsync()
        {
            ThrowIfDisposed();

            if (_playbackState == PlaybackState.Playing)
            {
                _waveOut?.Pause();
                UpdatePlaybackState(PlaybackState.Paused);
                if (_currentItem != null)
                {
                    UpdateItemStatus(_currentItem, TtsQueueItemStatus.Paused);
                }
            }

            await Task.CompletedTask;
        }

        public async Task ResumeAsync()
        {
            ThrowIfDisposed();

            if (_playbackState == PlaybackState.Paused)
            {
                _waveOut?.Play();
                UpdatePlaybackState(PlaybackState.Playing);
                if (_currentItem != null)
                {
                    UpdateItemStatus(_currentItem, TtsQueueItemStatus.Playing);
                }
            }

            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            ThrowIfDisposed();

            _playbackCts?.Cancel();
            await _audioPlaybackService.StopAsync();

            _waveOut?.Stop();
            _audioReader?.Dispose();
            _audioReader = null;

            UpdatePlaybackState(PlaybackState.Stopped);

            if (_currentItem != null)
            {
                UpdateItemStatus(_currentItem, TtsQueueItemStatus.Synthesized);
                _currentItem = null;
            }
        }

        public async Task SkipToNextAsync()
        {
            ThrowIfDisposed();

            await StopAsync();
            await PlayNextAsync();
        }

        public async Task SkipToPreviousAsync()
        {
            ThrowIfDisposed();

            await _queueSemaphore.WaitAsync();
            try
            {
                if (_currentItem != null)
                {
                    var currentIndex = _queue.IndexOf(_currentItem);
                    if (currentIndex > 0)
                    {
                        var previousItem = _queue[currentIndex - 1];
                        if (previousItem.Status == TtsQueueItemStatus.Completed ||
                            previousItem.Status == TtsQueueItemStatus.Synthesized)
                        {
                            await StopAsync();
                            _currentItem = previousItem;
                            await PlayItemAsync(previousItem);
                        }
                    }
                }
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        public async Task SeekAsync(TimeSpan position)
        {
            ThrowIfDisposed();

            if (_audioReader != null && _waveOut != null)
            {
                _audioReader.CurrentTime = position;
                if (_currentItem != null)
                {
                    _currentItem.CurrentPosition = position;
                }
            }

            await Task.CompletedTask;
        }

        public async Task ClearQueueAsync()
        {
            ThrowIfDisposed();

            await StopAsync();

            await _queueSemaphore.WaitAsync();
            try
            {
                foreach (var item in _queue.Where(q => q.Status == TtsQueueItemStatus.Pending ||
                                                       q.Status == TtsQueueItemStatus.Synthesizing))
                {
                    UpdateItemStatus(item, TtsQueueItemStatus.Cancelled);
                }
                _queue.Clear();
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        public async Task RemoveItemAsync(string itemId)
        {
            ThrowIfDisposed();

            await _queueSemaphore.WaitAsync();
            try
            {
                var item = _queue.FirstOrDefault(q => q.Id == itemId);
                if (item != null)
                {
                    if (item == _currentItem)
                    {
                        await StopAsync();
                    }

                    _queue.Remove(item);
                    UpdateItemStatus(item, TtsQueueItemStatus.Cancelled);
                }
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        public async Task<bool> MoveItemAsync(string itemId, int newIndex)
        {
            ThrowIfDisposed();

            await _queueSemaphore.WaitAsync();
            try
            {
                var item = _queue.FirstOrDefault(q => q.Id == itemId);
                if (item != null && newIndex >= 0 && newIndex < _queue.Count)
                {
                    _queue.Remove(item);
                    _queue.Insert(newIndex, item);
                    return true;
                }
                return false;
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        public async Task PreloadNextAsync(int count = 2)
        {
            ThrowIfDisposed();

            var itemsToPreload = new List<TtsQueueItem>();

            await _queueSemaphore.WaitAsync();
            try
            {
                itemsToPreload = _queue
                    .Where(q => q.Status == TtsQueueItemStatus.Pending)
                    .Take(count)
                    .ToList();
            }
            finally
            {
                _queueSemaphore.Release();
            }

            // Synthesize items in parallel
            var tasks = itemsToPreload.Select(item => SynthesizeItemAsync(item, CancellationToken.None));
            await Task.WhenAll(tasks);
        }

        private void UpdateItemStatus(TtsQueueItem item, TtsQueueItemStatus status)
        {
            item.Status = status;
            ItemStatusChanged?.Invoke(this, item);
        }

        private void UpdatePlaybackState(PlaybackState state)
        {
            _playbackState = state;
            PlaybackStateChanged?.Invoke(this, state);
        }

        private void UpdateProgress(object? state)
        {
            if (_audioReader != null && _currentItem != null)
            {
                _currentItem.CurrentPosition = _audioReader.CurrentTime;
                _currentItem.Duration = _audioReader.TotalTime;
                _currentItem.Progress = _audioReader.TotalTime.TotalSeconds > 0
                    ? _audioReader.CurrentTime.TotalSeconds / _audioReader.TotalTime.TotalSeconds
                    : 0;

                ProgressChanged?.Invoke(this, _currentItem.Progress);
            }
        }

        private static string ComputeTextHash(string text)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TtsQueueService));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _processingCts?.Cancel();
            _playbackCts?.Cancel();
            _progressTimer?.Dispose();
            _waveOut?.Dispose();
            _audioReader?.Dispose();
            _queueSemaphore?.Dispose();
            _playbackSemaphore?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}