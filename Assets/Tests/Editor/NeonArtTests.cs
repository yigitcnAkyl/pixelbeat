using System.Collections.Generic;
using NUnit.Framework;
using PixelBeat.Core;
using PixelBeat.EditorTools.NeonArt;
using PixelBeat.Gameplay;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Tests
{
    /// <summary>Neon görünüm: palet, üretilen sprite desenleri, AI görsel dönüştürme, vuruş parçacıkları.</summary>
    public class NeonArtTests
    {
        private static void AssertPaletteOnly(PixelCanvas c, string name)
        {
            for (int y = 0; y < c.Height; y++)
            for (int x = 0; x < c.Width; x++)
            {
                int v = c[x, y];
                Assert.That(v, Is.InRange(-1, Palette.Colors.Length - 1), $"{name} ({x},{y})");
            }
        }

        private static int Filled(PixelCanvas c)
        {
            int n = 0;
            for (int y = 0; y < c.Height; y++)
            for (int x = 0; x < c.Width; x++)
                if (c[x, y] >= 0) n++;
            return n;
        }

        [Test]
        public void Palette_Has16DistinctColors()
        {
            Assert.AreEqual(16, Palette.Colors.Length);
            var set = new HashSet<Color32>(Palette.Colors);
            Assert.AreEqual(16, set.Count);
            Assert.AreEqual(16, System.Enum.GetValues(typeof(PaletteColor)).Length);
        }

        [Test]
        public void PaletteRef_ConvertsWithAlpha()
        {
            PaletteRef r = PaletteColor.Cyan;
            Assert.AreEqual((Color)Palette.Colors[(int)PaletteColor.Cyan], (Color)r);
            var half = new PaletteRef(PaletteColor.Void, 0.5f);
            Assert.AreEqual(0.5f, ((Color)half).a, 1e-6);
        }

        private static readonly PaletteColor[] Lanes = { PaletteColor.Cyan, PaletteColor.Violet, PaletteColor.Magenta, PaletteColor.Lime };

        [Test]
        public void BakedPatterns_UsePaletteOnly_WithExpectedSizes()
        {
            foreach (var c in NeonSkin.AccentColors)
            {
                var note = NeonPixelArt.Diamond(c);
                Assert.AreEqual(NeonPixelArt.NoteSize, note.Width);
                Assert.AreEqual(NeonPixelArt.NoteSize, note.Height);
                AssertPaletteOnly(note, "note");
                AssertPaletteOnly(NeonPixelArt.DiamondHalo(c), "halo");
                AssertPaletteOnly(NeonPixelArt.Diamond(c, NeonPixelArt.TailSize), "tail");
                AssertPaletteOnly(NeonPixelArt.Beam(c), "beam");
                AssertPaletteOnly(NeonPixelArt.BeamGlow(c), "beam glow");
                AssertPaletteOnly(NeonPixelArt.KeyPadIdle(c), "key");
                AssertPaletteOnly(NeonPixelArt.KeyPadPressed(c), "key pressed");
                AssertPaletteOnly(NeonPixelArt.FrameGlow(c), "frame glow");
                AssertPaletteOnly(NeonPixelArt.ButtonFrame(c), "button");
                AssertPaletteOnly(NeonPixelArt.LaneFlash(c), "flash");
            }
            AssertPaletteOnly(NeonPixelArt.Logo(), "logo");
            AssertPaletteOnly(NeonPixelArt.IconPlay(PaletteColor.Cyan), "play");
            AssertPaletteOnly(NeonPixelArt.IconGear(PaletteColor.Magenta), "gear");

            var sky = NeonPixelArt.Sky();
            Assert.AreEqual(360, sky.Width, "arka plan 360 px genişlik");
            Assert.AreEqual(NeonPixelArt.SkyHeight, sky.Height);
            AssertPaletteOnly(sky, "sky");
            Assert.AreEqual(sky.Width * sky.Height, Filled(sky), "arka plan tamamen opak");
        }

        [Test]
        public void Diamond_IsSymmetric_WithNeonRimAndDarkInside()
        {
            foreach (int n in new[] { NeonPixelArt.NoteSize, NeonPixelArt.TailSize })
            {
                var d = NeonPixelArt.Diamond(PaletteColor.Cyan, n);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Assert.AreEqual(d[x, y] >= 0, d[n - 1 - x, y] >= 0, $"n={n} yatay simetri ({x},{y})");
                    Assert.AreEqual(d[x, y] >= 0, d[x, n - 1 - y] >= 0, $"n={n} dikey simetri ({x},{y})");
                }
                // Köşeler boş (elmas), uçlar dolu.
                Assert.AreEqual(-1, d[0, 0]);
                Assert.AreEqual(-1, d[n - 1, n - 1]);
                Assert.GreaterOrEqual(d[0, n / 2], 0);
                Assert.GreaterOrEqual(d[n / 2, n - 1], 0);
                // Ortadan sola giderken: renkli çerçeve görülür.
                bool rim = false;
                for (int x = 0; x < n / 2; x++) rim |= d[x, n / 2] == (int)PaletteColor.Cyan;
                Assert.IsTrue(rim, $"n={n} neon çerçeve");
            }
            var note = NeonPixelArt.Diamond(PaletteColor.Cyan);
            Assert.AreEqual((int)PaletteColor.Night, note[NeonPixelArt.NoteSize / 2 - 7, NeonPixelArt.NoteSize / 2], "koyu iç");
        }

        [Test]
        public void Halo_SurroundsNote_EmptyInside_FadesOutwards()
        {
            var halo = NeonPixelArt.DiamondHalo(PaletteColor.Cyan);
            int m = NeonPixelArt.HaloMargin, n = NeonPixelArt.NoteSize;
            Assert.AreEqual(n + 2 * m, halo.Width);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if (NeonPixelArt.DiamondMetric(x, y, n) <= n)
                    Assert.AreEqual(-1, halo[x + m, y + m], "nota üstünde hale yok");

            int Ring(int k0, int k1)
            {
                int c = 0;
                for (int y = 0; y < halo.Height; y++)
                for (int x = 0; x < halo.Width; x++)
                {
                    int d = NeonPixelArt.DiamondMetric(x - m, y - m, n);
                    int k = (d - n + 1) / 2;
                    if (d > n && k >= k0 && k <= k1 && halo[x, y] >= 0) c++;
                }
                return c;
            }
            Assert.Greater(Ring(1, 1), 0);
            Assert.Greater(Ring(2, 3), Ring(6, 7), "dışa doğru seyrelir");
        }

        [Test]
        public void Beam_DarkEdges_BrightCenter_Tileable()
        {
            foreach (var c in Lanes)
            {
                var b = NeonPixelArt.Beam(c);
                int w = b.Width;
                Assert.AreEqual((int)Palette.DarkOf(c), b[0, 0], "sol kenar koyu");
                Assert.AreEqual((int)Palette.DarkOf(c), b[w - 1, 0], "sağ kenar koyu");
                Assert.AreEqual((int)PaletteColor.White, b[w / 2, 0], "merkez açık");
                Assert.AreEqual((int)PaletteColor.White, b[w / 2 - 1, 1]);
                Assert.AreEqual(2, b.Height, "dikey döşeme için 2 px (dither periyodu)");
            }
        }

        [Test]
        public void KeyPad_NoIcon_PressedFillsWithLaneColor()
        {
            foreach (var c in Lanes)
            {
                var idle = NeonPixelArt.KeyPadIdle(c);
                var pressed = NeonPixelArt.KeyPadPressed(c);
                var s = NeonPixelArt.KeyPadSpec;
                // 9-slice orta bölge tek düze (ikon yok): boşta koyu iç.
                for (int y = s.B; y < s.H - s.T; y++)
                for (int x = s.L; x < s.W - s.R; x++)
                {
                    Assert.AreEqual((int)PaletteColor.Night, idle[x, y], $"{c} boşta iç ({x},{y})");
                    Assert.AreEqual((int)c, pressed[x, y], $"{c} basılı orta bölge tek düze (Sliced esnemesi) ({x},{y})");
                }
                Assert.AreEqual((int)c, pressed[s.W / 2, s.H - s.T - 1], $"{c} basılı: iç şerit rengi");
                Assert.AreEqual(-1, idle[0, 0], "yuvarlak köşe");
                // Çerçevede şerit rengi
                Assert.AreEqual((int)c, idle[1, s.H / 2]);
            }
        }

        [Test]
        public void Words_AreItalic_WithClosedDarkOutline_AndGradient()
        {
            var word = NeonPixelArt.Word("PERFECT!", PaletteColor.Cyan, PaletteColor.Violet, PaletteColor.Magenta);
            // Harf pikseli (palet rengi, Void değil) hiçbir zaman saydam bir komşuya dokunmaz → dış hat kapalı.
            for (int y = 0; y < word.Height; y++)
            for (int x = 0; x < word.Width; x++)
            {
                int v = word[x, y];
                if (v < 0 || v == (int)PaletteColor.Void) continue;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Assert.GreaterOrEqual(word[x + dx, y + dy], 0, $"({x},{y}) dış hatsız");
            }

            // Renk geçişi: üst yarıda Cyan, alt yarıda Magenta baskın.
            int topCyan = 0, bottomMagenta = 0;
            for (int y = 0; y < word.Height; y++)
            for (int x = 0; x < word.Width; x++)
            {
                if (y > word.Height * 2 / 3 && word[x, y] == (int)PaletteColor.Cyan) topCyan++;
                if (y < word.Height / 3 && word[x, y] == (int)PaletteColor.Magenta) bottomMagenta++;
            }
            Assert.Greater(topCyan, 0);
            Assert.Greater(bottomMagenta, 0);

            // İtalik: "!" gibi dik bir sütunun üst pikseli alt pikselinden sağda.
            var bang = NeonPixelArt.BoldMask("!");
            int h = bang.GetLength(1);
            Assert.Greater(NeonPixelArt.ItalicShift(h - 1), NeonPixelArt.ItalicShift(0), "üst satır sağa kayar");
        }

        [Test]
        public void Logo_HasGradientAndNeonRim()
        {
            var logo = NeonPixelArt.Logo();
            Assert.Greater(logo.Width, 150, "logo genişliği");
            bool cyan = false, magentaRim = false;
            for (int y = 0; y < logo.Height; y++)
            for (int x = 0; x < logo.Width; x++)
            {
                cyan |= logo[x, y] == (int)PaletteColor.Cyan;
                magentaRim |= (x == 0 || y == 0) && logo[x, y] == (int)PaletteColor.Magenta;
            }
            Assert.IsTrue(cyan);
            for (int x = 0; x < logo.Width; x++) magentaRim |= logo[x, 0] == (int)PaletteColor.Magenta;
            Assert.IsTrue(magentaRim, "en dışta magenta neon hat");
        }

        [Test]
        public void NineSliceCenters_AreTileable()
        {
            foreach (var spec in new[] { NeonPixelArt.KeyPadSpec, NeonPixelArt.ButtonSpec })
                Assert.Greater(spec.W - spec.L - spec.R, 0);

            // Buton çerçevesi: orta sütunlar aynı (yatay esner).
            var btn = NeonPixelArt.ButtonFrame(PaletteColor.Cyan);
            var s = NeonPixelArt.ButtonSpec;
            for (int y = 0; y < btn.Height; y++)
            for (int x = s.L; x < s.W - s.R; x++)
                Assert.AreEqual(btn[s.L, y], btn[x, y], $"buton ortası satır {y}");

            var flash = NeonPixelArt.LaneFlash(PaletteColor.White);
            for (int y = 0; y < flash.Height; y++)
            for (int x = 0; x < flash.Width; x++)
                Assert.AreEqual(flash[x % 8, y], flash[x, y]);
        }

        [Test]
        public void LaneFlash_FadesUpwards()
        {
            var f = NeonPixelArt.LaneFlash(PaletteColor.White);
            int Row(int y0, int y1)
            {
                int n = 0;
                for (int y = y0; y < y1; y++)
                for (int x = 0; x < f.Width; x++)
                    if (f[x, y] >= 0) n++;
                return n;
            }
            Assert.Greater(Row(0, 12), Row(12, 32) * 6 / 10);
            Assert.Greater(Row(12, 32), Row(32, 60) * 10 / 14);
            Assert.Greater(Row(32, 60), Row(60, 96));
        }

        [Test]
        public void Layout360_FourLanesFitWidth()
        {
            Assert.AreEqual(360, ScreenLayout.ReferenceWidthPx);
            const int laneW = 80;
            Assert.LessOrEqual(4 * laneW + 2, ScreenLayout.ReferenceWidthPx, "4 şerit + kenar ayraçları ekrana sığar");
            Assert.GreaterOrEqual(4 * laneW, ScreenLayout.ReferenceWidthPx * 8 / 10, "şeritler genişliği neredeyse doldurur");
            Assert.LessOrEqual(NeonPixelArt.NoteHaloSize, laneW, "nota halesi şeride sığar");
            Assert.LessOrEqual(NeonPixelArt.BeamGlowWidth, laneW);
        }

        // ---------- AI görsel dönüştürme ----------

        [Test]
        public void Quantize_KeepsPaletteColors_AndMapsNearColors()
        {
            var src = new Color32[Palette.Colors.Length + 3];
            for (int i = 0; i < Palette.Colors.Length; i++) src[i] = Palette.Colors[i];
            src[16] = new Color32(0x30, 0xF0, 0xF8, 255); // ~Cyan
            src[17] = new Color32(0xF8, 0x48, 0xC8, 255); // ~Magenta
            src[18] = new Color32(0x10, 0x10, 0x10, 40);  // saydam

            var q = PaletteImageTools.Quantize(src, src.Length);
            for (int i = 0; i < Palette.Colors.Length; i++) Assert.AreEqual(Palette.Colors[i], q[i], $"palet rengi {i}");
            Assert.AreEqual(Palette.Colors[(int)PaletteColor.Cyan], q[16]);
            Assert.AreEqual(Palette.Colors[(int)PaletteColor.Magenta], q[17]);
            Assert.AreEqual(0, q[18].a);
        }

        [Test]
        public void Quantize_OutputIsPaletteOnly_EvenWithDither()
        {
            var rnd = new System.Random(3);
            var src = new Color32[64 * 64];
            for (int i = 0; i < src.Length; i++)
                src[i] = new Color32((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256), 255);
            var pal = new HashSet<Color32>(Palette.Colors);
            foreach (bool dither in new[] { false, true })
                foreach (var c in PaletteImageTools.Quantize(src, 64, 128, dither))
                    Assert.IsTrue(pal.Contains(c), $"dither={dither}: {c}");
        }

        [Test]
        public void Dither_MixesTwoColors_AtMidpoint()
        {
            // Void ile Night'ın tam ortası → yaklaşık yarı yarıya dama.
            var a = Palette.Colors[(int)PaletteColor.Void];
            var b = Palette.Colors[(int)PaletteColor.Night];
            var mid = new Color32((byte)((a.r + b.r) / 2), (byte)((a.g + b.g) / 2), (byte)((a.b + b.b) / 2), 255);
            var src = new Color32[16];
            for (int i = 0; i < src.Length; i++) src[i] = mid;
            var q = PaletteImageTools.Quantize(src, 4, 128, dither: true);
            int countA = 0;
            foreach (var c in q) if (c.Equals(a)) countA++;
            Assert.That(countA, Is.InRange(4, 12));
        }

        [Test]
        public void ResizeNearest_PicksBlockCenters()
        {
            // 4x2 → 2x1: her hedef piksel kaynak bloğunun merkezindeki pikseli alır.
            var src = new Color32[8];
            for (int i = 0; i < 8; i++) src[i] = new Color32((byte)i, 0, 0, 255);
            var dst = PaletteImageTools.ResizeNearest(src, 4, 2, 2, 1);
            Assert.AreEqual(2, dst.Length);
            Assert.AreEqual(5, dst[0].r); // x=1, y=1
            Assert.AreEqual(7, dst[1].r); // x=3, y=1
        }

        // ---------- Vuruş geri bildirimi ----------

        [Test]
        public void ParticleCounts_PerfectIsMostVisible_MissHasNone()
        {
            Assert.Greater(HitFeedback.ParticleCountFor(Judgement.Perfect, JudgementKind.Press),
                HitFeedback.ParticleCountFor(Judgement.Great, JudgementKind.Press));
            Assert.Greater(HitFeedback.ParticleCountFor(Judgement.Great, JudgementKind.Press),
                HitFeedback.ParticleCountFor(Judgement.Good, JudgementKind.Press));
            Assert.AreEqual(0, HitFeedback.ParticleCountFor(Judgement.Miss, JudgementKind.Press));
            Assert.Less(HitFeedback.ParticleCountFor(Judgement.Perfect, JudgementKind.Release),
                HitFeedback.ParticleCountFor(Judgement.Perfect, JudgementKind.Press));
        }

        [Test]
        public void CrtScanline_DarkensBottomThirdOfEachGamePixelRow()
        {
            var a = CrtOverlay.ScanlineAlphas(6, 0.2f);
            CollectionAssert.AreEqual(new[] { 0.2f, 0.2f, 0f, 0f, 0f, 0f }, a);
            CollectionAssert.AreEqual(new[] { 0f, 0f }, CrtOverlay.ScanlineAlphas(2, 0.2f), "küçük büyütmede scanline yok");
        }
    }
}
