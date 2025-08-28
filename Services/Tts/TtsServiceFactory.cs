using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Buddie.Services.Tts
{
    /// <summary>
    /// TTS服务工厂
    /// </summary>
    public static class TtsServiceFactory
    {
        private static readonly Dictionary<TtsChannelType, Func<ITtsService>> _serviceFactories 
            = new Dictionary<TtsChannelType, Func<ITtsService>>
            {
                { TtsChannelType.OpenAI, () => new OpenAiTtsService() },
                { TtsChannelType.ElevenLabs, () => new ElevenLabsTtsService() },
                { TtsChannelType.MiniMax, () => new MiniMaxTtsService() }
            };

        /// <summary>
        /// 创建TTS服务实例
        /// </summary>
        /// <param name="channelType">TTS渠道类型</param>
        /// <returns>TTS服务实例</returns>
        /// <exception cref="NotSupportedException">不支持的渠道类型</exception>
        public static ITtsService CreateService(TtsChannelType channelType)
        {
            Debug.WriteLine($"TTS Factory: 创建 {channelType} 服务");

            if (_serviceFactories.TryGetValue(channelType, out var factory))
            {
                return factory();
            }

            throw new NotSupportedException($"不支持的TTS渠道类型: {channelType}");
        }

        /// <summary>
        /// 检查是否支持指定的渠道类型
        /// </summary>
        /// <param name="channelType">TTS渠道类型</param>
        /// <returns>是否支持</returns>
        public static bool IsSupported(TtsChannelType channelType)
        {
            return _serviceFactories.ContainsKey(channelType);
        }

        /// <summary>
        /// 获取所有支持的渠道类型
        /// </summary>
        /// <returns>支持的渠道类型列表</returns>
        public static TtsChannelType[] GetSupportedChannelTypes()
        {
            var result = new TtsChannelType[_serviceFactories.Count];
            var index = 0;
            
            foreach (var channelType in _serviceFactories.Keys)
            {
                result[index++] = channelType;
            }
            
            return result;
        }

        /// <summary>
        /// 验证TTS配置
        /// </summary>
        /// <param name="configuration">TTS配置</param>
        /// <returns>验证结果</returns>
        public static TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            if (configuration == null)
            {
                return TtsValidationResult.Failure("TTS配置不能为空");
            }

            try
            {
                var service = CreateService(configuration.ChannelType);
                return service.ValidateConfiguration(configuration);
            }
            catch (NotSupportedException ex)
            {
                return TtsValidationResult.Failure(ex.Message);
            }
        }
    }
}

