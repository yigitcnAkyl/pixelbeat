using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>GDD §8 neon paleti (16 renk). Tüm placeholder renkler buradan seçilir.</summary>
    public enum PaletteColor
    {
        Void = 0,
        Night = 1,
        Slate = 2,
        Steel = 3,
        Ash = 4,
        Haze = 5,
        White = 6,
        Cyan = 7,
        Teal = 8,
        Magenta = 9,
        Plum = 10,
        Violet = 11,
        Lime = 12,
        Gold = 13,
        Orange = 14,
        Red = 15,
    }

    /// <summary>
    /// Inspector'da paletten seçilen renk (+ isteğe bağlı saydamlık). Kodda Color'a kendiliğinden dönüşür,
    /// böylece serbest renk girmek mümkün olmaz.
    /// </summary>
    [System.Serializable]
    public struct PaletteRef
    {
        public PaletteColor color;
        [Tooltip("0 = opak")]
        [Range(0f, 1f)] public float transparency;

        public PaletteRef(PaletteColor color, float alpha = 1f)
        {
            this.color = color;
            transparency = 1f - alpha;
        }

        public float Alpha => 1f - transparency;

        public Color ToColor() => Palette.Get(color, Alpha);

        public static implicit operator Color(PaletteRef p) => p.ToColor();
        public static implicit operator PaletteRef(PaletteColor c) => new PaletteRef(c);
    }

    public static class Palette
    {
        public static readonly Color32[] Colors =
        {
            new Color32(0x0B, 0x0B, 0x16, 0xFF), // Void
            new Color32(0x14, 0x14, 0x28, 0xFF), // Night
            new Color32(0x22, 0x22, 0x3A, 0xFF), // Slate
            new Color32(0x34, 0x34, 0x48, 0xFF), // Steel
            new Color32(0x50, 0x50, 0x68, 0xFF), // Ash
            new Color32(0x8A, 0x8A, 0xB0, 0xFF), // Haze
            new Color32(0xFF, 0xFF, 0xFF, 0xFF), // White
            new Color32(0x33, 0xFF, 0xFF, 0xFF), // Cyan
            new Color32(0x1C, 0x8C, 0x9C, 0xFF), // Teal
            new Color32(0xFF, 0x40, 0xC0, 0xFF), // Magenta
            new Color32(0x8E, 0x24, 0x70, 0xFF), // Plum
            new Color32(0x7A, 0x4D, 0xFF, 0xFF), // Violet
            new Color32(0x66, 0xFF, 0x66, 0xFF), // Lime
            new Color32(0xFF, 0xD0, 0x40, 0xFF), // Gold
            new Color32(0xFF, 0x8A, 0x3D, 0xFF), // Orange
            new Color32(0xFF, 0x40, 0x60, 0xFF), // Red
        };

        public static Color Get(PaletteColor c) => Colors[(int)c];

        public static Color Get(PaletteColor c, float alpha)
        {
            Color col = Colors[(int)c];
            col.a = alpha;
            return col;
        }

        /// <summary>Nota / gövde gölgesi için koyu eş (Cyan → Teal, Magenta → Plum, diğerleri → Steel).</summary>
        public static PaletteColor DarkOf(PaletteColor c)
        {
            switch (c)
            {
                case PaletteColor.Cyan: return PaletteColor.Teal;
                case PaletteColor.Magenta: return PaletteColor.Plum;
                case PaletteColor.Violet: return PaletteColor.Plum;
                case PaletteColor.Lime: return PaletteColor.Teal;
                case PaletteColor.Gold: return PaletteColor.Orange;
                case PaletteColor.Orange: return PaletteColor.Plum;
                case PaletteColor.Red: return PaletteColor.Plum;
                case PaletteColor.White: return PaletteColor.Haze;
                case PaletteColor.Haze: return PaletteColor.Ash;
                case PaletteColor.Ash: return PaletteColor.Steel;
                default: return PaletteColor.Void;
            }
        }
    }
}
