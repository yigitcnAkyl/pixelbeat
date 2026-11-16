using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// FAILED ve şarkı sonu sonuç ekranı (GDD §5). Üstteki CAL / R butonlarının altında kalır.
    /// Yeniden başlatma dokunuşunu GameSession dinler.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        private const int Order = 15;
        private const int LineGap = 10;

        [SerializeField] private GameSession gameSession;
        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;

        [SerializeField] private PaletteRef overlayColor = new PaletteRef(PaletteColor.Void, 0.94f);
        [SerializeField] private PaletteRef failedColor = PaletteColor.Red;
        [SerializeField] private PaletteRef textColor = PaletteColor.Haze;
        [SerializeField] private PaletteRef valueColor = PaletteColor.White;
        [SerializeField] private PaletteRef specialColor = PaletteColor.Gold;
        [SerializeField] private PaletteRef perfectColor = PaletteColor.Cyan;
        [SerializeField] private PaletteRef greatColor = PaletteColor.Lime;
        [SerializeField] private PaletteRef goodColor = PaletteColor.Gold;
        [SerializeField] private PaletteRef missColor = PaletteColor.Red;

        private static readonly string[] Labels = { "PERFECT", "GREAT", "GOOD", "MISS" };

        private GameObject root;
        private SpriteRenderer overlay;
        private PixelText headline, special, score, maxCombo, hint;
        private readonly PixelText[] countLabels = new PixelText[4];
        private readonly PixelText[] countValues = new PixelText[4];

        private void Awake()
        {
            root = new GameObject("Result UI");
            root.transform.SetParent(transform, false);
            var t = root.transform;

            overlay = playfield.CreateRect("Overlay", t, 0, 0, 1, 1, overlayColor, Order);
            headline = PixelText.Create("Headline", t, Order + 1);
            special = PixelText.Create("Special", t, Order + 1);
            score = PixelText.Create("Score", t, Order + 1);
            maxCombo = PixelText.Create("Max Combo", t, Order + 1);
            hint = PixelText.Create("Hint", t, Order + 1);
            for (int i = 0; i < 4; i++)
            {
                countLabels[i] = PixelText.Create($"Label {Labels[i]}", t, Order + 1);
                countValues[i] = PixelText.Create($"Count {Labels[i]}", t, Order + 1);
            }

            root.SetActive(false);
        }

        private void OnEnable()
        {
            gameSession.StateChanged += OnStateChanged;
            screenLayout.Changed += Refresh;
        }

        private void OnDisable()
        {
            gameSession.StateChanged -= OnStateChanged;
            screenLayout.Changed -= Refresh;
        }

        private void OnStateChanged(GameSession.SessionState state) => Refresh();

        private void Refresh()
        {
            var state = gameSession.State;
            bool failed = state == GameSession.SessionState.Failed;
            bool results = state == GameSession.SessionState.Results;
            root.SetActive(failed || results);
            if (!failed && !results) return;

            var l = screenLayout;
            Playfield.PlaceRect(overlay.transform, l.LeftPx, l.BottomPx, l.RightPx - l.LeftPx, l.HeightPx, playfield.PixelSprite.rect.width);

            var k = gameSession.Keeper;
            string scoreStr = k.Score.ToString("0000000");
            hint.Set("DOKUN: TEKRAR", 0, l.SafeBottomPx + 16, PixelText.Align.Center, textColor);

            if (failed)
            {
                headline.Set("FAILED", 0, 12, PixelText.Align.Center, failedColor, 3);
                score.Set(scoreStr, 0, -4, PixelText.Align.Center, valueColor);
                special.Visible = maxCombo.Visible = false;
                for (int i = 0; i < 4; i++) countLabels[i].Visible = countValues[i].Visible = false;
                return;
            }

            special.Visible = maxCombo.Visible = true;

            // Derece: büyük harf, en üstte.
            var grade = k.Grade;
            headline.Set(grade.ToString(), 0, 52, PixelText.Align.Center, GradeColor(grade), 4);

            string specialText = k.IsAllPerfect ? "ALL PERFECT" : k.IsFullCombo ? "FULL COMBO" : "";
            special.Set(specialText, 0, 40, PixelText.Align.Center, specialColor);

            score.Set(scoreStr, 0, 20, PixelText.Align.Center, valueColor, 2);
            maxCombo.Set($"MAX COMBO {k.MaxCombo}", 0, 6, PixelText.Align.Center, textColor);

            Color[] colors = { perfectColor, greatColor, goodColor, missColor };
            for (int i = 0; i < 4; i++)
            {
                int y = -10 - i * LineGap;
                countLabels[i].Visible = countValues[i].Visible = true;
                countLabels[i].Set(Labels[i], -45, y, PixelText.Align.Left, colors[i]);
                countValues[i].Set(k.Count((Judgement)i).ToString(), 45, y, PixelText.Align.Right, valueColor);
            }
        }

        private Color GradeColor(Grade g)
        {
            switch (g)
            {
                case Grade.S: return specialColor;
                case Grade.A: return perfectColor;
                case Grade.B: return greatColor;
                case Grade.C: return goodColor;
                default: return missColor;
            }
        }
    }
}
