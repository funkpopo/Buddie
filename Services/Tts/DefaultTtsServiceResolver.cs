using System;
using Microsoft.Extensions.DependencyInjection;

namespace Buddie.Services.Tts
{
    public class DefaultTtsServiceResolver : ITtsServiceResolver
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultTtsServiceResolver(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITtsService Create(TtsChannelType channelType)
        {
            return channelType switch
            {
                TtsChannelType.OpenAI => _serviceProvider.GetRequiredService<OpenAiTtsService>(),
                TtsChannelType.ElevenLabs => _serviceProvider.GetRequiredService<ElevenLabsTtsService>(),
                TtsChannelType.MiniMax => _serviceProvider.GetRequiredService<MiniMaxTtsService>(),
                _ => throw new NotSupportedException($"不支持的TTS渠道类型: {channelType}")
            };
        }

        public bool IsSupported(TtsChannelType channelType)
        {
            return channelType == TtsChannelType.OpenAI
                || channelType == TtsChannelType.ElevenLabs
                || channelType == TtsChannelType.MiniMax;
        }

        public TtsValidationResult ValidateConfiguration(TtsConfiguration configuration)
        {
            if (configuration == null)
            {
                return TtsValidationResult.Failure("TTS配置不能为空");
            }

            using var service = Create(configuration.ChannelType);
            return service.ValidateConfiguration(configuration);
        }
    }
}

