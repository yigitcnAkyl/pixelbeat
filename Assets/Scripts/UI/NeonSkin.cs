using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Neon placeholder pixel art sprite'ları (GDD §8 sanat yönü). Editor'de *PixelBeat > Bake Neon Sprites* ile paletten üretilir;
    /// PNG'ler elle çizilenlerle değiştirilebilir. Çok tonlu sprite'lar renk başına ayrı üretilir (tint yok → sadece palet renkleri).
    /// Parlamalar ayrı "hale" sprite'larıdır ve <see cref="Additive"/> materyaliyle çizilir (bloom yok).
    /// </summary>
    [CreateAssetMenu(fileName = "NeonSkin", menuName = "PixelBeat/Neon Skin")]
    public class NeonSkin : ScriptableObject
    {
        /// <summary>Renk başına üretilen sprite'ların renkleri (şerit renkleri + Ash = kaçan nota + vurgu renkleri).</summary>
        public static readonly PaletteColor[] AccentColors =
        {
            PaletteColor.Cyan, PaletteColor.Violet, PaletteColor.Magenta, PaletteColor.Lime,
            PaletteColor.Gold, PaletteColor.Orange, PaletteColor.Red, PaletteColor.White, PaletteColor.Ash,
        };

        public const int PaletteSize = 16;

        /// <summary>Buton / dokunma düğmesi halesinin çerçeveden taşma payı (piksel).</summary>
        public const int ButtonGlowMarginPx = 4;

        public enum Word
        {
            Perfect,
            Great,
            Good,
            Miss,
        }

        /// <summary>Sonuç ekranı etiketleri (hazır sprite + hale).</summary>
        public enum Label
        {
            GradeS,
            GradeA,
            GradeB,
            GradeC,
            GradeD,
            Failed,
            NewRecord,
            FullCombo,
            AllPerfect,
        }

        public const int LabelCount = 9;

        [Tooltip("Additive parlama materyali (PixelBeat/SpriteAdditive).")]
        [SerializeField] private Material additive;

        [Header("Tekli")]
        [SerializeField] private Sprite lanePanel;
        [SerializeField] private Sprite hitLineIdle;
        [SerializeField] private Sprite hitLineBeat;
        [SerializeField] private Sprite particle1;
        [SerializeField] private Sprite particle2;
        [Tooltip("Varsayılan arka plan (gökyüzü + güneş + şehir + zemin ızgarası). Pivot = ufuk çizgisi.")]
        [SerializeField] private Sprite sky;
        [SerializeField] private Sprite logo;
        [SerializeField] private Sprite logoGlow;
        [SerializeField] private Sprite iconPlay;
        [SerializeField] private Sprite iconGear;
        [SerializeField] private Sprite iconBack;

        [Header("Değerlendirme yazıları (PERFECT!, GREAT!, GOOD!, MISS!)")]
        [SerializeField] private Sprite[] words = new Sprite[4];
        [SerializeField] private Sprite[] wordGlows = new Sprite[4];

        [Header("Sonuç etiketleri (S A B C D, FAILED, YENI REKOR!, FULL COMBO, ALL PERFECT)")]
        [SerializeField] private Sprite[] labels = new Sprite[LabelCount];
        [SerializeField] private Sprite[] labelGlows = new Sprite[LabelCount];

        [Header("Renk başına (palet indeksine göre)")]
        [SerializeField] private Sprite[] noteHead = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] noteGlow = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] holdBody = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] holdGlow = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] holdTail = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] tailGlow = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] keyPadIdle = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] keyPadPressed = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] keyPadGlow = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] laneFlash = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] buttonFrame = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] buttonGlow = new Sprite[PaletteSize];

        public Material Additive => additive;
        public Sprite LanePanel => lanePanel;
        public Sprite HitLineIdle => hitLineIdle;
        public Sprite HitLineBeat => hitLineBeat;
        public Sprite Particle1 => particle1;
        public Sprite Particle2 => particle2;
        public Sprite Sky => sky;
        public Sprite Logo => logo;
        public Sprite LogoGlow => logoGlow;
        public Sprite IconPlay => iconPlay;
        public Sprite IconGear => iconGear;
        public Sprite IconBack => iconBack;

        public Sprite LabelSprite(Label l) => labels != null && labels.Length > (int)l ? labels[(int)l] : null;
        public Sprite LabelGlow(Label l) => labelGlows != null && labelGlows.Length > (int)l ? labelGlows[(int)l] : null;

        public static Label GradeLabel(int grade) => (Label)Mathf.Clamp(grade, 0, 4);

        public Sprite WordSprite(Word w) => words != null && words.Length > (int)w ? words[(int)w] : null;
        public Sprite WordGlow(Word w) => wordGlows != null && wordGlows.Length > (int)w ? wordGlows[(int)w] : null;

        public Sprite NoteHead(PaletteColor c) => Pick(noteHead, c);
        public Sprite NoteGlow(PaletteColor c) => Pick(noteGlow, c);
        public Sprite HoldBody(PaletteColor c) => Pick(holdBody, c);
        public Sprite HoldGlow(PaletteColor c) => Pick(holdGlow, c);
        public Sprite HoldTail(PaletteColor c) => Pick(holdTail, c);
        public Sprite TailGlow(PaletteColor c) => Pick(tailGlow, c);
        public Sprite KeyPadIdle(PaletteColor c) => Pick(keyPadIdle, c);
        public Sprite KeyPadPressed(PaletteColor c) => Pick(keyPadPressed, c);
        public Sprite KeyPadGlow(PaletteColor c) => Pick(keyPadGlow, c);
        public Sprite LaneFlash(PaletteColor c) => Pick(laneFlash, c);
        public Sprite ButtonFrame(PaletteColor c) => Pick(buttonFrame, c);
        public Sprite ButtonGlow(PaletteColor c) => Pick(buttonGlow, c);

        /// <summary>Renk için üretilmemişse beyaz sürüme düşer.</summary>
        private static Sprite Pick(Sprite[] arr, PaletteColor c)
        {
            if (arr == null || arr.Length < PaletteSize) return null;
            return arr[(int)c] != null ? arr[(int)c] : arr[(int)PaletteColor.White];
        }

#if UNITY_EDITOR
        /// <summary>Bake çıktısı (Editor). Diziler palet indeksine göre, uzunluk <see cref="PaletteSize"/>.</summary>
        public sealed class BakedSet
        {
            public Material Additive;
            public Sprite LanePanel, HitLineIdle, HitLineBeat, Particle1, Particle2, Sky, Logo, LogoGlow, IconPlay, IconGear, IconBack;
            public Sprite[] Words = new Sprite[4], WordGlows = new Sprite[4];
            public Sprite[] Labels = new Sprite[LabelCount], LabelGlows = new Sprite[LabelCount];
            public Sprite[] NoteHead = new Sprite[PaletteSize], NoteGlow = new Sprite[PaletteSize];
            public Sprite[] HoldBody = new Sprite[PaletteSize], HoldGlow = new Sprite[PaletteSize];
            public Sprite[] HoldTail = new Sprite[PaletteSize], TailGlow = new Sprite[PaletteSize];
            public Sprite[] KeyPadIdle = new Sprite[PaletteSize], KeyPadPressed = new Sprite[PaletteSize], KeyPadGlow = new Sprite[PaletteSize];
            public Sprite[] LaneFlash = new Sprite[PaletteSize];
            public Sprite[] ButtonFrame = new Sprite[PaletteSize], ButtonGlow = new Sprite[PaletteSize];
        }

        public void SetBaked(BakedSet b)
        {
            additive = b.Additive;
            lanePanel = b.LanePanel;
            hitLineIdle = b.HitLineIdle;
            hitLineBeat = b.HitLineBeat;
            particle1 = b.Particle1;
            particle2 = b.Particle2;
            sky = b.Sky;
            logo = b.Logo;
            logoGlow = b.LogoGlow;
            iconPlay = b.IconPlay;
            iconGear = b.IconGear;
            iconBack = b.IconBack;
            words = b.Words;
            wordGlows = b.WordGlows;
            labels = b.Labels;
            labelGlows = b.LabelGlows;
            noteHead = b.NoteHead;
            noteGlow = b.NoteGlow;
            holdBody = b.HoldBody;
            holdGlow = b.HoldGlow;
            holdTail = b.HoldTail;
            tailGlow = b.TailGlow;
            keyPadIdle = b.KeyPadIdle;
            keyPadPressed = b.KeyPadPressed;
            keyPadGlow = b.KeyPadGlow;
            laneFlash = b.LaneFlash;
            buttonFrame = b.ButtonFrame;
            buttonGlow = b.ButtonGlow;
        }
#endif
    }
}
