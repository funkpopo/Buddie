using System;
using System.Threading;
using System.Threading.Tasks;

namespace Buddie.Services
{
    public interface IAudioPlaybackService : IDisposable
    {
        Task PlayAsync(byte[] audioBytes, string? contentType = null, CancellationToken cancellationToken = default);
        Task StopAsync();
        void Stop();
    }
}

