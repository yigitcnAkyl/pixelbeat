using System;

namespace PixelBeat.EditorTools.BeatmapGen
{
    /// <summary>Analiz öncesi ses hazırlığı: mono'ya indirme ve ~22 kHz'e düşürme.</summary>
    public static class AudioPrep
    {
        public const int TargetRate = 22050;

        /// <summary>Kanal ortalaması alıp, örnek hızını tam sayı oranla (blok ortalaması) ~22 kHz'e indirir.</summary>
        public static float[] ToMonoDownsampled(float[] interleaved, int channels, int sampleRate, out int outRate)
        {
            if (channels < 1) throw new ArgumentOutOfRangeException(nameof(channels));
            int frames = interleaved.Length / channels;
            int factor = Math.Max(1, (int)Math.Round(sampleRate / (double)TargetRate));
            outRate = sampleRate / factor;

            int outLen = frames / factor;
            var mono = new float[outLen];
            float norm = 1f / (channels * factor);
            for (int o = 0; o < outLen; o++)
            {
                double sum = 0;
                int start = o * factor;
                for (int f = 0; f < factor; f++)
                {
                    int baseIdx = (start + f) * channels;
                    for (int c = 0; c < channels; c++) sum += interleaved[baseIdx + c];
                }
                mono[o] = (float)(sum * norm);
            }
            return mono;
        }
    }
}
