using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Buddie.Services.Tts;

namespace Buddie.Tests.Services.Tts
{
    public class TtsModelsTests
    {
        [Fact]
        public void TtsRequest_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var request = new TtsRequest();

            // Assert
            request.Text.Should().BeEmpty();
            request.Configuration.Should().NotBeNull();
            request.AdditionalParameters.Should().NotBeNull().And.BeEmpty();
            request.AudioFormat.Should().Be("wav");
            request.SampleRate.Should().Be(32000);
            request.Bitrate.Should().Be(128);
            request.Volume.Should().Be(1.0);
            request.Pitch.Should().Be(0);
            request.Emotion.Should().BeEmpty();
            request.Language.Should().BeEmpty();
        }

        [Fact]
        public void TtsRequest_AudioFormat_ShouldGetAndSetCorrectly()
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.AudioFormat = "mp3";

            // Assert
            request.AudioFormat.Should().Be("mp3");
            request.AdditionalParameters["audio_format"].Should().Be("mp3");
        }

        [Fact]
        public void TtsRequest_SampleRate_ShouldGetAndSetCorrectly()
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.SampleRate = 48000;

            // Assert
            request.SampleRate.Should().Be(48000);
            request.AdditionalParameters["sample_rate"].Should().Be(48000);
        }

        [Fact]
        public void TtsRequest_Volume_ShouldClampToValidRange()
        {
            // Arrange
            var request = new TtsRequest();

            // Act & Assert - Test lower bound
            request.Volume = 0.05;
            request.Volume.Should().Be(0.1);

            // Act & Assert - Test upper bound
            request.Volume = 5.0;
            request.Volume.Should().Be(3.0);

            // Act & Assert - Test valid value
            request.Volume = 1.5;
            request.Volume.Should().Be(1.5);
        }

        [Fact]
        public void TtsRequest_Pitch_ShouldClampToValidRange()
        {
            // Arrange
            var request = new TtsRequest();

            // Act & Assert - Test lower bound
            request.Pitch = -20;
            request.Pitch.Should().Be(-12);

            // Act & Assert - Test upper bound
            request.Pitch = 20;
            request.Pitch.Should().Be(12);

            // Act & Assert - Test valid value
            request.Pitch = 5;
            request.Pitch.Should().Be(5);
        }

        [Fact]
        public void TtsRequest_IsStreamingEnabled_ShouldAlwaysReturnFalse()
        {
            // Arrange
            var request = new TtsRequest();

            // Act & Assert
            request.IsStreamingEnabled.Should().BeFalse();

#pragma warning disable CS0618 // Type or member is obsolete
            request.IsStreamingEnabled = true;
#pragma warning restore CS0618

            request.IsStreamingEnabled.Should().BeFalse();
            request.AdditionalParameters["stream"].Should().Be(false);
        }

        [Fact]
        public void TtsResponse_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var response = new TtsResponse();

            // Assert
            response.AudioData.Should().BeEmpty();
            response.ContentType.Should().Be("audio/wav");
            response.IsSuccess.Should().BeFalse();
            response.ErrorMessage.Should().BeNull();
            response.ErrorDetails.Should().BeNull();
            response.ProcessingTime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void TtsValidationResult_Success_ShouldCreateValidResult()
        {
            // Act
            var result = TtsValidationResult.Success();

            // Assert
            result.IsValid.Should().BeTrue();
            result.ErrorMessages.Should().BeEmpty();
            result.WarningMessages.Should().BeEmpty();
        }

        [Fact]
        public void TtsValidationResult_Failure_ShouldCreateInvalidResult()
        {
            // Act
            var result = TtsValidationResult.Failure("Error 1", "Error 2");

            // Assert
            result.IsValid.Should().BeFalse();
            result.ErrorMessages.Should().HaveCount(2);
            result.ErrorMessages.Should().Contain("Error 1");
            result.ErrorMessages.Should().Contain("Error 2");
            result.WarningMessages.Should().BeEmpty();
        }

        [Fact]
        public void TtsValidationResult_AddError_ShouldSetIsValidToFalse()
        {
            // Arrange
            var result = new TtsValidationResult { IsValid = true };

            // Act
            result.AddError("Test error");

            // Assert
            result.IsValid.Should().BeFalse();
            result.ErrorMessages.Should().Contain("Test error");
        }

        [Fact]
        public void TtsValidationResult_AddWarning_ShouldNotAffectIsValid()
        {
            // Arrange
            var result = new TtsValidationResult { IsValid = true };

            // Act
            result.AddWarning("Test warning");

            // Assert
            result.IsValid.Should().BeTrue();
            result.WarningMessages.Should().Contain("Test warning");
        }

        [Fact]
        public void TtsException_ShouldStoreChannelTypeAndMessage()
        {
            // Act
            var exception = new TtsException(TtsChannelType.OpenAI, "Test error");

            // Assert
            exception.ChannelType.Should().Be(TtsChannelType.OpenAI);
            exception.Message.Should().Be("Test error");
            exception.ErrorDetails.Should().BeNull();
        }

        [Fact]
        public void TtsException_WithErrorDetails_ShouldStoreAllInformation()
        {
            // Act
            var exception = new TtsException(TtsChannelType.ElevenLabs, "Test error", "Detailed error info");

            // Assert
            exception.ChannelType.Should().Be(TtsChannelType.ElevenLabs);
            exception.Message.Should().Be("Test error");
            exception.ErrorDetails.Should().Be("Detailed error info");
        }

        [Fact]
        public void TtsException_WithInnerException_ShouldStoreInnerException()
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new TtsException(TtsChannelType.MiniMax, "Test error", innerException);

            // Assert
            exception.ChannelType.Should().Be(TtsChannelType.MiniMax);
            exception.Message.Should().Be("Test error");
            exception.InnerException.Should().Be(innerException);
        }

        [Fact]
        public void TtsRequest_Emotion_ShouldHandleNullValue()
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.Emotion = null!;

            // Assert
            request.Emotion.Should().BeEmpty();
            request.AdditionalParameters["emotion"].Should().Be("");
        }

        [Fact]
        public void TtsRequest_Language_ShouldHandleNullValue()
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.Language = null!;

            // Assert
            request.Language.Should().BeEmpty();
            request.AdditionalParameters["language"].Should().Be("");
        }

        [Fact]
        public void TtsRequest_ComplexScenario_ShouldHandleMultipleParameters()
        {
            // Arrange
            var request = new TtsRequest
            {
                Text = "Hello, world!",
                AudioFormat = "mp3",
                SampleRate = 48000,
                Bitrate = 256,
                Volume = 2.0,
                Pitch = 5,
                Emotion = "happy",
                Language = "en"
            };

            // Assert
            request.Text.Should().Be("Hello, world!");
            request.AudioFormat.Should().Be("mp3");
            request.SampleRate.Should().Be(48000);
            request.Bitrate.Should().Be(256);
            request.Volume.Should().Be(2.0);
            request.Pitch.Should().Be(5);
            request.Emotion.Should().Be("happy");
            request.Language.Should().Be("en");
        }

        [Fact]
        public void TtsRequest_AdditionalParameters_ShouldAllowCustomValues()
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.AdditionalParameters["custom_param"] = "custom_value";
            request.AdditionalParameters["numeric_param"] = 42;
            request.AdditionalParameters["bool_param"] = true;

            // Assert
            request.AdditionalParameters["custom_param"].Should().Be("custom_value");
            request.AdditionalParameters["numeric_param"].Should().Be(42);
            request.AdditionalParameters["bool_param"].Should().Be(true);
        }

        [Fact]
        public void TtsResponse_SuccessfulResponse_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var audioData = new byte[] { 1, 2, 3, 4, 5 };
            var processingTime = TimeSpan.FromMilliseconds(500);

            // Act
            var response = new TtsResponse
            {
                AudioData = audioData,
                ContentType = "audio/mp3",
                IsSuccess = true,
                ProcessingTime = processingTime
            };

            // Assert
            response.AudioData.Should().BeEquivalentTo(audioData);
            response.ContentType.Should().Be("audio/mp3");
            response.IsSuccess.Should().BeTrue();
            response.ErrorMessage.Should().BeNull();
            response.ErrorDetails.Should().BeNull();
            response.ProcessingTime.Should().Be(processingTime);
        }

        [Fact]
        public void TtsResponse_FailedResponse_ShouldSetErrorPropertiesCorrectly()
        {
            // Arrange & Act
            var response = new TtsResponse
            {
                IsSuccess = false,
                ErrorMessage = "API rate limit exceeded",
                ErrorDetails = "429 Too Many Requests - Please retry after 60 seconds",
                ProcessingTime = TimeSpan.FromMilliseconds(100)
            };

            // Assert
            response.AudioData.Should().BeEmpty();
            response.IsSuccess.Should().BeFalse();
            response.ErrorMessage.Should().Be("API rate limit exceeded");
            response.ErrorDetails.Should().Be("429 Too Many Requests - Please retry after 60 seconds");
            response.ProcessingTime.Should().Be(TimeSpan.FromMilliseconds(100));
        }

        [Theory]
        [InlineData(0.1, 0.1)]    // Min valid value
        [InlineData(3.0, 3.0)]    // Max valid value
        [InlineData(1.0, 1.0)]    // Default value
        [InlineData(0.05, 0.1)]   // Below min should clamp
        [InlineData(5.0, 3.0)]    // Above max should clamp
        [InlineData(-1.0, 0.1)]   // Negative should clamp to min
        public void TtsRequest_Volume_ShouldClampCorrectly(double input, double expected)
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.Volume = input;

            // Assert
            request.Volume.Should().Be(expected);
        }

        [Theory]
        [InlineData(-12, -12)]    // Min valid value
        [InlineData(12, 12)]      // Max valid value
        [InlineData(0, 0)]        // Default value
        [InlineData(-20, -12)]    // Below min should clamp
        [InlineData(20, 12)]      // Above max should clamp
        public void TtsRequest_Pitch_ShouldClampCorrectly(int input, int expected)
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.Pitch = input;

            // Assert
            request.Pitch.Should().Be(expected);
        }

        [Theory]
        [InlineData("wav")]
        [InlineData("mp3")]
        [InlineData("flac")]
        [InlineData("aac")]
        [InlineData("opus")]
        [InlineData("ogg")]
        public void TtsRequest_AudioFormat_ShouldAcceptVariousFormats(string format)
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.AudioFormat = format;

            // Assert
            request.AudioFormat.Should().Be(format);
            request.AdditionalParameters["audio_format"].Should().Be(format);
        }

        [Theory]
        [InlineData(8000)]
        [InlineData(16000)]
        [InlineData(24000)]
        [InlineData(32000)]
        [InlineData(48000)]
        public void TtsRequest_SampleRate_ShouldAcceptStandardRates(int rate)
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.SampleRate = rate;

            // Assert
            request.SampleRate.Should().Be(rate);
            request.AdditionalParameters["sample_rate"].Should().Be(rate);
        }

        [Theory]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(192)]
        [InlineData(256)]
        [InlineData(320)]
        public void TtsRequest_Bitrate_ShouldAcceptStandardBitrates(int bitrate)
        {
            // Arrange
            var request = new TtsRequest();

            // Act
            request.Bitrate = bitrate;

            // Assert
            request.Bitrate.Should().Be(bitrate);
            request.AdditionalParameters["bitrate"].Should().Be(bitrate);
        }

        [Fact]
        public void TtsValidationResult_MultipleErrorsAndWarnings_ShouldAccumulateCorrectly()
        {
            // Arrange
            var result = new TtsValidationResult { IsValid = true };

            // Act
            result.AddWarning("Warning 1");
            result.AddWarning("Warning 2");
            result.AddError("Error 1");
            result.AddError("Error 2");
            result.AddWarning("Warning 3");

            // Assert
            result.IsValid.Should().BeFalse();
            result.ErrorMessages.Should().HaveCount(2);
            result.ErrorMessages.Should().Contain(new[] { "Error 1", "Error 2" });
            result.WarningMessages.Should().HaveCount(3);
            result.WarningMessages.Should().Contain(new[] { "Warning 1", "Warning 2", "Warning 3" });
        }

        [Fact]
        public void TtsRequest_GetPropertyWithMissingKey_ShouldReturnDefault()
        {
            // Arrange
            var request = new TtsRequest();
            request.AdditionalParameters.Clear();

            // Act & Assert
            request.AudioFormat.Should().Be("wav");      // Default when missing
            request.SampleRate.Should().Be(32000);        // Default when missing
            request.Bitrate.Should().Be(128);             // Default when missing
            request.Volume.Should().Be(1.0);              // Default when missing
            request.Pitch.Should().Be(0);                 // Default when missing
            request.Emotion.Should().BeEmpty();           // Default when missing
            request.Language.Should().BeEmpty();          // Default when missing
        }

        [Fact]
        public void TtsRequest_GetPropertyWithWrongType_ShouldReturnDefault()
        {
            // Arrange
            var request = new TtsRequest();
            request.AdditionalParameters["audio_format"] = 123;      // Wrong type (int instead of string)
            request.AdditionalParameters["sample_rate"] = "wrong";   // Wrong type (string instead of int)
            request.AdditionalParameters["volume"] = "invalid";      // Wrong type (string instead of double)

            // Act & Assert
            request.AudioFormat.Should().Be("wav");      // Default when wrong type
            request.SampleRate.Should().Be(32000);       // Default when wrong type
            request.Volume.Should().Be(1.0);             // Default when wrong type
        }
    }
}