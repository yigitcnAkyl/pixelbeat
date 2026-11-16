using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Neon pixel buton (GDD §8): koyu iç + neon çerçeve (NeonSkin, 9-slice) + additive hale + büyük harf yazı (UI ölçeği).
    /// Skin yoksa düz dikdörtgene düşer. Tıklama testi oyun pikseli konumla yapılır.
    /// </summary>
    public class PixelButton
    {
        /// <summary>Parmak için görselden biraz geniş tıklama alanı (piksel).</summary>
        private const int HitPaddingPx = 3 * ScreenLayout.UiScale;

        /// <summary>Yazının iki yanındaki boşluk (UI ölçeğinde 4 px).</summary>
        private const int SidePaddingPx = 4 * ScreenLayout.UiScale;

        public const int Height = 13 * ScreenLayout.UiScale;

        private static readonly Color BgColor = Palette.Get(PaletteColor.Slate);

        private readonly GameObject root;
        private readonly Playfield playfield;
        private readonly SpriteRenderer frame;
        private readonly SpriteRenderer glow;
        private readonly PixelText label;
        private readonly Color textColor;
        private string text;
        private RectInt rect;

        /// <summary>Yazıyı değiştirir (yeniden yerleştirmek için Place çağrılmalı).</summary>
        public void SetText(string newText) => text = newText;

        public string Text => text;

        public PixelButton(Playfield playfield, Transform parent, string text, int sortingOrder, PaletteColor color = PaletteColor.Cyan)
        {
            this.playfield = playfield;
            this.text = text;
            textColor = Palette.Get(color);
            root = new GameObject($"Button {text}");
            root.transform.SetParent(parent, false);

            var skin = playfield.Skin;
            var frameSprite = skin != null ? skin.ButtonFrame(color) : null;
            if (frameSprite != null)
            {
                frame = playfield.CreateSkinned("Frame", root.transform, frameSprite, SpriteDrawMode.Sliced, 0, 0, 8, 8, sortingOrder);
                var glowSprite = skin.ButtonGlow(color);
                if (glowSprite != null)
                {
                    // Hale Tiled: dither deseni esnetilmez, döşenir.
                    glow = playfield.CreateSkinned("Glow", root.transform, glowSprite, SpriteDrawMode.Tiled, 0, 0, 8, 8, sortingOrder - 1, additive: true);
                    glow.color = new Color(1f, 1f, 1f, 0.6f);
                }
            }
            else
            {
                frame = playfield.CreateRect("Bg", root.transform, 0, 0, 1, 1, BgColor, sortingOrder);
            }
            label = PixelText.Create("Label", root.transform, sortingOrder + 1);
        }

        public bool Visible
        {
            get => root.activeSelf;
            set => root.SetActive(value);
        }

        /// <summary>Genişlik verilmezse yazıya göre (kenarlarda boşluk).</summary>
        public static int WidthFor(string text) => PixelFont.MeasureUiWidth(text) + 2 * SidePaddingPx;

        /// <summary>Sol-alt köşe (oyun pikseli).</summary>
        public void Place(int xPx, int yPx, int widthPx = 0)
        {
            int w = widthPx > 0 ? widthPx : WidthFor(text);
            rect = new RectInt(xPx, yPx, w, Height);
            if (frame.drawMode == SpriteDrawMode.Sliced) Playfield.PlaceSized(frame, xPx, yPx, w, Height);
            else Playfield.PlaceRect(frame.transform, xPx, yPx, w, Height, playfield.PixelSprite.rect.width);
            if (glow != null)
            {
                int m = NeonSkin.ButtonGlowMarginPx;
                Playfield.PlaceSized(glow, xPx - m, yPx - m, w + 2 * m, Height + 2 * m);
            }            label.Set(text, xPx + w / 2, yPx + (Height - PixelFont.UiGlyphHeight) / 2, PixelText.Align.Center, textColor);
        }

        public bool Contains(Vector2 px) =>
            Visible &&
            px.x >= rect.xMin - HitPaddingPx && px.x < rect.xMax + HitPaddingPx &&
            px.y >= rect.yMin - HitPaddingPx && px.y < rect.yMax + HitPaddingPx;
    }
}
