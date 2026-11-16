using System;
using System.Collections.Generic;

namespace PixelBeat.EditorTools.BeatmapGen
{
    public struct Onset
    {
        public double Time;
        public double Frame;
        /// <summary>Normalleştirilmiş onset gücü (std birimi).</summary>
        public double Strength;
        /// <summary>Baskın bant: 0 bas, 1 orta, 2 tiz.</summary>
        public int Band;
        /// <summary>Spektral ağırlık merkezi (Hz) — şerit seçiminde ses perdesi ipucu.</summary>
        public double Centroid;
    }

    /// <summary>Onset eğrisinde uyarlanabilir eşikli tepe seçimi.</summary>
    public static class OnsetPicker
    {
        /// <param name="o">Normalleştirilmiş onset eğrisi (TempoTracker.NormalizeOnset).</param>
        /// <param name="delta">Yerel ortalamanın üstünde gereken pay (std birimi).</param>
        public static List<Onset> Pick(SpectralAnalysis a, double[] o, double delta = 0.5, double minGapSec = 0.03, double avgWinSec = 1.0)
        {
            var result = new List<Onset>();
            int n = o.Length;
            if (n < 3) return result;

            int halfPeak = Math.Max(1, (int)Math.Round(minGapSec * a.FrameRate));
            int halfAvg = Math.Max(1, (int)Math.Round(avgWinSec * a.FrameRate * 0.5));
            var prefix = new double[n + 1];
            for (int i = 0; i < n; i++) prefix[i + 1] = prefix[i] + o[i];

            // Bant akısı ölçekleri (bantlar arasında adil karşılaştırma için).
            var bandStd = new double[SpectralAnalysis.BandCount];
            for (int b = 0; b < bandStd.Length; b++)
            {
                double s = 0, sq = 0;
                foreach (double v in a.BandFlux[b]) { s += v; sq += v * v; }
                double m = s / n;
                bandStd[b] = Math.Sqrt(Math.Max(1e-12, sq / n - m * m));
            }

            for (int i = 1; i < n - 1; i++)
            {
                double v = o[i];
                if (v <= 0) continue;

                bool isMax = true;
                for (int j = Math.Max(0, i - halfPeak); j <= Math.Min(n - 1, i + halfPeak); j++)
                {
                    if (o[j] > v || (o[j] == v && j < i)) { isMax = false; break; }
                }
                if (!isMax) continue;

                int lo = Math.Max(0, i - halfAvg), hi = Math.Min(n, i + halfAvg + 1);
                double mean = (prefix[hi] - prefix[lo]) / (hi - lo);
                if (v < mean + delta) continue;

                int band = 0;
                double bestBand = double.NegativeInfinity;
                for (int b = 0; b < SpectralAnalysis.BandCount; b++)
                {
                    double bv = a.BandFlux[b][i] / bandStd[b];
                    if (bv > bestBand) { bestBand = bv; band = b; }
                }

                double frame = TempoTracker.RefinePeak(o, i);
                result.Add(new Onset
                {
                    Frame = frame,
                    Time = a.FrameToTime(frame),
                    Strength = v,
                    Band = band,
                    Centroid = a.Centroid[Math.Min(n - 1, i + 1)],
                });
            }

            return result;
        }
    }
}
