using System;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Vuruş sonuçlarına (PERFECT, GREAT, GOOD, MISS) göre skoru, komboyu ve kombo çarpanını (x1 - x5)
    /// hesaplayan merkezi skor yöneticisi.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Temel Vuruş Puanları")]
        [SerializeField] private int baseScorePerfect = 100;
        [SerializeField] private int baseScoreGreat = 70;
        [SerializeField] private int baseScoreGood = 40;

        // Skor ve Kombo Durumu
        public int TotalScore { get; private set; }
        public int CurrentCombo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Multiplier { get; private set; } = 1;

        // İstatistik Sayaçları
        public int PerfectCount { get; private set; }
        public int GreatCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }

        // Olaylar (UI ve Efektler için)
        public static event Action<int, int> OnScoreUpdated;                  // (totalScore, addedPoints)
        public static event Action<int, int> OnComboUpdated;                  // (currentCombo, multiplier)
        public static event Action OnComboBroken;
        public static event Action<HitAccuracy, int, int> OnHitProcessed;     // (accuracy, addedPoints, combo)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            HitDetector.OnHitJudged += HandleHitJudged;
        }

        private void OnDisable()
        {
            HitDetector.OnHitJudged -= HandleHitJudged;
        }

        /// <summary>
        /// HitDetector'dan gelen vuruş değerlendirmesini puanlar ve komboyu günceller.
        /// </summary>
        private void HandleHitJudged(HitDetector.HitResult result)
        {
            if (result.accuracy == HitAccuracy.Miss)
            {
                MissCount++;
                CurrentCombo = 0;
                Multiplier = 1;

                OnComboBroken?.Invoke();
                OnComboUpdated?.Invoke(CurrentCombo, Multiplier);
                OnHitProcessed?.Invoke(HitAccuracy.Miss, 0, CurrentCombo);
            }
            else
            {
                // Başarılı vuruş
                CurrentCombo++;
                if (CurrentCombo > MaxCombo) MaxCombo = CurrentCombo;

                // Kombo Çarpanı Sistemi
                Multiplier = CalculateMultiplier(CurrentCombo);

                int basePoints = 0;
                switch (result.accuracy)
                {
                    case HitAccuracy.Perfect:
                        PerfectCount++;
                        basePoints = baseScorePerfect;
                        break;
                    case HitAccuracy.Great:
                        GreatCount++;
                        basePoints = baseScoreGreat;
                        break;
                    case HitAccuracy.Good:
                        GoodCount++;
                        basePoints = baseScoreGood;
                        break;
                }

                int addedPoints = basePoints * Multiplier;
                TotalScore += addedPoints;

                OnScoreUpdated?.Invoke(TotalScore, addedPoints);
                OnComboUpdated?.Invoke(CurrentCombo, Multiplier);
                OnHitProcessed?.Invoke(result.accuracy, addedPoints, CurrentCombo);
            }
        }

        /// <summary>
        /// 1-9: x1, 10-19: x2, 20-29: x3, 30-39: x4, 40+: x5
        /// </summary>
        public int CalculateMultiplier(int combo)
        {
            if (combo >= 40) return 5;
            if (combo >= 30) return 4;
            if (combo >= 20) return 3;
            if (combo >= 10) return 2;
            return 1;
        }

        /// <summary>
        /// Yeni şarkı veya tur başladığında skorları sıfırlar.
        /// </summary>
        public void ResetScore()
        {
            TotalScore = 0;
            CurrentCombo = 0;
            MaxCombo = 0;
            Multiplier = 1;
            PerfectCount = 0;
            GreatCount = 0;
            GoodCount = 0;
            MissCount = 0;

            OnScoreUpdated?.Invoke(0, 0);
            OnComboUpdated?.Invoke(0, 1);
        }
    }
}
