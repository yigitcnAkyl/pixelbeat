using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI.Menu
{
    /// <summary>
    /// Neon kaydırıcı: koyu ray + renkli dolu kısım + elmas tutamak (hold kuyruğu elması ve halesi). Değer 0–1.
    /// Tutamak ya da ray basılıyken sürüklenir (ScreenRouter → ekran → <see cref="Press"/>/<see cref="Drag"/>/<see cref="Release"/>).
    /// </summary>
    public class PixelSlider
    {
        private const int RailH = 4;
        private const int HitPadY = 14;

        private readonly Playfield playfield;
        private readonly SpriteRenderer rail;
        private readonly SpriteRenderer fill;
        private readonly SpriteRenderer handle;
        private readonly SpriteRenderer handleGlow;
        private RectInt rect;
        private bool dragging;

        public float Value { get; private set; }
        public bool Dragging => dragging;

        public PixelSlider(Playfield playfield, Transform parent, PaletteColor color, int order)
        {
            this.playfield = playfield;
            var root = new GameObject("Slider").transform;
            root.SetParent(parent, false);
            rail = playfield.CreateRect("Rail", root, 0, 0, 1, 1, Palette.Get(PaletteColor.Steel), order);
            fill = playfield.CreateRect("Fill", root, 0, 0, 1, 1, Palette.Get(color), order + 1);
            var skin = playfield.Skin;
            handleGlow = NewSprite("Handle Glow", root, skin.TailGlow(color), order + 1);
            if (skin.Additive != null) handleGlow.sharedMaterial = skin.Additive;
            handleGlow.color = new Color(1f, 1f, 1f, 0.6f);
            handle = NewSprite("Handle", root, skin.HoldTail(color), order + 2);
        }

        private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Rayın sol kenarı x, dikey merkezi y, genişliği w.</summary>
        public void Place(int x, int y, int w)
        {
            rect = new RectInt(x, y - RailH / 2, w, RailH);
            Playfield.PlaceRect(rail.transform, x, y - RailH / 2, w, RailH, playfield.PixelSprite.rect.width);
            Redraw();
        }

        public void SetValue(float v)
        {
            Value = Mathf.Clamp01(v);
            Redraw();
        }

        private void Redraw()
        {
            int fw = Mathf.RoundToInt(rect.width * Value);
            fill.enabled = fw > 0;
            if (fw > 0) Playfield.PlaceRect(fill.transform, rect.x, rect.y, fw, RailH, playfield.PixelSprite.rect.width);
            int cx = rect.x + fw, cy = rect.y + RailH / 2;
            Center(handle, cx, cy);
            Center(handleGlow, cx, cy);
        }

        private static void Center(SpriteRenderer sr, int cx, int cy)
        {
            if (sr.sprite == null) return;
            int w = (int)sr.sprite.rect.width, h = (int)sr.sprite.rect.height;
            Playfield.PlaceSized(sr, cx - w / 2, cy - h / 2, w, h);
        }

        public bool Contains(Vector2 px) =>
            px.x >= rect.xMin - 10 && px.x <= rect.xMax + 10 && px.y >= rect.y - HitPadY && px.y <= rect.yMax + HitPadY;

        /// <summary>Basış raydaysa sürükleme başlar; döner: başladı mı.</summary>
        public bool Press(Vector2 px)
        {
            if (!Contains(px)) return false;
            dragging = true;
            Drag(px);
            return true;
        }

        public bool Drag(Vector2 px)
        {
            if (!dragging) return false;
            SetValue((px.x - rect.x) / Mathf.Max(1f, rect.width));
            return true;
        }

        public bool Release()
        {
            bool was = dragging;
            dragging = false;
            return was;
        }
    }
}
