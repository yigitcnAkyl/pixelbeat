using System;

namespace PixelBeat.EditorTools
{
    /// <summary>
    /// Pixel art için yumuşatmasız döndürme (RotSprite benzeri). Girdi/çıktı ikili maske: [x, y], y aşağıdan yukarı.
    /// 1) Scale2x ile 3 kez büyütme (8x): kenarlar kırılmadan inceltilir.
    /// 2) Her çıktı pikselinin merkezi ters döndürülüp 8x görüntüden en yakın komşu ile örneklenir
    ///    (döndürme + 8x küçültme tek adımda). Ara renk oluşmaz.
    /// 90°'nin katlarında birebir piksel dönüşümü yapılır.
    /// </summary>
    public static class PixelRotator
    {
        private const int Upscale = 8;

        /// <summary>Çıktı pikseli içindeki alt-örnek ızgarası (n x n).</summary>
        private const int SubSamples = 4;

        /// <summary>
        /// Saat yönünde döndürür. Çıktı boyutları çifttir (merkez tam piksel sınırına düşer) ve 1 px kenar boşluğu vardır.
        /// Her çıktı pikseli, içindeki 4x4 alt-noktanın en az <paramref name="coverage"/> oranı doluysa yanar
        /// (tek merkez örneği 1 px'lik çapraz çizgileri koparıyordu). Sonuç yine ikilidir, ara renk yok.
        /// </summary>
        public static bool[,] RotateClockwise(bool[,] src, float angleDeg, float coverage = 0.4f)
        {
            float a = ((angleDeg % 360f) + 360f) % 360f;
            if (Math.Abs(a % 90f) < 1e-4f || Math.Abs(a % 90f - 90f) < 1e-4f)
                return RotateQuarterTurns(src, (int)Math.Round(a / 90f) % 4);

            int w = src.GetLength(0), h = src.GetLength(1);
            var big = Scale2x(Scale2x(Scale2x(src)));

            double rad = a * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);

            // Döndürülmüş sınır kutusu (kaynak merkezine göre).
            double halfW = w / 2.0, halfH = h / 2.0;
            double extX = Math.Abs(halfW * cos) + Math.Abs(halfH * sin);
            double extY = Math.Abs(halfW * sin) + Math.Abs(halfH * cos);
            int outW = MakeEven((int)Math.Ceiling(extX * 2) + 2);
            int outH = MakeEven((int)Math.Ceiling(extY * 2) + 2);

            var dst = new bool[outW, outH];
            int bw = big.GetLength(0), bh = big.GetLength(1);

            int needed = Math.Max(1, (int)Math.Ceiling(coverage * SubSamples * SubSamples));

            for (int oy = 0; oy < outH; oy++)
            for (int ox = 0; ox < outW; ox++)
            {
                int hits = 0;
                for (int j = 0; j < SubSamples; j++)
                for (int i = 0; i < SubSamples; i++)
                {
                    // Alt-nokta, çıktı merkezine göre.
                    double qx = ox + (i + 0.5) / SubSamples - outW / 2.0;
                    double qy = oy + (j + 0.5) / SubSamples - outH / 2.0;

                    // Saat yönü döndürmenin tersi = saat yönünün tersine döndür.
                    double sx = qx * cos - qy * sin + halfW;
                    double sy = qx * sin + qy * cos + halfH;

                    int bx = (int)Math.Floor(sx * Upscale);
                    int by = (int)Math.Floor(sy * Upscale);
                    if (bx >= 0 && by >= 0 && bx < bw && by < bh && big[bx, by]) hits++;
                }
                dst[ox, oy] = hits >= needed;
            }

            return dst;
        }

        /// <summary>Scale2x (EPX): her pikseli 2x2'ye böler, çapraz kenarları yumuşatmadan tamamlar.</summary>
        public static bool[,] Scale2x(bool[,] src)
        {
            int w = src.GetLength(0), h = src.GetLength(1);
            var dst = new bool[w * 2, h * 2];

            bool At(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && src[x, y];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool e = src[x, y];
                bool b = At(x, y + 1); // üst
                bool d = At(x - 1, y); // sol
                bool f = At(x + 1, y); // sağ
                bool hh = At(x, y - 1); // alt

                bool topLeft = (d == b && b != f && d != hh) ? d : e;
                bool topRight = (b == f && b != d && f != hh) ? f : e;
                bool bottomLeft = (d == hh && d != b && hh != f) ? d : e;
                bool bottomRight = (hh == f && d != hh && b != f) ? f : e;

                dst[x * 2, y * 2 + 1] = topLeft;
                dst[x * 2 + 1, y * 2 + 1] = topRight;
                dst[x * 2, y * 2] = bottomLeft;
                dst[x * 2 + 1, y * 2] = bottomRight;
            }

            return dst;
        }

        /// <summary>Saat yönünde k × 90° (birebir), çift boyuta tamamlanır ve 1 px kenar boşluğu eklenir.</summary>
        private static bool[,] RotateQuarterTurns(bool[,] src, int k)
        {
            int w = src.GetLength(0), h = src.GetLength(1);
            bool swap = k % 2 == 1;
            int rw = swap ? h : w, rh = swap ? w : h;
            int outW = MakeEven(rw + 2), outH = MakeEven(rh + 2);
            int offX = (outW - rw) / 2, offY = (outH - rh) / 2;
            var dst = new bool[outW, outH];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!src[x, y]) continue;
                int nx, ny;
                switch (k)
                {
                    case 1: nx = y; ny = w - 1 - x; break;          // 90° saat yönü
                    case 2: nx = w - 1 - x; ny = h - 1 - y; break;  // 180°
                    case 3: nx = h - 1 - y; ny = x; break;          // 270°
                    default: nx = x; ny = y; break;
                }
                dst[nx + offX, ny + offY] = true;
            }

            return dst;
        }

        private static int MakeEven(int n) => n % 2 == 0 ? n : n + 1;

        public static int Count(bool[,] mask)
        {
            int n = 0;
            foreach (bool b in mask) if (b) n++;
            return n;
        }
    }
}
