using System;
using System.Collections.Generic;
using PixelBeat.Core;

namespace PixelBeat.EditorTools.BeatmapGen.Melody
{
    public enum MelodyDifficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>Melodi modu zorluk ön ayarı (GDD §7.1).</summary>
    public sealed class MelodyPreset
    {
        public MelodyDifficulty Difficulty;
        /// <summary>JSON'daki "difficulty" alanı.</summary>
        public string Label;
        /// <summary>Dosya adı eki: &lt;ad&gt;_&lt;Suffix&gt;.json</summary>
        public string Suffix;
        public double TargetNps;
        public double MinGapSec;
        public double HoldMinSec;
        public bool Chords;
        /// <summary>Notaların en fazla bu oranı 2'li akor olur.</summary>
        public double ChordShare;

        public static MelodyPreset For(MelodyDifficulty d)
        {
            switch (d)
            {
                case MelodyDifficulty.Easy:
                    return new MelodyPreset { Difficulty = d, Label = "Kolay", Suffix = "easy", TargetNps = 1.5, MinGapSec = 0.30, HoldMinSec = 0.5 };
                case MelodyDifficulty.Normal:
                    return new MelodyPreset { Difficulty = d, Label = "Normal", Suffix = "normal", TargetNps = 2.5, MinGapSec = 0.20, HoldMinSec = 0.4 };
                default:
                    return new MelodyPreset { Difficulty = d, Label = "Zor", Suffix = "hard", TargetNps = 3.5, MinGapSec = 0.15, HoldMinSec = 0.4, Chords = true, ChordShare = 0.10 };
            }
        }
    }

    /// <summary>
    /// MIDI melodisinden beatmap (GDD §7.1). Unity'ye bağımlı değil, belirleyici.
    /// Melodi hattı → seyreltme → hold / akor → kontur şeritleri + iki parmak kısıtları → denetim.
    /// Nota zamanları MIDI'deki gerçek zamanlarıdır (+ global offset); beat ızgarasına oturtulmaz,
    /// sadece şarkıdan çıkarılan beat zamanlarına göre kesirli beat'e çevrilir.
    /// </summary>
    public static class MelodyGenerator
    {
        public const double MaxHoldSec = 2.0;
        /// <summary>Hold, sonraki notadan en az bu kadar önce biter (el serbest kalsın).</summary>
        public const double HoldReleaseGapSec = LaneAssigner.MinHandGapSec;
        /// <summary>Akor için iki notanın da bu velocity yüzdeliğinin üstünde olması gerekir.</summary>
        public const double ChordStrengthPercentile = 0.7;

        public static GeneratorResult Generate(IReadOnlyList<MelodyNote> melody, IReadOnlyList<double> beatTimes,
            MelodyPreset preset, double offsetSec = 0)
        {
            var result = new GeneratorResult { CandidateCount = melody.Count };
            if (beatTimes == null || beatTimes.Count < 2)
            {
                result.Warnings.Add("Beat bulunamadı (ses analizi).");
                return result;
            }
            result.BeatTimes = new List<double>(beatTimes);
            BeatmapGenerator.FillTempoStats(result);
            var tempo = TempoMap.FromBeatTimes(beatTimes);

            var thinned = MelodyThinner.Thin(melody, preset.TargetNps, preset.MinGapSec);
            var planned = new List<PlannedNote>();
            var source = new List<MelodyNote>();
            foreach (var m in thinned)
            {
                double t = m.Time + offsetSec;
                if (t < 0) continue;
                planned.Add(new PlannedNote { Time = t, Beat = tempo.TimeToBeat(t), Strength = m.Velocity / 127.0, Centroid = m.Pitch, Level = 0 });
                source.Add(m);
            }
            result.OnsetCount = planned.Count;

            AssignHolds(planned, source, tempo, preset);
            if (preset.Chords) AssignChords(planned, source, preset);

            var times = new double[planned.Count];
            var pitches = new int[planned.Count];
            for (int i = 0; i < planned.Count; i++)
            {
                times[i] = planned[i].Time;
                pitches[i] = source[i].Pitch;
            }
            ContourLanes.Compute(times, pitches, out var prefs, out var steps);

            result.Notes = LaneAssigner.Assign(planned, prefs, steps, tempo, out var laneStats);
            result.LaneStats = laneStats;

            var seenChordTimes = new HashSet<double>();
            foreach (var n in result.Notes)
            {
                if (n.IsHold) result.Holds++;
                if (!seenChordTimes.Add(n.Time)) result.Chords++;
                result.DurationSec = Math.Max(result.DurationSec, n.EndTime);
            }
            foreach (var p in PlayabilityRules.Check(result.Notes)) result.Warnings.Add(p);
            return result;
        }

        /// <summary>Yeterince uzun tutulan notalar hold; uzunluk üst sınırlı ve sonraki notadan önce biter.</summary>
        public static void AssignHolds(List<PlannedNote> planned, List<MelodyNote> source, TempoMap tempo, MelodyPreset preset)
        {
            for (int i = 0; i < planned.Count; i++)
            {
                var m = source[i];
                if (m.Duration < preset.HoldMinSec) continue;
                double t = planned[i].Time;
                double end = t + Math.Min(m.Duration, MaxHoldSec);
                if (i + 1 < planned.Count) end = Math.Min(end, planned[i + 1].Time - HoldReleaseGapSec);
                if (end - t < preset.HoldMinSec) continue;
                planned[i].HoldBeats = tempo.TimeToBeat(end) - planned[i].Beat;
            }
        }

        /// <summary>Aynı anda güçlü vurulan ikinci nota → 2'li akor (hold'lar hariç, en güçlüler, payla sınırlı).</summary>
        public static void AssignChords(List<PlannedNote> planned, List<MelodyNote> source, MelodyPreset preset)
        {
            if (planned.Count == 0) return;
            var vels = new List<int>();
            foreach (var m in source) vels.Add(m.Velocity);
            vels.Sort();
            int strong = vels[Math.Min(vels.Count - 1, (int)(vels.Count * ChordStrengthPercentile))];

            var candidates = new List<int>();
            for (int i = 0; i < planned.Count; i++)
            {
                var m = source[i];
                if (planned[i].HoldBeats > 0 || m.ChordPitch < 0) continue;
                if (m.Velocity >= strong && m.ChordVelocity >= strong) candidates.Add(i);
            }
            candidates.Sort((a, b) =>
            {
                int sa = source[a].Velocity + source[a].ChordVelocity, sb = source[b].Velocity + source[b].ChordVelocity;
                return sa != sb ? sb.CompareTo(sa) : a.CompareTo(b);
            });
            int max = (int)(planned.Count * preset.ChordShare);
            for (int k = 0; k < candidates.Count && k < max; k++) planned[candidates[k]].Chord = true;
        }
    }
}
