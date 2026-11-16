using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PixelBeat.Core;
using PixelBeat.EditorTools.BeatmapGen;
using PixelBeat.EditorTools.BeatmapGen.Melody;

namespace PixelBeat.Tests
{
    /// <summary>Melodi tabanlı beatmap üretimi (GDD §7.1): MIDI okuma, melodi çıkarma, seyreltme, kontur şeritleri, hold.</summary>
    public class MelodyGenTests
    {
        private const string LumiereMidi = "Assets/Audio/Midi/lumiere_basicpitch.mid";

        // ---------- MIDI üretici (testler için) ----------

        private sealed class TrackBuilder
        {
            public readonly List<byte> Bytes = new List<byte>();

            public TrackBuilder Event(int delta, params int[] data)
            {
                Vlq(Bytes, delta);
                foreach (int b in data) Bytes.Add((byte)b);
                return this;
            }

            public TrackBuilder Tempo(int delta, double bpm)
            {
                int us = (int)Math.Round(60e6 / bpm);
                return Event(delta, 0xFF, 0x51, 0x03, (us >> 16) & 0xFF, (us >> 8) & 0xFF, us & 0xFF);
            }

            public TrackBuilder End() => Event(0, 0xFF, 0x2F, 0x00);
        }

        private static void Vlq(List<byte> o, int v)
        {
            var stack = new Stack<byte>();
            stack.Push((byte)(v & 0x7F));
            while ((v >>= 7) > 0) stack.Push((byte)((v & 0x7F) | 0x80));
            o.AddRange(stack);
        }

        private static byte[] Midi(int format, int division, params TrackBuilder[] tracks)
        {
            var o = new List<byte> { (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6 };
            o.Add((byte)(format >> 8)); o.Add((byte)format);
            o.Add((byte)(tracks.Length >> 8)); o.Add((byte)tracks.Length);
            o.Add((byte)(division >> 8)); o.Add((byte)division);
            foreach (var t in tracks)
            {
                o.AddRange(new[] { (byte)'M', (byte)'T', (byte)'r', (byte)'k' });
                int len = t.Bytes.Count;
                o.Add((byte)(len >> 24)); o.Add((byte)(len >> 16)); o.Add((byte)(len >> 8)); o.Add((byte)len);
                o.AddRange(t.Bytes);
            }
            return o.ToArray();
        }

        // ---------- MidiReader ----------

        [Test]
        public void MidiReader_ReadsExtremePitchesAndVelocitiesExactly()
        {
            // Format 1: tempo izi + nota izi. 480 ppq, 120 BPM → 1 tick = 1/960 sn.
            var tempo = new TrackBuilder().Tempo(0, 120).End();
            var notes = new TrackBuilder();
            int[] pitches = { 0, 21, 28, 98, 108, 127 };
            int[] vels = { 1, 19, 43, 113, 64, 127 };
            for (int i = 0; i < pitches.Length; i++)
            {
                notes.Event(i == 0 ? 0 : 480, 0x90, pitches[i], vels[i]);
                notes.Event(240, 0x80, pitches[i], 0);
            }
            notes.End();

            var file = MidiReader.Read(Midi(1, 480, tempo, notes));
            Assert.AreEqual(1, file.Format);
            Assert.AreEqual(pitches.Length, file.Notes.Count);
            for (int i = 0; i < pitches.Length; i++)
            {
                Assert.AreEqual(pitches[i], file.Notes[i].Pitch, $"nota {i} perdesi");
                Assert.AreEqual(vels[i], file.Notes[i].Velocity, $"nota {i} velocity");
                Assert.AreEqual(i * 0.75, file.Notes[i].Start, 1e-9);
                Assert.AreEqual(0.25, file.Notes[i].Duration, 1e-9);
            }
        }

        [Test]
        public void MidiReader_RunningStatusAndVelocityZeroNoteOff()
        {
            var t = new TrackBuilder()
                .Event(0, 0x90, 60, 100) // note-on
                .Event(0, 64, 90)        // running status: note-on 64
                .Event(480, 60, 0)       // running status, velocity 0 = note-off
                .Event(480, 64, 0)
                .End();
            var file = MidiReader.Read(Midi(0, 480, t));
            Assert.AreEqual(2, file.Notes.Count);
            Assert.AreEqual(60, file.Notes[0].Pitch);
            Assert.AreEqual(0.5, file.Notes[0].End, 1e-9);
            Assert.AreEqual(64, file.Notes[1].Pitch);
            Assert.AreEqual(90, file.Notes[1].Velocity);
            Assert.AreEqual(1.0, file.Notes[1].End, 1e-9);
        }

        [Test]
        public void MidiReader_TempoChange_ConvertsTicksPiecewise()
        {
            // 0–960 tick 120 BPM (1 sn), sonra 60 BPM: 1440. tick = 1 + 480/480 sn = 2 sn.
            var tempo = new TrackBuilder().Tempo(0, 120).Tempo(960, 60).End();
            var notes = new TrackBuilder().Event(1440, 0x90, 70, 80).Event(480, 0x80, 70, 0).End();
            var file = MidiReader.Read(Midi(1, 480, tempo, notes));
            Assert.AreEqual(1, file.Notes.Count);
            Assert.AreEqual(2.0, file.Notes[0].Start, 1e-9);
            Assert.AreEqual(3.0, file.Notes[0].End, 1e-9);
            Assert.AreEqual(2, file.Tempos.Count);
            Assert.AreEqual(1.0, file.Tempos[1].Key, 1e-9);
            Assert.AreEqual(60, file.Tempos[1].Value, 1e-6);
        }

        [Test]
        public void MidiReader_SkipsDrumsAndClosesOpenNotesAtTrackEnd()
        {
            var t = new TrackBuilder()
                .Event(0, 0x99, 36, 100).Event(100, 0x89, 36, 0) // kanal 10 davul
                .Event(0, 0x90, 72, 80)                          // kapanmayan nota
                .Event(380, 0xFF, 0x2F, 0x00);
            var file = MidiReader.Read(Midi(0, 480, t));
            Assert.AreEqual(1, file.Notes.Count);
            Assert.AreEqual(72, file.Notes[0].Pitch);
            Assert.AreEqual(0.5, file.Notes[0].End, 1e-9);
            Assert.AreEqual(2, MidiReader.Read(Midi(0, 480, t), includeDrums: true).Notes.Count);
        }

        [Test]
        public void MidiReader_RejectsNonMidi()
        {
            Assert.Throws<FormatException>(() => MidiReader.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }));
        }

        [Test]
        public void MidiReader_LumiereFile_MatchesReferenceReader()
        {
            if (!File.Exists(LumiereMidi)) Assert.Ignore($"{LumiereMidi} yok (yerel test dosyası).");
            var file = MidiReader.Read(File.ReadAllBytes(LumiereMidi));
            // pretty_midi ile aynı: 2181 nota, perde 28–98.
            Assert.AreEqual(2181, file.Notes.Count);
            int min = 127, max = 0;
            foreach (var n in file.Notes)
            {
                min = Math.Min(min, n.Pitch);
                max = Math.Max(max, n.Pitch);
            }
            Assert.AreEqual(28, min);
            Assert.AreEqual(98, max);
            Assert.AreEqual(0.232, file.Notes[0].Start, 0.001);
            Assert.AreEqual(47, file.Notes[0].Pitch);
        }

        // ---------- Melodi çıkarma ----------

        private static MidiNote N(double start, double dur, int pitch, int vel = 80) =>
            new MidiNote { Start = start, End = start + dur, Pitch = pitch, Velocity = vel };

        [Test]
        public void Skyline_TakesHighestOfSimultaneousNotes_AtItsOwnTime()
        {
            var notes = new List<MidiNote> { N(1.000, 0.3, 60), N(1.020, 0.3, 64), N(1.040, 0.3, 67, 90), N(1.200, 0.3, 69) };
            var line = MelodyExtractor.Extract(notes);
            Assert.AreEqual(2, line.Count);
            Assert.AreEqual(67, line[0].Pitch);
            Assert.AreEqual(1.040, line[0].Time, 1e-9);
            Assert.AreEqual(64, line[0].ChordPitch, "en güçlü ikinci nota (≥ 3 yarım ses aşağıda) akor adayı");
            Assert.AreEqual(69, line[1].Pitch);
        }

        [Test]
        public void Skyline_NotesBeyond60msAreSeparate()
        {
            var notes = new List<MidiNote> { N(1.0, 0.05 + 0.1, 64), N(1.07, 0.3, 65) };
            var line = MelodyExtractor.Skyline(notes, new MelodyExtractSettings());
            Assert.AreEqual(2, line.Count);
        }

        [Test]
        public void Extract_RemovesAccompanimentUnderSustainedMelody()
        {
            var notes = new List<MidiNote> { N(1.0, 1.0, 72), N(1.1, 0.3, 67), N(1.7, 0.3, 66) };
            var line = MelodyExtractor.Extract(notes);
            CollectionAssert.AreEqual(new[] { 72, 66 }, Pitches(line), "67 tiz notanın gölgesinde, 66 gölge (0.5 sn) bittikten sonra");
        }

        [Test]
        public void Extract_RemovesGhostsLowPitchesAndOrnaments()
        {
            var notes = new List<MidiNote>
            {
                N(1.0, 0.3, 64), N(1.5, 0.3, 65), N(2.0, 0.3, 67), N(2.5, 0.3, 65),
                N(2.8, 0.04, 70),        // çok kısa → hayalet
                N(3.0, 0.3, 66, 20),     // çevresine göre çok zayıf → hayalet
                N(3.5, 0.3, 47),         // alt perde sınırı (48) altında
                N(4.0, 0.09, 62),        // süsleme: kısa, 0.1 sn sonra 2 yarım ses yukarı
                N(4.1, 0.3, 64),
                N(4.6, 0.3, 48),         // sınırda: kalır mı? register filtresi atar (64'ün 7'den fazla altı)
            };
            var line = MelodyExtractor.Extract(notes);
            CollectionAssert.AreEqual(new[] { 64, 65, 67, 65, 64 }, Pitches(line));
        }

        [Test]
        public void Extract_MinPitchBoundaryIsInclusive()
        {
            var s = new MelodyExtractSettings { MinPitch = 48, RegisterDropSemitones = 127 };
            var line = MelodyExtractor.Extract(new List<MidiNote> { N(1.0, 0.3, 47), N(2.0, 0.3, 48) }, s);
            CollectionAssert.AreEqual(new[] { 48 }, Pitches(line));
        }

        [Test]
        public void Extract_DropsBassFarBelowLocalMelody()
        {
            var notes = new List<MidiNote>();
            for (int i = 0; i < 10; i++) notes.Add(N(i * 0.5, 0.2, 72 + i % 3));
            notes.Add(N(2.25, 0.2, 55)); // melodi arasında tek başına bas
            var line = MelodyExtractor.Extract(notes);
            CollectionAssert.DoesNotContain(Pitches(line), 55);
            Assert.AreEqual(10, line.Count);
        }

        private static int[] Pitches(List<MelodyNote> line)
        {
            var p = new int[line.Count];
            for (int i = 0; i < p.Length; i++) p[i] = line[i].Pitch;
            return p;
        }

        // ---------- Seyreltme ----------

        private static List<MelodyNote> DenseMelody(double seconds, double nps, int seed)
        {
            var rnd = new Random(seed);
            var line = new List<MelodyNote>();
            int pitch = 64;
            for (double t = 0.5; t < seconds; t += 1.0 / nps)
            {
                pitch = Math.Max(55, Math.Min(80, pitch + rnd.Next(-4, 5)));
                line.Add(new MelodyNote { Time = t, Duration = 0.9 / nps + rnd.NextDouble() * 0.2, Pitch = pitch, Velocity = 40 + rnd.Next(80) });
            }
            return line;
        }

        [Test]
        public void Thin_HitsTargetDensity_AndKeepsMinGap()
        {
            var line = DenseMelody(60, 10, 3);
            foreach (var d in new[] { MelodyDifficulty.Easy, MelodyDifficulty.Normal, MelodyDifficulty.Hard })
            {
                var p = MelodyPreset.For(d);
                var thin = MelodyThinner.Thin(line, p.TargetNps, p.MinGapSec);
                double nps = thin.Count / (line[line.Count - 1].Time - line[0].Time);
                Assert.AreEqual(p.TargetNps, nps, p.TargetNps * 0.15, $"{d} yoğunluk");
                for (int i = 1; i < thin.Count; i++)
                    Assert.GreaterOrEqual(thin[i].Time - thin[i - 1].Time, p.MinGapSec - 1e-9, $"{d} min boşluk");
            }
        }

        [Test]
        public void Thin_AlwaysKeepsPhraseStarts()
        {
            var line = DenseMelody(10, 8, 5);
            // 2 sn sessizlik sonra zayıf, kısa bir cümle başı + yoğun güçlü notalar.
            var weak = new MelodyNote { Time = 12.0, Duration = 0.1, Pitch = 60, Velocity = 10 };
            line.Add(weak);
            for (int i = 1; i < 20; i++) line.Add(new MelodyNote { Time = 12.0 + i * 0.125, Duration = 0.1, Pitch = 70, Velocity = 120 });

            var thin = MelodyThinner.Thin(line, 1.5, 0.3);
            Assert.Contains(weak, thin);
            Assert.IsTrue(weak.PhraseStart);
            Assert.AreSame(line[0], thin[0], "ilk nota her zaman cümle başı");
        }

        [Test]
        public void Thin_IsDeterministic()
        {
            var a = MelodyThinner.Thin(DenseMelody(40, 7, 9), 2.5, 0.2);
            var b = MelodyThinner.Thin(DenseMelody(40, 7, 9), 2.5, 0.2);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].Time, b[i].Time);
        }

        // ---------- Kontur şeritleri ----------

        private static List<double> Beats(int count, double period = 0.5, double start = 0)
        {
            var b = new List<double>();
            for (int i = 0; i < count; i++) b.Add(start + i * period);
            return b;
        }

        private static List<MelodyNote> Line(double step, params int[] pitches)
        {
            var line = new List<MelodyNote>();
            for (int i = 0; i < pitches.Length; i++)
                line.Add(new MelodyNote { Time = 1.0 + i * step, Duration = 0.2, Pitch = pitches[i], Velocity = 80 });
            return line;
        }

        private static List<GeneratedNote> Generate(List<MelodyNote> line, MelodyDifficulty d = MelodyDifficulty.Hard,
            double offset = 0, List<double> beats = null)
        {
            var r = MelodyGenerator.Generate(line, beats ?? Beats(400), MelodyPreset.For(d), offset);
            CollectionAssert.IsEmpty(r.Warnings, "denetim");
            return r.Notes;
        }

        [Test]
        public void Contour_RisingScale_LanesNeverGoBack()
        {
            var notes = Generate(Line(0.35, 60, 62, 64, 65, 67));
            Assert.AreEqual(5, notes.Count);
            for (int i = 1; i < notes.Count; i++) Assert.GreaterOrEqual(notes[i].Lane, notes[i - 1].Lane, $"nota {i}");
            Assert.LessOrEqual(notes[0].Lane, 1);
            Assert.GreaterOrEqual(notes[notes.Count - 1].Lane, 2);
        }

        [Test]
        public void Contour_FallingScale_LanesNeverGoForward()
        {
            var notes = Generate(Line(0.35, 72, 71, 69, 67, 65));
            Assert.LessOrEqual(notes[notes.Count - 1].Lane, 1);
            Assert.GreaterOrEqual(notes[0].Lane, 2);
            for (int i = 1; i < notes.Count; i++) Assert.LessOrEqual(notes[i].Lane, notes[i - 1].Lane, $"nota {i}");
        }

        [Test]
        public void Contour_RepeatedPitch_SameOrNeighbourLane_NoLongRuns()
        {
            var notes = Generate(Line(0.35, 64, 67, 67, 67, 67, 67, 62));
            for (int i = 2; i < 6; i++) Assert.LessOrEqual(Math.Abs(notes[i].Lane - notes[i - 1].Lane), 1, $"nota {i}");
        }

        [Test]
        public void Contour_BigLeap_GoesToFarLane()
        {
            var notes = Generate(Line(0.4, 60, 62, 60, 62, 74, 60, 62, 74));
            Assert.GreaterOrEqual(notes[4].Lane - notes[3].Lane, 2, "60→74 sıçrama");
            Assert.LessOrEqual(notes[5].Lane - notes[4].Lane, -2, "74→60 düşüş");
        }

        [Test]
        public void Contour_SlidingWindow_UsesAllLanesInEachRegister()
        {
            // İlk 20 sn kalın register (50–57), sonraki 20 sn ince (72–79): ikisinde de 4 şerit kullanılmalı.
            var rnd = new Random(11);
            var pitches = new List<int>();
            for (int i = 0; i < 80; i++) pitches.Add((i < 40 ? 50 : 72) + rnd.Next(8));
            var notes = Generate(Line(0.5, pitches.ToArray()));
            var low = new HashSet<int>();
            var high = new HashSet<int>();
            foreach (var n in notes) (n.Time < 21 ? low : high).Add(n.Lane);
            Assert.AreEqual(4, low.Count, "kalın bölüm");
            Assert.AreEqual(4, high.Count, "ince bölüm");
        }

        // ---------- Hold / zamanlama / oynanabilirlik ----------

        [Test]
        public void Holds_LongNotesBecomeHolds_CappedAndReleasedBeforeNext()
        {
            var line = new List<MelodyNote>
            {
                new MelodyNote { Time = 1.0, Duration = 0.6, Pitch = 64, Velocity = 80 }, // hold
                new MelodyNote { Time = 2.0, Duration = 0.3, Pitch = 65, Velocity = 80 }, // tap
                new MelodyNote { Time = 3.0, Duration = 5.0, Pitch = 67, Velocity = 80 }, // 2 sn ile sınırlı
                new MelodyNote { Time = 6.0, Duration = 1.0, Pitch = 65, Velocity = 80 }, // sonraki notadan 0.15 sn önce biter
                new MelodyNote { Time = 6.7, Duration = 0.3, Pitch = 64, Velocity = 80 },
            };
            var notes = Generate(line, MelodyDifficulty.Normal);
            Assert.AreEqual(5, notes.Count);
            Assert.IsTrue(notes[0].IsHold);
            Assert.AreEqual(1.6, notes[0].EndTime, 1e-6);
            Assert.IsFalse(notes[1].IsHold);
            Assert.AreEqual(3.0 + MelodyGenerator.MaxHoldSec, notes[2].EndTime, 1e-6);
            Assert.AreEqual(6.7 - MelodyGenerator.HoldReleaseGapSec, notes[3].EndTime, 1e-6);
        }

        [Test]
        public void Timing_NotesStayAtMidiTime_NotSnappedToGrid()
        {
            // Düzensiz beat zamanları (tempo kayıyor); MIDI zamanları ızgaraya denk gelmiyor.
            var beats = new List<double>();
            double t = 0.3;
            for (int i = 0; i < 200; i++) { beats.Add(t); t += 0.45 + 0.05 * Math.Sin(i * 0.3); }
            var line = Line(0.377, 60, 64, 62, 67, 65, 69, 64, 60);
            var tempo = TempoMap.FromBeatTimes(beats);
            var notes = Generate(line, MelodyDifficulty.Hard, offset: 0.0125, beats: beats);
            Assert.AreEqual(line.Count, notes.Count);
            for (int i = 0; i < notes.Count; i++)
            {
                double back = tempo.BeatToTime((float)notes[i].Beat); // JSON'daki float beat'ten geri dönüş
                Assert.AreEqual(line[i].Time + 0.0125, back, 0.001, $"nota {i}");
            }
        }

        [Test]
        public void Generated_RandomMelody_PassesTwoThumbRules_AllDifficulties()
        {
            var line = DenseMelody(90, 6, 21);
            // Akor adayları ekle.
            for (int i = 0; i < line.Count; i += 5) { line[i].ChordPitch = line[i].Pitch - 5; line[i].ChordVelocity = 120; line[i].Velocity = 120; }
            foreach (var d in new[] { MelodyDifficulty.Easy, MelodyDifficulty.Normal, MelodyDifficulty.Hard })
            {
                var r = MelodyGenerator.Generate(line, Beats(400), MelodyPreset.For(d));
                CollectionAssert.IsEmpty(PlayabilityRules.Check(r.Notes), d.ToString());
                Assert.Greater(r.Notes.Count, 0);
                if (d == MelodyDifficulty.Hard) Assert.Greater(r.Chords, 0, "Zor'da akor");
                else Assert.AreEqual(0, r.Chords, $"{d}'da akor yok");
            }
        }

        [Test]
        public void Lumiere_NormalMap_StartsAtExpectedPhrase()
        {
            if (!File.Exists(LumiereMidi)) Assert.Ignore($"{LumiereMidi} yok (yerel test dosyası).");
            var file = MidiReader.Read(File.ReadAllBytes(LumiereMidi));
            var line = MelodyExtractor.Extract(file.Notes);
            var r = MelodyGenerator.Generate(line, Beats(500), MelodyPreset.For(MelodyDifficulty.Normal));
            // Kulakla kontrol: melodi 0.95, 1.31, 2.42 sn'de başlar (öncesi bas/giriş).
            Assert.AreEqual(0.952, r.Notes[0].Time, 0.01);
            Assert.AreEqual(1.311, r.Notes[1].Time, 0.01);
            Assert.AreEqual(2.417, r.Notes[2].Time, 0.01);
            CollectionAssert.IsEmpty(r.Warnings);
        }
    }
}
