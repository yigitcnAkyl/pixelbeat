using System;
using System.Collections.Generic;

namespace PixelBeat.EditorTools.BeatmapGen
{
    /// <summary>
    /// Tempo tahmini ve beat takibi (sabit BPM varsaymaz).
    /// 1) Onset eğrisinin otokorelasyonu + log-normal tempo önseli (BPM ipucu varsa ona yakın) → genel periyot.
    /// 2) ~8 sn'lik kayan pencerelerde genel periyodun ±%15'inde yerel periyot → kare başına periyot eğrisi.
    /// 3) Dinamik programlama ile beat takibi (Ellis 2007): her beat bir öncekinden yerel periyoda yakın uzaklıkta
    ///    ve güçlü onset'lerin üstünde olmayı tercih eder.
    /// </summary>
    public static class TempoTracker
    {
        public const double MinBpm = 60;
        public const double MaxBpm = 220;

        /// <summary>Onset eğrisini normalleştirir: 1 sn'lik hareketli ortalamayı çıkar, negatifi sıfırla, std'ye böl.</summary>
        public static double[] NormalizeOnset(double[] flux, double frameRate)
        {
            int n = flux.Length;
            var o = new double[n];
            if (n == 0) return o;

            int half = Math.Max(1, (int)Math.Round(frameRate * 0.5));
            var prefix = new double[n + 1];
            for (int i = 0; i < n; i++) prefix[i + 1] = prefix[i] + flux[i];

            for (int i = 0; i < n; i++)
            {
                int a = Math.Max(0, i - half), b = Math.Min(n, i + half + 1);
                double mean = (prefix[b] - prefix[a]) / (b - a);
                o[i] = Math.Max(0, flux[i] - mean);
            }

            double sum = 0, sq = 0;
            for (int i = 0; i < n; i++) { sum += o[i]; sq += o[i] * o[i]; }
            double m = sum / n;
            double std = Math.Sqrt(Math.Max(1e-12, sq / n - m * m));
            for (int i = 0; i < n; i++) o[i] /= std;
            return o;
        }

        /// <summary>
        /// Otokorelasyonla periyot (kare cinsinden, kesirli). centerBpm: önselin merkezi; sigmaOctaves: genişliği.
        /// [start, start+length) aralığına bakar; lag araması [minLag, maxLag].
        /// </summary>
        public static double EstimatePeriod(double[] o, double frameRate, double centerBpm, double sigmaOctaves,
            int start = 0, int length = -1, double minLag = -1, double maxLag = -1)
        {
            if (length < 0) length = o.Length - start;
            if (minLag < 0) minLag = frameRate * 60.0 / MaxBpm;
            if (maxLag < 0) maxLag = frameRate * 60.0 / MinBpm;
            int lo = Math.Max(1, (int)Math.Floor(minLag));
            int hi = Math.Min(length - 2, (int)Math.Ceiling(maxLag));
            if (hi <= lo) return frameRate * 60.0 / centerBpm;

            var score = new double[hi + 2];
            int bestLag = -1;
            double best = double.NegativeInfinity;
            for (int lag = lo; lag <= hi + 1 && lag < length; lag++)
            {
                double s = 0;
                int count = length - lag;
                for (int i = 0; i < count; i++) s += o[start + i] * o[start + i + lag];
                s /= count;

                double bpm = frameRate * 60.0 / lag;
                double oct = Math.Log(bpm / centerBpm, 2) / sigmaOctaves;
                s *= Math.Exp(-0.5 * oct * oct);
                if (lag < score.Length) score[lag] = s;

                if (lag <= hi && s > best)
                {
                    best = s;
                    bestLag = lag;
                }
            }

            if (bestLag < 0) return frameRate * 60.0 / centerBpm;

            // Parabolik inceltme
            if (bestLag > lo && bestLag < hi)
            {
                double a = score[bestLag - 1], b = score[bestLag], c = score[bestLag + 1];
                double den = a - 2 * b + c;
                if (Math.Abs(den) > 1e-12)
                {
                    double d = 0.5 * (a - c) / den;
                    if (Math.Abs(d) <= 0.5) return bestLag + d;
                }
            }
            return bestLag;
        }

        /// <summary>Kayan pencerelerde yerel periyot; genel periyodun ±%15'i ile sınırlı. Kare başına değer döner.</summary>
        public static double[] LocalPeriods(double[] o, double frameRate, double globalPeriod, double windowSec = 8, double stepSec = 2)
        {
            int n = o.Length;
            var perFrame = new double[n];
            int win = (int)Math.Round(windowSec * frameRate);
            int step = Math.Max(1, (int)Math.Round(stepSec * frameRate));
            if (n < win * 0.75)
            {
                for (int i = 0; i < n; i++) perFrame[i] = globalPeriod;
                return perFrame;
            }

            var centers = new List<double>();
            var periods = new List<double>();
            double globalBpm = frameRate * 60.0 / globalPeriod;
            for (int s = 0; s + win / 2 < n; s += step)
            {
                int len = Math.Min(win, n - s);
                if (len < win / 2) break;
                double p = EstimatePeriod(o, frameRate, globalBpm, 0.15, s, len, globalPeriod / 1.15, globalPeriod * 1.15);
                centers.Add(s + len / 2.0);
                periods.Add(p);
            }

            // 3'lü medyan ile sıçramaları bastır.
            var smooth = new double[periods.Count];
            for (int i = 0; i < periods.Count; i++)
            {
                double a = periods[Math.Max(0, i - 1)], b = periods[i], c = periods[Math.Min(periods.Count - 1, i + 1)];
                smooth[i] = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
            }

            for (int i = 0, w = 0; i < n; i++)
            {
                while (w < centers.Count - 1 && centers[w + 1] <= i) w++;
                if (i <= centers[0]) perFrame[i] = smooth[0];
                else if (w >= centers.Count - 1) perFrame[i] = smooth[centers.Count - 1];
                else
                {
                    double t = (i - centers[w]) / (centers[w + 1] - centers[w]);
                    perFrame[i] = smooth[w] + t * (smooth[w + 1] - smooth[w]);
                }
            }
            return perFrame;
        }

        /// <summary>
        /// Dinamik programlama ile beat kareleri (kesirli, onset tepesine göre inceltilmiş).
        /// tightness: periyottan sapma cezası (büyük = daha düzenli).
        /// </summary>
        public static List<double> TrackBeats(double[] o, double[] periods, double tightness = 100)
        {
            int n = o.Length;
            var beats = new List<double>();
            if (n == 0) return beats;

            var c = new double[n];
            var back = new int[n];
            for (int t = 0; t < n; t++)
            {
                double p = periods[t];
                int lo = t - (int)Math.Round(2 * p);
                int hi = t - (int)Math.Round(p / 2);
                double best = double.NegativeInfinity;
                int arg = -1;
                for (int tau = Math.Max(0, lo); tau <= hi; tau++)
                {
                    double r = Math.Log((t - tau) / p);
                    double s = c[tau] - tightness * r * r;
                    if (s > best)
                    {
                        best = s;
                        arg = tau;
                    }
                }

                if (arg < 0 || best < 0)
                {
                    c[t] = o[t];
                    back[t] = -1;
                }
                else
                {
                    c[t] = o[t] + best;
                    back[t] = arg;
                }
            }

            // Son beat: son 1.5 periyotta en yüksek birikimli skor.
            int tailStart = Math.Max(0, n - (int)Math.Round(1.5 * periods[n - 1]));
            int last = tailStart;
            for (int t = tailStart; t < n; t++)
                if (c[t] > c[last]) last = t;

            var frames = new List<int>();
            for (int t = last; t >= 0; t = back[t]) frames.Add(t);
            frames.Reverse();

            foreach (int f in frames) beats.Add(RefinePeak(o, f));
            return beats;
        }

        /// <summary>Yerel tepe ise parabolik alt-kare inceltme.</summary>
        public static double RefinePeak(double[] o, int i)
        {
            if (i <= 0 || i >= o.Length - 1) return i;
            double a = o[i - 1], b = o[i], c = o[i + 1];
            if (b < a || b < c) return i;
            double den = a - 2 * b + c;
            if (Math.Abs(den) < 1e-12) return i;
            double d = 0.5 * (a - c) / den;
            return i + Math.Max(-0.5, Math.Min(0.5, d));
        }
    }
}
