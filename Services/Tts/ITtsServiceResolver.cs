using System;

namespace Buddie.Services.Tts
{
    public interface ITtsServiceResolver
    {
        ITtsService Create(TtsChannelType channelType);
        bool IsSupported(TtsChannelType channelType);
        TtsValidationResult ValidateConfiguration(TtsConfiguration configuration);
    }
}

