using System.Collections.Generic;
using NUnit.Framework;
using PixelBeat.Core;
using PixelBeat.Gameplay;

namespace PixelBeat.Tests
{
    /// <summary>Değişken tempo desteği: TempoMap ve beatmap formatı.</summary>
    public class TempoMapTests
    {
        [Test]
        public void Constant_MatchesLegacyFormula()
        {
            var m = TempoMap.Constant(120, 0.25);
            Assert.AreEqual(0.25, m.BeatToTime(0), 1e-12);
            Assert.AreEqual(0.25 + 4.5 * 0.5, m.BeatToTime(4.5), 1e-12);
            Assert.AreEqual(4.5, m.TimeToBeat(0.25 + 4.5 * 0.5), 1e-12);
            Assert.IsFalse(m.IsVariable);
        }

        [Test]
        public void Variable_InterpolatesBetweenBeats()
        {
            var m = TempoMap.FromBeatTimes(new List<double> { 1.0, 1.5, 2.1, 2.6 });
            Assert.AreEqual(1.5, m.BeatToTime(1), 1e-12);
            Assert.AreEqual(1.5 + 0.25 * 0.6, m.BeatToTime(1.25), 1e-12); // 1/4 kendi beat aralığında
            Assert.AreEqual(1.25, m.TimeToBeat(1.65), 1e-12);
            Assert.AreEqual(60.0 / 0.6, m.BpmAt(1.5), 1e-9);
        }

        [Test]
        public void Variable_ExtrapolatesWithEdgeIntervals()
        {
            var m = TempoMap.FromBeatTimes(new List<double> { 1.0, 1.5, 2.1, 2.6 });
            Assert.AreEqual(0.5, m.BeatToTime(-1), 1e-12);
            Assert.AreEqual(3.1, m.BeatToTime(4), 1e-12);
            Assert.AreEqual(-1, m.TimeToBeat(0.5), 1e-12);
            Assert.AreEqual(4, m.TimeToBeat(3.1), 1e-12);
        }

        [Test]
        public void RoundTrip_IsConsistent()
        {
            var m = TempoMap.FromBeatTimes(new List<double> { 0.3, 0.71, 1.09, 1.52, 1.90, 2.33 });
            for (double b = -2; b <= 7; b += 0.125)
                Assert.AreEqual(b, m.TimeToBeat(m.BeatToTime(b)), 1e-9);
        }

        [Test]
        public void FromBeatTimes_RejectsNonIncreasing()
        {
            Assert.Throws<System.ArgumentException>(() => TempoMap.FromBeatTimes(new List<double> { 1, 1 }));
            Assert.Throws<System.ArgumentException>(() => TempoMap.FromBeatTimes(new List<double> { 1 }));
        }

        [Test]
        public void Beatmap_Json_WithBeatTimes_RoundTrips_AndDrivesNoteTimes()
        {
            var map = new Beatmap { bpm = 150, beatTimes = new List<double> { 0.5, 0.9, 1.3, 1.75, 2.2 } };
            map.notes.Add(new BeatmapNote { lane = 1, beat = 2.5f, lengthBeats = 1f });
            string json = UnityEngine.JsonUtility.ToJson(map);
            var back = Beatmap.FromJson(json);

            Assert.IsTrue(back.HasBeatTimes);
            CollectionAssert.AreEqual(map.beatTimes, back.beatTimes);

            var chart = NoteChart.FromBeatmap(back, logWarnings: false);
            var note = chart.Notes[0];
            Assert.AreEqual(1.3 + 0.5 * 0.45, note.TimeSec, 1e-6);
            Assert.AreEqual(1.75 + 0.5 * 0.45, note.EndTimeSec, 1e-6);
        }

        [Test]
        public void LegacyTestBeatmap_TimesUnchanged()
        {
            var json = System.IO.File.ReadAllText("Assets/Data/Beatmaps/metronome_test.json");
            var map = Beatmap.FromJson(json);
            Assert.IsFalse(map.HasBeatTimes);
            var chart = NoteChart.FromBeatmap(map, logWarnings: false);
            double spb = 60.0 / map.bpm;
            foreach (var n in chart.Notes) Assert.AreEqual(map.offset + n.Beat * spb, n.TimeSec, 1e-9);
        }
    }
}
