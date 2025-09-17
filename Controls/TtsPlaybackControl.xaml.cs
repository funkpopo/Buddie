using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Buddie.Services.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace Buddie.Controls
{
    public partial class TtsPlaybackControl : UserControl, INotifyPropertyChanged, IDisposable
    {
        private ITtsQueueService? _ttsQueueService;
        private TtsPlaybackViewModel _viewModel;
        private DispatcherTimer _progressTimer;
        private bool _isSeekingProgress;
        private bool _disposed;

        public TtsPlaybackControl()
        {
            InitializeComponent();

            _viewModel = new TtsPlaybackViewModel();
            DataContext = _viewModel;

            // Setup progress update timer
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _progressTimer.Tick += ProgressTimer_Tick;

            // Try to get TtsQueueService from DI
            _ttsQueueService = App.Services?.GetService<ITtsQueueService>();
            if (_ttsQueueService != null)
            {
                AttachToQueueService(_ttsQueueService);
            }
        }

        public void Initialize(ITtsQueueService ttsQueueService)
        {
            _ttsQueueService = ttsQueueService;
            AttachToQueueService(ttsQueueService);
        }

        private void AttachToQueueService(ITtsQueueService queueService)
        {
            // Detach from previous service
            DetachFromQueueService();

            _ttsQueueService = queueService;

            // Attach event handlers
            queueService.ItemAdded += OnItemAdded;
            queueService.ItemStatusChanged += OnItemStatusChanged;
            queueService.ItemCompleted += OnItemCompleted;
            queueService.ItemFailed += OnItemFailed;
            queueService.PlaybackStateChanged += OnPlaybackStateChanged;
            queueService.ProgressChanged += OnProgressChanged;

            // Update initial state
            UpdateViewModel();
        }

        private void DetachFromQueueService()
        {
            if (_ttsQueueService != null)
            {
                _ttsQueueService.ItemAdded -= OnItemAdded;
                _ttsQueueService.ItemStatusChanged -= OnItemStatusChanged;
                _ttsQueueService.ItemCompleted -= OnItemCompleted;
                _ttsQueueService.ItemFailed -= OnItemFailed;
                _ttsQueueService.PlaybackStateChanged -= OnPlaybackStateChanged;
                _ttsQueueService.ProgressChanged -= OnProgressChanged;
            }
        }

        private void OnItemAdded(object? sender, TtsQueueItem e)
        {
            Dispatcher.Invoke(() => UpdateViewModel());
        }

        private void OnItemStatusChanged(object? sender, TtsQueueItem e)
        {
            Dispatcher.Invoke(() => UpdateViewModel());
        }

        private void OnItemCompleted(object? sender, TtsQueueItem e)
        {
            Dispatcher.Invoke(() => UpdateViewModel());
        }

        private void OnItemFailed(object? sender, TtsQueueItem e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateViewModel();
                // Optionally show error message
            });
        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState e)
        {
            Dispatcher.Invoke(() =>
            {
                _viewModel.PlaybackState = e;
                UpdateViewModel();

                if (e == PlaybackState.Playing)
                {
                    _progressTimer.Start();
                }
                else
                {
                    _progressTimer.Stop();
                }
            });
        }

        private void OnProgressChanged(object? sender, double e)
        {
            if (!_isSeekingProgress)
            {
                Dispatcher.Invoke(() => _viewModel.Progress = e);
            }
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (_ttsQueueService?.CurrentItem != null && !_isSeekingProgress)
            {
                _viewModel.CurrentPosition = _ttsQueueService.CurrentItem.CurrentPosition;
                _viewModel.Duration = _ttsQueueService.CurrentItem.Duration;
                _viewModel.Progress = _ttsQueueService.CurrentItem.Progress;
            }
        }

        private void UpdateViewModel()
        {
            if (_ttsQueueService == null) return;

            _viewModel.QueueCount = _ttsQueueService.QueueItems.Count;
            _viewModel.PendingCount = _ttsQueueService.QueueItems.Count(i => i.Status == TtsQueueItemStatus.Pending);

            var currentItem = _ttsQueueService.CurrentItem;
            _viewModel.HasCurrentItem = currentItem != null;
            _viewModel.CurrentItemText = currentItem?.DisplayText ?? "";
            _viewModel.CurrentPosition = currentItem?.CurrentPosition ?? TimeSpan.Zero;
            _viewModel.Duration = currentItem?.Duration ?? TimeSpan.Zero;
            _viewModel.Progress = currentItem?.Progress ?? 0;

            _viewModel.PlaybackState = _ttsQueueService.PlaybackState;
            _viewModel.Volume = _ttsQueueService.Volume;
            _viewModel.PlaybackSpeed = _ttsQueueService.PlaybackSpeed;

            // Update can states
            _viewModel.CanPlay = _ttsQueueService.QueueItems.Any(i =>
                i.Status == TtsQueueItemStatus.Synthesized ||
                i.Status == TtsQueueItemStatus.Paused);
            _viewModel.CanStop = _ttsQueueService.PlaybackState != PlaybackState.Stopped;
            _viewModel.CanSeek = currentItem != null && _ttsQueueService.PlaybackState != PlaybackState.Stopped;
            _viewModel.CanSkipNext = _ttsQueueService.QueueItems.Any(i =>
                i.Status == TtsQueueItemStatus.Synthesized && i != currentItem);
            _viewModel.CanSkipPrevious = false; // TODO: Implement previous item logic

            // Update tooltip
            _viewModel.PlayPauseTooltip = _ttsQueueService.PlaybackState == PlaybackState.Playing ? "暂停" : "播放";
        }

        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ttsQueueService == null) return;

            switch (_ttsQueueService.PlaybackState)
            {
                case PlaybackState.Playing:
                    await _ttsQueueService.PauseAsync();
                    break;
                case PlaybackState.Paused:
                    await _ttsQueueService.ResumeAsync();
                    break;
                case PlaybackState.Stopped:
                    await _ttsQueueService.PlayAsync();
                    break;
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ttsQueueService != null)
            {
                await _ttsQueueService.StopAsync();
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ttsQueueService != null)
            {
                await _ttsQueueService.SkipToNextAsync();
            }
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ttsQueueService != null)
            {
                await _ttsQueueService.SkipToPreviousAsync();
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_ttsQueueService != null)
            {
                _ttsQueueService.Volume = e.NewValue;
            }
        }

        private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ttsQueueService != null && SpeedComboBox.SelectedItem is ComboBoxItem item)
            {
                if (float.TryParse(item.Tag?.ToString(), out float speed))
                {
                    _ttsQueueService.PlaybackSpeed = speed;
                }
            }
        }

        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isSeekingProgress = true;
        }

        private async void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isSeekingProgress = false;

            if (_ttsQueueService?.CurrentItem != null)
            {
                var position = TimeSpan.FromSeconds(_viewModel.Progress * _viewModel.Duration.TotalSeconds);
                await _ttsQueueService.SeekAsync(position);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _progressTimer?.Stop();
            DetachFromQueueService();

            GC.SuppressFinalize(this);
        }
    }

    public class TtsPlaybackViewModel : INotifyPropertyChanged
    {
        private bool _hasCurrentItem;
        private string _currentItemText = "";
        private TimeSpan _currentPosition;
        private TimeSpan _duration;
        private double _progress;
        private PlaybackState _playbackState = PlaybackState.Stopped;
        private double _volume = 1.0;
        private float _playbackSpeed = 1.0f;
        private int _queueCount;
        private int _pendingCount;
        private bool _canPlay;
        private bool _canStop;
        private bool _canSeek;
        private bool _canSkipNext;
        private bool _canSkipPrevious;
        private string _playPauseTooltip = "播放";

        public bool HasCurrentItem
        {
            get => _hasCurrentItem;
            set { _hasCurrentItem = value; OnPropertyChanged(); }
        }

        public string CurrentItemText
        {
            get => _currentItemText;
            set { _currentItemText = value; OnPropertyChanged(); }
        }

        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set { _currentPosition = value; OnPropertyChanged(); }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public PlaybackState PlaybackState
        {
            get => _playbackState;
            set { _playbackState = value; OnPropertyChanged(); }
        }

        public double Volume
        {
            get => _volume;
            set { _volume = value; OnPropertyChanged(); }
        }

        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set { _playbackSpeed = value; OnPropertyChanged(); }
        }

        public int QueueCount
        {
            get => _queueCount;
            set { _queueCount = value; OnPropertyChanged(); }
        }

        public int PendingCount
        {
            get => _pendingCount;
            set { _pendingCount = value; OnPropertyChanged(); }
        }

        public bool CanPlay
        {
            get => _canPlay;
            set { _canPlay = value; OnPropertyChanged(); }
        }

        public bool CanStop
        {
            get => _canStop;
            set { _canStop = value; OnPropertyChanged(); }
        }

        public bool CanSeek
        {
            get => _canSeek;
            set { _canSeek = value; OnPropertyChanged(); }
        }

        public bool CanSkipNext
        {
            get => _canSkipNext;
            set { _canSkipNext = value; OnPropertyChanged(); }
        }

        public bool CanSkipPrevious
        {
            get => _canSkipPrevious;
            set { _canSkipPrevious = value; OnPropertyChanged(); }
        }

        public string PlayPauseTooltip
        {
            get => _playPauseTooltip;
            set { _playPauseTooltip = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PlaybackStateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlaybackState state)
            {
                return state switch
                {
                    PlaybackState.Playing => "\uE769", // Pause icon
                    PlaybackState.Paused => "\uE768",  // Play icon
                    PlaybackState.Stopped => "\uE768", // Play icon
                    _ => "\uE768"
                };
            }
            return "\uE768";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TimeSpanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan timeSpan)
            {
                if (timeSpan.TotalHours >= 1)
                {
                    return timeSpan.ToString(@"h\:mm\:ss");
                }
                return timeSpan.ToString(@"mm\:ss");
            }
            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}