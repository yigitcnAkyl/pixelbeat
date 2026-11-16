using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Streak göstergesinin önceden döndürülmüş pixel sprite'ları (GDD §5). Editor'de "Sprite'ları üret" ile
    /// PixelFont'tan yumuşatmasız (RotSprite benzeri) döndürülerek üretilir; ileride elle çizilmiş PNG'lerle değiştirilebilir.
    /// Açıyı değiştirince sprite'lar yeniden üretilmeli.
    /// </summary>
    [CreateAssetMenu(fileName = "StreakGlyphs", menuName = "PixelBeat/Streak Glyph Set")]
    public class StreakGlyphSet : ScriptableObject
    {
        [Header("Üretim ayarları")]
        [Tooltip("Saat yönünde dönüş açısı (derece). Yazı soldan sağa okunurken aşağı iner.")]
        [SerializeField, Range(0f, 90f)] private float angleDeg = 35f;
        [Tooltip("Sayının normal ölçeği (tam sayı).")]
        [SerializeField, Range(1, 4)] private int numberScale = 2;
        [Tooltip("Kademe atlarken kısa büyüme için sayı ölçeği (tam sayı).")]
        [SerializeField, Range(1, 5)] private int bumpScale = 3;
        [SerializeField, Range(1, 3)] private int labelScale = 1;
        [SerializeField] private string outputFolder = "Assets/Art/UI/Streak";

        [Header("Üretilen sprite'lar (otomatik doldurulur)")]
        [SerializeField] private float bakedAngleDeg = float.NaN;
        [SerializeField] private Sprite label;
        [SerializeField] private Sprite[] digits = new Sprite[10];
        [SerializeField] private Sprite[] bumpDigits = new Sprite[10];

        public const string LabelText = "PERFECT";

        public float AngleDeg => angleDeg;
        public int NumberScale => numberScale;
        public int BumpScale => bumpScale;
        public int LabelScale => labelScale;
        public string OutputFolder => outputFolder;

        /// <summary>Sprite'ların üretildiği açı. AngleDeg'den farklıysa yeniden üretmek gerekir.</summary>
        public float BakedAngleDeg => bakedAngleDeg;
        public bool IsBaked => label != null && digits != null && digits.Length == 10 && digits[0] != null;
        public bool NeedsRebake => !IsBaked || float.IsNaN(bakedAngleDeg) || Mathf.Abs(bakedAngleDeg - angleDeg) > 0.001f;

        public Sprite Label => label;
        public Sprite Digit(int d, bool bump) => (bump ? bumpDigits : digits)[d];

#if UNITY_EDITOR
        /// <summary>Sadece Editor üreticisi çağırır.</summary>
        public void SetBaked(float angle, Sprite newLabel, Sprite[] newDigits, Sprite[] newBumpDigits)
        {
            bakedAngleDeg = angle;
            label = newLabel;
            digits = newDigits;
            bumpDigits = newBumpDigits;
        }
#endif
    }
}
