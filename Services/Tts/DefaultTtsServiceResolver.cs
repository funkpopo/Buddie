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
            // 通过Keyed DI解析对应的实现，避免维护手工映射/静态字典
            return _serviceProvider.GetRequiredKeyedService<ITtsService>(channelType);
        }

        public bool IsSupported(TtsChannelType channelType)
        {
            // 若已注册对应Key则支持；尝试解析并立即释放
            try
            {
                using var service = _serviceProvider.GetKeyedService<ITtsService>(channelType);
                return service != null;
            }
            catch
            {
                return false;
            }
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
