using System.Collections.Generic;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Kodla tanımlı 5x7 piksel font. Metinden runtime'da Point filtreli sprite üretir,
    /// böylece yazılar 320x180 piksel ızgarasına tam oturur. Gerçek pixel font adım 6'da gelecek.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;
        public const int Spacing = 1;

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            [':'] = new[] { ".....", "..#..", ".....", ".....", ".....", "..#..", "....." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "..#.." },
        };

        public static int MeasureWidth(string text) =>
            string.IsNullOrEmpty(text) ? 0 : text.Length * (GlyphWidth + Spacing) - Spacing;

        /// <summary>
        /// Metni piksel maskesine çevirir: [x, y], y aşağıdan yukarı. scale tam sayı büyütmedir (her piksel scale x scale).
        /// </summary>
        public static bool[,] Rasterize(string text, int scale = 1)
        {
            text = text.ToUpperInvariant();
            scale = Mathf.Max(1, scale);
            int width = Mathf.Max(1, MeasureWidth(text)) * scale;
            int height = GlyphHeight * scale;
            var mask = new bool[width, height];

            for (int c = 0; c < text.Length; c++)
            {
                if (!Glyphs.TryGetValue(text[c], out var rows)) continue; // boşluk / bilinmeyen
                int x0 = c * (GlyphWidth + Spacing);
                for (int row = 0; row < GlyphHeight; row++)
                {
                    int y = GlyphHeight - 1 - row; // y aşağıdan yukarı
                    for (int col = 0; col < GlyphWidth; col++)
                    {
                        if (rows[row][col] != '#') continue;
                        for (int sy = 0; sy < scale; sy++)
                        for (int sx = 0; sx < scale; sx++)
                            mask[(x0 + col) * scale + sx, y * scale + sy] = true;
                    }
                }
            }
            return mask;
        }

        /// <summary>Beyaz metin sprite'ı üretir (pivot sol-alt). Rengi SpriteRenderer.color ile verilir.</summary>
        public static Sprite CreateSprite(string text, float pixelsPerUnit)
        {
            text = text.ToUpperInvariant();
            var mask = Rasterize(text);
            int width = mask.GetLength(0);
            var tex = new Texture2D(width, GlyphHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"PixelText_{text}"
            };

            var pixels = new Color32[width * GlyphHeight];
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < GlyphHeight; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = mask[x, y] ? white : clear;

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, width, GlyphHeight), Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
