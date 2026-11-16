using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PixelBeat.EditorTools.BeatmapGen;
using PixelBeat.EditorTools.BeatmapGen.Melody;
using PixelBeat.EditorTools.BeatmapGen.Vocal;

namespace PixelBeat.Tests
{
    /// <summary>Vokal modu (GDD §7.2): kapı, tek ses, hece bölme, kaynak önceliği, geçiş yoğunluğu, vokal akorları.</summary>
    public class VocalGenTests
    {
        private const int Rate = 22050;

        // ---------- Vokal kapısı ----------

        /// <summary>Verilen aralıklarda yüksek gürültü, geri kalanında çok zayıf gürültü.</summary>
        private static float[] Bursts(double durationSec, float gain, params (double start, double end)[] loud)
        {
            var rnd = new Random(7);
            var mono = new float[(int)(durationSec * Rate)];
            for (int i = 0; i < mono.Length; i++) mono[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.001) * gain;
            foreach (var (s, e) in loud)
                for (int i = (int)(s * Rate); i < (int)(e * Rate) && i < mono.Length; i++)
                    mono[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.5) * gain;
            return mono;
        }

        [Test]
        public void Gate_FindsSections_MergesBreaths_DropsBlips()
        {
            var mono = Bursts(50, 1f, (2, 10), (11, 15), (20, 30), (40, 40.4));
            var sections = VocalGate.Detect(mono, Rate);
            Assert.AreEqual(2, sections.Count, string.Join(", ", sections));
            Assert.AreEqual(2 - 0.15, sections[0].Start, 0.06, "1 sn nefes birleşir");
            Assert.AreEqual(15 + 0.15, sections[0].End, 0.06);
            Assert.AreEqual(20 - 0.15, sections[1].Start, 0.06);
            Assert.AreEqual(30 + 0.15, sections[1].End, 0.06);
        }

        [Test]
        public void Gate_ThresholdIsRelativeToStemLevel()
        {
            var loud = VocalGate.Detect(Bursts(40, 1f, (5, 12), (20, 30)), Rate);
            var quiet = VocalGate.Detect(Bursts(40, 0.01f, (5, 12), (20, 30)), Rate);
            Assert.AreEqual(loud.Count, quiet.Count);
            for (int i = 0; i < loud.Count; i++)
            {
                Assert.AreEqual(loud[i].Start, quiet[i].Start, 1e-9);
                Assert.AreEqual(loud[i].End, quiet[i].End, 1e-9);
            }
        }

        [Test]
        public void FillRegions_OnlyInSilencesOfAtLeast4s_WithHalfSecondMargins()
        {
            var vocal = VocalGate.MergeClose(new[] { new Span(6, 60), new Span(63, 122), new Span(147, 196) }, VocalGenerator.FillMinSilenceSec);
            Assert.AreEqual(2, vocal.Count, "3 sn boşluk vokal bölgesine katılır");
            var fill = VocalGenerator.FillRegions(vocal, 222);
            Assert.AreEqual(3, fill.Count);
            Assert.AreEqual(0, fill[0].Start, 1e-9);
            Assert.AreEqual(5.5, fill[0].End, 1e-9);
            Assert.AreEqual(122.5, fill[1].Start, 1e-9);
            Assert.AreEqual(146.5, fill[1].End, 1e-9);
            Assert.AreEqual(196.5, fill[2].Start, 1e-9);
            Assert.AreEqual(222, fill[2].End, 1e-9);
        }

        // ---------- Tek ses / aykırılar ----------

        private static MidiNote N(double start, double dur, int pitch, int vel = 80) =>
            new MidiNote { Start = start, End = start + dur, Pitch = pitch, Velocity = vel };

        [Test]
        public void VocalLine_GateDropsNotesOutsideSections()
        {
            var notes = new List<MidiNote> { N(1, 0.2, 60), N(5, 0.2, 62), N(9.9, 0.2, 64), N(12, 0.2, 65) };
            var gated = VocalLine.Gate(notes, new[] { new Span(4, 10) });
            Assert.AreEqual(2, gated.Count);
            Assert.AreEqual(62, gated[0].Pitch);
            Assert.AreEqual(64, gated[1].Pitch);
        }

        [Test]
        public void VocalLine_SimultaneousNotes_KeepsClosestToPreviousPitch()
        {
            var notes = new List<MidiNote>
            {
                N(1.00, 0.2, 60, 50), N(1.02, 0.2, 48, 90), // ilk grup: en güçlü (48)
                N(1.50, 0.2, 50, 60), N(1.53, 0.2, 62, 100), // önceki 48 → 50 (62 daha güçlü ama uzak, oktav hatası)
                N(2.00, 0.2, 52, 70), N(2.01, 0.2, 52 + 12, 70),
            };
            var line = VocalLine.Monophonic(notes);
            Assert.AreEqual(3, line.Count);
            Assert.AreEqual(48, line[0].Pitch);
            Assert.AreEqual(50, line[1].Pitch);
            Assert.AreEqual(52, line[2].Pitch);
        }

        [Test]
        public void VocalLine_RemovesOctaveOutliers()
        {
            var line = new List<MelodyNote>();
            for (int i = 0; i < 10; i++) line.Add(new MelodyNote { Time = i * 0.4, Duration = 0.2, Pitch = 62 + i % 3, Velocity = 80 });
            line.Insert(5, new MelodyNote { Time = 1.9, Duration = 0.2, Pitch = 28, Velocity = 80 });
            var clean = VocalLine.RemoveOutliers(line);
            Assert.AreEqual(10, clean.Count);
            foreach (var n in clean) Assert.AreNotEqual(28, n.Pitch);
        }

        // ---------- Hece bölme ----------

        private static MelodyNote M(double time, double dur, int pitch = 64, int vel = 80) =>
            new MelodyNote { Time = time, Duration = dur, Pitch = pitch, Velocity = vel };

        [Test]
        public void Syllables_SplitAtInnerOnsets_IgnoreEdgesAndTooClose()
        {
            var line = new List<MelodyNote> { M(1.0, 1.0) };
            var onsets = new List<double> { 1.02, 1.40, 1.45, 1.75, 1.97 };
            var pieces = SyllableSplitter.Split(line, onsets, out int split);
            Assert.AreEqual(1, split);
            Assert.AreEqual(3, pieces.Count);
            Assert.AreEqual(1.0, pieces[0].Time, 1e-9);
            Assert.AreEqual(1.40, pieces[1].Time, 1e-9);
            Assert.AreEqual(1.75, pieces[2].Time, 1e-9);
            Assert.AreEqual(2.0, pieces[2].End, 1e-9);
            Assert.IsTrue(pieces[0].NoHold);
            Assert.IsTrue(pieces[1].NoHold);
            Assert.IsFalse(pieces[2].NoHold, "son parça gerçekten uzatılan hece olabilir");
        }

        [Test]
        public void Syllables_ShortNotesAndTinyLastPieceNotSplit()
        {
            var line = new List<MelodyNote> { M(1.0, 0.25), M(2.0, 1.0) };
            var pieces = SyllableSplitter.Split(line, new List<double> { 1.12, 2.5, 2.9 }, out int split);
            Assert.AreEqual(1, split);
            Assert.AreEqual(3, pieces.Count, "kısa nota bölünmez; 2.9 son parçayı 0.1 sn bırakır → kalkar");
            Assert.AreEqual(2.5, pieces[2].Time, 1e-9);
            Assert.AreEqual(3.0, pieces[2].End, 1e-9);
        }

        // ---------- Üretim: sentetik şarkı ----------

        private static List<double> Beats(double until, double period = 0.5)
        {
            var b = new List<double>();
            for (double t = 0; t < until; t += period) b.Add(t);
            return b;
        }

        /// <summary>
        /// 120 sn: vokal bölümleri 5–40, 42.5–70 (arada 2.5 sn nefes → boş kalmalı), 80–100. Vokal MIDI'sinde dışarıda sızıntı notalar var.
        /// Enstrüman hattı her yerde yoğun (8 nota/sn) ve akor adaylı.
        /// </summary>
        private static VocalInputs Song(double vocalNps = 3, double instrumentNps = 8, int seed = 1)
        {
            var rnd = new Random(seed);
            var sections = new List<Span> { new Span(5, 40), new Span(42.5, 70), new Span(80, 100) };
            var vocal = new List<MidiNote>();
            foreach (var s in sections)
            {
                int pitch = 64;
                for (double t = s.Start + 0.1; t < s.End - 0.3; t += 1.0 / vocalNps)
                {
                    pitch = Math.Max(57, Math.Min(74, pitch + rnd.Next(-3, 4)));
                    double dur = rnd.NextDouble() < 0.15 ? 0.8 : 0.2;
                    vocal.Add(N(t, Math.Min(dur, s.End - t - 0.05), pitch, 50 + rnd.Next(70)));
                }
            }
            foreach (double leak in new[] { 1.0, 41.0, 75.0, 110.0 }) vocal.Add(N(leak, 0.3, 66, 90));

            var instrument = new List<MelodyNote>();
            int ip = 70;
            for (double t = 0.2; t < 119.5; t += 1.0 / instrumentNps)
            {
                ip = Math.Max(60, Math.Min(84, ip + rnd.Next(-4, 5)));
                instrument.Add(new MelodyNote
                {
                    Time = t, Duration = rnd.NextDouble() < 0.1 ? 0.9 : 0.1, Pitch = ip, Velocity = 40 + rnd.Next(87),
                    ChordPitch = ip - 5, ChordVelocity = 40 + rnd.Next(87),
                });
            }

            return new VocalInputs
            {
                VocalMidi = vocal,
                Sections = sections,
                VocalOnsets = new List<double>(),
                InstrumentLine = instrument,
                BeatTimes = Beats(125),
                SongDurationSec = 120,
            };
        }

        private static readonly MelodyDifficulty[] All = { MelodyDifficulty.Easy, MelodyDifficulty.Normal, MelodyDifficulty.Hard };

        [Test]
        public void SourcePriority_NoInstrumentNotesInVocalRegionsOrShortGaps()
        {
            var input = Song();
            foreach (var d in All)
            {
                var r = VocalGenerator.Generate(input, MelodyPreset.For(d));
                CollectionAssert.IsEmpty(r.Map.Warnings, $"{d} denetim");
                Assert.AreEqual(r.Map.Notes.Count, r.Sources.Count);
                Assert.Greater(r.VocalNotes, 0);
                Assert.Greater(r.InstrumentNotes, 0, $"{d}: ≥ 4 sn susmalarda dolgu olmalı");

                // Vokal bölgeleri: bölümler + 4 sn'den kısa boşluklar (40–42.5 nefesi dahil) tek bölge.
                Assert.AreEqual(2, r.VocalRegions.Count, string.Join(", ", r.VocalRegions));

                int instrumentInVocal = 0;
                for (int i = 0; i < r.Map.Notes.Count; i++)
                {
                    var n = r.Map.Notes[i];
                    if (r.Sources[i] == NoteSource.Instrument)
                    {
                        foreach (var v in r.VocalRegions)
                            if (n.Time >= v.Start - VocalGenerator.BoundaryGapSec && n.Time <= v.End + VocalGenerator.BoundaryGapSec)
                                instrumentInVocal++;
                        // Dolgu notası (hold dahil) bir sonraki vokal bölgesine 0.5 sn'den fazla yaklaşmaz.
                        foreach (var v in r.VocalRegions)
                            if (n.Time < v.Start)
                                Assert.LessOrEqual(n.EndTime, v.Start - VocalGenerator.BoundaryGapSec + 1e-6, $"{d}: {n.Time:0.00} hold sınırı");
                    }
                    else
                    {
                        Assert.IsTrue(VocalGate.Inside(input.Sections, n.Time), $"{d}: vokal nota {n.Time:0.00} bölüm dışında (sızıntı)");
                    }
                }
                Assert.AreEqual(0, instrumentInVocal, $"{d}: vokal bölgesinde enstrüman kaynaklı nota");

                // 1.5–4 sn nefes boşluğu (40–42.5) boş: hiçbir nota başlamaz.
                foreach (var n in r.Map.Notes)
                    Assert.IsFalse(n.Time > 40.0 && n.Time < 42.5, $"{d}: nefes boşluğunda nota {n.Time:0.00}");
            }
        }

        [Test]
        public void Transitions_NoDensityJump()
        {
            var input = Song(vocalNps: 1.2, instrumentNps: 8, seed: 3);
            foreach (var d in All)
            {
                var r = VocalGenerator.Generate(input, MelodyPreset.For(d));
                Assert.Greater(r.Transitions.Count, 0);
                foreach (var (time, before, after) in r.Transitions)
                    Assert.LessOrEqual(Math.Abs(after - before), 0.75, $"{d}: {time:0.0} sn geçişi {before:0.00}→{after:0.00} nota/sn");
            }
        }

        [Test]
        public void HardChords_ComeFromVocalNoteItself_OnlyOnHard()
        {
            var input = Song();
            foreach (var d in All)
            {
                var r = VocalGenerator.Generate(input, MelodyPreset.For(d));
                var byTime = new Dictionary<double, List<int>>();
                for (int i = 0; i < r.Map.Notes.Count; i++)
                {
                    if (!byTime.TryGetValue(r.Map.Notes[i].Time, out var list)) byTime[r.Map.Notes[i].Time] = list = new List<int>();
                    list.Add(i);
                }

                int vocalChords = 0;
                foreach (var kv in byTime)
                {
                    if (kv.Value.Count < 2) continue;
                    Assert.AreEqual(2, kv.Value.Count);
                    Assert.AreEqual(r.Sources[kv.Value[0]], r.Sources[kv.Value[1]], "akorun iki notası aynı kaynaktan");
                    if (r.Sources[kv.Value[0]] == NoteSource.Vocal) vocalChords++;
                }
                if (d == MelodyDifficulty.Hard) Assert.Greater(vocalChords, 0, "Zor'da vokal akoru");
                else Assert.AreEqual(0, r.Map.Chords, $"{d}'da akor yok");
            }
        }

        [Test]
        public void SplitSyllables_AreTaps_LastPieceMayHold()
        {
            var input = Song(vocalNps: 1, seed: 5);
            input.VocalMidi = new List<MidiNote> { N(10.0, 1.2, 64), N(20.0, 0.3, 66) };
            input.Sections = new List<Span> { new Span(9, 21) };
            input.VocalOnsets = new List<double> { 10.4 };
            var r = VocalGenerator.Generate(input, MelodyPreset.For(MelodyDifficulty.Normal));
            Assert.AreEqual(1, r.SplitNotes);
            GeneratedNote first = null, second = null;
            foreach (var n in r.Map.Notes)
            {
                if (Math.Abs(n.Time - 10.0) < 1e-6) first = n;
                if (Math.Abs(n.Time - 10.4) < 1e-6) second = n;
            }
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.IsFalse(first.IsHold, "bölünen parça tap");
            Assert.IsTrue(second.IsHold, "uzatılan son hece hold");
        }

        [Test]
        public void Deterministic()
        {
            var a = VocalGenerator.Generate(Song(seed: 9), MelodyPreset.For(MelodyDifficulty.Hard));
            var b = VocalGenerator.Generate(Song(seed: 9), MelodyPreset.For(MelodyDifficulty.Hard));
            Assert.AreEqual(a.Map.Notes.Count, b.Map.Notes.Count);
            for (int i = 0; i < a.Map.Notes.Count; i++)
            {
                Assert.AreEqual(a.Map.Notes[i].Time, b.Map.Notes[i].Time);
                Assert.AreEqual(a.Map.Notes[i].Lane, b.Map.Notes[i].Lane);
            }
        }

        // ---------- Lumière (yerel dosyalar varsa) ----------

        [Test]
        public void Lumiere_GateAndSourcePriority()
        {
            const string stem = "Assets/Audio/Stems/lumiere_vocals.mp3";
            const string vocalMid = "Assets/Audio/Midi/lumiere_vocals.mid";
            const string mixMid = "Assets/Audio/Midi/lumiere_basicpitch.mid";
            if (!File.Exists(stem) || !File.Exists(vocalMid) || !File.Exists(mixMid)) Assert.Ignore("Lumière stem / MIDI yok (yerel test dosyaları).");

            var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(stem);
            clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) Assert.Ignore("Stem örnekleri okunamadı (Load Type = Decompress On Load olmalı; Beatmap Üretici bunu yapar).");

            BeatmapGeneratorWindow.AnalyzeVocalStem(data, clip.channels, clip.frequency, out var sections, out var onsets);
            Assert.AreEqual(2, sections.Count, string.Join(", ", sections));
            // Ölçüm: ~8–122 ve ~147–196 sn. Kapı 6.0 sn'deki giriş hecesini de yakalıyor.
            Assert.That(sections[0].Start, Is.InRange(5.0, 8.5));
            Assert.AreEqual(122, sections[0].End, 1.0);
            Assert.AreEqual(147, sections[1].Start, 1.0);
            Assert.AreEqual(196, sections[1].End, 1.0);

            var input = new VocalInputs
            {
                VocalMidi = MidiReader.Read(File.ReadAllBytes(vocalMid)).Notes,
                Sections = sections,
                VocalOnsets = onsets,
                InstrumentLine = MelodyExtractor.Extract(MidiReader.Read(File.ReadAllBytes(mixMid)).Notes),
                BeatTimes = Beats(230),
                SongDurationSec = clip.length,
            };
            foreach (var d in All)
            {
                var r = VocalGenerator.Generate(input, MelodyPreset.For(d));
                Assert.AreEqual(120, r.GatedOut, 25, "kapı dışı ~120 nota");
                CollectionAssert.IsEmpty(r.Map.Warnings, d.ToString());
                for (int i = 0; i < r.Map.Notes.Count; i++)
                {
                    if (r.Sources[i] != NoteSource.Instrument) continue;
                    foreach (var v in r.VocalRegions)
                        Assert.IsFalse(r.Map.Notes[i].Time >= v.Start - 0.5 && r.Map.Notes[i].Time <= v.End + 0.5,
                            $"{d}: vokal bölgesinde enstrüman notası {r.Map.Notes[i].Time:0.00}");
                }
            }
        }
    }
}
