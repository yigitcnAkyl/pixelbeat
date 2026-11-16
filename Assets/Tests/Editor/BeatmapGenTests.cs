using System;
using System.Collections.Generic;
using NUnit.Framework;
using PixelBeat.Core;
using PixelBeat.EditorTools.BeatmapGen;
using PixelBeat.Gameplay;

namespace PixelBeat.Tests
{
    /// <summary>Otomatik beatmap üreticinin analiz katmanı (sentetik sinyallerle).</summary>
    public class BeatmapGenTests
    {
        private const int Rate = 22050;

        // ---------- Sentetik sinyaller ----------

        /// <summary>Verilen anlarda kısa gürültü patlaması (perküsyon benzeri).</summary>
        private static float[] Clicks(IEnumerable<double> times, double durationSec, int seed = 1, float gain = 1f)
        {
            var mono = new float[(int)(durationSec * Rate)];
            var rnd = new Random(seed);
            foreach (double t in times)
            {
                int s = (int)(t * Rate);
                for (int i = 0; i < 220 && s + i < mono.Length; i++)
                    mono[s + i] += (float)((rnd.NextDouble() * 2 - 1) * Math.Exp(-i / 40.0) * gain);
            }
            return mono;
        }

        private static List<double> Grid(double start, double end, double period)
        {
            var list = new List<double>();
            for (double t = start; t < end; t += period) list.Add(t);
            return list;
        }

        /// <summary>Periyodu start'tan end'e doğrusal değişen tık zamanları (hızlanma / yavaşlama).</summary>
        private static List<double> Ramp(double t0, double duration, double bpmStart, double bpmEnd)
        {
            var list = new List<double>();
            double t = t0;
            while (t < t0 + duration)
            {
                list.Add(t);
                double frac = (t - t0) / duration;
                double bpm = bpmStart + (bpmEnd - bpmStart) * frac;
                t += 60.0 / bpm;
            }
            return list;
        }

        private static double NearestError(double t, IReadOnlyList<double> truth)
        {
            double best = double.MaxValue;
            foreach (double x in truth)
                if (Math.Abs(t - x) < Math.Abs(best)) best = t - x;
            return best;
        }

        // ---------- FFT ----------

        [Test]
        public void Fft_SinePeaksAtItsBin()
        {
            const int n = 1024;
            var re = new double[n];
            var im = new double[n];
            for (int i = 0; i < n; i++) re[i] = Math.Sin(2 * Math.PI * 37 * i / n);
            Fft.Transform(re, im);

            int peak = 0;
            double best = 0;
            for (int k = 0; k < n / 2; k++)
            {
                double m = re[k] * re[k] + im[k] * im[k];
                if (m > best) { best = m; peak = k; }
            }
            Assert.AreEqual(37, peak);
            Assert.AreEqual(n / 2.0, Math.Sqrt(best), 1e-6);
        }

        [Test]
        public void AudioPrep_StereoToMono_AndDownsamples()
        {
            var stereo = new float[44100 * 2]; // 1 sn, 44.1 kHz
            for (int i = 0; i < 44100; i++) { stereo[i * 2] = 1f; stereo[i * 2 + 1] = 0f; }
            var mono = AudioPrep.ToMonoDownsampled(stereo, 2, 44100, out int rate);
            Assert.AreEqual(22050, rate);
            Assert.AreEqual(22050, mono.Length);
            Assert.AreEqual(0.5f, mono[100], 1e-6);
        }

        // ---------- Onset ----------

        [Test]
        public void Onsets_FoundWithin10ms_NoExtras()
        {
            var truth = Grid(0.5, 19.5, 0.413);
            var a = SpectralAnalysis.Analyze(Clicks(truth, 20), Rate);
            var o = TempoTracker.NormalizeOnset(a.Flux, a.FrameRate);
            var onsets = OnsetPicker.Pick(a, o);

            Assert.AreEqual(truth.Count, onsets.Count);
            double sum = 0;
            foreach (var on in onsets)
            {
                double e = NearestError(on.Time, truth);
                Assert.Less(Math.Abs(e), 0.010, $"onset {on.Time:0.000}");
                sum += e;
            }
            Assert.Less(Math.Abs(sum / onsets.Count), 0.003, "ortalama sapma");
        }

        // ---------- Tempo / beat takibi ----------

        [TestCase(120.0, 0.0)]
        [TestCase(161.0, 161.0)]
        [TestCase(95.0, 0.0)]
        public void ConstantTempo_IsFound(double bpm, double hint)
        {
            var truth = Grid(0.3, 29.7, 60.0 / bpm);
            var a = SpectralAnalysis.Analyze(Clicks(truth, 30), Rate);
            var beats = BeatmapGenerator.TrackBeatTimes(a, hint, out _);

            var intervals = new List<double>();
            for (int i = 1; i < beats.Count; i++) intervals.Add(beats[i] - beats[i - 1]);
            intervals.Sort();
            double medianBpm = 60.0 / intervals[intervals.Count / 2];
            Assert.AreEqual(bpm, medianBpm, 1.0);
        }

        [Test]
        public void BpmHint_ResolvesDoubleTimeAmbiguity()
        {
            // 160 BPM'de vurgu her ikinci beat'te: ipucusuz 80 sanılabilir; ipucu 160'a çekmeli.
            var strong = Grid(0.3, 29.7, 60.0 / 80);
            var weak = Grid(0.3 + 60.0 / 160, 29.7, 60.0 / 80);
            var mono = Clicks(strong, 30, 1);
            var weakSig = Clicks(weak, 30, 2, 0.5f);
            for (int i = 0; i < mono.Length; i++) mono[i] += weakSig[i];

            var a = SpectralAnalysis.Analyze(mono, Rate);
            var beats = BeatmapGenerator.TrackBeatTimes(a, 160, out _);
            double medianBpm = 60.0 / ((beats[beats.Count - 1] - beats[0]) / (beats.Count - 1));
            Assert.AreEqual(160, medianBpm, 3.0);
        }

        [Test]
        public void VariableTempo_BeatsFollowRamp()
        {
            // 150 → 170 BPM, 40 sn
            var truth = Ramp(0.5, 39, 150, 170);
            var a = SpectralAnalysis.Analyze(Clicks(truth, 40), Rate);
            var beats = BeatmapGenerator.TrackBeatTimes(a, 160, out _);

            int matched = 0;
            foreach (double b in beats)
            {
                if (b < 2 || b > 38) continue; // kenarlar hariç
                Assert.Less(Math.Abs(NearestError(b, truth)), 0.020, $"beat {b:0.000}");
                matched++;
            }
            Assert.Greater(matched, 80);
        }

        // ---------- Izgaraya oturtma ----------

        [Test]
        public void Snap_ToQuarterGrid_WithLevels()
        {
            var tempo = TempoMap.FromBeatTimes(new List<double> { 1.0, 1.5, 2.0, 2.5, 3.0 });
            var onsets = new List<Onset>
            {
                new Onset { Time = 1.004, Strength = 2 },   // beat 0
                new Onset { Time = 1.248, Strength = 2 },   // beat 0.5
                new Onset { Time = 1.627, Strength = 2 },   // beat 1.25
                new Onset { Time = 1.630, Strength = 5 },   // aynı nokta, daha güçlü
                new Onset { Time = 0.5, Strength = 2 },     // haritadan önce: atlanır
            };
            var c = NoteSelector.Snap(onsets, tempo, 5);
            Assert.AreEqual(3, c.Count);
            Assert.AreEqual(0.0, c[0].Beat); Assert.AreEqual(1, c[0].Level);
            Assert.AreEqual(0.5, c[1].Beat); Assert.AreEqual(2, c[1].Level);
            Assert.AreEqual(1.25, c[2].Beat); Assert.AreEqual(4, c[2].Level);
            Assert.AreEqual(5, c[2].Strength, "aynı noktada en güçlüsü kalır");
            Assert.AreEqual(1.625, c[2].Time, 1e-9, "zaman ızgaraya oturur");
        }

        [Test]
        public void Select_RespectsSubdivisionAndDensity()
        {
            var cands = new List<PlannedNote>();
            for (int i = 0; i < 64 * 4; i++)
                cands.Add(new PlannedNote { Beat = i / 4.0, Time = i * 0.1, Strength = 1 + (i % 7), Level = i % 4 == 0 ? 1 : i % 2 == 0 ? 2 : 4 });

            var easy = NoteSelector.Select(cands, DifficultySettings.For(2));
            foreach (var n in easy) Assert.LessOrEqual(n.Level, 2, "kolayda 1/4 yok");

            var hard = NoteSelector.Select(cands, DifficultySettings.For(10));
            Assert.Greater(hard.Count, easy.Count);
            bool anyQuarter = false;
            foreach (var n in hard) anyQuarter |= n.Level == 4;
            Assert.IsTrue(anyQuarter, "zorda 1/4 olabilir");

            for (int i = 1; i < easy.Count; i++)
                Assert.GreaterOrEqual(easy[i].Time - easy[i - 1].Time, DifficultySettings.For(2).MinGapSec - 1e-9);
        }

        // ---------- Hold ----------

        [Test]
        public void Hold_SustainedTone_vs_ShortClick()
        {
            // 0.5 sn'de 1.2 sn süren 220 Hz ton (bas bant), 3.0 sn'de kısa tık.
            var mono = Clicks(new[] { 3.0 }, 5);
            for (int i = (int)(0.5 * Rate); i < (int)(1.7 * Rate); i++)
                mono[i] += (float)(0.8 * Math.Sin(2 * Math.PI * 220 * i / Rate));

            var a = SpectralAnalysis.Analyze(mono, Rate);
            var o = TempoTracker.NormalizeOnset(a.Flux, a.FrameRate);
            var onsets = OnsetPicker.Pick(a, o);

            var tone = new PlannedNote { Time = 0.5, Band = 0, Strength = 3 };
            var click = new PlannedNote { Time = 3.0, Band = 2, Strength = 3 };
            int idx = 0;
            double toneSec = NoteSelector.SustainSeconds(tone, a, onsets, ref idx);
            idx = 0;
            double clickSec = NoteSelector.SustainSeconds(click, a, onsets, ref idx);

            Assert.AreEqual(1.2, toneSec, 0.1, "ton süresi");
            Assert.Less(clickSec, 0.15, "tık kısa");
        }

        [Test]
        public void AssignHolds_LimitsShareAndQuantizes()
        {
            var tempo = TempoMap.Constant(120, 0);
            var mono = new float[Rate * 12];
            for (int i = 0; i < mono.Length; i++) mono[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 110 * i / Rate)); // hep süren bas
            var a = SpectralAnalysis.Analyze(mono, Rate);

            var notes = new List<PlannedNote>();
            for (int b = 0; b < 20; b++) notes.Add(new PlannedNote { Beat = b, Time = tempo.BeatToTime(b), Band = 0, Strength = 1 });
            NoteSelector.AssignHolds(notes, a, new List<Onset>(), tempo, DifficultySettings.For(5));

            int holds = 0;
            foreach (var n in notes)
            {
                if (n.HoldBeats <= 0) continue;
                holds++;
                Assert.GreaterOrEqual(n.HoldBeats, NoteSelector.MinHoldBeats);
                Assert.LessOrEqual(n.HoldBeats, NoteSelector.MaxHoldBeats);
                Assert.AreEqual(0, (n.HoldBeats * 2) % 1, 1e-9, "1/2 beat'e yuvarlı");
            }
            Assert.LessOrEqual(holds, (int)Math.Floor(20 * DifficultySettings.For(5).HoldShare));
            Assert.Greater(holds, 0);
        }

        // ---------- Şerit dağıtımı ----------

        private static List<PlannedNote> RandomPlan(int seed, int count, double avgGapSec)
        {
            var rnd = new Random(seed);
            var tempo = TempoMap.Constant(160, 0);
            var list = new List<PlannedNote>();
            double beat = 0;
            for (int i = 0; i < count; i++)
            {
                beat += Math.Max(0.25, Math.Round(rnd.NextDouble() * avgGapSec * 2 / tempo.AverageSecPerBeat * 4) / 4);
                list.Add(new PlannedNote
                {
                    Beat = beat,
                    Time = tempo.BeatToTime(beat),
                    Strength = rnd.NextDouble() * 5,
                    Centroid = rnd.NextDouble() < 0.7 ? 300 : 3000, // yığılmayı zorla
                    Level = beat % 1 == 0 ? 1 : 2,
                    Chord = rnd.NextDouble() < 0.2,
                    HoldBeats = rnd.NextDouble() < 0.15 ? 1 + Math.Floor(rnd.NextDouble() * 6) / 2 : 0,
                });
            }
            return list;
        }

        [Test]
        public void LaneAssigner_RandomPlans_AlwaysPlayableWithTwoThumbs()
        {
            var tempo = TempoMap.Constant(160, 0);
            for (int seed = 1; seed <= 300; seed++)
            {
                var plan = RandomPlan(seed, 120, 0.2);
                var notes = LaneAssigner.Assign(plan, tempo, out _);
                var problems = PlayabilityRules.Check(notes);
                Assert.IsEmpty(problems, $"seed {seed}: {(problems.Count > 0 ? problems[0] : "")}");

                // Oyunun kendi doğrulaması da temiz olmalı (hold sürerken aynı şeride nota yok).
                var map = new Beatmap { bpm = 160 };
                foreach (var n in notes)
                    map.notes.Add(new BeatmapNote { lane = n.Lane, beat = (float)n.Beat, lengthBeats = (float)n.LengthBeats });
                CollectionAssert.IsEmpty(NoteChart.FromBeatmap(map, logWarnings: false).Validate(), $"seed {seed}");
            }
        }

        [Test]
        public void LaneAssigner_ChordsAreTwoHands_AndNoChordDuringHold()
        {
            var tempo = TempoMap.Constant(120, 0);
            var plan = new List<PlannedNote>
            {
                new PlannedNote { Beat = 0, Time = 0.0, Chord = true, Centroid = 500 },
                new PlannedNote { Beat = 2, Time = 1.0, HoldBeats = 4, Centroid = 500 },      // 1.0 .. 3.0
                new PlannedNote { Beat = 4, Time = 2.0, Chord = true, Centroid = 500 },       // hold sürerken → tek
                new PlannedNote { Beat = 8, Time = 4.0, Chord = true, Centroid = 500 },
            };
            var notes = LaneAssigner.Assign(plan, tempo, out var stats);

            Assert.AreEqual(6, notes.Count); // 2 + 1 + 1 + 2
            Assert.AreNotEqual(LaneAssigner.HandOf(notes[0].Lane), LaneAssigner.HandOf(notes[1].Lane));
            Assert.IsTrue(notes[2].IsHold);
            Assert.AreNotEqual(LaneAssigner.HandOf(notes[2].Lane), LaneAssigner.HandOf(notes[3].Lane), "hold sırasında diğer el");
            Assert.AreEqual(1, stats.DemotedChords);
            CollectionAssert.IsEmpty(PlayabilityRules.Check(notes));
        }

        [Test]
        public void LaneAssigner_NoLongSameLaneRuns_EvenWithConstantPitch()
        {
            var tempo = TempoMap.Constant(100, 0);
            var plan = new List<PlannedNote>();
            for (int i = 0; i < 40; i++) plan.Add(new PlannedNote { Beat = i, Time = tempo.BeatToTime(i), Centroid = 200 });
            var notes = LaneAssigner.Assign(plan, tempo, out _);
            Assert.AreEqual(40, notes.Count);
            CollectionAssert.IsEmpty(PlayabilityRules.Check(notes));
        }

        [Test]
        public void PlayabilityRules_DetectViolations()
        {
            var bad = new List<GeneratedNote>
            {
                new GeneratedNote { Lane = 0, Beat = 0, Time = 0, EndTime = 0 },
                new GeneratedNote { Lane = 1, Beat = 0, Time = 0, EndTime = 0 },   // akor aynı elde
                new GeneratedNote { Lane = 2, Beat = 0, Time = 0, EndTime = 0 },   // 3'lü
            };
            Assert.IsNotEmpty(PlayabilityRules.Check(bad));

            var holdConflict = new List<GeneratedNote>
            {
                new GeneratedNote { Lane = 3, Beat = 0, LengthBeats = 2, Time = 0, EndTime = 1 },
                new GeneratedNote { Lane = 3, Beat = 2, Time = 1, EndTime = 1 }, // bitişte aynı şerit
            };
            Assert.IsNotEmpty(PlayabilityRules.Check(holdConflict));
        }

        // ---------- Uçtan uca ----------

        [Test]
        public void EndToEnd_ClickTrack_ProducesPlayableMapOnBeats()
        {
            var truth = Ramp(0.5, 29, 150, 170);
            var mono = Clicks(truth, 30);
            var result = BeatmapGenerator.GenerateFromMono(mono, Rate, new GeneratorSettings { Difficulty = 6, BpmHint = 160 });

            Assert.Greater(result.Notes.Count, 20);
            CollectionAssert.IsEmpty(result.Warnings);
            foreach (var n in result.Notes)
                Assert.Less(Math.Abs(NearestError(n.Time, truth)), 0.025, $"nota {n.Time:0.000} bir tıka denk gelmeli");
        }
    }
}
