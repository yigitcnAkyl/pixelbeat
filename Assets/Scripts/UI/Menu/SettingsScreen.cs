using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI.Menu
{
    /// <summary>
    /// Ayarlar (GDD §11): nota hızı (kaydırıcı + canlı önizleme şeridi, oyundaki piksel/sn hızıyla), ses offset'i (±5 ms)
    /// ve KALIBRE ET, müzik sesi, efekt sesi, CRT. Değerler değişince kaydedilir ve uygulanır.
    /// Offset Conductor'da (gerçek cihazda PlayerPrefs; Editor'de kaydedilmez), CRT CrtOverlay'de tutulur.
    /// </summary>
    public class SettingsScreen : UiScreen
    {
        private const int OffsetStepMs = 5;
        private const int MaxOffsetMs = 500;
        private const int SliderW = 200;
        private const int PreviewW = 64, PreviewH = 120;
        /// <summary>Önizleme notaları arası mesafe (piksel); hız oyundakiyle aynı (piksel/sn).</summary>
        private const int PreviewSpacingPx = 48;
        private const int PreviewNotes = 4;

        private PixelButton back;
        private PixelText header;
        private SpriteRenderer backdrop;

        private PixelText speedLabel, speedValue;
        private PixelSlider speed;
        private SpriteRenderer previewPanel, previewHitLine;
        private readonly SpriteRenderer[] previewNotes = new SpriteRenderer[PreviewNotes];

        private PixelText offsetLabel, offsetValue, offsetNote;
        private PixelButton offsetMinus, offsetPlus, calibrate;

        private PixelText musicLabel, musicValue;
        private PixelSlider music;
        private PixelText sfxLabel, sfxValue;
        private PixelSlider sfx;

        private PixelText crtLabel;
        private PixelButton crt;

        private int previewTop, previewBottom, previewX;
        private float previewTime;
        private PixelSlider active;

        protected override void Build()
        {
            (back, header) = Header("AYARLAR");
            backdrop = Backdrop();

            speedLabel = Text("Speed Label");
            speedValue = Text("Speed Value");
            speed = new PixelSlider(Ctx.Playfield, Root, PaletteColor.Cyan, Order + 1);
            previewPanel = Rect("Preview Panel", PaletteColor.Night, Order, 0.9f);
            previewHitLine = Rect("Preview Hit Line", PaletteColor.Haze, Order + 1);
            for (int i = 0; i < PreviewNotes; i++)
                previewNotes[i] = Sprite($"Preview Note {i}", Ctx.Skin.NoteHead(PaletteColor.Cyan), Order + 2);

            offsetLabel = Text("Offset Label");
            offsetValue = Text("Offset Value");
            offsetNote = Text("Offset Note");
            offsetMinus = Button("-", PaletteColor.Cyan);
            offsetPlus = Button("+", PaletteColor.Cyan);
            calibrate = Button("KALIBRE ET", PaletteColor.Magenta);

            musicLabel = Text("Music Label");
            musicValue = Text("Music Value");
            music = new PixelSlider(Ctx.Playfield, Root, PaletteColor.Violet, Order + 1);
            sfxLabel = Text("Sfx Label");
            sfxValue = Text("Sfx Value");
            sfx = new PixelSlider(Ctx.Playfield, Root, PaletteColor.Lime, Order + 1);

            crtLabel = Text("Crt Label");
            crt = Button("ACIK", PaletteColor.Cyan);
        }

        private static float ScrollToSlider(float sec) =>
            Mathf.InverseLerp(GameSettings.MinScrollSec, GameSettings.MaxScrollSec, sec);

        private static float SliderToScroll(float v) =>
            Mathf.Round(Mathf.Lerp(GameSettings.MinScrollSec, GameSettings.MaxScrollSec, v) * 20f) / 20f; // 0.05 sn adım

        public override void Layout()
        {
            LayoutHeader(back, header, "AYARLAR");
            LayoutBackdrop(backdrop);
            var s = Ctx.Settings;
            var haze = Palette.Get(PaletteColor.Haze);
            var white = Palette.Get(PaletteColor.White);
            const int left = -150, right = 150;
            int gh = PixelFont.UiGlyphHeight;
            int y = HeaderBottom - 24 - gh;

            // Nota hızı + önizleme (sağda)
            speedLabel.Set("NOTA HIZI", left, y, PixelText.Align.Left, haze);
            speedValue.Set($"{s.NoteScrollSec:0.00} SN", left + SliderW, y, PixelText.Align.Right, white);
            previewX = right - PreviewW;
            previewTop = y + gh;
            previewBottom = previewTop - PreviewH;
            PlaceRect(previewPanel, previewX, previewBottom, PreviewW, PreviewH);
            PlaceRect(previewHitLine, previewX, previewBottom + 12, PreviewW, 2);
            y -= 22;
            speed.Place(left, y, SliderW);
            speed.SetValue(ScrollToSlider(s.NoteScrollSec));

            // Offset
            y = previewBottom - 28 - gh;
            offsetLabel.Set("SES OFFSET", left, y, PixelText.Align.Left, haze);
            offsetValue.Set(Ms(Ctx.Conductor.UserOffsetMs), right, y, PixelText.Align.Right, white);
            y -= 10 + PixelButton.Height;
            offsetMinus.Place(left, y, 44);
            offsetPlus.Place(left + 52, y, 44);
            calibrate.Place(right - PixelButton.WidthFor("KALIBRE ET"), y);
            y -= 8 + gh;
            bool canSave = Conductor.CanPersistUserOffset;
            offsetNote.Visible = !canSave;
            if (!canSave) offsetNote.Set("EDITORDE KAYDEDILMEZ", 0, y, PixelText.Align.Center, Palette.Get(PaletteColor.Gold));

            // Sesler
            y -= 30 + gh;
            musicLabel.Set("MUZIK SESI", left, y, PixelText.Align.Left, haze);
            musicValue.Set(Percent(s.MusicVolume), right, y, PixelText.Align.Right, white);
            y -= 22;
            music.Place(left, y, right - left);
            music.SetValue(s.MusicVolume);

            y -= 30 + gh;
            sfxLabel.Set("EFEKT SESI", left, y, PixelText.Align.Left, haze);
            sfxValue.Set(Percent(s.SfxVolume), right, y, PixelText.Align.Right, white);
            y -= 22;
            sfx.Place(left, y, right - left);
            sfx.SetValue(s.SfxVolume);

            // CRT
            y -= 36 + PixelButton.Height;
            crtLabel.Set("CRT", left, y + (PixelButton.Height - gh) / 2, PixelText.Align.Left, haze);
            bool on = Ctx.Crt != null && Ctx.Crt.IsOn;
            crt.SetText(on ? "ACIK" : "KAPALI");
            crt.SetColor(on ? PaletteColor.Cyan : PaletteColor.Ash, on ? 0.6f : 0f);
            crt.Place(right - PixelButton.WidthFor("KAPALI"), y, PixelButton.WidthFor("KAPALI"));
        }

        private static string Ms(float ms) => $"{(ms >= 0 ? "+" : "-")}{Mathf.Abs(Mathf.RoundToInt(ms))} MS";
        private static string Percent(float v) => Mathf.RoundToInt(v * 100f).ToString();

        public override void Tick(float dt)
        {
            // Önizleme: oyundaki hızla (vuruş çizgisine kadar olan mesafe / nota hızı) akan elmaslar.
            previewTime += dt;
            float travelPx = Mathf.Max(1, Ctx.Playfield.SpawnYPx - Ctx.Playfield.HitLineYPx);
            float pxPerSec = travelPx / Ctx.Settings.NoteScrollSec;
            int hitY = previewBottom + 13;
            int cx = previewX + PreviewW / 2;
            for (int i = 0; i < PreviewNotes; i++)
            {
                var sr = previewNotes[i];
                // i'inci notanın vuruş çizgisine kalan mesafesi (periyodik, oyundaki hızla azalır)
                float d = Mathf.Repeat(i * PreviewSpacingPx - previewTime * pxPerSec, PreviewNotes * PreviewSpacingPx);
                int y = hitY + Mathf.RoundToInt(d);
                int h = (int)sr.sprite.rect.height;
                bool visible = y - h / 2 >= previewBottom && y + h / 2 <= previewTop;
                sr.enabled = visible;
                if (visible) Playfield.PlaceSized(sr, cx - h / 2, y - h / 2, h, h);
            }
        }

        public override void OnPointerDown(Vector2 px)
        {
            active = null;
            foreach (var sl in new[] { speed, music, sfx })
                if (sl.Press(px))
                {
                    active = sl;
                    OnSliderChanged(sl);
                    return;
                }
        }

        public override void OnDrag(Vector2 px)
        {
            if (active != null && active.Drag(px)) OnSliderChanged(active);
        }

        public override void OnPointerUp(Vector2 px, bool tap, float velocityY)
        {
            if (active != null)
            {
                active.Release();
                if (active == sfx) Ctx.Sfx.Click(); // yeni efekt sesini duyur
                active = null;
                Ctx.SaveSettings();
                return;
            }
            base.OnPointerUp(px, tap, velocityY);
        }

        private void OnSliderChanged(PixelSlider sl)
        {
            var s = Ctx.Settings;
            if (sl == speed) s.NoteScrollSec = SliderToScroll(sl.Value);
            else if (sl == music) s.MusicVolume = sl.Value;
            else if (sl == sfx) s.SfxVolume = sl.Value;
            Ctx.Router.ApplySettings();
            var white = Palette.Get(PaletteColor.White);
            const int left = -150, right = 150;
            if (sl == speed)
            {
                speedValue.Set($"{s.NoteScrollSec:0.00} SN", left + SliderW, Y(speedValue), PixelText.Align.Right, white);
                speed.SetValue(ScrollToSlider(s.NoteScrollSec)); // adıma oturt
            }
            else if (sl == music) musicValue.Set(Percent(s.MusicVolume), right, Y(musicValue), PixelText.Align.Right, white);
            else sfxValue.Set(Percent(s.SfxVolume), right, Y(sfxValue), PixelText.Align.Right, white);
        }

        private static int Y(PixelText t) => Mathf.RoundToInt(t.transform.localPosition.y * Gameplay.Playfield.PixelsPerUnit);

        protected override void OnTap(Vector2 px)
        {
            if (back.Contains(px))
            {
                Ctx.Router.Back();
                return;
            }
            if (offsetMinus.Contains(px) || offsetPlus.Contains(px))
            {
                float ms = Ctx.Conductor.UserOffsetMs + (offsetPlus.Contains(px) ? OffsetStepMs : -OffsetStepMs);
                ms = Mathf.Clamp(Mathf.Round(ms), -MaxOffsetMs, MaxOffsetMs);
                Ctx.Conductor.SetUserOffsetMs(ms, persist: true);
                Ctx.Sfx.Click();
                Layout();
                return;
            }
            if (calibrate.Contains(px))
            {
                Ctx.Router.Navigate(ScreenId.Calibration);
                return;
            }
            if (crt.Contains(px) && Ctx.Crt != null)
            {
                Ctx.Crt.Toggle();
                Ctx.Sfx.Click();
                Layout();
            }
        }
    }
}
