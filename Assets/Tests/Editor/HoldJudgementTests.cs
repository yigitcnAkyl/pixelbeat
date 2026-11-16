using System.Collections.Generic;
using NUnit.Framework;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.Tests
{
    /// <summary>
    /// GDD §3-4 değerlendirme kuralları (ChartJudge). 120 BPM: 1 beat = 0.5 sn.
    /// Pencereler varsayılan: basış 40/80/120 ms, bırakış 60/120/180 ms.
    /// </summary>
    public class HoldJudgementTests
    {
        private struct Event
        {
            public ChartNote Note;
            public JudgementKind Kind;
            public Judgement Result;
        }

        private JudgementWindows windows;
        private ChartJudge judge;
        private List<Event> events;

        [SetUp]
        public void SetUp()
        {
            windows = ScriptableObject.CreateInstance<JudgementWindows>();
            judge = new ChartJudge(windows);
            events = new List<Event>();
            judge.Judged += (n, k, r, d) => events.Add(new Event { Note = n, Kind = k, Result = r });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(windows);

        private static NoteChart Chart(params (int lane, float beat, float length)[] notes)
        {
            var map = new Beatmap { bpm = 120f };
            foreach (var n in notes) map.notes.Add(new BeatmapNote { lane = n.lane, beat = n.beat, lengthBeats = n.length });
            return NoteChart.FromBeatmap(map, logWarnings: false);
        }

        private ChartNote Load(params (int lane, float beat, float length)[] notes)
        {
            var chart = Chart(notes);
            judge.Load(chart);
            return chart.Notes[0];
        }

        // ---------- Tap ----------

        [Test]
        public void Tap_OnTime_IsPerfect()
        {
            var note = Load((0, 2, 0)); // t = 1.0
            judge.Press(0, 1.02);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(Judgement.Perfect, events[0].Result);
            Assert.AreEqual(NoteState.Hit, note.State);
        }

        [Test]
        public void Tap_TooEarly_IsIgnored_ThenCanBeHit()
        {
            var note = Load((0, 2, 0));
            judge.Press(0, 1.0 - 0.13);
            Assert.AreEqual(0, events.Count);
            Assert.AreEqual(NoteState.Pending, note.State);

            judge.Press(0, 1.0 - 0.06);
            Assert.AreEqual(Judgement.Great, events[0].Result);
        }

        [Test]
        public void Tap_NotPressed_IsMissAfterGoodWindow()
        {
            var note = Load((0, 2, 0));
            judge.Tick(1.11);
            Assert.AreEqual(0, events.Count);
            judge.Tick(1.13);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(Judgement.Miss, events[0].Result);
            Assert.AreEqual(NoteState.Missed, note.State);
        }

        // ---------- Hold basış / bırakış ----------

        [Test]
        public void Hold_PressAndReleaseOnTime_TwoPerfects()
        {
            var hold = Load((1, 2, 2)); // 1.0 .. 2.0
            judge.Press(1, 1.0);
            Assert.AreEqual(NoteState.Holding, hold.State);
            Assert.AreSame(hold, judge.ActiveHold(1));

            judge.Tick(1.5);
            Assert.AreEqual(1.5, hold.HeldUntilSec, 1e-9, "Tutulurken gövde şimdiki zamana kadar yenir");

            judge.Release(1, 2.03);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(JudgementKind.Press, events[0].Kind);
            Assert.AreEqual(Judgement.Perfect, events[0].Result);
            Assert.AreEqual(JudgementKind.Release, events[1].Kind);
            Assert.AreEqual(Judgement.Perfect, events[1].Result);
            Assert.AreEqual(NoteState.Hit, hold.State);
            Assert.IsNull(judge.ActiveHold(1));
        }

        [TestCase(0.055, Judgement.Perfect)]
        [TestCase(-0.059, Judgement.Perfect)]
        [TestCase(0.100, Judgement.Great)]   // basışta Good olurdu, bırakışta 1.5x → Great
        [TestCase(-0.119, Judgement.Great)]
        [TestCase(0.150, Judgement.Good)]
        [TestCase(-0.170, Judgement.Good)]
        public void Hold_ReleaseWindows_Are1_5x(double releaseDiff, Judgement expected)
        {
            Load((0, 2, 2)); // bitiş 2.0
            judge.Press(0, 1.0);
            judge.Release(0, 2.0 + releaseDiff);
            Assert.AreEqual(expected, events[1].Result);
        }

        [Test]
        public void Hold_ReleasedTooEarly_BreaksAsMiss_AndKeepsFalling()
        {
            var hold = Load((0, 2, 2));
            judge.Press(0, 1.0);
            judge.Release(0, 1.5); // bitişten 500 ms önce

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(Judgement.Perfect, events[0].Result, "Basış sonucu korunur");
            Assert.AreEqual(JudgementKind.Release, events[1].Kind);
            Assert.AreEqual(Judgement.Miss, events[1].Result);
            Assert.AreEqual(NoteState.Missed, hold.State, "Kırılan hold gri olarak akmaya devam eder");
            Assert.AreEqual(1.5, hold.HeldUntilSec, 1e-9, "Kalan gövde bırakış anından başlar");
            Assert.IsNull(judge.ActiveHold(0));

            // Sonradan gelen basış/bırakış ve zaman ilerlemesi yeni sonuç üretmez.
            judge.Press(0, 1.9);
            judge.Release(0, 2.0);
            judge.Tick(3.0);
            Assert.AreEqual(2, events.Count);
        }

        [Test]
        public void Hold_ReleasedJustInsideEarlyGoodWindow_IsNotBroken()
        {
            var hold = Load((0, 2, 2));
            judge.Press(0, 1.0);
            judge.Release(0, 2.0 - 0.179);
            Assert.AreEqual(Judgement.Good, events[1].Result);
            Assert.AreEqual(NoteState.Hit, hold.State);
        }

        [Test]
        public void Hold_NotReleased_AutoGoodAfterReleaseWindow()
        {
            var hold = Load((0, 2, 2));
            judge.Press(0, 1.0);

            judge.Tick(2.17);
            Assert.AreEqual(1, events.Count, "Pencere içinde hâlâ bekler");
            Assert.AreSame(hold, judge.ActiveHold(0));

            judge.Tick(2.19);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(Judgement.Good, events[1].Result);
            Assert.AreEqual(NoteState.Hit, hold.State);
            Assert.IsNull(judge.ActiveHold(0));

            judge.Release(0, 2.3); // geç gelen bırakış yok sayılır
            Assert.AreEqual(2, events.Count);
        }

        [Test]
        public void Hold_LateReleaseEventBeforeTick_IsStillGood()
        {
            Load((0, 2, 2));
            judge.Press(0, 1.0);
            judge.Release(0, 2.25); // Tick gelmeden bırakış olayı: yine GOOD (MISS değil)
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(Judgement.Good, events[1].Result);
        }

        [Test]
        public void Hold_HeadNeverPressed_WholeHoldMiss()
        {
            var hold = Load((2, 2, 4));
            judge.Tick(1.13);

            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(JudgementKind.Press, events[0].Kind);
            Assert.AreEqual(Judgement.Miss, events[0].Result);
            Assert.AreEqual(JudgementKind.Release, events[1].Kind);
            Assert.AreEqual(Judgement.Miss, events[1].Result);
            Assert.AreEqual(NoteState.Missed, hold.State);
            Assert.AreEqual(hold.TimeSec, hold.HeldUntilSec, "Gövdenin tamamı gri akar");

            // Gövdenin ortasında basmak hiçbir şey yapmaz.
            judge.Press(2, 2.0);
            judge.Release(2, 3.0);
            Assert.AreEqual(2, events.Count);
            Assert.IsNull(judge.ActiveHold(2));
        }

        [Test]
        public void Hold_TapsOnOtherLanesWhileHolding_AreJudged()
        {
            var chart = Chart((1, 2, 4), (3, 3, 0), (2, 4, 0)); // hold 1.0..3.0; taplar 1.5, 2.0
            judge.Load(chart);

            judge.Press(1, 1.0);
            judge.Press(3, 1.5);
            judge.Press(2, 2.05);
            judge.Release(1, 3.0);

            Assert.AreEqual(4, events.Count);
            Assert.AreEqual(3, events[1].Note.Lane);
            Assert.AreEqual(Judgement.Perfect, events[1].Result);
            Assert.AreEqual(2, events[2].Note.Lane);
            Assert.AreEqual(Judgement.Great, events[2].Result);
            Assert.AreEqual(JudgementKind.Release, events[3].Kind);
            Assert.AreEqual(Judgement.Perfect, events[3].Result);
        }

        [Test]
        public void Hold_SecondPressOnSameLaneWhileHolding_IsIgnored()
        {
            var hold = Load((0, 2, 4));
            judge.Press(0, 1.0);
            judge.Press(0, 1.6); // ikinci parmak
            Assert.AreEqual(1, events.Count);
            Assert.AreSame(hold, judge.ActiveHold(0));
        }

        [Test]
        public void ReleaseWithoutActiveHold_DoesNothing()
        {
            Load((0, 2, 0));
            judge.Press(0, 1.0);
            judge.Release(0, 1.1);
            Assert.AreEqual(1, events.Count);
        }

        [Test]
        public void ChordHolds_AreIndependent()
        {
            var chart = Chart((1, 2, 2), (2, 2, 2));
            judge.Load(chart);
            judge.Press(1, 1.0);
            judge.Press(2, 1.0);
            judge.Release(1, 1.4); // erken: kırılır
            judge.Release(2, 2.0);

            Assert.AreEqual(NoteState.Missed, chart.LaneNotes(1)[0].State);
            Assert.AreEqual(NoteState.Hit, chart.LaneNotes(2)[0].State);
            Assert.AreEqual(Judgement.Perfect, chart.LaneNotes(2)[0].ReleaseResult);
        }

        // ---------- Harita doğrulaması ----------

        [Test]
        public void Validate_FindsNoteInsideHoldOnSameLane()
        {
            var chart = Chart((0, 4, 2), (0, 5, 0), (1, 5, 0));
            var issues = chart.Validate();
            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains("Şerit 0", issues[0]);
        }

        [Test]
        public void Validate_NoteExactlyAtHoldEnd_IsReported()
        {
            Assert.AreEqual(1, Chart((0, 4, 2), (0, 6, 0)).Validate().Count);
        }

        [Test]
        public void Validate_NoteAfterHoldEnd_IsFine()
        {
            Assert.AreEqual(0, Chart((0, 4, 2), (0, 6.5f, 0), (1, 5, 0)).Validate().Count);
        }

        [Test]
        public void Validate_TestBeatmap_HasNoIssues()
        {
            var json = System.IO.File.ReadAllText("Assets/Data/Beatmaps/metronome_test.json");
            var chart = NoteChart.FromBeatmap(Beatmap.FromJson(json), logWarnings: false);
            CollectionAssert.IsEmpty(chart.Validate());

            int holds = 0;
            foreach (var n in chart.Notes) if (n.IsHold) holds++;
            Assert.That(holds, Is.InRange(6, 8));
        }
    }
}
