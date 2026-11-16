using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Neon pixel buton (GDD §8): koyu iç + neon çerçeve (NeonSkin, 9-slice) + additive hale + büyük harf yazı (UI ölçeği),
    /// isteğe bağlı solda ikon. Skin yoksa düz dikdörtgene düşer. Tıklama testi oyun pikseli konumla yapılır.
    /// </summary>
    public class PixelButton
    {
        /// <summary>Parmak için görselden biraz geniş tıklama alanı (piksel).</summary>
        private const int HitPaddingPx = 3 * ScreenLayout.UiScale;

        /// <summary>Yazının iki yanındaki boşluk (UI ölçeğinde 4 px).</summary>
        private const int SidePaddingPx = 4 * ScreenLayout.UiScale;
        private const int IconGapPx = 8;

        public const int Height = 13 * ScreenLayout.UiScale;

        private static readonly Color BgColor = Palette.Get(PaletteColor.Slate);

        private readonly GameObject root;
        private readonly Playfield playfield;
        private readonly NeonSkin skin;
        private readonly SpriteRenderer frame;
        private readonly SpriteRenderer glow;
        private readonly SpriteRenderer icon;
        private readonly PixelText label;
        private PaletteColor color;
        private string text;
        private RectInt rect;
        private float glowAlpha = 0.6f;

        /// <summary>Yazıyı değiştirir (yeniden yerleştirmek için Place çağrılmalı).</summary>
        public void SetText(string newText) => text = newText;

        public string Text => text;
        public RectInt Rect => rect;
        public PaletteColor ButtonColor => color;

        public PixelButton(Playfield playfield, Transform parent, string text, int sortingOrder,
            PaletteColor color = PaletteColor.Cyan, Sprite iconSprite = null)
        {
            this.playfield = playfield;
            this.text = text;
            this.color = color;
            skin = playfield.Skin;
            root = new GameObject($"Button {text}");
            root.transform.SetParent(parent, false);

            var frameSprite = skin != null ? skin.ButtonFrame(color) : null;
            if (frameSprite != null)
            {
                frame = playfield.CreateSkinned("Frame", root.transform, frameSprite, SpriteDrawMode.Sliced, 0, 0, 8, 8, sortingOrder);
                var glowSprite = skin.ButtonGlow(color);
                if (glowSprite != null)
                {
                    // Hale Tiled: dither deseni esnetilmez, döşenir.
                    glow = playfield.CreateSkinned("Glow", root.transform, glowSprite, SpriteDrawMode.Tiled, 0, 0, 8, 8, sortingOrder - 1, additive: true);
                    glow.color = new Color(1f, 1f, 1f, glowAlpha);
                }
            }
            else
            {
                frame = playfield.CreateRect("Bg", root.transform, 0, 0, 1, 1, BgColor, sortingOrder);
            }

            if (iconSprite != null)
            {
                var go = new GameObject("Icon");
                go.transform.SetParent(root.transform, false);
                icon = go.AddComponent<SpriteRenderer>();
                icon.sprite = iconSprite;
                icon.sortingOrder = sortingOrder + 1;
            }
            label = PixelText.Create("Label", root.transform, sortingOrder + 1);
        }

        public bool Visible
        {
            get => root.activeSelf;
            set => root.SetActive(value);
        }

        /// <summary>Renk değiştirir (seçili / seçili değil): çerçeve, hale ve yazı.</summary>
        public void SetColor(PaletteColor newColor, float newGlowAlpha = 0.6f)
        {
            color = newColor;
            glowAlpha = newGlowAlpha;
            if (skin == null || frame.drawMode != SpriteDrawMode.Sliced) return;
            frame.sprite = skin.ButtonFrame(newColor);
            if (glow != null)
            {
                glow.sprite = skin.ButtonGlow(newColor);
                glow.color = new Color(1f, 1f, 1f, newGlowAlpha);
                glow.enabled = newGlowAlpha > 0f;
            }
        }

        /// <summary>Genişlik verilmezse yazıya göre (kenarlarda boşluk).</summary>
        public static int WidthFor(string text) => PixelFont.MeasureUiWidth(text) + 2 * SidePaddingPx;

        /// <summary>Sol-alt köşe (oyun pikseli). widthPx / heightPx 0 ise yazıya göre / <see cref="Height"/>. textScale: ek yazı büyütmesi.</summary>
        public void Place(int xPx, int yPx, int widthPx = 0, int heightPx = 0, int textScale = 1)
        {
            int h = heightPx > 0 ? heightPx : Height;
            int iconW = icon != null ? (int)icon.sprite.rect.width : 0;
            int textW = string.IsNullOrEmpty(text) ? 0 : PixelFont.MeasureUiWidth(text, textScale);
            int contentW = textW + (iconW > 0 ? iconW + (textW > 0 ? IconGapPx : 0) : 0);
            int w = widthPx > 0 ? widthPx : contentW + 2 * SidePaddingPx;
            rect = new RectInt(xPx, yPx, w, h);

            if (frame.drawMode == SpriteDrawMode.Sliced) Playfield.PlaceSized(frame, xPx, yPx, w, h);
            else Playfield.PlaceRect(frame.transform, xPx, yPx, w, h, playfield.PixelSprite.rect.width);
            if (glow != null)
            {
                int m = NeonSkin.ButtonGlowMarginPx;
                Playfield.PlaceSized(glow, xPx - m, yPx - m, w + 2 * m, h + 2 * m);
            }

            int cx = xPx + (w - contentW) / 2;
            if (icon != null)
            {
                int ih = (int)icon.sprite.rect.height;
                Playfield.PlaceSized(icon, cx, yPx + (h - ih) / 2, iconW, ih);
                cx += iconW + IconGapPx;
            }
            label.Set(text, cx, yPx + (h - PixelFont.UiGlyphHeight * textScale) / 2, PixelText.Align.Left,
                Palette.Get(color == PaletteColor.Ash ? PaletteColor.Haze : color), textScale);
        }

        public bool Contains(Vector2 px) =>
            Visible && root.activeInHierarchy &&
            px.x >= rect.xMin - HitPaddingPx && px.x < rect.xMax + HitPaddingPx &&
            px.y >= rect.yMin - HitPaddingPx && px.y < rect.yMax + HitPaddingPx;
    }
}
