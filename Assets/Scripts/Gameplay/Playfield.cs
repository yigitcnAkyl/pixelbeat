using PixelBeat.Core;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Dikey oyun alanı düzeni (oyun pikseli, ekran merkezi = 0,0, genişlik 360, yükseklik telefona göre) ve statik görseller:
    /// yarı saydam neon şerit panelleri, ayraçlar, vuruş çizgisi, şerit renginde ikonsuz neon dokunma düğmeleri (basınca dolar
    /// ve additive hale ile parlar). Ekran/safe area değişince yeniden kurulur.
    /// Sprite'lar NeonSkin'den (paletten üretilmiş pixel art); 9-slice / döşeme ile tam piksel boyutlanır.
    /// </summary>
    public class Playfield : MonoBehaviour
    {
        public const float PixelsPerUnit = 16f;

        [SerializeField] private Conductor conductor;
        [SerializeField] private LaneInput laneInput;
        [SerializeField] private ScreenLayout screenLayout;
        [Tooltip("Hold tutulurken şerit parlaması için.")]
        [SerializeField] private JudgementSystem judgementSystem;
        [SerializeField] private NoteSpawner noteSpawner;
        [SerializeField] private NeonSkin skin;
        [Tooltip("16x16 beyaz placeholder sprite (PPU 16) — düz dikdörtgenler için.")]
        [SerializeField] private Sprite pixelSprite;

        [Header("Düzen")]
        [SerializeField] private int laneWidthPx = 80;
        [Tooltip("Vuruş çizgisinin safe area altından yüksekliği (safe area yüksekliğinin oranı).")]
        [SerializeField, Range(0.1f, 0.4f)] private float hitLineFromBottom = 0.2f;
        [Tooltip("Dokunma bölgesinin ekranın altından yüksekliği (ekran yüksekliğinin oranı).")]
        [SerializeField, Range(0.2f, 0.6f)] private float touchZoneHeight = 0.35f;
        [Tooltip("Dokunma göstergelerinin vuruş çizgisine ve safe area altına uzaklığı (piksel).")]
        [SerializeField, Min(0)] private int keyPadMarginPx = 8;
        [Tooltip("Dokunma düğmesinin şerit kenarlarından içeri girintisi (piksel).")]
        [SerializeField, Min(0)] private int keyPadInsetPx = 6;
        [Tooltip("Basılı düğme halesinin gücü.")]
        [SerializeField, Range(0f, 1f)] private float keyGlowAlpha = 0.8f;

        [Header("Görünüm")]
        [Tooltip("Şerit panelinin opaklığı (arka plan altından hafifçe görünsün).")]
        [SerializeField, Range(0f, 1f)] private float panelAlpha = 0.82f;
        [SerializeField] private PaletteRef separatorColor = PaletteColor.Steel;
        [SerializeField] private PaletteRef keyLabelIdleColor = PaletteColor.Haze;
        [SerializeField] private PaletteRef keyLabelPressedColor = PaletteColor.White;
        [SerializeField, Range(0.05f, 1f)] private float beatGlowBeats = 0.2f;
        [Tooltip("Hold tutulurken şerit panelinin parlaklığı (şerit rengi alfa).")]
        [SerializeField, Range(0f, 1f)] private float holdGlowAlpha = 0.07f;

        private Transform visualsRoot;
        private SpriteRenderer[] laneGlows;
        private SpriteRenderer hitLine;
        private SpriteRenderer[] keyBoxes;
        private SpriteRenderer[] keyGlows;
        private SpriteRenderer[] keyLabels;
        private float hitFlashTimer;

        public int LaneCount => NoteChart.LaneCount;
        public int LaneWidthPx => laneWidthPx;
        public int HitLineYPx { get; private set; }
        /// <summary>Notaların doğduğu yükseklik (ekranın üstü).</summary>
        public int SpawnYPx { get; private set; }
        /// <summary>Notaların kaybolduğu yükseklik (ekranın altı).</summary>
        public int DespawnYPx { get; private set; }
        /// <summary>Bu yüksekliğin altında başlayan dokunuşlar şerit basışıdır.</summary>
        public int TouchZoneTopYPx { get; private set; }
        public int FieldWidthPx => LaneCount * laneWidthPx;
        public int FieldLeftPx => -FieldWidthPx / 2;
        public Sprite PixelSprite => pixelSprite;
        public ScreenLayout Layout => screenLayout;
        public NeonSkin Skin => skin;

        public int LaneLeftPx(int lane) => FieldLeftPx + lane * laneWidthPx;
        public int LaneCenterPx(int lane) => LaneLeftPx(lane) + laneWidthPx / 2;

        /// <summary>Yatay konumdan şerit. Dış şeritler ekran kenarına kadar uzar (4 bölge tüm genişliği kaplar).</summary>
        public int LaneAtPx(float xPx) => Mathf.Clamp(Mathf.FloorToInt((xPx - FieldLeftPx) / laneWidthPx), 0, LaneCount - 1);

        /// <summary>Sol-alt köşesi (xPx, yPx) olan wPx x hPx boyutunda dikdörtgen yerleştirir (pivotu merkez olan sprite için).</summary>
        public static void PlaceRect(Transform t, float xPx, float yPx, int wPx, int hPx, float spriteSizePx = 16f)
        {
            t.localPosition = new Vector3((xPx + wPx * 0.5f) / PixelsPerUnit, (yPx + hPx * 0.5f) / PixelsPerUnit, 0f);
            t.localScale = new Vector3(wPx / spriteSizePx, hPx / spriteSizePx, 1f);
        }

        /// <summary>9-slice / döşemeli sprite'ı ölçeklemeden, tam piksel boyutla yerleştirir (sol-alt köşe).</summary>
        public static void PlaceSized(SpriteRenderer sr, float xPx, float yPx, int wPx, int hPx)
        {
            sr.transform.localPosition = new Vector3((xPx + wPx * 0.5f) / PixelsPerUnit, (yPx + hPx * 0.5f) / PixelsPerUnit, 0f);
            sr.transform.localScale = Vector3.one;
            sr.size = new Vector2(wPx / PixelsPerUnit, hPx / PixelsPerUnit);
        }

        public SpriteRenderer CreateRect(string objName, Transform parent, int xPx, int yPx, int wPx, int hPx, Color color, int order)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixelSprite;
            sr.color = color;
            sr.sortingOrder = order;
            PlaceRect(go.transform, xPx, yPx, wPx, hPx, pixelSprite.rect.width);
            return sr;
        }

        /// <summary>NeonSkin sprite'ı (Sliced ya da Tiled) oluşturur. additive = parlama materyali.</summary>
        public SpriteRenderer CreateSkinned(string objName, Transform parent, Sprite sprite, SpriteDrawMode mode,
            int xPx, int yPx, int wPx, int hPx, int order, bool additive = false)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = mode;
            if (mode == SpriteDrawMode.Tiled) sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = order;
            if (additive && skin != null && skin.Additive != null) sr.sharedMaterial = skin.Additive;
            PlaceSized(sr, xPx, yPx, wPx, hPx);
            return sr;
        }

        private bool visible = true;

        /// <summary>Menülerde oyun alanı (şeritler, vuruş çizgisi, düğmeler) gizlenir; arka plan kalır.</summary>
        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                if (visualsRoot != null) visualsRoot.gameObject.SetActive(value);
            }
        }

        /// <summary>Vuruşta çizgi kısa süre parlak sprite'a geçer (HitFeedback çağırır).</summary>
        public void FlashHitLine(float seconds) => hitFlashTimer = Mathf.Max(hitFlashTimer, seconds);

        private void Awake()
        {
            screenLayout.Refresh(false);
            Build();
        }

        private void OnEnable() => screenLayout.Changed += Build;

        private void OnDisable() => screenLayout.Changed -= Build;

        private PaletteColor LaneColor(int lane) => noteSpawner != null ? noteSpawner.LanePalette(lane) : PaletteColor.Cyan;

        private void Build()
        {
            var l = screenLayout;
            HitLineYPx = l.SafeBottomPx + Mathf.RoundToInt(l.SafeHeightPx * hitLineFromBottom);
            SpawnYPx = l.TopPx + 2;
            DespawnYPx = l.BottomPx - 2;
            TouchZoneTopYPx = l.BottomPx + Mathf.RoundToInt(l.HeightPx * touchZoneHeight);

            if (visualsRoot != null) Destroy(visualsRoot.gameObject);
            visualsRoot = new GameObject("Visuals").transform;
            visualsRoot.SetParent(transform, false);
            visualsRoot.gameObject.SetActive(visible);
            var root = visualsRoot;

            int bottom = l.BottomPx;
            int height = l.HeightPx;

            // Yarı saydam döşemeli şerit panelleri (arka plan altından görünür).
            for (int i = 0; i < LaneCount; i++)
            {
                var panel = CreateSkinned($"Lane {i}", root, skin.LanePanel, SpriteDrawMode.Tiled,
                    LaneLeftPx(i) + 1, bottom, laneWidthPx - 1, height, 0);
                panel.color = new Color(1f, 1f, 1f, panelAlpha);
            }

            // Hold tutulurken parlayan şerit katmanı.
            laneGlows = new SpriteRenderer[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                laneGlows[i] = CreateRect($"Lane Glow {i}", root, LaneLeftPx(i) + 1, bottom, laneWidthPx - 1, height, Color.clear, 1);
                laneGlows[i].enabled = false;
            }

            // Ayraçlar: dış kenarlar panelin hemen dışında, iç ayraçlar şerit sınırında.
            CreateRect("Border L", root, FieldLeftPx - 1, bottom, 1, height, separatorColor, 1);
            CreateRect("Border R", root, FieldLeftPx + FieldWidthPx, bottom, 1, height, separatorColor, 1);
            for (int i = 1; i < LaneCount; i++)
                CreateRect($"Separator {i}", root, LaneLeftPx(i), bottom, 1, height, separatorColor, 1);

            hitLine = CreateSkinned("Hit Line", root, skin.HitLineIdle, SpriteDrawMode.Sliced,
                FieldLeftPx - 1, HitLineYPx - 2, FieldWidthPx + 2, 4, 3);

            // Dokunma düğmeleri: safe area altı ile vuruş çizgisi arası; en fazla kare (konseptteki gibi), alana dikeyde ortalı.
            int areaY = l.SafeBottomPx + keyPadMarginPx;
            int areaH = Mathf.Max(8, HitLineYPx - keyPadMarginPx - areaY);
            int boxW = laneWidthPx - 2 * keyPadInsetPx;
            int padH = Mathf.Min(areaH, boxW);
            int padY = areaY + (areaH - padH) / 2;
            bool showKeyLabels = Application.isEditor && laneInput != null;
            int glowM = NeonSkin.ButtonGlowMarginPx;

            keyBoxes = new SpriteRenderer[LaneCount];
            keyGlows = new SpriteRenderer[LaneCount];
            keyLabels = new SpriteRenderer[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                int boxX = LaneLeftPx(i) + keyPadInsetPx;
                keyBoxes[i] = CreateSkinned($"Key {i}", root, skin.KeyPadIdle(LaneColor(i)), SpriteDrawMode.Sliced, boxX, padY, boxW, padH, 3);
                // Hale Tiled: dither deseni esnetilmez, döşenir.
                keyGlows[i] = CreateSkinned($"Key Glow {i}", root, skin.KeyPadGlow(LaneColor(i)), SpriteDrawMode.Tiled,
                    boxX - glowM, padY - glowM, boxW + 2 * glowM, padH + 2 * glowM, 2, additive: true);
                keyGlows[i].color = new Color(1f, 1f, 1f, keyGlowAlpha);
                keyGlows[i].enabled = false;

                if (!showKeyLabels) continue;
                string label = laneInput.GetKeyLabel(i);
                var labelGo = new GameObject($"Key Label {i}");
                labelGo.transform.SetParent(root, false);
                labelGo.transform.localScale = new Vector3(ScreenLayout.UiScale, ScreenLayout.UiScale, 1f);
                var sr = labelGo.AddComponent<SpriteRenderer>();
                sr.sprite = PixelFont.CreateSprite(label, PixelsPerUnit);
                sr.color = keyLabelIdleColor;
                sr.sortingOrder = 4;
                int lx = boxX + (boxW - PixelFont.MeasureUiWidth(label)) / 2;
                int ly = padY + (padH - PixelFont.UiGlyphHeight) / 2;
                labelGo.transform.localPosition = new Vector3(lx / PixelsPerUnit, ly / PixelsPerUnit, 0f);
                keyLabels[i] = sr;
            }
        }

        private void Update()
        {
            if (!visible) return;
            for (int i = 0; i < LaneCount; i++)
            {
                bool held = laneInput != null && laneInput.IsHeld(i);
                keyBoxes[i].sprite = held ? skin.KeyPadPressed(LaneColor(i)) : skin.KeyPadIdle(LaneColor(i));
                keyGlows[i].enabled = held;
                if (keyLabels[i] != null) keyLabels[i].color = held ? keyLabelPressedColor : keyLabelIdleColor;

                bool holding = judgementSystem != null && judgementSystem.IsHolding(i);
                laneGlows[i].enabled = holding;
                if (holding) laneGlows[i].color = Palette.Get(LaneColor(i), holdGlowAlpha);
            }

            // Vuruş çizgisi: vuruşta ve her beat başında kısa süre parlak sprite (ses-görüntü senkron kontrolü).
            hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.unscaledDeltaTime);
            bool bright = hitFlashTimer > 0f;
            if (!bright && conductor != null && conductor.IsPlaying)
            {
                double beat = conductor.SongPositionBeats;
                bright = beat >= 0 && beat - System.Math.Floor(beat) < beatGlowBeats;
            }
            hitLine.sprite = bright ? skin.HitLineBeat : skin.HitLineIdle;
        }
    }
}
