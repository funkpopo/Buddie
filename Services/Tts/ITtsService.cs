using System;
using System.Threading.Tasks;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// TTS服务接口，定义统一的文本转语音功能
    /// </summary>
    public interface ITtsService : IDisposable
    {
        /// <summary>
        /// 将文本转换为音频数据
        /// </summary>
        /// <param name="request">TTS请求</param>
        /// <returns>音频数据</returns>
        Task<TtsResponse> ConvertTextToSpeechAsync(TtsRequest request);

        /// <summary>
        /// 验证TTS配置是否有效
        /// </summary>
        /// <param name="configuration">TTS配置</param>
        /// <returns>验证结果</returns>
        TtsValidationResult ValidateConfiguration(TtsConfiguration configuration);

        /// <summary>
        /// 支持的TTS渠道类型
        /// </summary>
        TtsChannelType SupportedChannelType { get; }
    }
}

