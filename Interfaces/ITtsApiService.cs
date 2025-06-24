using System;
using System.Threading;
using System.Threading.Tasks;
using Lyxie_desktop.Models;

namespace Lyxie_desktop.Interfaces
{
    /// <summary>
    /// TTS播放状态变化回调委托
    /// </summary>
    /// <param name="state">播放状态</param>
    /// <param name="message">状态消息</param>
    public delegate void TtsStateChangedCallback(TtsPlaybackState state, string message = "");

    /// <summary>
    /// TTS错误回调委托
    /// </summary>
    /// <param name="error">错误信息</param>
    public delegate void TtsErrorCallback(string error);

    /// <summary>
    /// TTS API服务接口
    /// </summary>
    public interface ITtsApiService : IDisposable
    {
        /// <summary>
        /// 当前播放状态
        /// </summary>
        TtsPlaybackState CurrentState { get; }

        /// <summary>
        /// 播放状态变化事件
        /// </summary>
        event TtsStateChangedCallback? StateChanged;

        /// <summary>
        /// 错误事件
        /// </summary>
        event TtsErrorCallback? ErrorOccurred;

        /// <summary>
        /// 测试TTS API连接
        /// </summary>
        /// <param name="config">TTS API配置</param>
        /// <returns>测试结果和消息</returns>
        Task<(bool Success, string Message)> TestApiAsync(TtsApiConfig config);

        /// <summary>
        /// 合成语音并播放
        /// </summary>
        /// <param name="config">TTS API配置</param>
        /// <param name="text">要转换的文本</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>成功则返回缓存文件路径，否则返回null</returns>
        Task<string?> SpeakAsync(TtsApiConfig config, string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从缓存文件播放语音
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="config">TTS API配置（用于确定解码格式）</param>
        Task PlayFromFileAsync(string filePath, TtsApiConfig config);

        /// <summary>
        /// 暂停播放
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复播放
        /// </summary>
        void Resume();

        /// <summary>
        /// 停止播放
        /// </summary>
        void Stop();

        /// <summary>
        /// 设置音量
        /// </summary>
        /// <param name="volume">音量 (0.0 - 1.0)</param>
        void SetVolume(float volume);

        /// <summary>
        /// 获取当前音量
        /// </summary>
        float GetVolume();

        /// <summary>
        /// 是否正在播放
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 是否已暂停
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 清理当前播放队列
        /// </summary>
        void ClearQueue();
    }
} 