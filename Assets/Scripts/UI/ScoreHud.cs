using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Oynanış HUD'u (GDD §8): üstte skor (CAL ile R arasında) ve altında ince can barı;
    /// ortada değerlendirme yazısının altında combo (0 iken gizli).
    /// </summary>
    public class ScoreHud : MonoBehaviour
    {
        private const int Order = 20;
        /// <summary>HUD buton sırası ile can barı arası (piksel).</summary>
        public const int BarGapPx = 4 * ScreenLayout.UiScale;

        /// <summary>Can barının alt kenarı (StreakDisplay buna göre yerleşir).</summary>
        public int HealthBarBottomPx => hudButtons.RowBottomPx - BarGapPx - healthBarHeightPx;

        [SerializeField] private GameSession gameSession;
        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private HudButtons hudButtons;

        [Tooltip("Combo sayısının üst kenarı (piksel, ekran merkezine göre). Değerlendirme yazısının alt kenarı y = 0'da.")]
        [SerializeField] private int comboYPx = -8;
        [Tooltip("UI ölçeğinin (ScreenLayout.UiScale) üstüne ek büyütme.")]
        [SerializeField, Range(1, 4)] private int comboScale = 2;
        [SerializeField, Min(1)] private int healthBarHeightPx = 4;
        [Tooltip("Can bu değerin altına inince bar kırmızı olur.")]
        [SerializeField, Range(0, 100)] private int lowHealth = 30;

        [SerializeField] private PaletteRef scoreColor = PaletteColor.White;
        [SerializeField] private PaletteRef comboColor = PaletteColor.Magenta;
        [SerializeField] private PaletteRef healthColor = PaletteColor.Cyan;
        [SerializeField] private PaletteRef lowHealthColor = PaletteColor.Red;
        [SerializeField] private PaletteRef healthBackColor = PaletteColor.Slate;

        private PixelText scoreText;
        private PixelText comboText;
        private SpriteRenderer healthBack;
        private SpriteRenderer healthFill;
        private int barX, barY, barW;
        private int shownScore = -1, shownCombo = -1, shownHealth = -1;

        private void Awake()
        {
            scoreText = PixelText.Create("Score", transform, Order);
            comboText = PixelText.Create("Combo", transform, Order);
            healthBack = playfield.CreateRect("Health Back", transform, 0, 0, 1, 1, healthBackColor, Order);
            healthFill = playfield.CreateRect("Health Fill", transform, 0, 0, 1, 1, healthColor, Order + 1);
        }

        private void OnEnable() => screenLayout.Changed += Layout;

        private void OnDisable() => screenLayout.Changed -= Layout;

        private void Start() => Layout();

        private void Layout()
        {
            int row = hudButtons.RowBottomPx;
            barX = playfield.FieldLeftPx;
            barW = playfield.FieldWidthPx;
            barY = row - BarGapPx - healthBarHeightPx;
            Playfield.PlaceRect(healthBack.transform, barX, barY, barW, healthBarHeightPx, playfield.PixelSprite.rect.width);
            shownScore = shownCombo = shownHealth = -1; // yeniden çiz
        }

        private void Update()
        {
            bool playing = gameSession.State == GameSession.SessionState.Playing;
            scoreText.Visible = playing;
            healthBack.enabled = playing;
            healthFill.enabled = playing;
            if (!playing)
            {
                comboText.Visible = false;
                return;
            }

            var k = gameSession.Keeper;

            if (k.Score != shownScore)
            {
                shownScore = k.Score;
                int row = hudButtons.RowBottomPx;
                scoreText.Set(k.Score.ToString("0000000"), 0, row + (PixelButton.Height - PixelFont.UiGlyphHeight) / 2,
                    PixelText.Align.Center, scoreColor);
            }

            if (k.Health != shownHealth)
            {
                shownHealth = k.Health;
                int w = Mathf.RoundToInt(barW * k.Health / (float)ScoreKeeper.MaxHealth);
                healthFill.enabled = w > 0;
                if (w > 0) Playfield.PlaceRect(healthFill.transform, barX, barY, w, healthBarHeightPx, playfield.PixelSprite.rect.width);
                healthFill.color = k.Health < lowHealth ? lowHealthColor : healthColor;
            }

            if (k.Combo != shownCombo)
            {
                shownCombo = k.Combo;
                if (k.Combo > 0) comboText.Set(k.Combo.ToString(), 0, comboYPx - PixelFont.UiGlyphHeight * comboScale,
                    PixelText.Align.Center, comboColor, comboScale);
            }
            comboText.Visible = k.Combo > 0;
        }
    }
}
