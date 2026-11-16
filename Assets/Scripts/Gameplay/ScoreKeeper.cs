using System;
using PixelBeat.Core;

namespace PixelBeat.Gameplay
{
    public enum Grade
    {
        S,
        A,
        B,
        C,
        D
    }

    /// <summary>
    /// GDD §5 skor / combo / can hesabı. Unity'ye bağımlı değil (birim testli).
    /// Her değerlendirme (tap = 1, hold = basış + bırakış = 2) skordan eşit pay alır.
    /// </summary>
    public class ScoreKeeper
    {
        public const int MaxScore = 1_000_000;
        public const int MaxHealth = 100;

        // GDD §5 harf derecesi eşikleri (skor ≥ eşik).
        public const int GradeS = 950_000;
        public const int GradeA = 900_000;
        public const int GradeB = 800_000;
        public const int GradeC = 700_000;

        private readonly int[] counts = new int[4];
        private long points; // Perfect 100, Great 70, Good 40, Miss 0

        /// <summary>Şarkıdaki toplam değerlendirme sayısı.</summary>
        public int Total { get; private set; }
        public int Judged { get; private set; }
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Health { get; private set; } = MaxHealth;
        public bool IsFailed { get; private set; }

        /// <summary>Art arda Perfect sayısı (combo'dan ayrı). Great / Good / Miss sıfırlar.</summary>
        public int PerfectStreak { get; private set; }
        public int MaxPerfectStreak { get; private set; }

        /// <summary>Şimdiki streak'in kademesi (0–3). Can bonusu ve gösterge rengi buna bağlı.</summary>
        public int StreakTier => StreakTierFor(PerfectStreak);

        public bool IsComplete => Judged >= Total;
        public bool IsFullCombo => IsComplete && !IsFailed && counts[(int)Judgement.Miss] == 0;
        public bool IsAllPerfect => IsComplete && !IsFailed && counts[(int)Judgement.Perfect] == Total;
        public Grade Grade => GradeFor(Score);

        public int Count(Judgement j) => counts[(int)j];

        public void Reset(int totalJudgements)
        {
            Total = Math.Max(0, totalJudgements);
            Judged = 0;
            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            Health = MaxHealth;
            IsFailed = false;
            PerfectStreak = 0;
            MaxPerfectStreak = 0;
            points = 0;
            Array.Clear(counts, 0, counts.Length);
        }

        /// <summary>Bir değerlendirme ekler. Başarısızlıktan ya da şarkı bittikten sonra yok sayılır (false döner).</summary>
        public bool Add(Judgement j)
        {
            if (IsFailed || IsComplete) return false;

            Judged++;
            counts[(int)j]++;
            points += PointsFor(j);
            // Tam sayı aritmetiği: hepsi Perfect ise tam 1.000.000, asla aşmaz.
            Score = (int)(points * MaxScore / (100L * Total));

            if (j == Judgement.Miss)
            {
                Combo = 0;
            }
            else
            {
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;
            }

            if (j == Judgement.Perfect)
            {
                PerfectStreak++;
                if (PerfectStreak > MaxPerfectStreak) MaxPerfectStreak = PerfectStreak;
            }
            else
            {
                PerfectStreak = 0;
            }

            // Perfect'in can kazancı, bu Perfect sayıldıktan sonraki streak'e göre (10. Perfect zaten +3).
            Health = Math.Max(0, Math.Min(MaxHealth, Health + HealthDelta(j, PerfectStreak)));
            if (Health == 0) IsFailed = true;
            return true;
        }

        public static int PointsFor(Judgement j)
        {
            switch (j)
            {
                case Judgement.Perfect: return 100;
                case Judgement.Great: return 70;
                case Judgement.Good: return 40;
                default: return 0;
            }
        }

        public const int MissHealthPenalty = -6;

        /// <summary>Streak kademe başlangıçları: 10 / 25 / 50.</summary>
        public static readonly int[] StreakTierStarts = { 10, 25, 50 };

        /// <param name="streakAfter">Perfect için: bu Perfect sayıldıktan sonraki streak.</param>
        public static int HealthDelta(Judgement j, int streakAfter)
        {
            switch (j)
            {
                case Judgement.Perfect: return PerfectHealthGain(streakAfter);
                case Judgement.Great: return 1;
                case Judgement.Good: return 0;
                default: return MissHealthPenalty;
            }
        }

        /// <summary>GDD §5: 1–9 → +2, 10–24 → +3, 25–49 → +4, 50+ → +5.</summary>
        public static int PerfectHealthGain(int streak) => 2 + StreakTierFor(streak);

        public static int StreakTierFor(int streak)
        {
            int tier = 0;
            foreach (int start in StreakTierStarts)
                if (streak >= start) tier++;
            return tier;
        }

        public static Grade GradeFor(int score)
        {
            if (score >= GradeS) return Grade.S;
            if (score >= GradeA) return Grade.A;
            if (score >= GradeB) return Grade.B;
            if (score >= GradeC) return Grade.C;
            return Grade.D;
        }

        /// <summary>Tap = 1, hold = 2 değerlendirme.</summary>
        public static int TotalJudgementsFor(NoteChart chart)
        {
            if (chart == null) return 0;
            int total = 0;
            foreach (var n in chart.Notes) total += n.IsHold ? 2 : 1;
            return total;
        }
    }
}
