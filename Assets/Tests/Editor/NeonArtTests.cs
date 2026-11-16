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

        [Test]
        public void BakedPatterns_UsePaletteOnly_WithExpectedSizes()
        {
            foreach (var c in NeonSkin.AccentColors)
            {
                var head = NeonPixelArt.NoteHead(c);
                Assert.AreEqual(NeonPixelArt.NoteHeadSpec.W, head.Width);
                Assert.AreEqual(NeonPixelArt.NoteHeadSpec.H, head.Height);
                AssertPaletteOnly(head, "head");
                AssertPaletteOnly(NeonPixelArt.HoldBody(c), "body");
                AssertPaletteOnly(NeonPixelArt.HoldTail(c), "tail");
                AssertPaletteOnly(NeonPixelArt.NoteGlow(c), "glow");
                AssertPaletteOnly(NeonPixelArt.LaneFlash(c), "flash");
            }
            var sky = NeonPixelArt.Sky();
            Assert.AreEqual(NeonPixelArt.SkyWidth, sky.Width);
            Assert.AreEqual(NeonPixelArt.SkyHeight, sky.Height);
            AssertPaletteOnly(sky, "sky");
            Assert.AreEqual(sky.Width * sky.Height, Filled(sky), "arka plan tamamen opak");
        }

        [Test]
        public void NineSliceCenters_AreTileable()
        {
            // Sliced/Tiled ile genişleyen orta sütunlar: gövde ve baş ortası tek düze, hale ortası 2 px periyotlu.
            var head = NeonPixelArt.NoteHead(PaletteColor.Cyan);
            var s = NeonPixelArt.NoteHeadSpec;
            for (int y = 0; y < head.Height; y++)
                for (int x = s.L; x < s.W - s.R; x++)
                    Assert.AreEqual(head[s.L, y], head[x, y], $"baş ortası satır {y}");

            var glow = NeonPixelArt.NoteGlow(PaletteColor.Cyan);
            var g = NeonPixelArt.NoteGlowSpec;
            Assert.AreEqual(2, g.W - g.L - g.R, "hale ortası 2 px");

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
            Assert.Greater(Row(0, 6), Row(6, 16) * 6 / 10 * 1);
            Assert.Greater(Row(6, 16), Row(16, 30) * 10 / 14);
            Assert.Greater(Row(16, 30), Row(30, 48));
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
