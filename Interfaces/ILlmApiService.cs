using System;
using System.Threading;
using System.Threading.Tasks;
using Lyxie_desktop.Views;

namespace Lyxie_desktop.Interfaces;

/// <summary>
/// 流式数据接收回调委托
/// </summary>
/// <param name="content">接收到的内容片段</param>
/// <param name="isComplete">是否为完整消息结束</param>
public delegate void StreamingDataCallback(string content, bool isComplete);

/// <summary>
/// 流式错误回调委托
/// </summary>
/// <param name="error">错误信息</param>
public delegate void StreamingErrorCallback(string error);

public interface ILlmApiService : IDisposable
{
    /// <summary>
    /// 测试LLM API连接
    /// </summary>
    Task<(bool Success, string Message)> TestApiAsync(LlmApiConfig config);
    
    /// <summary>
    /// 发送流式消息请求
    /// </summary>
    /// <param name="config">LLM API配置</param>
    /// <param name="message">用户消息</param>
    /// <param name="onDataReceived">数据接收回调</param>
    /// <param name="onError">错误回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功启动流式请求</returns>
    Task<bool> SendStreamingMessageAsync(
        LlmApiConfig config,
        string message,
        StreamingDataCallback onDataReceived,
        StreamingErrorCallback? onError = null,
        CancellationToken cancellationToken = default);
} 