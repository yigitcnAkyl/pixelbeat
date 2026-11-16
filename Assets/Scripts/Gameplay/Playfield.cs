using PixelBeat.Core;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Dikey oyun alanı düzeni (oyun pikseli, ekran merkezi = 0,0, genişlik 180, yükseklik telefona göre) ve statik görseller:
    /// şerit paneli, ayraçlar, vuruş çizgisi, dokunma göstergeleri. Ekran/safe area değişince yeniden kurulur.
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
        [Tooltip("16x16 beyaz placeholder sprite (PPU 16)")]
        [SerializeField] private Sprite pixelSprite;

        [Header("Düzen")]
        [SerializeField] private int laneWidthPx = 40;
        [Tooltip("Vuruş çizgisinin safe area altından yüksekliği (safe area yüksekliğinin oranı).")]
        [SerializeField, Range(0.1f, 0.4f)] private float hitLineFromBottom = 0.2f;
        [Tooltip("Dokunma bölgesinin ekranın altından yüksekliği (ekran yüksekliğinin oranı).")]
        [SerializeField, Range(0.2f, 0.6f)] private float touchZoneHeight = 0.35f;
        [Tooltip("Dokunma göstergelerinin vuruş çizgisine ve safe area altına uzaklığı (piksel).")]
        [SerializeField, Min(0)] private int keyPadMarginPx = 4;

        [Header("Renkler")]
        [SerializeField] private Color backgroundColor = new Color32(0x0B, 0x0B, 0x16, 0xFF);
        [SerializeField] private Color panelColor = new Color32(0x14, 0x14, 0x28, 0xFF);
        [SerializeField] private Color separatorColor = new Color32(0x2A, 0x2A, 0x48, 0xFF);
        [SerializeField] private Color hitLineColor = new Color32(0x8A, 0x8A, 0xB0, 0xFF);
        [SerializeField] private Color hitLineBeatColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        [SerializeField] private Color keyIdleColor = new Color32(0x1A, 0x1A, 0x30, 0xFF);
        [SerializeField] private Color keyPressedColor = new Color32(0x33, 0xFF, 0xFF, 0xFF);
        [SerializeField] private Color keyLabelIdleColor = new Color32(0x8A, 0x8A, 0xB0, 0xFF);
        [SerializeField] private Color keyLabelPressedColor = new Color32(0x0B, 0x0B, 0x16, 0xFF);
        [SerializeField, Range(0.05f, 1f)] private float beatGlowBeats = 0.2f;
        [Tooltip("Hold tutulurken şerit panelinin parlaklığı (şerit rengi alfa).")]
        [SerializeField, Range(0f, 1f)] private float holdGlowAlpha = 0.07f;

        private Transform visualsRoot;
        private SpriteRenderer[] laneGlows;
        private SpriteRenderer hitLine;
        private SpriteRenderer[] keyBoxes;
        private SpriteRenderer[] keyLabels;

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

        public int LaneLeftPx(int lane) => FieldLeftPx + lane * laneWidthPx;

        /// <summary>Yatay konumdan şerit. Dış şeritler ekran kenarına kadar uzar (4 bölge tüm genişliği kaplar).</summary>
        public int LaneAtPx(float xPx) => Mathf.Clamp(Mathf.FloorToInt((xPx - FieldLeftPx) / laneWidthPx), 0, LaneCount - 1);

        /// <summary>Sol-alt köşesi (xPx, yPx) olan wPx x hPx boyutunda dikdörtgen yerleştirir (pivotu merkez olan sprite için).</summary>
        public static void PlaceRect(Transform t, float xPx, float yPx, int wPx, int hPx, float spriteSizePx = 16f)
        {
            t.localPosition = new Vector3((xPx + wPx * 0.5f) / PixelsPerUnit, (yPx + hPx * 0.5f) / PixelsPerUnit, 0f);
            t.localScale = new Vector3(wPx / spriteSizePx, hPx / spriteSizePx, 1f);
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

        private void Awake()
        {
            screenLayout.Refresh(false);
            Build();
        }

        private void OnEnable() => screenLayout.Changed += Build;

        private void OnDisable() => screenLayout.Changed -= Build;

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
            var root = visualsRoot;

            int bottom = l.BottomPx;
            int height = l.HeightPx;

            // Arka plan tüm ekranı (çentik altı dahil) kaplar; şerit paneli tam boy.
            CreateRect("Background", root, l.LeftPx, bottom, l.RightPx - l.LeftPx, height, backgroundColor, -1);
            CreateRect("Panel", root, FieldLeftPx, bottom, FieldWidthPx, height, panelColor, 0);

            // Hold tutulurken parlayan şerit katmanı (panelin üstü, ayraç pikselleriyle çakışmaz).
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

            hitLine = CreateRect("Hit Line", root, FieldLeftPx, HitLineYPx - 1, FieldWidthPx, 2, hitLineColor, 2);

            // Dokunma göstergeleri: safe area altı ile vuruş çizgisi arası.
            int padY = l.SafeBottomPx + keyPadMarginPx;
            int padH = Mathf.Max(4, HitLineYPx - keyPadMarginPx - padY);
            bool showKeyLabels = Application.isEditor && laneInput != null;

            keyBoxes = new SpriteRenderer[LaneCount];
            keyLabels = new SpriteRenderer[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                int boxX = LaneLeftPx(i) + 2;
                int boxW = laneWidthPx - 4;
                keyBoxes[i] = CreateRect($"Key {i}", root, boxX, padY, boxW, padH, keyIdleColor, 3);

                if (!showKeyLabels) continue;
                string label = laneInput.GetKeyLabel(i);
                var labelGo = new GameObject($"Key Label {i}");
                labelGo.transform.SetParent(root, false);
                var sr = labelGo.AddComponent<SpriteRenderer>();
                sr.sprite = PixelFont.CreateSprite(label, PixelsPerUnit);
                sr.color = keyLabelIdleColor;
                sr.sortingOrder = 4;
                int lx = boxX + (boxW - PixelFont.MeasureWidth(label)) / 2;
                int ly = padY + (padH - PixelFont.GlyphHeight) / 2;
                labelGo.transform.localPosition = new Vector3(lx / PixelsPerUnit, ly / PixelsPerUnit, 0f);
                keyLabels[i] = sr;
            }
        }

        private void Update()
        {
            for (int i = 0; i < LaneCount; i++)
            {
                bool held = laneInput != null && laneInput.IsHeld(i);
                keyBoxes[i].color = held ? keyPressedColor : keyIdleColor;
                if (keyLabels[i] != null) keyLabels[i].color = held ? keyLabelPressedColor : keyLabelIdleColor;

                bool holding = judgementSystem != null && judgementSystem.IsHolding(i);
                laneGlows[i].enabled = holding;
                if (holding && noteSpawner != null)
                {
                    var c = noteSpawner.LaneColor(i);
                    c.a = holdGlowAlpha;
                    laneGlows[i].color = c;
                }
            }

            // Vuruş çizgisi her beat'te kısa parlar (ses-görüntü senkron kontrolü).
            double beat = conductor != null && conductor.IsPlaying ? conductor.SongPositionBeats : -1;
            if (beat < 0)
            {
                hitLine.color = hitLineColor;
                return;
            }

            float phase = (float)(beat - System.Math.Floor(beat));
            hitLine.color = Color.Lerp(hitLineBeatColor, hitLineColor, phase / beatGlowBeats);
        }
    }
}
