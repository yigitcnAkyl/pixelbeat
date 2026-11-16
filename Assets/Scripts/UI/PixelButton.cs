using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>Placeholder pixel buton: düz dikdörtgen + yazı. Tıklama testi oyun pikseli konumla yapılır.</summary>
    public class PixelButton
    {
        /// <summary>Parmak için görselden biraz geniş tıklama alanı (piksel).</summary>
        private const int HitPaddingPx = 3;

        private static readonly Color BgColor = Palette.Get(PaletteColor.Slate);
        private static readonly Color TextColor = Palette.Get(PaletteColor.Cyan);

        private readonly GameObject root;
        private readonly Playfield playfield;
        private readonly SpriteRenderer bg;
        private readonly PixelText label;
        private string text;
        private RectInt rect;

        /// <summary>Yazıyı değiştirir (yeniden yerleştirmek için Place çağrılmalı).</summary>
        public void SetText(string newText) => text = newText;

        public string Text => text;

        public PixelButton(Playfield playfield, Transform parent, string text, int sortingOrder)
        {
            this.playfield = playfield;
            this.text = text;
            root = new GameObject($"Button {text}");
            root.transform.SetParent(parent, false);
            bg = playfield.CreateRect("Bg", root.transform, 0, 0, 1, 1, BgColor, sortingOrder);
            label = PixelText.Create("Label", root.transform, sortingOrder + 1);
        }

        public bool Visible
        {
            get => root.activeSelf;
            set => root.SetActive(value);
        }

        /// <summary>Genişlik verilmezse yazıya göre (kenarlarda 4 px boşluk).</summary>
        public static int WidthFor(string text) => PixelFont.MeasureWidth(text) + 8;

        public const int Height = 13;

        /// <summary>Sol-alt köşe (oyun pikseli).</summary>
        public void Place(int xPx, int yPx, int widthPx = 0)
        {
            int w = widthPx > 0 ? widthPx : WidthFor(text);
            rect = new RectInt(xPx, yPx, w, Height);
            Playfield.PlaceRect(bg.transform, xPx, yPx, w, Height, playfield.PixelSprite.rect.width);
            label.Set(text, xPx + w / 2, yPx + (Height - PixelFont.GlyphHeight) / 2, PixelText.Align.Center, TextColor);
        }

        public bool Contains(Vector2 px) =>
            Visible &&
            px.x >= rect.xMin - HitPaddingPx && px.x < rect.xMax + HitPaddingPx &&
            px.y >= rect.yMin - HitPaddingPx && px.y < rect.yMax + HitPaddingPx;
    }
}
