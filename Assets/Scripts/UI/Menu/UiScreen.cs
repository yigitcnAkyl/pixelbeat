using PixelBeat.Audio;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI.Menu
{
    /// <summary>Ekranların ortak bağımlılıkları (ScreenRouter kurar).</summary>
    public sealed class UiContext
    {
        public ScreenRouter Router;
        public Playfield Playfield;
        public ScreenLayout Layout;
        public NeonSkin Skin;
        public UiPointer Pointer;
        public UiSfx Sfx;
        public SongCatalog Catalog;
        public ScoreBook Scores;
        public GameSettings Settings;
        public ISettingsStore Store;
        public Conductor Conductor;
        public NoteSpawner NoteSpawner;
        public BeatClickTrack ClickTrack;
        public CrtOverlay Crt;

        /// <summary>Şarkı listesinde seçilen şarkı (detay ekranı gösterir).</summary>
        public SongEntry SelectedSong;

        /// <summary>Ayarlar değişince kaydedip uygular (nota hızı, sesler).</summary>
        public void SaveSettings()
        {
            Settings.Save(Store);
            Router.ApplySettings();
        }
    }

    /// <summary>
    /// Menü ekranı tabanı (GDD §11): görseller Build'de Root altında kodla kurulur, Show/Hide ile açılır.
    /// Dokunuşlar ScreenRouter'dan gelir (UiPointer): butonlar bırakışta (tıklama) tetiklenir.
    /// </summary>
    public abstract class UiScreen : MonoBehaviour
    {
        protected const int S = ScreenLayout.UiScale;
        protected const int Order = 30;

        protected UiContext Ctx { get; private set; }
        protected Transform Root { get; private set; }

        public bool IsShown => Root != null && Root.gameObject.activeSelf;

        public void Init(UiContext ctx)
        {
            Ctx = ctx;
            Root = new GameObject($"{GetType().Name} UI").transform;
            Root.SetParent(transform, false);
            Build();
            Root.gameObject.SetActive(false);
        }

        protected abstract void Build();

        /// <summary>Ekran boyutu / içerik değişince yerleşim.</summary>
        public abstract void Layout();

        public virtual void Show()
        {
            Root.gameObject.SetActive(true);
            Layout();
        }

        public virtual void Hide() => Root.gameObject.SetActive(false);

        public virtual void OnPointerDown(Vector2 px) { }
        public virtual void OnDrag(Vector2 px) { }

        /// <summary>Bırakış; tap = parmak kaymadı.</summary>
        public virtual void OnPointerUp(Vector2 px, bool tap, float velocityY)
        {
            if (tap) OnTap(px);
        }

        protected virtual void OnTap(Vector2 px) { }

        public virtual void Tick(float dt) { }

        // ---------- Yardımcılar ----------

        protected PixelText Text(string objName, int order = Order + 2) => PixelText.Create(objName, Root, order);

        protected PixelButton Button(string text, PaletteColor color, Sprite icon = null, int order = Order + 1) =>
            new PixelButton(Ctx.Playfield, Root, text, order, color, icon);

        protected SpriteRenderer Sprite(string objName, Sprite sprite, int order, bool additive = false)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(Root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (additive && Ctx.Skin.Additive != null) sr.sharedMaterial = Ctx.Skin.Additive;
            return sr;
        }

        protected SpriteRenderer Rect(string objName, PaletteColor color, int order, float alpha = 1f) =>
            Ctx.Playfield.CreateRect(objName, Root, 0, 0, 1, 1, Palette.Get(color, alpha), order);

        protected void PlaceRect(SpriteRenderer sr, int x, int y, int w, int h) =>
            Playfield.PlaceRect(sr.transform, x, y, w, h, Ctx.Playfield.PixelSprite.rect.width);

        /// <summary>Sol-alt köşesi (x, y) olan sprite'ı gerçek boyutunda koyar.</summary>
        protected static void PlaceSprite(SpriteRenderer sr, int x, int y)
        {
            if (sr.sprite == null) return;
            Playfield.PlaceSized(sr, x, y, (int)sr.sprite.rect.width, (int)sr.sprite.rect.height);
        }

        /// <summary>Merkez pivotlu sprite'ı yatay merkez cx, alt kenar y'ye koyar.</summary>
        protected static void PlaceSpriteCentered(SpriteRenderer sr, int cx, int y)
        {
            if (sr.sprite == null) return;
            int w = (int)sr.sprite.rect.width;
            PlaceSprite(sr, cx - w / 2, y);
        }

        /// <summary>Sol-alt pivotlu (kelime/etiket) sprite'ı yatay ortalar.</summary>
        protected static void PlaceLabelCentered(SpriteRenderer sr, int cx, int y)
        {
            if (sr.sprite == null) return;
            int w = (int)sr.sprite.rect.width;
            sr.transform.localPosition = new Vector3((cx - w / 2) / Playfield.PixelsPerUnit, y / Playfield.PixelsPerUnit, 0f);
        }

        /// <summary>Sol üstte ◀ geri butonu + ortada başlık (alt sınıflar Build'de çağırır).</summary>
        protected (PixelButton back, PixelText title) Header(string titleText, int order = Order + 1)
        {
            var back = Button("", PaletteColor.Cyan, Ctx.Skin.IconBack, order);
            var title = Text("Title", order + 1);
            title.Set(titleText, 0, 0, PixelText.Align.Center, Palette.Get(PaletteColor.Magenta));
            return (back, title);
        }

        protected void LayoutHeader(PixelButton back, PixelText title, string titleText)
        {
            var l = Ctx.Layout;
            int y = l.SafeTopPx - 8 - PixelButton.Height;
            back.Place(l.LeftPx + 10, y, PixelButton.Height + 8);
            title.Set(titleText, 0, y + (PixelButton.Height - PixelFont.UiGlyphHeight) / 2, PixelText.Align.Center,
                Palette.Get(PaletteColor.Magenta));
        }

        /// <summary>İçerik alanının arkasında yarı saydam koyu zemin (yazılar arka plan üstünde okunsun).</summary>
        protected SpriteRenderer Backdrop() => Rect("Backdrop", PaletteColor.Void, Order - 2, 0.78f);

        /// <summary>Zemini tam genişlikte, başlığın altından safe area altına kadar yerleştirir.</summary>
        protected void LayoutBackdrop(SpriteRenderer backdrop)
        {
            var l = Ctx.Layout;
            int top = HeaderBottom - 6, bottom = l.SafeBottomPx + 6;
            PlaceRect(backdrop, l.LeftPx, bottom, l.RightPx - l.LeftPx, top - bottom);
        }

        /// <summary>Başlık sırasının alt kenarı (içerik bunun altından başlar).</summary>
        protected int HeaderBottom => Ctx.Layout.SafeTopPx - 8 - PixelButton.Height;

        public static Color GradeColor(Grade g)
        {
            switch (g)
            {
                case Grade.S: return Palette.Get(PaletteColor.Gold);
                case Grade.A: return Palette.Get(PaletteColor.Cyan);
                case Grade.B: return Palette.Get(PaletteColor.Lime);
                case Grade.C: return Palette.Get(PaletteColor.Orange);
                default: return Palette.Get(PaletteColor.Red);
            }
        }
    }
}
