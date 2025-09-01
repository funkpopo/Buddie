using System;
using System.Linq;

namespace Buddie.Services
{
    public class LocalVadDetector
    {
        private readonly double _threshold;
        private readonly int _minSpeechFrames;
        private readonly int _minSilenceFrames;
        private int _speechFrameCount;
        private int _silenceFrameCount;
        private bool _isSpeaking;

        public event Action? OnSpeechStart;
        public event Action? OnSpeechEnd;

        public LocalVadDetector(double threshold = 0.02, int minSpeechFrames = 3, int minSilenceFrames = 10)
        {
            _threshold = threshold;
            _minSpeechFrames = minSpeechFrames;
            _minSilenceFrames = minSilenceFrames;
        }

        public void ProcessAudioFrame(byte[] audioData)
        {
            try
            {
                // 将字节数组转换为16位PCM样本
                var samples = new short[audioData.Length / 2];
                Buffer.BlockCopy(audioData, 0, samples, 0, audioData.Length);

                // 计算音频能量（RMS）
                var energy = CalculateRmsEnergy(samples);
                
                // 基于能量阈值进行语音检测
                var hasVoiceActivity = energy > _threshold;

                if (hasVoiceActivity)
                {
                    _speechFrameCount++;
                    _silenceFrameCount = 0;

                    if (!_isSpeaking && _speechFrameCount >= _minSpeechFrames)
                    {
                        _isSpeaking = true;
                        OnSpeechStart?.Invoke();
                    }
                }
                else
                {
                    _silenceFrameCount++;
                    _speechFrameCount = 0;

                    if (_isSpeaking && _silenceFrameCount >= _minSilenceFrames)
                    {
                        _isSpeaking = false;
                        OnSpeechEnd?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VAD处理错误: {ex.Message}");
            }
        }

        private static double CalculateRmsEnergy(short[] samples)
        {
            if (samples.Length == 0) return 0;

            double sum = samples.Sum(sample => (double)sample * sample);
            return Math.Sqrt(sum / samples.Length) / short.MaxValue;
        }

        public void Reset()
        {
            _speechFrameCount = 0;
            _silenceFrameCount = 0;
            _isSpeaking = false;
        }

        public bool IsSpeaking => _isSpeaking;
    }
}