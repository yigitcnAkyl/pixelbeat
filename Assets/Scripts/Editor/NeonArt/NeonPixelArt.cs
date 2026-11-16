using System;
using System.Collections.Generic;
using PixelBeat.Core;
using PixelBeat.UI;

namespace PixelBeat.EditorTools.NeonArt
{
    /// <summary>Palet indeksli piksel tuvali: -1 = saydam. [x, y], y aşağıdan yukarı.</summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        private readonly int[] px;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            px = new int[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = -1;
        }

        public int this[int x, int y]
        {
            get => x < 0 || y < 0 || x >= Width || y >= Height ? -1 : px[y * Width + x];
            set
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                px[y * Width + x] = value;
            }
        }

        public bool Filled(int x, int y) => this[x, y] >= 0;

        public void Set(int x, int y, PaletteColor c) => this[x, y] = (int)c;

        public void FillRect(int x0, int y0, int w, int h, PaletteColor c)
        {
            for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                Set(x, y, c);
        }

        public void Line(int x0, int y0, int x1, int y1, PaletteColor c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }

    /// <summary>
    /// Neon placeholder sprite desenleri (GDD §8 sanat yönü, 360 px), sadece palet renkleri. Boyutlar ve 9-slice kenarları <see cref="Spec"/>'te.
    /// Parlamalar (hale) ayrı sprite'lardır ve additive çizilir; bloom yok.
    /// </summary>
    public static class NeonPixelArt
    {
        public struct Spec
        {
            public int W, H;
            /// <summary>Sol, alt, sağ, üst (9-slice kenarları, piksel).</summary>
            public int L, B, R, T;
        }

        // ---------- Boyutlar ----------

        /// <summary>Tap nota / hold başı elması (kenar uzunluğu, piksel).</summary>
        public const int NoteSize = 26;
        /// <summary>Elmas halesinin notadan taşma payı (her yönde).</summary>
        public const int HaloMargin = 7;
        public const int NoteHaloSize = NoteSize + 2 * HaloMargin;
        public const int TailSize = 14;
        public const int TailHaloSize = TailSize + 2 * HaloMargin;
        /// <summary>Hold ışını genişliği ve halesi (dikey döşenir).</summary>
        public const int BeamWidth = 12;
        public const int BeamGlowWidth = 28;
        public const int KeyGlowMargin = 4;

        public static readonly Spec BeamSpec = new Spec { W = BeamWidth, H = 2 };
        public static readonly Spec BeamGlowSpec = new Spec { W = BeamGlowWidth, H = 2 };
        public static readonly Spec KeyPadSpec = new Spec { W = 24, H = 24, L = 8, B = 8, R = 8, T = 8 };
        public static readonly Spec KeyGlowSpec = new Spec { W = 32, H = 32, L = 12, B = 12, R = 12, T = 12 };
        public static readonly Spec ButtonSpec = new Spec { W = 24, H = 24, L = 8, B = 8, R = 8, T = 8 };
        public static readonly Spec HitLineSpec = new Spec { W = 8, H = 4, L = 1, R = 1 };
        public static readonly Spec LaneFlashSpec = new Spec { W = 8, H = 96 };
        public static readonly Spec LanePanelSpec = new Spec { W = 16, H = 16 };
        public const int SkyWidth = 360, SkyHeight = 960, SkyHorizon = 400;

        // ---------- Elmas (tap nota, hold başı / kuyruğu) ----------

        /// <summary>
        /// Elmas ölçüsü: tuvalin merkezine göre |2x−(n−1)| + |2y−(n−1)|. İçeride ≤ n; bir piksel içeri = 2 azalır.
        /// </summary>
        public static int DiamondMetric(int x, int y, int n) => Math.Abs(2 * x - (n - 1)) + Math.Abs(2 * y - (n - 1));

        /// <summary>
        /// Neon elmas: dışta 1 px koyu kenar, 2 px şerit rengi çerçeve, üst yarıda beyaz iç çizgi, koyu iç,
        /// ortada koyu → renkli küçük taş.
        /// </summary>
        public static PixelCanvas Diamond(PaletteColor c, int n = NoteSize)
        {
            var p = new PixelCanvas(n, n);
            var dark = Palette.DarkOf(c);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int d = DiamondMetric(x, y, n);
                if (d > n) continue;
                int k = (n - d) / 2; // dış kenardan içeri piksel
                bool upper = 2 * y >= n;
                PaletteColor col;
                if (k == 0) col = dark;
                else if (k <= 2) col = c;
                else if (k == 3) col = upper ? PaletteColor.White : c;
                else if (d <= n * 16 / 100) col = c;
                else if (d <= n * 36 / 100) col = dark;
                else col = PaletteColor.Night;
                p.Set(x, y, col);
            }
            return p;
        }

        /// <summary>Elmas halesi (additive): notanın dışında, uzaklaştıkça seyrelen dither. İçi boş.</summary>
        public static PixelCanvas DiamondHalo(PaletteColor c, int noteSize = NoteSize)
        {
            int m = HaloMargin, n = noteSize + 2 * m;
            var p = new PixelCanvas(n, n);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int d = DiamondMetric(x - m, y - m, noteSize);
                if (d <= noteSize) continue;
                int k = (d - noteSize + 1) / 2; // nota kenarından dışarı piksel (1..)
                if (k > m) continue;
                if (Dither(x, y, k)) p.Set(x, y, c);
            }
            return p;
        }

        /// <summary>Uzaklık k (1 = bitişik) için dither yoğunluğu: 1 tam, 2–3 dama, 4–5 çeyrek, 6–7 seyrek.</summary>
        public static bool Dither(int x, int y, int k)
        {
            if (k <= 1) return true;
            if (k <= 3) return ((x + y) & 1) == 0;
            if (k <= 5) return (x & 1) == 0 && (y & 1) == 0;
            return (x & 3) == 0 && (y & 3) == ((x >> 2) & 1) * 2;
        }

        // ---------- Hold ışını ----------

        /// <summary>Hold gövdesi: şerit rengi ışın, kenarlarda koyu, ortaya doğru açık, merkezde beyaz (dikey döşenir).</summary>
        public static PixelCanvas Beam(PaletteColor c)
        {
            var s = BeamSpec;
            var p = new PixelCanvas(s.W, s.H);
            var dark = Palette.DarkOf(c);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int fromEdge = Math.Min(x, s.W - 1 - x); // 0..5
                PaletteColor col = fromEdge == 0 ? dark : fromEdge <= 3 ? c : PaletteColor.White;
                if (fromEdge == 3 && ((x + y) & 1) == 0) col = PaletteColor.White; // açık merkeze dither geçiş
                p.Set(x, y, col);
            }
            return p;
        }

        /// <summary>Işın halesi (additive): ışının iki yanında seyrelen dither, ışının üstünde dama (tutulunca parlaklaştırır).</summary>
        public static PixelCanvas BeamGlow(PaletteColor c)
        {
            var s = BeamGlowSpec;
            var p = new PixelCanvas(s.W, s.H);
            int beamL = (s.W - BeamWidth) / 2, beamR = beamL + BeamWidth - 1;
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int k = x < beamL ? beamL - x : x > beamR ? x - beamR : 0;
                bool on = k == 0 ? ((x + y) & 1) == 0 : Dither(x, y, k + 1);
                if (on) p.Set(x, y, c);
            }
            return p;
        }

        // ---------- Yuvarlak köşeli çerçeveler (dokunma düğmesi, buton) ----------

        /// <summary>Yuvarlak köşeli dikdörtgende (x, y)'nin dış kenara uzaklığı (0 = en dış piksel); dışarıdaysa -1.</summary>
        public static int RoundRectDepth(int x, int y, int w, int h, int radius)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return -1;
            int depth = Math.Min(Math.Min(x, w - 1 - x), Math.Min(y, h - 1 - y));
            int cx = x < radius ? radius - x : x > w - 1 - radius ? x - (w - 1 - radius) : 0;
            int cy = y < radius ? radius - y : y > h - 1 - radius ? y - (h - 1 - radius) : 0;
            if (cx > 0 && cy > 0)
            {
                double dist = Math.Sqrt(cx * cx + cy * cy);
                if (dist > radius + 0.5) return -1;
                depth = Math.Min(depth, (int)Math.Floor(radius + 0.5 - dist));
            }
            return depth;
        }

        /// <summary>Dokunma düğmesi (boşta): koyu iç, şerit rengi neon çerçeve, ikon yok.</summary>
        public static PixelCanvas KeyPadIdle(PaletteColor c)
        {
            var s = KeyPadSpec;
            var p = new PixelCanvas(s.W, s.H);
            var dark = Palette.DarkOf(c);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int d = RoundRectDepth(x, y, s.W, s.H, 4);
                if (d < 0) continue;
                p.Set(x, y, d == 0 ? dark : d <= 2 ? c : d == 3 ? dark : PaletteColor.Night);
            }
            return p;
        }

        /// <summary>
        /// Dokunma düğmesi (basılı): çerçeve beyazlaşır, iç şerit rengiyle dolar. Üstte beyaz parıltı satırı, altta koyu bant;
        /// ikisi de 9-slice kenar bölgelerinde, orta bölge tek düze (esneyince bozulmaz).
        /// </summary>
        public static PixelCanvas KeyPadPressed(PaletteColor c)
        {
            var s = KeyPadSpec;
            var p = new PixelCanvas(s.W, s.H);
            var dark = Palette.DarkOf(c);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int d = RoundRectDepth(x, y, s.W, s.H, 4);
                if (d < 0) continue;
                PaletteColor col;
                if (d == 0) col = c;
                else if (d <= 2) col = PaletteColor.White;
                else if (d == 3) col = c;
                else if (y == s.H - 5) col = PaletteColor.White;                       // üst parıltı
                else if (y < s.B) col = y == s.B - 1 && (x & 1) == 0 ? c : dark;      // alt koyu bant, dither geçiş
                else col = c;
                p.Set(x, y, col);
            }
            return p;
        }

        /// <summary>Yuvarlak köşeli çerçeve halesi (additive, 9-slice): çerçevenin <see cref="KeyGlowMargin"/> px dışına dither.</summary>
        public static PixelCanvas FrameGlow(PaletteColor c)
        {
            var s = KeyGlowSpec;
            int m = KeyGlowMargin;
            var p = new PixelCanvas(s.W, s.H);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                if (RoundRectDepth(x - m, y - m, s.W - 2 * m, s.H - 2 * m, 4) >= 0) continue;
                // Çerçeveye uzaklık (piksel): genişletilmiş dikdörtgenlerden ilk içine girilen.
                for (int k = 1; k <= m; k++)
                {
                    if (RoundRectDepth(x - m + k, y - m + k, s.W - 2 * m + 2 * k, s.H - 2 * m + 2 * k, 4 + k) < 0) continue;
                    if (Dither(x, y, k * 2 - 1)) p.Set(x, y, c);
                    break;
                }
            }
            return p;
        }

        /// <summary>Menü butonu çerçevesi (9-slice): koyu iç, 2 px neon çerçeve, üst kenarda beyaz parıltı.</summary>
        public static PixelCanvas ButtonFrame(PaletteColor c)
        {
            var s = ButtonSpec;
            var p = new PixelCanvas(s.W, s.H);
            var dark = Palette.DarkOf(c);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int d = RoundRectDepth(x, y, s.W, s.H, 4);
                if (d < 0) continue;
                PaletteColor col;
                if (d == 0) col = dark;
                else if (d <= 2) col = d == 1 && y >= s.H - 2 && x > 4 && x < s.W - 5 ? PaletteColor.White : c;
                else if (d == 3) col = dark;
                else col = y >= s.H - 6 ? PaletteColor.Slate : PaletteColor.Night;
                p.Set(x, y, col);
            }
            return p;
        }

        // ---------- Vuruş çizgisi, şerit, parlama ----------

        public static PixelCanvas HitLine(PaletteColor core, PaletteColor edge)
        {
            var s = HitLineSpec;
            var p = new PixelCanvas(s.W, s.H);
            for (int x = 0; x < s.W; x++)
            {
                p.Set(x, 0, edge);
                p.Set(x, 1, core);
                p.Set(x, 2, core);
                p.Set(x, 3, edge);
            }
            return p;
        }

        /// <summary>Şerit parlaması (additive): alttan yukarı azalan dither yoğunluğu; 8 px periyotlu (Tiled).</summary>
        public static PixelCanvas LaneFlash(PaletteColor c)
        {
            var s = LaneFlashSpec;
            var p = new PixelCanvas(s.W, s.H);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                bool on;
                if (y < 12) on = true;
                else if (y < 32) on = ((x + y) & 1) == 0;
                else if (y < 60) on = (x & 1) == 0 && (y & 1) == 0;
                else on = (x & 3) == ((y >> 1) & 3) && (y & 1) == 0;
                if (on) p.Set(x, y, c);
            }
            return p;
        }

        /// <summary>Şerit paneli döşemesi: Night zemin, seyrek Slate noktalar (hafif doku).</summary>
        public static PixelCanvas LanePanel()
        {
            var s = LanePanelSpec;
            var p = new PixelCanvas(s.W, s.H);
            p.FillRect(0, 0, s.W, s.H, PaletteColor.Night);
            p.Set(8, 0, PaletteColor.Slate);
            p.Set(8, 8, PaletteColor.Slate);
            p.Set(0, 4, PaletteColor.Slate);
            p.Set(0, 12, PaletteColor.Slate);
            return p;
        }

        public static PixelCanvas Solid(int size, PaletteColor c)
        {
            var p = new PixelCanvas(size, size);
            p.FillRect(0, 0, size, size, c);
            return p;
        }

        // ---------- Kelimeler (PERFECT! vb.) ----------

        /// <summary>Kelime sprite'ının harf alanına göre kenar payı (dış hat + gölge).</summary>
        public const int WordPad = 2;
        public const int WordGlowMargin = 4;
        /// <summary>Her bu kadar satırda bir sağa 1 px (italik).</summary>
        public const int ItalicRowsPerPixel = 3;

        /// <summary>Kelime harflerinin büyütmesi (5x7 font → 15x21).</summary>
        public const int WordScale = 3;

        /// <summary>Kalın harf maskesi: 5x7 font scale kat, sonra 1 px sağa kalınlaştırma. [x, y], y aşağıdan yukarı.</summary>
        public static bool[,] BoldMask(string text, int scale = WordScale)
        {
            var m = PixelFont.Rasterize(text, scale);
            int w = m.GetLength(0) + 1, h = m.GetLength(1);
            var b = new bool[w, h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                b[x, y] = (x < w - 1 && m[x, y]) || (x > 0 && m[x - 1, y]);
            return b;
        }

        public static int ItalicShift(int y) => y / ItalicRowsPerPixel;

        /// <summary>
        /// Değerlendirme yazısı: kalın, sağa yatık, yukarıdan aşağı renk geçişi (dither'lı), en üst satır beyaz parıltı,
        /// sağ-aşağıda koyu renkli gölge ve kapalı koyu (Void) dış hat. colors: yukarıdan aşağı 2–3 ton.
        /// </summary>
        public static PixelCanvas Word(string text, params PaletteColor[] colors) => Word(text, WordScale, colors);

        /// <summary>Kelime / etiket (derece harfi, FAILED, YENİ REKOR, rozetler): scale = font büyütmesi.</summary>
        public static PixelCanvas Word(string text, int scale, params PaletteColor[] colors)
        {
            var mask = BoldMask(text, scale);
            int mw = mask.GetLength(0), mh = mask.GetLength(1);
            int pad = WordPad;
            int w = mw + ItalicShift(mh - 1) + 2 * pad, h = mh + 2 * pad;
            var p = new PixelCanvas(w, h);
            var letters = new bool[w, h];

            for (int y = 0; y < mh; y++)
            for (int x = 0; x < mw; x++)
            {
                if (!mask[x, y]) continue;
                int px = pad + x + ItalicShift(y), py = pad + y;
                letters[px, py] = true;
                p.Set(px, py, y == mh - 1 ? PaletteColor.White : GradientAt(colors, y, mh, px));
            }

            // Gölge: sağ-aşağı 1 px, alt rengin koyu eşi.
            var shadow = Palette.DarkOf(colors[colors.Length - 1]);
            for (int y = 1; y < h; y++)
            for (int x = 0; x < w - 1; x++)
                if (letters[x, y] && !letters[x + 1, y - 1]) p.Set(x + 1, y - 1, shadow);

            Outline(p, PaletteColor.Void);
            return p;
        }

        /// <summary>Kelime halesi (additive): kelimenin (dış hat dahil) <see cref="WordGlowMargin"/> px dışına dither, üst renkte.</summary>
        public static PixelCanvas WordGlow(PixelCanvas word, PaletteColor c) => GlowAround(word, c, WordGlowMargin);

        /// <summary>Şeklin (dolu pikseller) etrafına margin px dither hale; çıktı her yönde margin px büyük, şeklin kendisi boş.</summary>
        public static PixelCanvas GlowAround(PixelCanvas shape, PaletteColor c, int margin)
        {
            var p = new PixelCanvas(shape.Width + 2 * margin, shape.Height + 2 * margin);
            for (int y = 0; y < p.Height; y++)
            for (int x = 0; x < p.Width; x++)
            {
                if (shape.Filled(x - margin, y - margin)) continue;
                int k = ChebyshevToShape(shape, x - margin, y - margin, margin);
                if (k > 0 && Dither(x, y, k * 2 - 1)) p.Set(x, y, c);
            }
            return p;
        }

        private static int ChebyshevToShape(PixelCanvas s, int x, int y, int max)
        {
            for (int k = 1; k <= max; k++)
            for (int dy = -k; dy <= k; dy++)
            for (int dx = -k; dx <= k; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != k) continue;
                if (s.Filled(x + dx, y + dy)) return k;
            }
            return 0;
        }

        /// <summary>Satır y (0 = alt) için yukarıdan aşağı renk geçişi; bant sınırlarında 2 satır dama dither.</summary>
        public static PaletteColor GradientAt(PaletteColor[] colors, int y, int height, int x)
        {
            if (colors.Length == 1) return colors[0];
            double fromTop = (height - 1 - y) / (double)(height - 1); // 0 üst, 1 alt
            double pos = fromTop * (colors.Length - 1);
            int i = Math.Min(colors.Length - 2, (int)Math.Floor(pos));
            double f = pos - i;
            if (f < 0.35) return colors[i];
            if (f > 0.65) return colors[i + 1];
            return ((x + y) & 1) == 0 ? colors[i] : colors[i + 1];
        }

        /// <summary>Dolu piksellerin 8-komşu boş piksellerine dış hat çizer.</summary>
        public static void Outline(PixelCanvas p, PaletteColor c)
        {
            var add = new List<(int, int)>();
            for (int y = 0; y < p.Height; y++)
            for (int x = 0; x < p.Width; x++)
            {
                if (p.Filled(x, y)) continue;
                bool near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                for (int dx = -1; dx <= 1 && !near; dx++)
                    near = (dx != 0 || dy != 0) && p.Filled(x + dx, y + dy) && p[x + dx, y + dy] != (int)c;
                if (near) add.Add((x, y));
            }
            foreach (var (x, y) in add) p.Set(x, y, c);
        }

        // ---------- Logo ----------

        /// <summary>Logo için yuvarlak hatlı 5x7 glifler (küçük harfler sadece logoda).</summary>
        private static readonly Dictionary<char, string[]> LogoGlyphs = new Dictionary<char, string[]>
        {
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['i'] = new[] { "..#..", ".....", ".##..", "..#..", "..#..", "..#..", ".###." },
            ['x'] = new[] { ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#" },
            ['e'] = new[] { ".....", ".....", ".###.", "#...#", "#####", "#....", ".###." },
            ['l'] = new[] { ".##..", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['a'] = new[] { ".....", ".....", ".###.", "....#", ".####", "#...#", ".####" },
            ['t'] = new[] { "..#..", "..#..", "#####", "..#..", "..#..", "..#..", "...##" },
        };

        public const string LogoText = "PixelBeat";
        public const int LogoScale = 3;
        public const int LogoGlowMargin = 5;

        /// <summary>
        /// "PixelBeat" logosu: 5x7 glifler 3x + kalınlaştırma, dış köşeler yuvarlatılmış, yukarıdan aşağı Cyan → Violet → Magenta,
        /// vuruşların üstünde beyaz parıltı, koyu (Void) iç hat + Magenta neon dış hat.
        /// </summary>
        public static PixelCanvas Logo()
        {
            // Kalın vuruş: her font pikseli (s+2) x (s+1) blok (sağa 2, aşağı 1 px kalınlaştırma).
            const int s = LogoScale, bold = 2, gw = 5 * s + bold, gh = 7 * s + 1, gap = s - 1;
            int mw = LogoText.Length * (gw + gap) - gap;
            var mask = new bool[mw, gh];
            for (int c = 0; c < LogoText.Length; c++)
            {
                var rows = LogoGlyphs[LogoText[c]];
                int x0 = c * (gw + gap);
                for (int row = 0; row < 7; row++)
                for (int col = 0; col < 5; col++)
                {
                    if (rows[row][col] != '#') continue;
                    int y = (6 - row) * s; // +1 alttan kalınlaştırma için blok y..y+s
                    for (int sy = 0; sy <= s; sy++)
                    for (int sx = 0; sx < s + bold; sx++)
                        mask[x0 + col * s + sx, y + sy] = true;
                }
            }

            // Dış köşeleri yuvarlat: iki dik komşusu ve aradaki çaprazı boş olan dolu piksel silinir.
            var round = (bool[,])mask.Clone();
            for (int y = 0; y < gh; y++)
            for (int x = 0; x < mw; x++)
            {
                if (!mask[x, y]) continue;
                bool Get(int xx, int yy) => xx >= 0 && yy >= 0 && xx < mw && yy < gh && mask[xx, yy];
                foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                    if (!Get(x + dx, y) && !Get(x, y + dy) && !Get(x + dx, y + dy)) round[x, y] = false;
            }

            const int pad = 2;
            var p = new PixelCanvas(mw + 2 * pad, gh + 2 * pad);
            var colors = new[] { PaletteColor.Cyan, PaletteColor.Violet, PaletteColor.Magenta };
            for (int y = 0; y < gh; y++)
            for (int x = 0; x < mw; x++)
            {
                if (!round[x, y]) continue;
                bool topEdge = y == gh - 1 || !round[x, y + 1];
                var col = topEdge && y >= gh / 2 ? PaletteColor.White : GradientAt(colors, y, gh, x);
                p.Set(x + pad, y + pad, col);
            }
            Outline(p, PaletteColor.Void);
            Outline(p, PaletteColor.Magenta);
            return p;
        }

        // ---------- İkonlar ----------

        /// <summary>Oynat ▶ (9x11).</summary>
        public static PixelCanvas IconPlay(PaletteColor c)
        {
            var p = new PixelCanvas(9, 11);
            for (int y = 0; y < 11; y++)
            {
                int half = 5 - Math.Abs(y - 5); // 0..5
                for (int x = 0; x <= Math.Min(8, half * 8 / 5); x++) p.Set(x, y, c);
            }
            return p;
        }

        /// <summary>Geri ◀ (9x11): oynat ikonunun aynası.</summary>
        public static PixelCanvas IconBack(PaletteColor c)
        {
            var play = IconPlay(c);
            var p = new PixelCanvas(play.Width, play.Height);
            for (int y = 0; y < play.Height; y++)
            for (int x = 0; x < play.Width; x++)
                p[play.Width - 1 - x, y] = play[x, y];
            return p;
        }

        /// <summary>Ayarlar ⚙ (11x11): 8 dişli halka, ortası boş.</summary>
        public static PixelCanvas IconGear(PaletteColor c)
        {
            var p = new PixelCanvas(11, 11);
            for (int y = 0; y < 11; y++)
            for (int x = 0; x < 11; x++)
            {
                double dx = x - 5, dy = y - 5, r = Math.Sqrt(dx * dx + dy * dy);
                double ang = Math.Atan2(dy, dx);
                bool tooth = Math.Cos(ang * 8) > 0.3;
                if ((r >= 1.8 && r <= 3.6) || (tooth && r > 3.6 && r <= 5.4)) p.Set(x, y, c);
            }
            return p;
        }

        // ---------- Arka plan ----------

        /// <summary>
        /// Varsayılan arka plan (360 px): dither'lı gece gökyüzü, şeritli neon güneş, şehir silüeti ve ufukta birleşen zemin çizgileri.
        /// Ufuk y = <see cref="SkyHorizon"/>. Belirleyici (sabit seed).
        /// </summary>
        public static PixelCanvas Sky()
        {
            const int k = 2; // 180 px tasarımının ölçeği
            const int w = SkyWidth, h = SkyHeight, hz = SkyHorizon;
            var p = new PixelCanvas(w, h);
            var rnd = new Random(7);

            // Gökyüzü: yukarıdan aşağı Void → Night → Plum, geçişler dama dither.
            for (int y = hz; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int up = y - hz;
                bool checker = ((x + y) & 1) == 0;
                PaletteColor c;
                if (up > 170 * k) c = PaletteColor.Void;
                else if (up > 150 * k) c = checker ? PaletteColor.Void : PaletteColor.Night;
                else if (up > 60 * k) c = PaletteColor.Night;
                else if (up > 40 * k) c = checker ? PaletteColor.Night : PaletteColor.Plum;
                else if (up > 20 * k) c = (x + y) % 4 == 0 ? PaletteColor.Night : PaletteColor.Plum;
                else c = PaletteColor.Plum;
                p.Set(x, y, c);
            }

            // Yıldızlar (bazıları 4 kollu parıltı)
            for (int i = 0; i < 90; i++)
            {
                int x = rnd.Next(w), y = hz + 70 * k + rnd.Next(h - hz - 72 * k);
                var c = i % 5 == 0 ? PaletteColor.Haze : PaletteColor.Steel;
                p.Set(x, y, c);
                if (i % 11 == 0)
                {
                    p.Set(x - 1, y, PaletteColor.Steel);
                    p.Set(x + 1, y, PaletteColor.Steel);
                    p.Set(x, y - 1, PaletteColor.Steel);
                    p.Set(x, y + 1, PaletteColor.Steel);
                    p.Set(x, y, PaletteColor.White);
                }
            }

            // Güneş: üstte Gold → Orange → Magenta bantları, alt yarıda kalınlaşan yatay boşluklar.
            const int cx = w / 2, cy = hz + 22 * k, r = 34 * k;
            for (int y = hz; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r * r) continue;
                int fromTop = cy + r - y;
                if (y < cy)
                {
                    int band = (cy - y) / (4 * k);
                    if ((cy - y) % (6 * k) < Math.Min(4 * k, k * (1 + band / 2))) continue;
                }
                PaletteColor c = fromTop < 18 * k ? PaletteColor.Gold : fromTop < 38 * k ? PaletteColor.Orange : PaletteColor.Magenta;
                if (Math.Abs(fromTop - 18 * k) <= 1 || Math.Abs(fromTop - 38 * k) <= 1)
                    c = ((x + y) & 1) == 0 ? c : (fromTop < 18 * k + 2 ? PaletteColor.Gold : PaletteColor.Orange);
                p.Set(x, y, c);
            }

            // Şehir silüeti (güneşin önünde)
            int bx = 0;
            while (bx < w)
            {
                int bw = (6 + rnd.Next(14)) * k;
                int bh = (8 + rnd.Next(rnd.Next(3) == 0 ? 60 : 30)) * k;
                p.FillRect(bx, hz, bw, bh, PaletteColor.Void);
                // Çatı kenarı neon
                for (int x = bx; x < bx + bw; x++) if (rnd.Next(4) == 0) p.Set(x, hz + bh - 1, PaletteColor.Plum);
                for (int wy = hz + 4; wy < hz + bh - 3; wy += 4)
                for (int wx = bx + 3; wx < bx + bw - 2; wx += 4)
                {
                    int roll = rnd.Next(9);
                    var c = roll == 0 ? PaletteColor.Gold : roll == 1 ? PaletteColor.Cyan : roll == 2 ? PaletteColor.Plum : (PaletteColor)(-1);
                    if ((int)c < 0) continue;
                    p.Set(wx, wy, c);
                    p.Set(wx + 1, wy, c);
                }
                bx += bw + rnd.Next(2) * k;
            }

            // Ufuk çizgisi ve zemin
            for (int x = 0; x < w; x++)
            {
                p.Set(x, hz, PaletteColor.Magenta);
                p.Set(x, hz + 1, ((x & 1) == 0) ? PaletteColor.Magenta : PaletteColor.Plum);
            }
            p.FillRect(0, 0, w, hz, PaletteColor.Void);
            for (int y = hz - 30 * k; y < hz; y++)
            for (int x = 0; x < w; x++)
                if (((x + y) & 1) == 0 && y > hz - 30 * k + (hz - y) / 2) p.Set(x, y, PaletteColor.Night);

            // Birleşen zemin çizgileri (ufukta kaybolma noktasına)
            for (int xb = -480 * k; xb <= w + 480 * k; xb += 36 * k)
                p.Line(cx + (xb - cx) / 8, hz - 1, xb, 0, PaletteColor.Plum);
            for (int x = 0; x < w; x++) p.Set(x, hz - 1, PaletteColor.Violet);

            return p;
        }
    }
}
