using System;
using System.Threading.Tasks;
using Lyxie_desktop.Views;

namespace Lyxie_desktop.Interfaces;

public interface ILlmApiService : IDisposable
{
    Task<(bool Success, string Message)> TestApiAsync(LlmApiConfig config);
} 