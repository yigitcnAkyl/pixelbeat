using System;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.EditorTools.NeonArt
{
    /// <summary>
    /// AI görselini pixel art'a çevirme: nearest-neighbor küçültme + GDD paletine indirgeme (OKLab'de en yakın renk),
    /// isteğe bağlı 2x2 Bayer dither. Unity API'si kullanmaz (sadece Color32 veri tipi); test edilebilir.
    /// Piksel dizileri satır satır, alttan yukarı (Texture2D.GetPixels32 düzeni).
    /// </summary>
    public static class PaletteImageTools
    {
        /// <summary>Her hedef pikselin merkezine denk gelen kaynak pikseli alır.</summary>
        public static Color32[] ResizeNearest(Color32[] src, int srcW, int srcH, int dstW, int dstH)
        {
            if (src.Length != srcW * srcH) throw new ArgumentException("Boyut uyuşmuyor.");
            var dst = new Color32[dstW * dstH];
            for (int y = 0; y < dstH; y++)
            {
                int sy = Math.Min(srcH - 1, (int)((y + 0.5) * srcH / dstH));
                for (int x = 0; x < dstW; x++)
                {
                    int sx = Math.Min(srcW - 1, (int)((x + 0.5) * srcW / dstW));
                    dst[y * dstW + x] = src[sy * srcW + sx];
                }
            }
            return dst;
        }

        /// <summary>
        /// Her pikseli en yakın palet rengine indirger. alpha &lt; alphaThreshold → tamamen saydam, değilse opak.
        /// dither: 2x2 Bayer ile en yakın iki palet rengi arasında desen (geçişlerde bant yerine doku).
        /// </summary>
        public static Color32[] Quantize(Color32[] src, int width, byte alphaThreshold = 128, bool dither = false)
        {
            var pal = PaletteLab();
            var dst = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (c.a < alphaThreshold)
                {
                    dst[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                var lab = ToOkLab(c);
                Nearest2(pal, lab, out int best, out double d1, out int second, out double d2);
                int pick = best;
                if (dither && second >= 0)
                {
                    // İki aday arasındaki konum (0 = best, 0.5 = tam orta) → Bayer eşiğiyle seç.
                    double t = d1 / (d1 + d2 + 1e-12);
                    int x = i % width, y = i / width;
                    double threshold = Bayer2[(y & 1) * 2 + (x & 1)];
                    if (t > threshold) pick = second;
                }
                dst[i] = Palette.Colors[pick];
            }
            return dst;
        }

        // 2x2 Bayer eşikleri. t (0 = tam best, 0.5 = iki rengin tam ortası) → ikinci rengin oranı ≈ t
        // (ortada %50 dama, palet rengine yakınken hiç).
        private static readonly double[] Bayer2 = { 0.125, 0.625, 0.875, 0.375 };

        public static double[][] PaletteLab()
        {
            var pal = new double[Palette.Colors.Length][];
            for (int i = 0; i < pal.Length; i++) pal[i] = ToOkLab(Palette.Colors[i]);
            return pal;
        }

        public static int NearestIndex(Color32 c)
        {
            Nearest2(PaletteLab(), ToOkLab(c), out int best, out _, out _, out _);
            return best;
        }

        private static void Nearest2(double[][] pal, double[] lab, out int best, out double d1, out int second, out double d2)
        {
            best = -1; second = -1;
            d1 = double.MaxValue; d2 = double.MaxValue;
            for (int i = 0; i < pal.Length; i++)
            {
                double dl = pal[i][0] - lab[0], da = pal[i][1] - lab[1], db = pal[i][2] - lab[2];
                double d = Math.Sqrt(dl * dl + da * da + db * db);
                if (d < d1)
                {
                    second = best; d2 = d1;
                    best = i; d1 = d;
                }
                else if (d < d2)
                {
                    second = i; d2 = d;
                }
            }
        }

        /// <summary>sRGB (8 bit) → OKLab (algısal olarak eşit aralıklı renk uzayı).</summary>
        public static double[] ToOkLab(Color32 c)
        {
            double r = SrgbToLinear(c.r / 255.0), g = SrgbToLinear(c.g / 255.0), b = SrgbToLinear(c.b / 255.0);
            double l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
            double m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
            double s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;
            double l_ = Math.Pow(l, 1.0 / 3), m_ = Math.Pow(m, 1.0 / 3), s_ = Math.Pow(s, 1.0 / 3);
            return new[]
            {
                0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
                1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
                0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_,
            };
        }

        private static double SrgbToLinear(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
