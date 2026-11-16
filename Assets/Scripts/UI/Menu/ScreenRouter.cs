using System.Collections;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.UI.Menu
{
    public enum ScreenId
    {
        MainMenu,
        SongList,
        SongDetail,
        Settings,
        Calibration,
        Game,
    }

    /// <summary>
    /// Ekranlar arası akış (GDD §11): ana menü → şarkı listesi → şarkı detayı → oyun → sonuç; ayarlar → kalibrasyon.
    /// Tek sahne: menü ekranları kodla kurulur, oyun ekranı mevcut Playfield + HUD'dur. Geçişler kısa neon fade.
    /// Android geri tuşu (Escape) bir önceki ekrana döner; ana menüde uygulamadan çıkar.
    /// Skor kaydı burada yapılır (sonuç ekranı açılırken, fail edilmemiş oyunlar).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ScreenRouter : MonoBehaviour
    {
        private const float FadeSec = 0.12f;
        private const int FadeOrder = 900;

        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private SongLoader songLoader;
        [SerializeField] private GameSession gameSession;
        [SerializeField] private Conductor conductor;
        [SerializeField] private NoteSpawner noteSpawner;
        [SerializeField] private LaneInput laneInput;
        [SerializeField] private CalibrationScreen calibration;
        [SerializeField] private CrtOverlay crtOverlay;
        [SerializeField] private ResultScreen resultScreen;
        [SerializeField] private SongCatalog catalog;

        private readonly ScreenStack<ScreenId> stack = new ScreenStack<ScreenId>();
        private UiContext ctx;
        private UiScreen[] screens;
        private SpriteRenderer fade;
        private SpriteRenderer fadeLine;
        private bool transitioning;

        public ScreenId Current => stack.IsEmpty ? ScreenId.MainMenu : stack.Current;
        public UiContext Context => ctx;
        public bool InGame => !stack.IsEmpty && stack.Current == ScreenId.Game;

        /// <summary>Son sonuç yeni rekor muydu (sonuç ekranı gösterir).</summary>
        public bool LastResultWasRecord { get; private set; }

        private void Awake()
        {
            var store = new PlayerPrefsStore();
            var pointer = gameObject.AddComponent<UiPointer>();
            pointer.Configure(screenLayout);
            ctx = new UiContext
            {
                Router = this,
                Playfield = playfield,
                Layout = screenLayout,
                Skin = playfield.Skin,
                Pointer = pointer,
                Sfx = gameObject.AddComponent<UiSfx>(),
                Catalog = catalog,
                Scores = ScoreBook.Load(ScoreBook.DefaultPath),
                Settings = GameSettings.Load(store),
                Store = store,
                Conductor = conductor,
                NoteSpawner = noteSpawner,
                ClickTrack = songLoader.ClickTrack,
                Crt = crtOverlay,
            };

            screens = new UiScreen[(int)ScreenId.Game + 1];
            screens[(int)ScreenId.MainMenu] = CreateScreen<MainMenuScreen>("Main Menu");
            screens[(int)ScreenId.SongList] = CreateScreen<SongListScreen>("Song List");
            screens[(int)ScreenId.SongDetail] = CreateScreen<SongDetailScreen>("Song Detail");
            screens[(int)ScreenId.Settings] = CreateScreen<SettingsScreen>("Settings");

            fade = playfield.CreateRect("Fade", transform, 0, 0, 1, 1, Palette.Get(PaletteColor.Void, 0f), FadeOrder);
            fadeLine = playfield.CreateRect("Fade Line", transform, 0, 0, 1, 1, Palette.Get(PaletteColor.Magenta), FadeOrder + 1);
            fade.enabled = fadeLine.enabled = false;
        }

        private T CreateScreen<T>(string objName) where T : UiScreen
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<T>();
            s.Init(ctx);
            return s;
        }

        private void OnEnable()
        {
            ctx.Pointer.Down += OnPointerDown;
            ctx.Pointer.Dragged += OnDrag;
            ctx.Pointer.Up += OnPointerUp;
            gameSession.StateChanged += OnSessionState;
            calibration.Closed += OnCalibrationClosed;
            screenLayout.Changed += OnLayoutChanged;
        }

        private void OnDisable()
        {
            ctx.Pointer.Down -= OnPointerDown;
            ctx.Pointer.Dragged -= OnDrag;
            ctx.Pointer.Up -= OnPointerUp;
            gameSession.StateChanged -= OnSessionState;
            calibration.Closed -= OnCalibrationClosed;
            screenLayout.Changed -= OnLayoutChanged;
        }

        private void Start()
        {
            ApplySettings();
            if (songLoader.AutoStartCustom)
            {
                // Editor hızlı testi: SongLoader özel haritayı zaten başlattı.
                stack.Reset(ScreenId.MainMenu);
                stack.Push(ScreenId.Game);
                EnterGameVisuals();
                return;
            }
            stack.Reset(ScreenId.MainMenu);
            ShowOnly(ScreenId.MainMenu);
        }

        /// <summary>Kayıtlı ayarları uygular: nota hızı, müzik ve efekt sesleri.</summary>
        public void ApplySettings()
        {
            var s = ctx.Settings;
            noteSpawner.ScrollTimeSec = s.NoteScrollSec;
            if (!calibration.IsOpen) conductor.Volume = s.MusicVolume;
            if (ctx.ClickTrack != null) ctx.ClickTrack.Volume = s.SfxVolume;
            ctx.Sfx.Volume = s.SfxVolume;
        }

        // ---------- Gezinme ----------

        public void Navigate(ScreenId id)
        {
            if (transitioning) return;
            ctx.Sfx.Click();
            StartCoroutine(Transition(() =>
            {
                stack.Push(id);
                ShowOnly(id);
            }));
        }

        /// <summary>Seçilen haritayla oyunu başlatır.</summary>
        public void StartGame(SongEntry song, MapEntry map)
        {
            if (transitioning || song == null || map == null) return;
            ctx.Sfx.Click();
            StartCoroutine(Transition(() =>
            {
                stack.Push(ScreenId.Game);
                ShowOnly(ScreenId.Game);
                ApplySettings();
                songLoader.Play(song, map);
            }));
        }

        /// <summary>Geri: bir önceki ekran. Oyundan çıkarken harita boşaltılır; kalibrasyon kapanır; ana menüde çıkış.</summary>
        public void Back()
        {
            if (transitioning) return;
            if (Current == ScreenId.Calibration)
            {
                calibration.Close(); // Closed → OnCalibrationClosed
                return;
            }
            if (stack.Count <= 1)
            {
                Quit();
                return;
            }
            ctx.Sfx.Click();
            StartCoroutine(Transition(() =>
            {
                if (Current == ScreenId.Game) songLoader.Unload();
                stack.TryBack(out var previous);
                ShowOnly(previous);
            }));
        }

        private void Quit()
        {
#if UNITY_EDITOR
            Debug.Log("[ScreenRouter] Ana menüde geri: uygulama kapanır (Editor'de yok sayıldı).");
#else
            Application.Quit();
#endif
        }

        private void ShowOnly(ScreenId id)
        {
            foreach (var s in screens)
                if (s != null) s.Hide();

            bool game = id == ScreenId.Game;
            if (game) EnterGameVisuals();
            else
            {
                playfield.Visible = false;
                laneInput.LaneTouchesEnabled = false;
            }

            if (id == ScreenId.Calibration)
            {
                conductor.Volume = ctx.Settings.SfxVolume; // metronom efekt sesiyle
                calibration.Open();
                return;
            }

            var screen = screens[(int)id];
            if (screen != null) screen.Show();
        }

        private void EnterGameVisuals()
        {
            playfield.Visible = true;
            laneInput.LaneTouchesEnabled = true;
        }

        private void OnCalibrationClosed()
        {
            conductor.Volume = ctx.Settings.MusicVolume;
            if (Current != ScreenId.Calibration) return;
            StartCoroutine(Transition(() =>
            {
                stack.TryBack(out var previous);
                ShowOnly(previous);
            }));
        }

        private IEnumerator Transition(System.Action swap)
        {
            transitioning = true;
            ctx.Pointer.Blocked = true;
            var l = screenLayout;
            Playfield.PlaceRect(fade.transform, l.LeftPx, l.BottomPx, l.RightPx - l.LeftPx, l.HeightPx, playfield.PixelSprite.rect.width);
            fade.enabled = fadeLine.enabled = true;

            for (float t = 0f; t < FadeSec; t += Time.unscaledDeltaTime)
            {
                SetFade(t / FadeSec);
                yield return null;
            }
            SetFade(1f);
            swap();
            yield return null;
            for (float t = 0f; t < FadeSec; t += Time.unscaledDeltaTime)
            {
                SetFade(1f - t / FadeSec);
                yield return null;
            }
            fade.enabled = fadeLine.enabled = false;
            ctx.Pointer.Blocked = false;
            transitioning = false;
        }

        /// <summary>k: 0 → 1 karartma; ortadaki neon çizgi k ile genişler (tam piksele yuvarlı).</summary>
        private void SetFade(float k)
        {
            k = Mathf.Clamp01(k);
            fade.color = Palette.Get(PaletteColor.Void, k);
            var l = screenLayout;
            int w = Mathf.RoundToInt((l.RightPx - l.LeftPx) * k);
            fadeLine.enabled = w > 0 && k < 0.98f;
            if (w > 0) Playfield.PlaceRect(fadeLine.transform, -w / 2, 0, w, 2, playfield.PixelSprite.rect.width);
        }

        // ---------- Giriş ----------

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back(); // Android geri tuşu = Escape

            var current = CurrentScreen;
            if (current != null) current.Tick(Time.unscaledDeltaTime);
        }

        private UiScreen CurrentScreen => stack.IsEmpty ? null : screens[(int)stack.Current];

        private void OnPointerDown(Vector2 px)
        {
            if (transitioning) return;
            CurrentScreen?.OnPointerDown(px);
        }

        private void OnDrag(Vector2 px)
        {
            if (transitioning) return;
            CurrentScreen?.OnDrag(px);
        }

        private void OnPointerUp(Vector2 px, bool tap, float velocityY)
        {
            if (transitioning) return;
            if (InGame)
            {
                if (tap) resultScreen.OnTap(px);
                return;
            }
            CurrentScreen?.OnPointerUp(px, tap, velocityY);
        }

        private void OnLayoutChanged()
        {
            var current = CurrentScreen;
            if (current != null && current.IsShown) current.Layout();
        }

        // ---------- Oyun sonu ----------

        private void OnSessionState(GameSession.SessionState state)
        {
            if (state == GameSession.SessionState.Results)
            {
                LastResultWasRecord = false;
                var song = songLoader.CurrentSong;
                var map = songLoader.CurrentMap;
                if (song != null && map != null)
                {
                    var k = gameSession.Keeper;
                    LastResultWasRecord = ctx.Scores.Submit(ScoreBook.Key(song.Id, map.Type, map.Difficulty),
                        k.Score, k.Grade, k.MaxCombo, k.IsFullCombo, k.IsAllPerfect);
                    ctx.Scores.Save(ScoreBook.DefaultPath);
                }
            }
            else if (state == GameSession.SessionState.Failed)
            {
                LastResultWasRecord = false;
            }
            resultScreen.Refresh();
        }

        /// <summary>Sonuç ekranı TEKRAR.</summary>
        public void Retry()
        {
            if (transitioning) return;
            ctx.Sfx.Click();
            songLoader.Restart();
        }
    }
}
