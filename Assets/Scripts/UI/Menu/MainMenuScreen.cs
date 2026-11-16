using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.UI.Menu
{
    /// <summary>Ana menü (GDD §11): üstte nabız gibi parlayan PixelBeat logosu, altında büyük ▶ OYNA ve ⚙ AYARLAR.</summary>
    public class MainMenuScreen : UiScreen
    {
        private const float PulseHz = 0.8f;

        private SpriteRenderer logo;
        private SpriteRenderer logoGlow;
        private PixelButton play;
        private PixelButton settings;
        private PixelText hint;
        private float time;

        protected override void Build()
        {
            logoGlow = Sprite("Logo Glow", Ctx.Skin.LogoGlow, Order, additive: true);
            logo = Sprite("Logo", Ctx.Skin.Logo, Order + 1);
            play = Button("OYNA", PaletteColor.Cyan, Ctx.Skin.IconPlay);
            settings = Button("AYARLAR", PaletteColor.Magenta, Ctx.Skin.IconGear);
            hint = Text("Hint");
        }

        public override void Layout()
        {
            var l = Ctx.Layout;
            int logoY = l.SafeTopPx - Mathf.Max(90, l.SafeHeightPx / 5);
            PlaceSpriteCentered(logo, 0, logoY);
            PlaceSpriteCentered(logoGlow, 0, logoY - (int)(Ctx.Skin.LogoGlow.rect.height - Ctx.Skin.Logo.rect.height) / 2);

            const int w = 220, h = 44;
            play.Place(-w / 2, 10, w, h, 1);
            settings.Place(-w / 2, 10 - h - 24, w, h, 1);
            hint.Set("V0.2", 0, l.SafeBottomPx + 10, PixelText.Align.Center, Palette.Get(PaletteColor.Steel));
        }

        public override void Tick(float dt)
        {
            time += dt;
            float a = 0.55f + 0.35f * Mathf.Sin(time * PulseHz * 2f * Mathf.PI);
            logoGlow.color = new Color(1f, 1f, 1f, a);
        }

        protected override void OnTap(Vector2 px)
        {
            if (play.Contains(px)) Ctx.Router.Navigate(ScreenId.SongList);
            else if (settings.Contains(px)) Ctx.Router.Navigate(ScreenId.Settings);
        }
    }
}
