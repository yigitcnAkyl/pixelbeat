using System;
using System.Collections.Generic;

namespace PixelBeat.EditorTools.BeatmapGen.Vocal
{
    /// <summary>Zaman aralığı (sn), [Start, End].</summary>
    public struct Span
    {
        public double Start;
        public double End;

        public Span(double start, double end)
        {
            Start = start;
            End = end;
        }

        public double Length => End - Start;
        public bool Contains(double t) => t >= Start && t <= End;
        public override string ToString() => $"{Start:0.0}–{End:0.0}";
    }

    public sealed class VocalGateSettings
    {
        public double FrameSec = 0.05;
        /// <summary>Eşik: zarfın bu yüzdeliğinin <see cref="ThresholdDb"/> dB altı.</summary>
        public double ReferencePercentile = 0.95;
        public double ThresholdDb = 30;
        /// <summary>Bundan kısa susmalar bölümü bölmez (nefes).</summary>
        public double MergeGapSec = 1.5;
        /// <summary>Bundan kısa bölümler atılır (sızıntı patlamaları).</summary>
        public double MinSectionSec = 1.0;
        /// <summary>Bölüm kenarlarına eklenen pay.</summary>
        public double PadSec = 0.15;
    }

    /// <summary>
    /// Vokal kapısı (GDD §7.2): vokal stem'inin RMS zarfından vokal aktif bölümleri. Unity'ye bağımlı değil.
    /// Eşik göreli (zarfın %95'liğinin 30 dB altı), böylece stem'in genel seviyesinden bağımsız.
    /// </summary>
    public static class VocalGate
    {
        /// <summary>Kare başına dB (20·log10 RMS).</summary>
        public static double[] EnvelopeDb(float[] mono, int sampleRate, double frameSec)
        {
            int frame = Math.Max(1, (int)(sampleRate * frameSec));
            int count = mono.Length / frame;
            var db = new double[count];
            for (int f = 0; f < count; f++)
            {
                double sum = 0;
                int s = f * frame;
                for (int i = 0; i < frame; i++)
                {
                    double v = mono[s + i];
                    sum += v * v;
                }
                db[f] = 20 * Math.Log10(Math.Sqrt(sum / frame) + 1e-9);
            }
            return db;
        }

        public static List<Span> Detect(float[] mono, int sampleRate, VocalGateSettings s = null)
        {
            s = s ?? new VocalGateSettings();
            var db = EnvelopeDb(mono, sampleRate, s.FrameSec);
            return DetectFromEnvelope(db, s.FrameSec, s, mono.Length / (double)sampleRate);
        }

        public static List<Span> DetectFromEnvelope(double[] db, double frameSec, VocalGateSettings s, double durationSec)
        {
            var result = new List<Span>();
            if (db.Length == 0) return result;

            var sorted = (double[])db.Clone();
            Array.Sort(sorted);
            double reference = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * s.ReferencePercentile))];
            double threshold = reference - s.ThresholdDb;

            // Aktif kare koşuları
            var runs = new List<Span>();
            int start = -1;
            for (int f = 0; f <= db.Length; f++)
            {
                bool active = f < db.Length && db[f] >= threshold;
                if (active && start < 0) start = f;
                else if (!active && start >= 0)
                {
                    runs.Add(new Span(start * frameSec, f * frameSec));
                    start = -1;
                }
            }

            // Kısa susmaları birleştir
            var merged = new List<Span>();
            foreach (var r in runs)
            {
                if (merged.Count > 0 && r.Start - merged[merged.Count - 1].End < s.MergeGapSec)
                {
                    var last = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new Span(last.Start, r.End);
                }
                else merged.Add(r);
            }

            foreach (var m in merged)
            {
                if (m.Length < s.MinSectionSec) continue;
                result.Add(new Span(Math.Max(0, m.Start - s.PadSec), Math.Min(durationSec, m.End + s.PadSec)));
            }
            return result;
        }

        public static bool Inside(IReadOnlyList<Span> spans, double t)
        {
            foreach (var sp in spans)
                if (sp.Contains(t)) return true;
            return false;
        }

        /// <summary>
        /// Aralıkları birleştirir: arası &lt; maxGapSec olanlar tek aralık olur (sıralı girdi gerekmez).
        /// </summary>
        public static List<Span> MergeClose(IEnumerable<Span> spans, double maxGapSec)
        {
            var list = new List<Span>(spans);
            list.Sort((a, b) => a.Start.CompareTo(b.Start));
            var result = new List<Span>();
            foreach (var sp in list)
            {
                if (result.Count > 0 && sp.Start - result[result.Count - 1].End < maxGapSec)
                {
                    var last = result[result.Count - 1];
                    result[result.Count - 1] = new Span(last.Start, Math.Max(last.End, sp.End));
                }
                else result.Add(sp);
            }
            return result;
        }
    }
}
