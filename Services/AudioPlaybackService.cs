using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;
using Buddie.Services.Tts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace Buddie.Services
{
    public class AudioPlaybackService : IAudioPlaybackService
    {
        private readonly ILogger _logger;
        private readonly object _sync = new();
        private WaveOutEvent? _player;
        private AudioFileReader? _reader;
        private string? _tempFile;
        private CancellationTokenSource? _playbackCts;
        private bool _disposed;

        public AudioPlaybackService(ILoggerFactory? loggerFactory = null)
        {
            _logger = (loggerFactory?.CreateLogger<AudioPlaybackService>()) ?? NullLogger.Instance;
            ExceptionHandlingService.ExecuteSafely(() => MediaFoundationApi.Startup(),
                ExceptionHandlingService.HandlingStrategy.LogOnly,
                new ExceptionHandlingService.ExceptionContext { Component = nameof(AudioPlaybackService), Operation = "NAudio Startup" });
        }

        public async Task PlayAsync(byte[] audioBytes, string? contentType = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Ensure single playback at a time
            await StopAsync();

            // Create temp file in LocalAppData\Buddie
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buddie");
            Directory.CreateDirectory(appDataPath);
            var ext = TtsServiceBase.GetAudioExtension(contentType, audioBytes);
            var tempFile = Path.Combine(appDataPath, $"audio_{Guid.NewGuid()}{ext}");
            await File.WriteAllBytesAsync(tempFile, audioBytes, cancellationToken);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_sync)
            {
                _playbackCts = linkedCts;
                _tempFile = tempFile;
            }

            try
            {
                lock (_sync)
                {
                    _reader = new AudioFileReader(tempFile);
                    _player = new WaveOutEvent();
                    _player.PlaybackStopped += async (_, e) =>
                    {
                        await ExceptionHandlingService.ExecuteSafelyAsync(async () =>
                        {
                            if (e.Exception != null)
                            {
                                _logger.LogWarning(e.Exception, "Playback stopped with error");
                            }
                            await CleanupAsync();
                        }, ExceptionHandlingService.HandlingStrategy.LogOnly, new ExceptionHandlingService.ExceptionContext
                        {
                            Component = nameof(AudioPlaybackService),
                            Operation = "PlaybackStopped cleanup"
                        });
                    };
                    _player.Init(_reader);
                    _player.Play();
                }

                // Poll playback state, support cancellation
                while (true)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    lock (_sync)
                    {
                        if (_player == null || _player.PlaybackState != PlaybackState.Playing)
                            break;
                    }
                    await Task.Delay(100, linkedCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                await StopAsync();
                throw;
            }
            catch
            {
                // Fallback: try to cleanup on failure
                await CleanupAsync();
                throw;
            }
        }

        public async Task StopAsync()
        {
            ThrowIfDisposed();

            CancellationTokenSource? cts;
            lock (_sync)
            {
                cts = _playbackCts;
                _playbackCts = null;
            }
            try { cts?.Cancel(); } catch { }

            await CleanupAsync();
        }

        public void Stop()
        {
            ThrowIfDisposed();
            try
            {
                _playbackCts?.Cancel();
            }
            catch { }
            finally
            {
                ExceptionHandlingService.ExecuteSafely(() =>
                {
                    lock (_sync)
                    {
                        _player?.Stop();
                        _player?.Dispose();
                        _player = null;
                        _reader?.Dispose();
                        _reader = null;
                        if (!string.IsNullOrEmpty(_tempFile) && File.Exists(_tempFile))
                        {
                            try { File.Delete(_tempFile); } catch { }
                            _tempFile = null;
                        }
                    }
                }, ExceptionHandlingService.HandlingStrategy.LogOnly,
                new ExceptionHandlingService.ExceptionContext { Component = nameof(AudioPlaybackService), Operation = "Stop cleanup" });
            }
        }

        private async Task CleanupAsync()
        {
            await Task.Yield();
            lock (_sync)
            {
                try { _player?.Stop(); } catch { }
                try { _player?.Dispose(); } catch { }
                _player = null;
                try { _reader?.Dispose(); } catch { }
                _reader = null;
                if (!string.IsNullOrEmpty(_tempFile) && File.Exists(_tempFile))
                {
                    try { File.Delete(_tempFile); } catch { }
                    _tempFile = null;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _playbackCts?.Cancel(); } catch { }
            Stop();
            ExceptionHandlingService.ExecuteSafely(() => MediaFoundationApi.Shutdown(),
                ExceptionHandlingService.HandlingStrategy.LogOnly,
                new ExceptionHandlingService.ExceptionContext { Component = nameof(AudioPlaybackService), Operation = "NAudio Shutdown" });
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioPlaybackService));
        }
    }
}

