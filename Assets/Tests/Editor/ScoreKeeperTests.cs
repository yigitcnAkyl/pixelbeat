using NUnit.Framework;
using PixelBeat.Core;
using PixelBeat.Gameplay;

namespace PixelBeat.Tests
{
    /// <summary>GDD §5 skor / combo / can kuralları (ScoreKeeper).</summary>
    public class ScoreKeeperTests
    {
        private static ScoreKeeper Keeper(int total)
        {
            var k = new ScoreKeeper();
            k.Reset(total);
            return k;
        }

        private static void AddMany(ScoreKeeper k, Judgement j, int count)
        {
            for (int i = 0; i < count; i++) k.Add(j);
        }

        // ---------- Skor ----------

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(7)]
        [TestCase(1234)]
        public void AllPerfect_IsExactlyMaxScore(int total)
        {
            var k = Keeper(total);
            AddMany(k, Judgement.Perfect, total);
            Assert.AreEqual(ScoreKeeper.MaxScore, k.Score);
            Assert.IsTrue(k.IsAllPerfect);
            Assert.IsTrue(k.IsFullCombo);
            Assert.AreEqual(Grade.S, k.Grade);
        }

        [Test]
        public void EachJudgementGetsEqualShare()
        {
            var k = Keeper(4);
            k.Add(Judgement.Perfect); // 100
            k.Add(Judgement.Great);   //  70
            k.Add(Judgement.Good);    //  40
            k.Add(Judgement.Miss);    //   0
            Assert.AreEqual(1_000_000 * 210 / 400, k.Score); // 525000
        }

        [Test]
        public void Score_IsCumulativeDuringSong()
        {
            var k = Keeper(10);
            k.Add(Judgement.Perfect);
            Assert.AreEqual(100_000, k.Score);
            k.Add(Judgement.Great);
            Assert.AreEqual(170_000, k.Score);
        }

        [Test]
        public void Score_NeverExceedsMax_WithAwkwardTotals()
        {
            for (int total = 1; total <= 50; total++)
            {
                var k = Keeper(total);
                for (int i = 0; i < total; i++)
                {
                    k.Add(Judgement.Perfect);
                    Assert.LessOrEqual(k.Score, ScoreKeeper.MaxScore);
                }
            }
        }

        [Test]
        public void TotalJudgements_HoldCountsTwice()
        {
            var map = new Beatmap { bpm = 120f };
            map.notes.Add(new BeatmapNote { lane = 0, beat = 1, lengthBeats = 0 });
            map.notes.Add(new BeatmapNote { lane = 1, beat = 2, lengthBeats = 2 });
            map.notes.Add(new BeatmapNote { lane = 2, beat = 3, lengthBeats = 0 });
            var chart = NoteChart.FromBeatmap(map, logWarnings: false);
            Assert.AreEqual(4, ScoreKeeper.TotalJudgementsFor(chart));
        }

        // ---------- Combo ----------

        [Test]
        public void Combo_IncrementsOnNonMiss_ResetsOnMiss_KeepsMax()
        {
            var k = Keeper(10);
            k.Add(Judgement.Perfect);
            k.Add(Judgement.Great);
            k.Add(Judgement.Good);
            Assert.AreEqual(3, k.Combo);

            k.Add(Judgement.Miss);
            Assert.AreEqual(0, k.Combo);
            Assert.AreEqual(3, k.MaxCombo);

            k.Add(Judgement.Perfect);
            k.Add(Judgement.Perfect);
            Assert.AreEqual(2, k.Combo);
            Assert.AreEqual(3, k.MaxCombo);
        }

        [Test]
        public void FullCombo_vs_AllPerfect()
        {
            var k = Keeper(3);
            k.Add(Judgement.Perfect);
            k.Add(Judgement.Good);
            Assert.IsFalse(k.IsFullCombo, "Şarkı bitmeden FULL COMBO yok");
            k.Add(Judgement.Great);
            Assert.IsTrue(k.IsFullCombo);
            Assert.IsFalse(k.IsAllPerfect);

            var m = Keeper(2);
            m.Add(Judgement.Perfect);
            m.Add(Judgement.Miss);
            Assert.IsFalse(m.IsFullCombo);
        }

        // ---------- Can ----------

        [Test]
        public void Health_StartsAt100_AndIsCappedAt100()
        {
            var k = Keeper(10);
            Assert.AreEqual(100, k.Health);
            k.Add(Judgement.Perfect);
            Assert.AreEqual(100, k.Health);
        }

        [Test]
        public void Health_Deltas()
        {
            var k = Keeper(10);
            k.Add(Judgement.Miss);
            Assert.AreEqual(94, k.Health);
            k.Add(Judgement.Good);
            Assert.AreEqual(94, k.Health);
            k.Add(Judgement.Great);
            Assert.AreEqual(95, k.Health);
            k.Add(Judgement.Perfect);
            Assert.AreEqual(97, k.Health);
        }

        [Test]
        public void SeventeenMisses_Fail_AndFurtherJudgementsAreIgnored()
        {
            var k = Keeper(100);
            AddMany(k, Judgement.Miss, 16);
            Assert.AreEqual(4, k.Health);
            Assert.IsFalse(k.IsFailed);

            k.Add(Judgement.Miss);
            Assert.AreEqual(0, k.Health);
            Assert.IsTrue(k.IsFailed);

            int judged = k.Judged;
            Assert.IsFalse(k.Add(Judgement.Perfect));
            Assert.AreEqual(judged, k.Judged);
            Assert.AreEqual(0, k.Health, "Başarısızlıktan sonra can geri gelmez");
            Assert.IsFalse(k.IsFullCombo);
        }

        [Test]
        public void JudgementsAfterCompletion_AreIgnored()
        {
            var k = Keeper(1);
            k.Add(Judgement.Perfect);
            Assert.IsTrue(k.IsComplete);
            Assert.IsFalse(k.Add(Judgement.Miss));
            Assert.AreEqual(ScoreKeeper.MaxScore, k.Score);
        }

        [Test]
        public void Counts_AreTracked()
        {
            var k = Keeper(6);
            AddMany(k, Judgement.Perfect, 3);
            AddMany(k, Judgement.Great, 1);
            AddMany(k, Judgement.Good, 1);
            AddMany(k, Judgement.Miss, 1);
            Assert.AreEqual(3, k.Count(Judgement.Perfect));
            Assert.AreEqual(1, k.Count(Judgement.Great));
            Assert.AreEqual(1, k.Count(Judgement.Good));
            Assert.AreEqual(1, k.Count(Judgement.Miss));
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var k = Keeper(5);
            AddMany(k, Judgement.Miss, 5);
            k.Reset(3);
            Assert.AreEqual(0, k.Score);
            Assert.AreEqual(0, k.Combo);
            Assert.AreEqual(0, k.MaxCombo);
            Assert.AreEqual(0, k.PerfectStreak);
            Assert.AreEqual(0, k.MaxPerfectStreak);
            Assert.AreEqual(100, k.Health);
            Assert.AreEqual(0, k.Count(Judgement.Miss));
            Assert.IsFalse(k.IsFailed);
            Assert.IsFalse(k.IsComplete);
        }

        // ---------- Perfect streak ----------

        [Test]
        public void Streak_CountsPerfects_AndIsSeparateFromCombo()
        {
            var k = Keeper(20);
            AddMany(k, Judgement.Perfect, 3);
            Assert.AreEqual(3, k.PerfectStreak);
            Assert.AreEqual(3, k.Combo);

            k.Add(Judgement.Great);
            Assert.AreEqual(0, k.PerfectStreak, "Great streak'i sıfırlar");
            Assert.AreEqual(4, k.Combo, "ama combo'yu sıfırlamaz");

            AddMany(k, Judgement.Perfect, 2);
            k.Add(Judgement.Good);
            Assert.AreEqual(0, k.PerfectStreak);

            AddMany(k, Judgement.Perfect, 4);
            k.Add(Judgement.Miss);
            Assert.AreEqual(0, k.PerfectStreak);
            Assert.AreEqual(4, k.MaxPerfectStreak);
        }

        [Test]
        public void Streak_HoldPressAndReleaseCountSeparately()
        {
            var k = Keeper(4);
            k.Add(Judgement.Perfect); // hold basış
            k.Add(Judgement.Perfect); // hold bırakış
            Assert.AreEqual(2, k.PerfectStreak);
        }

        [TestCase(1, 2)]
        [TestCase(9, 2)]
        [TestCase(10, 3)]
        [TestCase(24, 3)]
        [TestCase(25, 4)]
        [TestCase(49, 4)]
        [TestCase(50, 5)]
        [TestCase(200, 5)]
        public void PerfectHealthGain_ByStreakAfterThisPerfect(int streak, int gain)
        {
            Assert.AreEqual(gain, ScoreKeeper.PerfectHealthGain(streak));
        }

        [Test]
        public void Health_TierBonusIsApplied_FromTheTierStartingPerfect()
        {
            var k = Keeper(200);
            AddMany(k, Judgement.Miss, 15); // can 10, streak 0
            Assert.AreEqual(10, k.Health);

            AddMany(k, Judgement.Perfect, 9); // 9 × +2
            Assert.AreEqual(28, k.Health);
            Assert.AreEqual(0, k.StreakTier);

            k.Add(Judgement.Perfect); // 10. Perfect: +3
            Assert.AreEqual(31, k.Health);
            Assert.AreEqual(1, k.StreakTier);

            AddMany(k, Judgement.Perfect, 14); // 11..24: +3
            Assert.AreEqual(73, k.Health);

            k.Add(Judgement.Perfect); // 25.: +4
            Assert.AreEqual(77, k.Health);
            Assert.AreEqual(2, k.StreakTier);
        }

        [Test]
        public void Health_BonusResetsWhenStreakBreaks()
        {
            var k = Keeper(100);
            AddMany(k, Judgement.Miss, 10); // can 40
            AddMany(k, Judgement.Perfect, 10); // 9×2 + 3 = 21 → 61
            Assert.AreEqual(61, k.Health);

            k.Add(Judgement.Great); // +1, streak 0
            k.Add(Judgement.Perfect); // streak 1 → +2
            Assert.AreEqual(64, k.Health);
        }

        [Test]
        public void Health_NeverExceedsMax_WithTierBonus()
        {
            var k = Keeper(100);
            AddMany(k, Judgement.Perfect, 60);
            Assert.AreEqual(ScoreKeeper.MaxHealth, k.Health);
            Assert.AreEqual(3, k.StreakTier);
        }

        [Test]
        public void Streak_FreezesAfterFail()
        {
            var k = Keeper(100);
            AddMany(k, Judgement.Miss, 17);
            Assert.IsTrue(k.IsFailed);
            k.Add(Judgement.Perfect);
            Assert.AreEqual(0, k.PerfectStreak);
        }

        // ---------- Derece ----------

        [TestCase(1_000_000, Grade.S)]
        [TestCase(950_000, Grade.S)]
        [TestCase(949_999, Grade.A)]
        [TestCase(900_000, Grade.A)]
        [TestCase(899_999, Grade.B)]
        [TestCase(800_000, Grade.B)]
        [TestCase(799_999, Grade.C)]
        [TestCase(700_000, Grade.C)]
        [TestCase(699_999, Grade.D)]
        [TestCase(0, Grade.D)]
        public void GradeThresholds(int score, Grade expected)
        {
            Assert.AreEqual(expected, ScoreKeeper.GradeFor(score));
        }
    }
}
