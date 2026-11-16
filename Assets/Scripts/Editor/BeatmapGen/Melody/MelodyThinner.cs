using System;
using System.Collections.Generic;

namespace PixelBeat.EditorTools.BeatmapGen.Melody
{
    /// <summary>
    /// Melodi hattını hedef yoğunluğa (nota/sn) seyreltir (GDD §7.1). Unity'ye bağımlı değil, belirleyici.
    /// - Cümle başları (uzun sessizlikten sonra gelen ilk nota) her zaman korunur.
    /// - Bütçe ~8 sn'lik pencerelere dağıtılır ki yoğun bölüm seyrek bölümün payını yemesin.
    /// - Pencere içinde önem sırasıyla seçilir (vuruş gücü, süre, melodik vurgu); notalar arası en kısa boşluk korunur.
    /// </summary>
    public static class MelodyThinner
    {
        public const double DefaultWindowSec = 8.0;
        /// <summary>Önceki melodi notasının bitişinden bu kadar sessizlik sonra gelen nota cümle başıdır.</summary>
        public const double PhraseGapSec = 0.5;
        /// <summary>Bu kadar yarım ses ve üstü atlama melodik vurgu sayılır.</summary>
        public const int AccentLeap = 5;

        public static void MarkPhraseStarts(IReadOnlyList<MelodyNote> line, double phraseGapSec = PhraseGapSec)
        {
            for (int i = 0; i < line.Count; i++)
                line[i].PhraseStart = i == 0 || line[i].Time - line[i - 1].End >= phraseGapSec;
        }

        /// <summary>Vuruş gücü + süre + melodik vurgu (yerel zirve, büyük atlama).</summary>
        public static double Importance(IReadOnlyList<MelodyNote> line, int i)
        {
            var n = line[i];
            double score = n.Velocity / 127.0 + 0.8 * Math.Min(n.Duration, 1.0);
            int prev = i > 0 ? line[i - 1].Pitch : n.Pitch;
            int next = i + 1 < line.Count ? line[i + 1].Pitch : n.Pitch;
            if (n.Pitch > prev && n.Pitch > next) score += 0.3;
            if (Math.Abs(n.Pitch - prev) >= AccentLeap) score += 0.2;
            return score;
        }

        public static List<MelodyNote> Thin(IReadOnlyList<MelodyNote> line, double targetNps, double minGapSec,
            double windowSec = DefaultWindowSec, double phraseGapSec = PhraseGapSec) =>
            Thin(line, _ => targetNps, minGapSec, windowSec, phraseGapSec);

        /// <summary>targetAt: pencere ortasındaki hedef nota/sn (geçişlerde yoğunluk rampası için).</summary>
        public static List<MelodyNote> Thin(IReadOnlyList<MelodyNote> line, Func<double, double> targetAt, double minGapSec,
            double windowSec = DefaultWindowSec, double phraseGapSec = PhraseGapSec)
        {
            var result = new List<MelodyNote>();
            if (line.Count == 0) return result;
            MarkPhraseStarts(line, phraseGapSec);

            var importance = new double[line.Count];
            for (int i = 0; i < line.Count; i++) importance[i] = Importance(line, i);

            var acceptedTimes = new List<double>(); // sıralı
            var accepted = new bool[line.Count];

            bool GapOk(double t)
            {
                int k = acceptedTimes.BinarySearch(t);
                if (k >= 0) return false;
                k = ~k;
                if (k > 0 && t - acceptedTimes[k - 1] < minGapSec - 1e-9) return false;
                if (k < acceptedTimes.Count && acceptedTimes[k] - t < minGapSec - 1e-9) return false;
                return true;
            }

            void Accept(int i)
            {
                accepted[i] = true;
                int k = acceptedTimes.BinarySearch(line[i].Time);
                acceptedTimes.Insert(k < 0 ? ~k : k, line[i].Time);
            }

            // 1) Cümle başları (zaman sırasıyla).
            for (int i = 0; i < line.Count; i++)
                if (line[i].PhraseStart && GapOk(line[i].Time)) Accept(i);

            // 2) Pencere pencere önem sırasıyla, bütçe kadar.
            double t0 = line[0].Time;
            double lastTime = line[line.Count - 1].Time;
            int start = 0;
            for (double ws = t0; ws <= lastTime; ws += windowSec)
            {
                double we = ws + windowSec;
                int end = start;
                while (end < line.Count && line[end].Time < we) end++;

                double spanEnd = Math.Min(we, lastTime + minGapSec);
                double span = spanEnd - ws;
                int budget = Math.Max(1, (int)Math.Round(targetAt((ws + spanEnd) / 2) * span));
                var order = new List<int>();
                int used = 0;
                for (int i = start; i < end; i++)
                {
                    if (accepted[i]) used++;
                    else order.Add(i);
                }
                order.Sort((a, b) => importance[a] != importance[b] ? importance[b].CompareTo(importance[a]) : a.CompareTo(b));
                foreach (int i in order)
                {
                    if (used >= budget) break;
                    if (!GapOk(line[i].Time)) continue;
                    Accept(i);
                    used++;
                }
                start = end;
            }

            for (int i = 0; i < line.Count; i++)
                if (accepted[i]) result.Add(line[i]);
            return result;
        }
    }
}
