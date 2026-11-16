using System;
using System.Collections.Generic;
using PixelBeat.Core;

namespace PixelBeat.EditorTools.BeatmapGen
{
    /// <summary>Zorluk (1–10) → üretim parametreleri.</summary>
    public sealed class DifficultySettings
    {
        public int Difficulty;
        /// <summary>Hedef nota sayısı / beat (müzikli bölümde).</summary>
        public double NotesPerBeat;
        /// <summary>İzin verilen en ince bölme: 2 (1/2) ya da 4 (1/4).</summary>
        public int MaxSubdivision;
        /// <summary>1/2 notaların gereken güç yüzdeliği (0 = sınırsız).</summary>
        public double HalfBeatMinPercentile;
        /// <summary>Notalar arası en kısa süre (sn), el ayrımından bağımsız.</summary>
        public double MinGapSec;
        /// <summary>Notaların en fazla bu oranı hold olur.</summary>
        public double HoldShare;
        /// <summary>Notaların en fazla bu oranı 2'li akor olur.</summary>
        public double ChordShare;

        public static DifficultySettings For(int difficulty)
        {
            int d = Math.Max(1, Math.Min(10, difficulty));
            return new DifficultySettings
            {
                Difficulty = d,
                NotesPerBeat = 0.4 + (d - 1) * 0.2,
                MaxSubdivision = d >= 8 ? 4 : 2,
                HalfBeatMinPercentile = d <= 3 ? 0.7 : 0,
                MinGapSec = d <= 3 ? 0.3 : d <= 6 ? 0.18 : 0.09,
                HoldShare = 0.08 + d * 0.012,
                ChordShare = d < 4 ? 0 : 0.03 + (d - 4) * 0.015,
            };
        }
    }

    /// <summary>Henüz şeridi atanmamış nota.</summary>
    public sealed class PlannedNote
    {
        public double Beat;
        public double Time;
        public double Strength;
        public int Band;
        public double Centroid;
        /// <summary>1 = beat üstü, 2 = 1/2, 4 = 1/4.</summary>
        public int Level;
        public bool Chord;
        public double HoldBeats;
    }

    /// <summary>Onset'leri beat ızgarasına oturtur, zorluğa göre seçer, hold ve akor işaretler.</summary>
    public static class NoteSelector
    {
        public const double MinHoldBeats = 1.0;
        public const double MaxHoldBeats = 4.0;

        /// <summary>Her onset'i en yakın 1/4 ızgara noktasına oturtur; aynı noktaya düşenlerden en güçlüsü kalır.</summary>
        public static List<PlannedNote> Snap(IReadOnlyList<Onset> onsets, TempoMap tempo, int beatCount)
        {
            var byBeat = new Dictionary<double, PlannedNote>();
            foreach (var o in onsets)
            {
                double beat = tempo.TimeToBeat(o.Time);
                if (beat < -0.125 || beat > beatCount - 1 + 0.125) continue;

                double q = Math.Round(beat * 4) / 4.0;
                if (q < 0 || q > beatCount - 1) continue;

                double frac = q - Math.Floor(q);
                int level = frac == 0 ? 1 : Math.Abs(frac - 0.5) < 1e-9 ? 2 : 4;

                if (byBeat.TryGetValue(q, out var existing) && existing.Strength >= o.Strength) continue;
                byBeat[q] = new PlannedNote
                {
                    Beat = q,
                    Time = tempo.BeatToTime(q),
                    Strength = o.Strength,
                    Band = o.Band,
                    Centroid = o.Centroid,
                    Level = level,
                };
            }

            var list = new List<PlannedNote>(byBeat.Values);
            list.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            return list;
        }

        /// <summary>Zorluğa göre en güçlü adayları seçer (yoğunluk hedefi + en kısa aralık). Zamana göre sıralı döner.</summary>
        public static List<PlannedNote> Select(IReadOnlyList<PlannedNote> candidates, DifficultySettings s)
        {
            var result = new List<PlannedNote>();
            if (candidates.Count == 0) return result;

            double halfThreshold = 0;
            if (s.HalfBeatMinPercentile > 0)
            {
                var strengths = new List<double>();
                foreach (var c in candidates) strengths.Add(c.Strength);
                strengths.Sort();
                halfThreshold = strengths[(int)Math.Min(strengths.Count - 1, Math.Floor(s.HalfBeatMinPercentile * strengths.Count))];
            }

            var allowed = new List<PlannedNote>();
            foreach (var c in candidates)
            {
                if (c.Level > s.MaxSubdivision) continue;
                if (c.Level == 2 && c.Strength < halfThreshold) continue;
                allowed.Add(c);
            }
            if (allowed.Count == 0) return result;

            double span = candidates[candidates.Count - 1].Beat - candidates[0].Beat + 1;
            int target = Math.Max(1, (int)Math.Round(span * s.NotesPerBeat));

            allowed.Sort((a, b) => Score(b).CompareTo(Score(a)));
            var times = new List<double>();
            foreach (var c in allowed)
            {
                if (result.Count >= target) break;
                int idx = times.BinarySearch(c.Time);
                if (idx < 0) idx = ~idx;
                bool tooClose = (idx > 0 && c.Time - times[idx - 1] < s.MinGapSec)
                                || (idx < times.Count && times[idx] - c.Time < s.MinGapSec);
                if (tooClose) continue;
                times.Insert(idx, c.Time);
                result.Add(c);
            }

            result.Sort((a, b) => a.Time.CompareTo(b.Time));
            return result;
        }

        private static double Score(PlannedNote c) => c.Strength * (c.Level == 1 ? 1.0 : c.Level == 2 ? 0.85 : 0.7);

        /// <summary>
        /// Uzun süren sesleri hold yapar: notanın baskın bandının enerjisi tepe değerin yarısının üstünde kaldığı ve
        /// araya yeni güçlü onset girmediği süre. En uzun sürenler, HoldShare oranına kadar hold olur (1–4 beat, 1/2'ye yuvarlı).
        /// </summary>
        public static void AssignHolds(List<PlannedNote> notes, SpectralAnalysis a, IReadOnlyList<Onset> onsets, TempoMap tempo, DifficultySettings s)
        {
            var sustains = new List<(PlannedNote note, double beats)>();
            int onsetIdx = 0;

            foreach (var note in notes)
            {
                note.HoldBeats = 0;
                double sustainSec = SustainSeconds(note, a, onsets, ref onsetIdx);
                double endBeat = tempo.TimeToBeat(note.Time + sustainSec);
                double beats = Math.Floor((endBeat - note.Beat) * 2) / 2.0;
                if (beats >= MinHoldBeats) sustains.Add((note, Math.Min(MaxHoldBeats, beats)));
            }

            int maxHolds = (int)Math.Floor(notes.Count * s.HoldShare);
            sustains.Sort((x, y) =>
            {
                int c = y.beats.CompareTo(x.beats);
                return c != 0 ? c : y.note.Strength.CompareTo(x.note.Strength);
            });
            for (int i = 0; i < sustains.Count && i < maxHolds; i++) sustains[i].note.HoldBeats = sustains[i].beats;
        }

        /// <summary>Notanın sesinin sürdüğü süre (sn).</summary>
        public static double SustainSeconds(PlannedNote note, SpectralAnalysis a, IReadOnlyList<Onset> onsets, ref int onsetIdx)
        {
            int n = a.FrameCount;
            if (n == 0) return 0;
            var energy = a.BandEnergy[note.Band];

            int start = a.FrameAtCenterTime(note.Time);
            double peak = 0;
            for (int i = start; i < Math.Min(n, start + 4); i++) peak = Math.Max(peak, energy[i]);
            if (peak <= 0) return 0;

            // Bir sonraki "kesen" onset: aynı bantta yeterince güçlü ya da her bantta çok güçlü.
            while (onsetIdx < onsets.Count && onsets[onsetIdx].Time <= note.Time + 0.05) onsetIdx++;
            double cutTime = double.MaxValue;
            for (int k = onsetIdx; k < onsets.Count; k++)
            {
                var o = onsets[k];
                if ((o.Band == note.Band && o.Strength >= 0.6 * note.Strength) || o.Strength >= 1.2 * note.Strength)
                {
                    cutTime = o.Time;
                    break;
                }
            }

            int end = start + 4;
            while (end < n && energy[end] >= 0.5 * peak && a.FrameCenterTime(end) < cutTime) end++;
            double endTime = Math.Min(a.FrameCenterTime(Math.Min(end, n - 1)), cutTime);
            return Math.Max(0, endTime - note.Time);
        }

        /// <summary>Beat üstündeki en güçlü (hold olmayan) notaların ChordShare oranı 2'li akor olur.</summary>
        public static void AssignChords(List<PlannedNote> notes, DifficultySettings s)
        {
            foreach (var n in notes) n.Chord = false;
            int max = (int)Math.Floor(notes.Count * s.ChordShare);
            if (max <= 0) return;

            var pool = new List<PlannedNote>();
            foreach (var n in notes)
                if (n.Level == 1 && n.HoldBeats <= 0) pool.Add(n);
            pool.Sort((x, y) => y.Strength.CompareTo(x.Strength));
            for (int i = 0; i < pool.Count && i < max; i++) pool[i].Chord = true;
        }
    }
}
