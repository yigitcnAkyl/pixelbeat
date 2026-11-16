using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Neon placeholder pixel art sprite'ları (GDD §8). Editor'de *PixelBeat > Bake Neon Sprites* ile paletten üretilir;
    /// PNG'ler elle çizilenlerle değiştirilebilir. Çok tonlu sprite'lar renk başına ayrı üretilir (tint yok → sadece palet renkleri).
    /// </summary>
    [CreateAssetMenu(fileName = "NeonSkin", menuName = "PixelBeat/Neon Skin")]
    public class NeonSkin : ScriptableObject
    {
        /// <summary>Renk başına üretilen sprite'ların renkleri.</summary>
        public static readonly PaletteColor[] AccentColors =
        {
            PaletteColor.Cyan, PaletteColor.Magenta, PaletteColor.Violet, PaletteColor.Lime,
            PaletteColor.Gold, PaletteColor.Orange, PaletteColor.Red, PaletteColor.White, PaletteColor.Ash,
        };

        public const int PaletteSize = 16;

        [Tooltip("Additive parlama materyali (PixelBeat/SpriteAdditive).")]
        [SerializeField] private Material additive;

        [Header("Tekli")]
        [SerializeField] private Sprite lanePanel;
        [SerializeField] private Sprite hitLineIdle;
        [SerializeField] private Sprite hitLineBeat;
        [SerializeField] private Sprite keyPadIdle;
        [SerializeField] private Sprite particle1;
        [SerializeField] private Sprite particle2;
        [Tooltip("Varsayılan arka plan (gökyüzü + güneş + şehir + zemin ızgarası). Pivot = ufuk çizgisi.")]
        [SerializeField] private Sprite sky;

        [Header("Renk başına (palet indeksine göre)")]
        [SerializeField] private Sprite[] noteHead = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] holdBody = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] holdTail = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] noteGlow = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] keyPadPressed = new Sprite[PaletteSize];
        [SerializeField] private Sprite[] laneFlash = new Sprite[PaletteSize];

        public Material Additive => additive;
        public Sprite LanePanel => lanePanel;
        public Sprite HitLineIdle => hitLineIdle;
        public Sprite HitLineBeat => hitLineBeat;
        public Sprite KeyPadIdle => keyPadIdle;
        public Sprite Particle1 => particle1;
        public Sprite Particle2 => particle2;
        public Sprite Sky => sky;

        public Sprite NoteHead(PaletteColor c) => Pick(noteHead, c);
        public Sprite HoldBody(PaletteColor c) => Pick(holdBody, c);
        public Sprite HoldTail(PaletteColor c) => Pick(holdTail, c);
        public Sprite NoteGlow(PaletteColor c) => Pick(noteGlow, c);
        public Sprite KeyPadPressed(PaletteColor c) => Pick(keyPadPressed, c);
        public Sprite LaneFlash(PaletteColor c) => Pick(laneFlash, c);

        /// <summary>Renk için üretilmemişse beyaz sürüme düşer.</summary>
        private static Sprite Pick(Sprite[] arr, PaletteColor c)
        {
            if (arr == null || arr.Length < PaletteSize) return null;
            return arr[(int)c] != null ? arr[(int)c] : arr[(int)PaletteColor.White];
        }

#if UNITY_EDITOR
        public void SetBaked(Material newAdditive, Sprite newLanePanel, Sprite newHitIdle, Sprite newHitBeat, Sprite newKeyIdle,
            Sprite newP1, Sprite newP2, Sprite newSky, Sprite[] heads, Sprite[] bodies, Sprite[] tails, Sprite[] glows,
            Sprite[] pressed, Sprite[] flashes)
        {
            additive = newAdditive;
            lanePanel = newLanePanel;
            hitLineIdle = newHitIdle;
            hitLineBeat = newHitBeat;
            keyPadIdle = newKeyIdle;
            particle1 = newP1;
            particle2 = newP2;
            sky = newSky;
            noteHead = heads;
            holdBody = bodies;
            holdTail = tails;
            noteGlow = glows;
            keyPadPressed = pressed;
            laneFlash = flashes;
        }
#endif
    }
}
