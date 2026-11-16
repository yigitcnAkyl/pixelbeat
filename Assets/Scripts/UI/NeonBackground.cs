using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Arka plan (GDD §8): sprite (varsayılan NeonSkin.Sky; AI ile çizilmiş görselle değiştirilebilir) +
    /// ufuktan size doğru akan perspektifli neon ızgara çizgileri (tam piksel, birkaç SpriteRenderer).
    /// Sprite'ın pivot'u ufka yerleştirilir.
    /// </summary>
    public class NeonBackground : MonoBehaviour
    {
        private const int SkyOrder = -10;
        private const int GridOrder = -9;

        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private Playfield playfield;
        [Tooltip("Boşsa NeonSkin'deki üretilmiş gökyüzü kullanılır. Pivot'u ufuk çizgisinde olmalı.")]
        [SerializeField] private Sprite backgroundSprite;

        [Tooltip("Ufkun ekranın altından yüksekliği (ekran yüksekliği oranı).")]
        [SerializeField, Range(0.2f, 0.8f)] private float horizonFromBottom = 0.45f;

        [Header("Hareketli ızgara")]
        [SerializeField] private bool animatedGrid = true;
        [SerializeField, Range(3, 16)] private int gridLines = 9;
        [Tooltip("Saniyede kaç çizgi aralığı akar.")]
        [SerializeField, Range(0f, 2f)] private float gridSpeed = 0.35f;
        [SerializeField] private PaletteRef gridNearColor = PaletteColor.Violet;
        [SerializeField] private PaletteRef gridFarColor = PaletteColor.Plum;

        private SpriteRenderer sky;
        private SpriteRenderer[] lines;
        private float phase;

        private void Awake()
        {
            var go = new GameObject("Sky");
            go.transform.SetParent(transform, false);
            sky = go.AddComponent<SpriteRenderer>();
            sky.sortingOrder = SkyOrder;

            lines = new SpriteRenderer[gridLines];
            for (int i = 0; i < gridLines; i++)
                lines[i] = playfield.CreateRect($"Grid {i}", transform, 0, 0, 1, 1, gridFarColor, GridOrder);
        }

        private void OnEnable() => screenLayout.Changed += Layout;

        private void OnDisable() => screenLayout.Changed -= Layout;

        private void Start() => Layout();

        private int HorizonY => screenLayout.BottomPx + Mathf.RoundToInt(screenLayout.HeightPx * horizonFromBottom);

        private void Layout()
        {
            sky.sprite = backgroundSprite != null ? backgroundSprite : playfield.Skin.Sky;
            sky.transform.localPosition = new Vector3(0f, HorizonY / Playfield.PixelsPerUnit, 0f);
        }

        private void Update()
        {
            foreach (var l in lines) l.enabled = animatedGrid;
            if (!animatedGrid) return;

            phase = (phase + Time.unscaledDeltaTime * gridSpeed) % 1f;
            int horizon = HorizonY;
            int floor = horizon - screenLayout.BottomPx;
            int left = screenLayout.LeftPx, width = screenLayout.RightPx - screenLayout.LeftPx;

            for (int i = 0; i < lines.Length; i++)
            {
                // p: 0 = ufuk, 1 = ekranın altı. Kare ile perspektif: çizgiler ufukta sıklaşır.
                float p = (i + phase) / lines.Length;
                int y = horizon - 1 - Mathf.RoundToInt(floor * p * p);
                var sr = lines[i];
                sr.color = p < 0.35f ? (Color)gridFarColor : gridNearColor;
                Playfield.PlaceRect(sr.transform, left, y, width, 1, playfield.PixelSprite.rect.width);
            }
        }
    }
}
