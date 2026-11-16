using System.IO;
using PixelBeat.Core;
using PixelBeat.UI;
using UnityEditor;
using UnityEngine;

namespace PixelBeat.EditorTools.NeonArt
{
    /// <summary>
    /// *PixelBeat > Sanat Önizlemesi*: NeonSkin sprite'larını tek bir 360 px genişliğinde tuvalde dizer (logo, menü butonları,
    /// değerlendirme yazıları, notalar, hold, dokunma düğmeleri) ve 3x büyütülmüş PNG olarak Docs/ui_360_sanat.png'ye yazar.
    /// Hale sprite'ları oyundaki gibi additive (toplamalı) karıştırılır. Konseptle karşılaştırma içindir.
    /// </summary>
    public static class NeonArtPreview
    {
        public const string OutputPath = "Docs/ui_360_sanat.png";
        private const int W = 360, H = 330, Upscale = 3;

        [MenuItem("PixelBeat/Sanat Önizlemesi")]
        public static void Render()
        {
            var skin = AssetDatabase.LoadAssetAtPath<NeonSkin>(NeonSpriteBaker.SkinPath);
            if (skin == null || skin.Logo == null)
            {
                Debug.LogError("[Sanat Önizlemesi] NeonSkin bake edilmemiş (PixelBeat > Bake Neon Sprites).");
                return;
            }

            var px = new Color[W * H];
            var bg = Palette.Get(PaletteColor.Void);
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // Logo
            int logoW = (int)skin.Logo.rect.width;
            int lx = (W - logoW) / 2, ly = H - 40;
            Blit(px, skin.LogoGlow, lx - NeonPixelArt.LogoGlowMargin, ly - NeonPixelArt.LogoGlowMargin, true, 0.8f);
            Blit(px, skin.Logo, lx, ly);

            // Menü butonları: ▶ OYNA (cyan), ⚙ AYARLAR (magenta)
            Button(px, skin, PaletteColor.Cyan, skin.IconPlay, "OYNA", 20, H - 92, 140, 34);
            Button(px, skin, PaletteColor.Magenta, skin.IconGear, "AYARLAR", 180, H - 92, 160, 34);

            // Değerlendirme yazıları (sağ sütun)
            int wy = H - 130;
            foreach (NeonSkin.Word word in System.Enum.GetValues(typeof(NeonSkin.Word)))
            {
                var s = skin.WordSprite(word);
                int ww = (int)s.rect.width;
                int wx = W - ww - 16;
                int m = ((int)skin.WordGlow(word).rect.width - ww) / 2;
                Blit(px, skin.WordGlow(word), wx - m, wy - m, true, 0.7f);
                Blit(px, s, wx, wy);
                wy -= 34;
            }

            // Notalar (tap) ve bir hold (sol sütun)
            var lanes = new[] { PaletteColor.Cyan, PaletteColor.Violet, PaletteColor.Magenta, PaletteColor.Lime };
            for (int i = 0; i < 4; i++)
            {
                int cx = 24 + i * 34, cy = H - 138;
                Centered(px, skin.NoteGlow(lanes[i]), cx, cy, true, 0.45f);
                Centered(px, skin.NoteHead(lanes[i]), cx, cy);
            }
            // Hold: kuyruk yukarıda, baş aşağıda, gövde tutuluyormuş gibi parlak.
            {
                int cx = 92, headY = H - 255, tailY = H - 175;
                var c = PaletteColor.Magenta;
                Column(px, skin.HoldGlow(c), cx, headY, tailY - headY, true, 0.9f);
                Column(px, skin.HoldBody(c), cx, headY, tailY - headY);
                Centered(px, skin.TailGlow(c), cx, tailY, true, 0.45f);
                Centered(px, skin.HoldTail(c), cx, tailY);
                Centered(px, skin.NoteGlow(c), cx, headY, true, 0.45f);
                Centered(px, skin.NoteHead(c), cx, headY);
            }
            {
                int cx = 30, headY = H - 225, tailY = H - 175;
                var c = PaletteColor.Cyan;
                Column(px, skin.HoldGlow(c), cx, headY, tailY - headY, true, 0.45f);
                Column(px, skin.HoldBody(c), cx, headY, tailY - headY);
                Centered(px, skin.HoldTail(c), cx, tailY);
                Centered(px, skin.NoteGlow(c), cx, headY, true, 0.45f);
                Centered(px, skin.NoteHead(c), cx, headY);
            }

            // Dokunma düğmeleri: boşta (Cyan, Violet) ve basılı (Magenta, Lime)
            for (int i = 0; i < 4; i++)
            {
                bool pressed = i >= 2;
                int x = 12 + i * 86, y = 8, w = 74, h = 56;
                if (pressed)
                {
                    int m = NeonSkin.ButtonGlowMarginPx;
                    Sliced(px, skin.KeyPadGlow(lanes[i]), x - m, y - m, w + 2 * m, h + 2 * m, true, 0.8f);
                }
                Sliced(px, pressed ? skin.KeyPadPressed(lanes[i]) : skin.KeyPadIdle(lanes[i]), x, y, w, h);
            }

            // 3x büyüt ve yaz
            var tex = new Texture2D(W * Upscale, H * Upscale, TextureFormat.RGBA32, false);
            var big = new Color[W * H * Upscale * Upscale];
            for (int y = 0; y < H * Upscale; y++)
            for (int x = 0; x < W * Upscale; x++)
            {
                var c = px[(y / Upscale) * W + x / Upscale];
                c.a = 1f;
                big[y * W * Upscale + x] = c;
            }
            tex.SetPixels(big);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[Sanat Önizlemesi] → {OutputPath}");
        }

        private static void Button(Color[] px, NeonSkin skin, PaletteColor c, Sprite icon, string text, int x, int y, int w, int h)
        {
            int m = NeonSkin.ButtonGlowMarginPx;
            Sliced(px, skin.ButtonGlow(c), x - m, y - m, w + 2 * m, h + 2 * m, true, 0.7f);
            Sliced(px, skin.ButtonFrame(c), x, y, w, h);
            const int scale = 2;
            var mask = PixelFont.Rasterize(text, scale);
            int iconW = (int)icon.rect.width, iconH = (int)icon.rect.height;
            int textW = mask.GetLength(0), textH = mask.GetLength(1);
            int total = iconW + 8 + textW;
            int ix = x + (w - total) / 2;
            Blit(px, icon, ix, y + (h - iconH) / 2);
            int tx = ix + iconW + 8, ty = y + (h - textH) / 2;
            var white = Palette.Get(PaletteColor.White);
            for (int yy = 0; yy < textH; yy++)
            for (int xx = 0; xx < textW; xx++)
                if (mask[xx, yy]) Put(px, tx + xx, ty + yy, white, false, 1f);
        }

        private static void Centered(Color[] px, Sprite s, int cx, int cy, bool additive = false, float alpha = 1f)
        {
            int w = (int)s.rect.width, h = (int)s.rect.height;
            Blit(px, s, cx - w / 2, cy - h / 2, additive, alpha);
        }

        private static void Column(Color[] px, Sprite s, int cx, int y0, int h, bool additive = false, float alpha = 1f)
        {
            var t = Pixels(s, out int sw, out int sh);
            int x0 = cx - sw / 2;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < sw; x++)
                Put(px, x0 + x, y0 + y, t[(y % sh) * sw + x], additive, alpha);
        }

        /// <summary>
        /// 9-slice: köşeler sabit; kenarlar ve orta Unity Sliced gibi nearest-neighbor esnetilir, additive (hale) için
        /// Tiled gibi döşenir (oyunda haleler Tiled çizilir).
        /// </summary>
        private static void Sliced(Color[] px, Sprite s, int x0, int y0, int w, int h, bool additive = false, float alpha = 1f)
        {
            var t = Pixels(s, out int sw, out int sh);
            var b = s.border; // L, B, R, T
            bool tiled = additive;
            int Map(int v, int size, int lo, int hi, int src)
            {
                if (v < lo) return v;
                if (v >= size - hi) return src - (size - v);
                int mid = Mathf.Max(1, src - lo - hi), span = Mathf.Max(1, size - lo - hi);
                return lo + (tiled ? (v - lo) % mid : Mathf.Min(mid - 1, (v - lo) * mid / span));
            }
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int sx = Map(x, w, (int)b.x, (int)b.z, sw);
                int sy = Map(y, h, (int)b.y, (int)b.w, sh);
                Put(px, x0 + x, y0 + y, t[sy * sw + sx], additive, alpha);
            }
        }

        private static void Blit(Color[] px, Sprite s, int x0, int y0, bool additive = false, float alpha = 1f)
        {
            var t = Pixels(s, out int sw, out int sh);
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                Put(px, x0 + x, y0 + y, t[y * sw + x], additive, alpha);
        }

        private static void Put(Color[] px, int x, int y, Color c, bool additive, float alpha)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || c.a <= 0f) return;
            int i = y * W + x;
            float a = c.a * alpha;
            px[i] = additive ? px[i] + c * a : Color.Lerp(px[i], c, a);
        }

        private static Color[] Pixels(Sprite s, out int w, out int h)
        {
            string path = AssetDatabase.GetAssetPath(s);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            w = tex.width;
            h = tex.height;
            var pixels = tex.GetPixels();
            Object.DestroyImmediate(tex);
            return pixels;
        }
    }
}
