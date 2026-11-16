using System;
using System.Collections.Generic;
using PixelBeat.Core;

namespace PixelBeat.EditorTools.BeatmapGen
{
    public sealed class GeneratorSettings
    {
        /// <summary>1–10 (nota yoğunluğu).</summary>
        public int Difficulty = 6;
        /// <summary>Tahmini tempo ipucu (BPM); 0 = otomatik.</summary>
        public double BpmHint;
    }

    public sealed class GeneratorResult
    {
        public List<double> BeatTimes = new List<double>();
        public List<GeneratedNote> Notes = new List<GeneratedNote>();
        public double DurationSec;
        public double MedianBpm, MinBpm, MaxBpm;
        public int OnsetCount;
        public int CandidateCount;
        public int Holds;
        public int Chords;
        public LaneAssignStats LaneStats;
        public List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Ses → beatmap (GDD §7). Unity'ye bağımlı değil:
    /// mono/22 kHz → spektral akı → tempo + değişken tempolu beat takibi → onset'ler → 1/4 ızgaraya oturtma →
    /// zorluğa göre seçim → hold / akor → iki parmakla oynanabilir şerit dağıtımı → denetim.
    /// </summary>
    public static class BeatmapGenerator
    {
        /// <summary>Beat takibinde periyottan sapma cezası (büyük = daha düzenli, tempo değişimine daha yavaş uyum).</summary>
        public const double DefaultTightness = 100;

        public static GeneratorResult Generate(float[] interleaved, int channels, int sampleRate, GeneratorSettings settings,
            Action<float, string> progress = null)
        {
            progress?.Invoke(0f, "Ses hazırlanıyor");
            var mono = AudioPrep.ToMonoDownsampled(interleaved, channels, sampleRate, out int rate);
            return GenerateFromMono(mono, rate, settings, progress);
        }

        public static GeneratorResult GenerateFromMono(float[] mono, int sampleRate, GeneratorSettings settings,
            Action<float, string> progress = null)
        {
            var result = new GeneratorResult { DurationSec = mono.Length / (double)sampleRate };
            var diff = DifficultySettings.For(settings.Difficulty);

            progress?.Invoke(0.05f, "Spektrum");
            var a = SpectralAnalysis.Analyze(mono, sampleRate, p => progress?.Invoke(0.05f + 0.5f * p, "Spektrum"));
            if (a.FrameCount < 16)
            {
                result.Warnings.Add("Ses çok kısa.");
                return result;
            }

            progress?.Invoke(0.6f, "Tempo ve beat takibi");
            var beatTimes = TrackBeatTimes(a, settings.BpmHint, out var o);
            result.BeatTimes = beatTimes;
            if (beatTimes.Count < 2)
            {
                result.Warnings.Add("Beat bulunamadı.");
                return result;
            }
            FillTempoStats(result);
            var tempo = TempoMap.FromBeatTimes(beatTimes);

            progress?.Invoke(0.75f, "Onset'ler");
            var onsets = OnsetPicker.Pick(a, o);
            result.OnsetCount = onsets.Count;

            progress?.Invoke(0.85f, "Notalar");
            var candidates = NoteSelector.Snap(onsets, tempo, beatTimes.Count);
            result.CandidateCount = candidates.Count;
            var planned = NoteSelector.Select(candidates, diff);
            NoteSelector.AssignHolds(planned, a, onsets, tempo, diff);
            NoteSelector.AssignChords(planned, diff);

            progress?.Invoke(0.95f, "Şeritler");
            result.Notes = LaneAssigner.Assign(planned, tempo, out var laneStats);
            result.LaneStats = laneStats;

            var seenChordTimes = new HashSet<double>();
            foreach (var n in result.Notes)
            {
                if (n.IsHold) result.Holds++;
                if (!seenChordTimes.Add(n.Time)) result.Chords++;
            }

            foreach (var p in PlayabilityRules.Check(result.Notes)) result.Warnings.Add(p);
            progress?.Invoke(1f, "Bitti");
            return result;
        }

        /// <summary>Beat zamanları (sn, kesin artan). o: normalleştirilmiş onset eğrisi.</summary>
        public static List<double> TrackBeatTimes(SpectralAnalysis a, double bpmHint, out double[] o, double tightness = DefaultTightness)
        {
            o = TempoTracker.NormalizeOnset(a.Flux, a.FrameRate);
            double center = bpmHint > 0 ? bpmHint : 120;
            double sigma = bpmHint > 0 ? 0.3 : 1.0;
            double globalPeriod = TempoTracker.EstimatePeriod(o, a.FrameRate, center, sigma);
            var periods = TempoTracker.LocalPeriods(o, a.FrameRate, globalPeriod);
            var beatFrames = TempoTracker.TrackBeats(o, periods, tightness);

            var times = new List<double>();
            foreach (double f in beatFrames)
            {
                double t = a.FrameToTime(f);
                if (times.Count == 0 || t > times[times.Count - 1] + 1e-4) times.Add(t);
            }
            return times;
        }

        private static void FillTempoStats(GeneratorResult r)
        {
            var bpms = new List<double>();
            for (int i = 1; i < r.BeatTimes.Count; i++) bpms.Add(60.0 / (r.BeatTimes[i] - r.BeatTimes[i - 1]));
            if (bpms.Count == 0) return;
            bpms.Sort();
            r.MedianBpm = bpms[bpms.Count / 2];
            r.MinBpm = bpms[(int)(bpms.Count * 0.05)];
            r.MaxBpm = bpms[Math.Min(bpms.Count - 1, (int)(bpms.Count * 0.95))];
        }
    }
}
