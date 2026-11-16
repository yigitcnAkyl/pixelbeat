using System;

namespace PixelBeat.EditorTools.BeatmapGen
{
    /// <summary>
    /// STFT tabanlı özellikler (kare başına): log-genlik spektral akı (onset gücü), bant enerjileri / akıları
    /// (bas &lt; 250 Hz, orta 250–2000 Hz, tiz &gt; 2000 Hz) ve spektral ağırlık merkezi.
    /// </summary>
    public sealed class SpectralAnalysis
    {
        public const int FrameSize = 1024;
        public const int Hop = 256;
        public const int BandCount = 3;
        private static readonly double[] BandEdgesHz = { 0, 250, 2000, double.MaxValue };

        /// <summary>
        /// Akı tepesinin, tetikleyen sesin pencere içindeki konumu (pencere oranı). Hann penceresinde log-akı,
        /// ses pencerenin sağ kenarından girip ağırlık hızla artarken en büyüktür; testlerle ayarlandı.
        /// </summary>
        public const double OnsetPositionInWindow = 0.775; // sentetik tık dizisiyle ölçüldü: ortalama ~0 ms, en büyük ~±6 ms

        /// <summary>Log sıkıştırma katsayısı: log(1 + γ·|X|).</summary>
        private const double Gamma = 100.0;

        public int SampleRate { get; }
        public int FrameCount { get; }
        public double FrameRate => SampleRate / (double)Hop;

        /// <summary>Toplam onset gücü (yarım dalga doğrultulmuş log-spektral akı).</summary>
        public double[] Flux { get; }
        public double[][] BandFlux { get; }
        /// <summary>Doğrusal bant enerjisi (|X|² toplamı).</summary>
        public double[][] BandEnergy { get; }
        /// <summary>Spektral ağırlık merkezi (Hz).</summary>
        public double[] Centroid { get; }

        private SpectralAnalysis(int sampleRate, int frames)
        {
            SampleRate = sampleRate;
            FrameCount = frames;
            Flux = new double[frames];
            Centroid = new double[frames];
            BandFlux = new double[BandCount][];
            BandEnergy = new double[BandCount][];
            for (int b = 0; b < BandCount; b++)
            {
                BandFlux[b] = new double[frames];
                BandEnergy[b] = new double[frames];
            }
        }

        /// <summary>Kare indeksindeki (kesirli olabilir) onset'in ses zamanı (sn).</summary>
        public double FrameToTime(double frame) => (frame * Hop + FrameSize * OnsetPositionInWindow) / SampleRate;

        public double TimeToFrame(double time) => (time * SampleRate - FrameSize * OnsetPositionInWindow) / Hop;

        /// <summary>Karenin pencere merkezinin zamanı (sn) — enerji gibi sürekli özellikler için.</summary>
        public double FrameCenterTime(int frame) => (frame * Hop + FrameSize * 0.5) / SampleRate;

        public int FrameAtCenterTime(double time) =>
            Math.Max(0, Math.Min(FrameCount - 1, (int)Math.Round((time * SampleRate - FrameSize * 0.5) / Hop)));

        public static SpectralAnalysis Analyze(float[] mono, int sampleRate, Action<float> progress = null)
        {
            int frames = Math.Max(0, (mono.Length - FrameSize) / Hop + 1);
            var a = new SpectralAnalysis(sampleRate, frames);
            if (frames == 0) return a;

            int bins = FrameSize / 2;
            var window = new double[FrameSize];
            for (int i = 0; i < FrameSize; i++) window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / FrameSize);

            var bandOfBin = new int[bins + 1];
            for (int k = 0; k <= bins; k++)
            {
                double hz = k * sampleRate / (double)FrameSize;
                int b = 0;
                while (b < BandCount - 1 && hz >= BandEdgesHz[b + 1]) b++;
                bandOfBin[k] = b;
            }

            var re = new double[FrameSize];
            var im = new double[FrameSize];
            var prevLog = new double[bins + 1];
            var curLog = new double[bins + 1];

            for (int f = 0; f < frames; f++)
            {
                int start = f * Hop;
                for (int i = 0; i < FrameSize; i++)
                {
                    re[i] = mono[start + i] * window[i];
                    im[i] = 0;
                }
                Fft.Transform(re, im);

                double flux = 0, magSum = 0, weighted = 0;
                for (int k = 1; k <= bins; k++)
                {
                    double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                    double lg = Math.Log(1 + Gamma * mag);
                    curLog[k] = lg;
                    int b = bandOfBin[k];
                    a.BandEnergy[b][f] += mag * mag;
                    magSum += mag;
                    weighted += mag * k * sampleRate / (double)FrameSize;

                    if (f > 0)
                    {
                        double d = lg - prevLog[k];
                        if (d > 0)
                        {
                            flux += d;
                            a.BandFlux[b][f] += d;
                        }
                    }
                }

                a.Flux[f] = flux;
                a.Centroid[f] = magSum > 1e-9 ? weighted / magSum : 0;
                (prevLog, curLog) = (curLog, prevLog);

                if (progress != null && (f & 1023) == 0) progress(f / (float)frames);
            }

            return a;
        }
    }
}
