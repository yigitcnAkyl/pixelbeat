using System;
using PixelBeat.Core;

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
            get => px[y * Width + x];
            set
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return;
                px[y * Width + x] = value;
            }
        }

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
    /// Neon placeholder sprite desenleri (sadece palet renkleri). Boyutlar ve 9-slice kenarları <see cref="Spec"/>'te.
    /// </summary>
    public static class NeonPixelArt
    {
        public struct Spec
        {
            public int W, H;
            /// <summary>Sol, alt, sağ, üst (9-slice kenarları, piksel).</summary>
            public int L, B, R, T;
        }

        public static readonly Spec NoteHeadSpec = new Spec { W = 8, H = 6, L = 3, R = 3 };
        public static readonly Spec HoldBodySpec = new Spec { W = 6, H = 1, L = 2, R = 2 };
        public static readonly Spec HoldTailSpec = new Spec { W = 8, H = 3, L = 3, R = 3 };
        public static readonly Spec NoteGlowSpec = new Spec { W = 12, H = 10, L = 5, R = 5 };
        public static readonly Spec KeyPadSpec = new Spec { W = 8, H = 8, L = 3, B = 3, R = 3, T = 3 };
        public static readonly Spec HitLineSpec = new Spec { W = 8, H = 3, L = 1, R = 1 };
        public static readonly Spec LaneFlashSpec = new Spec { W = 8, H = 48 };
        public static readonly Spec LanePanelSpec = new Spec { W = 8, H = 8 };
        public const int SkyWidth = 180, SkyHeight = 480, SkyHorizon = 200;

        /// <summary>Tap nota başı: üstte beyaz parıltı, gövde renk, altta koyu eş, köşeler yuvarlak.</summary>
        public static PixelCanvas NoteHead(PaletteColor c)
        {
            var s = NoteHeadSpec;
            var p = new PixelCanvas(s.W, s.H);
            var dark = Palette.DarkOf(c);
            for (int x = 0; x < s.W; x++)
            {
                bool edge = x == 0 || x == s.W - 1;
                p.Set(x, 0, dark);
                p.Set(x, 1, dark);
                p.Set(x, 2, c);
                p.Set(x, 3, c);
                p.Set(x, 4, c);
                p.Set(x, 5, PaletteColor.White);
                if (edge) { p[x, 0] = -1; p[x, 5] = -1; }
            }
            // Parıltı satırında köşeye yakın piksel renk kalsın (ışık yansıması hissi).
            p.Set(1, 5, c);
            p.Set(s.W - 2, 5, c);
            return p;
        }

        /// <summary>Hold gövdesi: kenarlarda renk, içi koyu eş (dikey/yatay esner).</summary>
        public static PixelCanvas HoldBody(PaletteColor c)
        {
            var p = new PixelCanvas(HoldBodySpec.W, HoldBodySpec.H);
            var dark = Palette.DarkOf(c);
            p.Set(0, 0, c);
            p.Set(1, 0, dark);
            p.Set(2, 0, dark);
            p.Set(3, 0, dark);
            p.Set(4, 0, dark);
            p.Set(5, 0, c);
            return p;
        }

        public static PixelCanvas HoldTail(PaletteColor c)
        {
            var s = HoldTailSpec;
            var p = new PixelCanvas(s.W, s.H);
            for (int x = 0; x < s.W; x++)
            {
                p.Set(x, 0, Palette.DarkOf(c));
                p.Set(x, 1, c);
                p.Set(x, 2, PaletteColor.White);
            }
            p[0, 2] = -1;
            p[s.W - 1, 2] = -1;
            p[0, 0] = -1;
            p[s.W - 1, 0] = -1;
            return p;
        }

        /// <summary>
        /// Nota halesi (additive): notanın 2 px çevresi, içte dolu halka, dışta dama dither. İç kısım boş.
        /// Orta sütun deseni 2 px periyotlu → Tiled çizimle genişler.
        /// </summary>
        public static PixelCanvas NoteGlow(PaletteColor c)
        {
            var s = NoteGlowSpec;
            var p = new PixelCanvas(s.W, s.H);
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                int dx = Math.Max(0, Math.Max(2 - x, x - (s.W - 3)));
                int dy = Math.Max(0, Math.Max(2 - y, y - (s.H - 3)));
                int d = Math.Max(dx, dy);
                if (d == 1 && !(dx == 1 && dy == 1)) p.Set(x, y, c);           // iç halka (köşe hariç)
                else if (d == 2 && ((x + y) & 1) == 0 && !(dx == 2 && dy == 2)) p.Set(x, y, c); // dış dither
            }
            return p;
        }

        public static PixelCanvas KeyPad(PaletteColor border, PaletteColor fill, PaletteColor highlight)
        {
            var s = KeyPadSpec;
            var p = new PixelCanvas(s.W, s.H);
            p.FillRect(1, 1, s.W - 2, s.H - 2, fill);
            for (int i = 1; i < s.W - 1; i++)
            {
                p.Set(i, 0, border);
                p.Set(i, s.H - 1, border);
                p.Set(0, i, border);
                p.Set(s.W - 1, i, border);
            }
            for (int x = 2; x < s.W - 2; x++) p.Set(x, s.H - 2, highlight);
            return p;
        }

        public static PixelCanvas HitLine(PaletteColor core, PaletteColor edge)
        {
            var s = HitLineSpec;
            var p = new PixelCanvas(s.W, s.H);
            for (int x = 0; x < s.W; x++)
            {
                p.Set(x, 0, edge);
                p.Set(x, 1, core);
                p.Set(x, 2, edge);
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
                if (y < 6) on = true;
                else if (y < 16) on = ((x + y) & 1) == 0;
                else if (y < 30) on = (x & 1) == 0 && (y & 1) == 0;
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
            p.Set(4, 0, PaletteColor.Slate);
            p.Set(4, 4, PaletteColor.Slate);
            return p;
        }

        public static PixelCanvas Solid(int size, PaletteColor c)
        {
            var p = new PixelCanvas(size, size);
            p.FillRect(0, 0, size, size, c);
            return p;
        }

        /// <summary>
        /// Varsayılan arka plan: dither'lı gece gökyüzü, şeritli neon güneş, şehir silüeti ve ufukta birleşen zemin çizgileri.
        /// Ufuk y = <see cref="SkyHorizon"/>. Belirleyici (sabit seed).
        /// </summary>
        public static PixelCanvas Sky()
        {
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
                if (up > 170) c = PaletteColor.Void;
                else if (up > 150) c = checker ? PaletteColor.Void : PaletteColor.Night;
                else if (up > 60) c = PaletteColor.Night;
                else if (up > 40) c = checker ? PaletteColor.Night : PaletteColor.Plum;
                else if (up > 20) c = (x + y) % 4 == 0 ? PaletteColor.Night : PaletteColor.Plum;
                else c = PaletteColor.Plum;
                p.Set(x, y, c);
            }

            // Yıldızlar
            for (int i = 0; i < 40; i++)
            {
                int x = rnd.Next(w), y = hz + 70 + rnd.Next(h - hz - 72);
                p.Set(x, y, i % 5 == 0 ? PaletteColor.Haze : PaletteColor.Steel);
            }

            // Güneş: üstte Gold → Orange → Magenta bantları, alt yarıda kalınlaşan yatay boşluklar.
            const int cx = w / 2, cy = hz + 22, r = 34;
            for (int y = hz; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r * r) continue;
                int fromTop = cy + r - y;
                if (y < cy)
                {
                    int band = (cy - y) / 4; // alt yarı: boşluklar
                    if ((cy - y) % 6 < Math.Min(4, 1 + band / 2)) continue;
                }
                PaletteColor c = fromTop < 18 ? PaletteColor.Gold : fromTop < 38 ? PaletteColor.Orange : PaletteColor.Magenta;
                p.Set(x, y, c);
            }

            // Şehir silüeti (güneşin önünde)
            int bx = 0;
            while (bx < w)
            {
                int bw = 6 + rnd.Next(14);
                int bh = 8 + rnd.Next(rnd.Next(3) == 0 ? 60 : 30);
                p.FillRect(bx, hz, bw, bh, PaletteColor.Void);
                for (int wy = hz + 3; wy < hz + bh - 2; wy += 3)
                for (int wx = bx + 2; wx < bx + bw - 1; wx += 3)
                {
                    int roll = rnd.Next(9);
                    if (roll == 0) p.Set(wx, wy, PaletteColor.Gold);
                    else if (roll == 1) p.Set(wx, wy, PaletteColor.Cyan);
                    else if (roll == 2) p.Set(wx, wy, PaletteColor.Plum);
                }
                bx += bw + rnd.Next(2);
            }

            // Ufuk çizgisi ve zemin
            for (int x = 0; x < w; x++) p.Set(x, hz, PaletteColor.Magenta);
            p.FillRect(0, 0, w, hz, PaletteColor.Void);
            for (int y = hz - 30; y < hz; y++)
            for (int x = 0; x < w; x++)
                if (((x + y) & 1) == 0 && y > hz - 30 + (hz - y) / 2) p.Set(x, y, PaletteColor.Night);

            // Birleşen zemin çizgileri (ufukta kaybolma noktasına)
            for (int xb = -480; xb <= w + 480; xb += 36)
                p.Line(cx + (xb - cx) / 8, hz - 1, xb, 0, PaletteColor.Plum);
            for (int x = 0; x < w; x++) p.Set(x, hz - 1, PaletteColor.Violet);

            return p;
        }
    }
}
