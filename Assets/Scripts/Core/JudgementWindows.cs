using UnityEngine;

namespace PixelBeat.Core
{
    public enum Judgement
    {
        Perfect,
        Great,
        Good,
        Miss
    }

    /// <summary>GDD §4 değerlendirme pencereleri (± ms). Kodun içine gömülmez, bu asset'ten okunur.</summary>
    [CreateAssetMenu(fileName = "JudgementWindows", menuName = "PixelBeat/Judgement Windows")]
    public class JudgementWindows : ScriptableObject
    {
        [Header("Basış (± ms)")]
        [SerializeField, Min(1f)] private float perfectMs = 40f;
        [SerializeField, Min(1f)] private float greatMs = 80f;
        [SerializeField, Min(1f)] private float goodMs = 120f;

        [Header("Bırakış - hold sonu (± ms)")]
        [SerializeField, Min(1f)] private float releasePerfectMs = 60f;
        [SerializeField, Min(1f)] private float releaseGreatMs = 120f;
        [SerializeField, Min(1f)] private float releaseGoodMs = 180f;

        /// <summary>Basış için Good penceresi (sn). Bundan erken basış yok sayılır, bundan geç kalan nota MISS olur.</summary>
        public double GoodSec => goodMs / 1000.0;
        public double ReleaseGoodSec => releaseGoodMs / 1000.0;

        /// <param name="diffSec">basış zamanı − nota zamanı (negatif = erken)</param>
        public Judgement EvaluatePress(double diffSec) => Evaluate(diffSec, perfectMs, greatMs, goodMs);

        public Judgement EvaluateRelease(double diffSec) => Evaluate(diffSec, releasePerfectMs, releaseGreatMs, releaseGoodMs);

        private static Judgement Evaluate(double diffSec, float perfect, float great, float good)
        {
            double ms = System.Math.Abs(diffSec) * 1000.0;
            if (ms <= perfect) return Judgement.Perfect;
            if (ms <= great) return Judgement.Great;
            if (ms <= good) return Judgement.Good;
            return Judgement.Miss;
        }
    }
}
