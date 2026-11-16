using NUnit.Framework;
using PixelBeat.EditorTools;
using PixelBeat.UI;

namespace PixelBeat.Tests
{
    /// <summary>Streak sprite'ları için yumuşatmasız döndürme (PixelRotator).</summary>
    public class RotatedGlyphTests
    {
        /// <summary>Maskeyi içerik sınır kutusuna kırpar (kenar boşluğundan bağımsız karşılaştırma için).</summary>
        private static bool[,] Trim(bool[,] m)
        {
            int w = m.GetLength(0), h = m.GetLength(1);
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!m[x, y]) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
            if (maxX < 0) return new bool[0, 0];
            var r = new bool[maxX - minX + 1, maxY - minY + 1];
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                r[x - minX, y - minY] = m[x, y];
            return r;
        }

        private static void AssertSame(bool[,] expected, bool[,] actual)
        {
            Assert.AreEqual(expected.GetLength(0), actual.GetLength(0), "genişlik");
            Assert.AreEqual(expected.GetLength(1), actual.GetLength(1), "yükseklik");
            for (int y = 0; y < expected.GetLength(1); y++)
            for (int x = 0; x < expected.GetLength(0); x++)
                Assert.AreEqual(expected[x, y], actual[x, y], $"piksel ({x},{y})");
        }

        [Test]
        public void ZeroDegrees_KeepsGlyphExactly()
        {
            var src = PixelFont.Rasterize("PERFECT");
            AssertSame(src, Trim(PixelRotator.RotateClockwise(src, 0f)));
        }

        [Test]
        public void NinetyDegreesClockwise_MapsPixelsExactly()
        {
            // 3x2 L şekli (y yukarı):  #.. / ###
            var src = new bool[3, 2];
            src[0, 0] = src[1, 0] = src[2, 0] = true;
            src[0, 1] = true;

            var r = Trim(PixelRotator.RotateClockwise(src, 90f));
            // Saat yönünde 90°: sol sütun (aşağıdan yukarı) üst satır olur → 2x3:  ## / #. / #.  (yukarıdan aşağı)
            Assert.AreEqual(2, r.GetLength(0));
            Assert.AreEqual(3, r.GetLength(1));
            Assert.IsTrue(r[0, 2] && r[1, 2]);
            Assert.IsTrue(r[0, 1] && !r[1, 1]);
            Assert.IsTrue(r[0, 0] && !r[1, 0]);
        }

        [TestCase("PERFECT", 1)]
        [TestCase("0", 2)]
        [TestCase("8", 2)]
        [TestCase("7", 3)]
        public void Rotated35_KeepsPixelCountClose(string text, int scale)
        {
            var src = PixelFont.Rasterize(text, scale);
            var rotated = PixelRotator.RotateClockwise(src, 35f);
            int before = PixelRotator.Count(src);
            int after = PixelRotator.Count(rotated);
            // Kaplama eşiği (0.4) 1 px'lik çapraz çizgileri kopmasın diye biraz kalınlaştırır.
            Assert.That(after, Is.InRange((int)(before * 0.75f), (int)(before * 1.25f) + 1),
                $"{text}: {before} → {after}");
        }

        [TestCase(0f)]
        [TestCase(35f)]
        [TestCase(90f)]
        [TestCase(17.5f)]
        public void OutputSizes_AreEven(float angle)
        {
            foreach (var text in new[] { "PERFECT", "1", "88" })
            {
                var r = PixelRotator.RotateClockwise(PixelFont.Rasterize(text, 2), angle);
                Assert.AreEqual(0, r.GetLength(0) % 2, $"{text} @ {angle}° genişlik");
                Assert.AreEqual(0, r.GetLength(1) % 2, $"{text} @ {angle}° yükseklik");
            }
        }

        [Test]
        public void Clockwise35_TextDescendsLeftToRight()
        {
            // Tek satırlık yatay çizgi: saat yönünde döndürülünce sol ucu sağ ucundan yüksekte olmalı.
            var src = new bool[20, 1];
            for (int x = 0; x < 20; x++) src[x, 0] = true;
            var r = Trim(PixelRotator.RotateClockwise(src, 35f));

            int w = r.GetLength(0), h = r.GetLength(1);
            int leftY = -1, rightY = -1;
            for (int y = 0; y < h; y++)
            {
                if (r[0, y]) leftY = y;
                if (r[w - 1, y]) rightY = y;
            }
            Assert.Greater(leftY, rightY, "Sol uç yukarıda, sağ uç aşağıda olmalı");
            Assert.AreEqual(System.Math.Tan(35 * System.Math.PI / 180), (double)(h - 1) / (w - 1), 0.12);
        }

        [Test]
        public void Scale2x_SinglePixelAndStraightLine_AreExactDoubles()
        {
            var dot = new bool[1, 1];
            dot[0, 0] = true;
            Assert.AreEqual(4, PixelRotator.Count(PixelRotator.Scale2x(dot)));

            // Düz çizgide köşe yok: her piksel 2x2 olur.
            var line = new bool[5, 1];
            for (int x = 0; x < 5; x++) line[x, 0] = true;
            var big = PixelRotator.Scale2x(line);
            Assert.AreEqual(20, PixelRotator.Count(big));
        }

        [Test]
        public void Scale2x_FillsDiagonalStaircase()
        {
            // Çapraz 1 px merdiven: EPX köşe boşluklarını doldurur → 2x'ten fazla piksel.
            var diag = new bool[3, 3];
            diag[0, 0] = diag[1, 1] = diag[2, 2] = true;
            Assert.Greater(PixelRotator.Count(PixelRotator.Scale2x(diag)), 12);
        }
    }
}
