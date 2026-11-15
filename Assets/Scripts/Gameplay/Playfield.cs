using PixelBeat.Core;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Oyun alanı düzeni (piksel cinsinden, ekran merkezi = 0,0, 320x180) ve statik görseller:
    /// şerit paneli, ayraçlar, vuruş çizgisi, tuş göstergeleri. Her şey tam piksel dikdörtgenlerdir.
    /// </summary>
    public class Playfield : MonoBehaviour
    {
        public const float PixelsPerUnit = 16f;

        [SerializeField] private Conductor conductor;
        [SerializeField] private LaneInput laneInput;
        [Tooltip("16x16 beyaz placeholder sprite (PPU 16)")]
        [SerializeField] private Sprite pixelSprite;

        [Header("Düzen (piksel)")]
        [SerializeField] private int laneWidthPx = 20;
        [SerializeField] private int hitLineYPx = -60;
        [SerializeField] private int spawnYPx = 94;
        [SerializeField] private int despawnYPx = -96;
        [SerializeField] private int keyBoxYPx = -86;
        [SerializeField] private int keyBoxHeightPx = 14;

        [Header("Renkler")]
        [SerializeField] private Color panelColor = new Color32(0x14, 0x14, 0x28, 0xFF);
        [SerializeField] private Color separatorColor = new Color32(0x2A, 0x2A, 0x48, 0xFF);
        [SerializeField] private Color hitLineColor = new Color32(0x8A, 0x8A, 0xB0, 0xFF);
        [SerializeField] private Color hitLineBeatColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        [SerializeField] private Color keyIdleColor = new Color32(0x22, 0x22, 0x3A, 0xFF);
        [SerializeField] private Color keyPressedColor = new Color32(0x33, 0xFF, 0xFF, 0xFF);
        [SerializeField] private Color keyLabelIdleColor = new Color32(0x8A, 0x8A, 0xB0, 0xFF);
        [SerializeField] private Color keyLabelPressedColor = new Color32(0x0B, 0x0B, 0x16, 0xFF);
        [SerializeField, Range(0.05f, 1f)] private float beatGlowBeats = 0.2f;

        private SpriteRenderer hitLine;
        private SpriteRenderer[] keyBoxes;
        private SpriteRenderer[] keyLabels;

        public int LaneCount => NoteChart.LaneCount;
        public int LaneWidthPx => laneWidthPx;
        public int HitLineYPx => hitLineYPx;
        public int SpawnYPx => spawnYPx;
        public int DespawnYPx => despawnYPx;
        public int FieldWidthPx => LaneCount * laneWidthPx;
        public int FieldLeftPx => -FieldWidthPx / 2;
        public Sprite PixelSprite => pixelSprite;

        public int LaneLeftPx(int lane) => FieldLeftPx + lane * laneWidthPx;

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
            Build();
        }

        private void Build()
        {
            var root = new GameObject("Visuals").transform;
            root.SetParent(transform, false);

            CreateRect("Panel", root, FieldLeftPx, -90, FieldWidthPx, 180, panelColor, 0);

            // Ayraçlar: dış kenarlar panelin hemen dışında, iç ayraçlar şerit sınırında.
            CreateRect("Border L", root, FieldLeftPx - 1, -90, 1, 180, separatorColor, 1);
            CreateRect("Border R", root, FieldLeftPx + FieldWidthPx, -90, 1, 180, separatorColor, 1);
            for (int i = 1; i < LaneCount; i++)
                CreateRect($"Separator {i}", root, LaneLeftPx(i), -90, 1, 180, separatorColor, 1);

            hitLine = CreateRect("Hit Line", root, FieldLeftPx, hitLineYPx - 1, FieldWidthPx, 2, hitLineColor, 2);

            keyBoxes = new SpriteRenderer[LaneCount];
            keyLabels = new SpriteRenderer[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                int boxX = LaneLeftPx(i) + 1;
                int boxW = laneWidthPx - 2;
                keyBoxes[i] = CreateRect($"Key {i}", root, boxX, keyBoxYPx, boxW, keyBoxHeightPx, keyIdleColor, 3);

                string label = laneInput != null ? laneInput.GetKeyLabel(i) : "?";
                var labelGo = new GameObject($"Key Label {i}");
                labelGo.transform.SetParent(root, false);
                var sr = labelGo.AddComponent<SpriteRenderer>();
                sr.sprite = PixelFont.CreateSprite(label, PixelsPerUnit);
                sr.color = keyLabelIdleColor;
                sr.sortingOrder = 4;
                int textW = PixelFont.MeasureWidth(label);
                int lx = boxX + (boxW - textW) / 2;
                int ly = keyBoxYPx + (keyBoxHeightPx - PixelFont.GlyphHeight) / 2;
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
                keyLabels[i].color = held ? keyLabelPressedColor : keyLabelIdleColor;
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
