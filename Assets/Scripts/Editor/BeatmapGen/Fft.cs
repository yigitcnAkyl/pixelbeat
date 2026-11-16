using System;

namespace PixelBeat.EditorTools.BeatmapGen
{
    /// <summary>Yerinde, iteratif radix-2 karmaşık FFT. Uzunluk 2'nin kuvveti olmalı.</summary>
    public static class Fft
    {
        public static void Transform(double[] re, double[] im)
        {
            int n = re.Length;
            if (n != im.Length) throw new ArgumentException("re ve im aynı uzunlukta olmalı.");
            if (n == 0 || (n & (n - 1)) != 0) throw new ArgumentException("Uzunluk 2'nin kuvveti olmalı.");

            // Bit-ters sıralama
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                double wRe = Math.Cos(ang), wIm = Math.Sin(ang);
                int half = len >> 1;
                for (int i = 0; i < n; i += len)
                {
                    double curRe = 1, curIm = 0;
                    for (int k = 0; k < half; k++)
                    {
                        int a = i + k, b = a + half;
                        double tRe = re[b] * curRe - im[b] * curIm;
                        double tIm = re[b] * curIm + im[b] * curRe;
                        re[b] = re[a] - tRe;
                        im[b] = im[a] - tIm;
                        re[a] += tRe;
                        im[a] += tIm;
                        double nRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = nRe;
                    }
                }
            }
        }
    }
}
