using PixelBeat.Core;
using PixelBeat.Gameplay;
using PixelBeat.UI.Menu;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Sonuç ve FAILED ekranı (GDD §5, §11): büyük, renk geçişli derece harfi (haleli), FULL COMBO / ALL PERFECT rozeti,
    /// yanıp sönen YENI REKOR!, skor, max combo, Perfect/Great/Good/Miss sayıları; altta TEKRAR ve MENU.
    /// FAILED'da da aynı butonlar. Açıldıktan sonra kısa süre dokunuş yok sayılır (son notaya basan parmak tetiklemesin).
    /// ScreenRouter durum değişince <see cref="Refresh"/> çağırır ve dokunuşları <see cref="OnTap"/>'e iletir.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        private const int Order = 15;
        private const int S = ScreenLayout.UiScale;
        private const int LineGap = 10 * S;
        private const float TapGuardSec = 0.5f;
        private const float BlinkHz = 2.5f;

        [SerializeField] private GameSession gameSession;
        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private ScreenRouter router;

        [SerializeField] private PaletteRef overlayColor = new PaletteRef(PaletteColor.Void, 0.94f);
        [SerializeField] private PaletteRef textColor = PaletteColor.Haze;
        [SerializeField] private PaletteRef valueColor = PaletteColor.White;
        [SerializeField] private PaletteRef perfectColor = PaletteColor.Cyan;
        [SerializeField] private PaletteRef greatColor = PaletteColor.Lime;
        [SerializeField] private PaletteRef goodColor = PaletteColor.Gold;
        [SerializeField] private PaletteRef missColor = PaletteColor.Red;
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.7f;

        private static readonly string[] Labels = { "PERFECT", "GREAT", "GOOD", "MISS" };

        private GameObject root;
        private SpriteRenderer overlay;
        private SpriteRenderer headline, headlineGlow, badge, badgeGlow, record, recordGlow;
        private PixelText score, maxCombo;
        private readonly PixelText[] countLabels = new PixelText[4];
        private readonly PixelText[] countValues = new PixelText[4];
        private PixelButton retry, menu;
        private float shownAt;

        private NeonSkin Skin => playfield.Skin;

        private void Awake()
        {
            root = new GameObject("Result UI");
            root.transform.SetParent(transform, false);
            var t = root.transform;

            overlay = playfield.CreateRect("Overlay", t, 0, 0, 1, 1, overlayColor, Order);
            headlineGlow = NewSprite("Headline Glow", t, Order + 1, true);
            headline = NewSprite("Headline", t, Order + 2, false);
            badgeGlow = NewSprite("Badge Glow", t, Order + 1, true);
            badge = NewSprite("Badge", t, Order + 2, false);
            recordGlow = NewSprite("Record Glow", t, Order + 1, true);
            record = NewSprite("Record", t, Order + 2, false);
            score = PixelText.Create("Score", t, Order + 2);
            maxCombo = PixelText.Create("Max Combo", t, Order + 2);
            for (int i = 0; i < 4; i++)
            {
                countLabels[i] = PixelText.Create($"Label {Labels[i]}", t, Order + 2);
                countValues[i] = PixelText.Create($"Count {Labels[i]}", t, Order + 2);
            }
            retry = new PixelButton(playfield, t, "TEKRAR", Order + 2, PaletteColor.Cyan);
            menu = new PixelButton(playfield, t, "MENU", Order + 2, PaletteColor.Magenta);

            root.SetActive(false);
        }

        private SpriteRenderer NewSprite(string objName, Transform parent, int order, bool additive)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (additive && Skin != null && Skin.Additive != null) sr.sharedMaterial = Skin.Additive;
            if (additive) sr.color = new Color(1f, 1f, 1f, glowAlpha);
            return sr;
        }

        private void OnEnable() => screenLayout.Changed += Refresh;

        private void OnDisable() => screenLayout.Changed -= Refresh;

        public bool IsShown => root != null && root.activeSelf;

        /// <summary>Etiket sprite'ını (sol-alt pivot) ortalı koyar; halesini etrafına.</summary>
        private void PlaceLabel(SpriteRenderer sr, SpriteRenderer glow, NeonSkin.Label label, int y)
        {
            sr.sprite = Skin.LabelSprite(label);
            glow.sprite = Skin.LabelGlow(label);
            sr.enabled = sr.sprite != null;
            glow.enabled = glow.sprite != null;
            if (sr.sprite == null) return;
            int w = (int)sr.sprite.rect.width;
            int x = -w / 2;
            sr.transform.localPosition = new Vector3(x / Playfield.PixelsPerUnit, y / Playfield.PixelsPerUnit, 0f);
            if (glow.sprite != null)
            {
                int m = ((int)glow.sprite.rect.width - w) / 2;
                glow.transform.localPosition = new Vector3((x - m) / Playfield.PixelsPerUnit, (y - m) / Playfield.PixelsPerUnit, 0f);
            }
        }

        public void Refresh()
        {
            var state = gameSession.State;
            bool failed = state == GameSession.SessionState.Failed;
            bool results = state == GameSession.SessionState.Results;
            bool show = failed || results;
            if (show && !root.activeSelf) shownAt = Time.unscaledTime;
            root.SetActive(show);
            if (!show) return;

            var l = screenLayout;
            Playfield.PlaceRect(overlay.transform, l.LeftPx, l.BottomPx, l.RightPx - l.LeftPx, l.HeightPx, playfield.PixelSprite.rect.width);

            var k = gameSession.Keeper;
            string scoreStr = k.Score.ToString("0000000");

            const int bw = 140, bh = 40, gap = 16;
            int by = l.SafeBottomPx + 28;
            retry.Place(-bw - gap / 2, by, bw, bh);
            menu.Place(gap / 2, by, bw, bh);

            if (failed)
            {
                PlaceLabel(headline, headlineGlow, NeonSkin.Label.Failed, 40);
                score.Set(scoreStr, 0, -10, PixelText.Align.Center, valueColor, 2);
                badge.enabled = badgeGlow.enabled = record.enabled = recordGlow.enabled = false;
                maxCombo.Visible = false;
                for (int i = 0; i < 4; i++) countLabels[i].Visible = countValues[i].Visible = false;
                return;
            }

            maxCombo.Visible = true;
            int top = l.SafeTopPx - 40;

            // Derece harfi (büyük, haleli)
            var gradeLabel = NeonSkin.GradeLabel((int)k.Grade);
            int gradeH = Skin.LabelSprite(gradeLabel) != null ? (int)Skin.LabelSprite(gradeLabel).rect.height : 50;
            int y = top - gradeH;
            PlaceLabel(headline, headlineGlow, gradeLabel, y);

            // Rozet
            y -= 12;
            var badgeLabel = k.IsAllPerfect ? NeonSkin.Label.AllPerfect : NeonSkin.Label.FullCombo;
            bool hasBadge = k.IsAllPerfect || k.IsFullCombo;
            if (hasBadge)
            {
                int h = (int)Skin.LabelSprite(badgeLabel).rect.height;
                y -= h;
                PlaceLabel(badge, badgeGlow, badgeLabel, y);
            }
            badge.enabled = badgeGlow.enabled = hasBadge;

            // Skor + YENI REKOR
            y -= 18 + PixelFont.UiGlyphHeight * 2;
            score.Set(scoreStr, 0, y, PixelText.Align.Center, valueColor, 2);
            bool newRecord = router != null && router.LastResultWasRecord;
            if (newRecord)
            {
                int h = (int)Skin.LabelSprite(NeonSkin.Label.NewRecord).rect.height;
                y -= 10 + h;
                PlaceLabel(record, recordGlow, NeonSkin.Label.NewRecord, y);
            }
            record.enabled = recordGlow.enabled = newRecord;

            y -= 14 + PixelFont.UiGlyphHeight;
            maxCombo.Set($"MAX COMBO {k.MaxCombo}", 0, y, PixelText.Align.Center, textColor);

            Color[] colors = { perfectColor, greatColor, goodColor, missColor };
            y -= 12;
            for (int i = 0; i < 4; i++)
            {
                y -= LineGap;
                countLabels[i].Visible = countValues[i].Visible = true;
                countLabels[i].Set(Labels[i], -45 * S, y, PixelText.Align.Left, colors[i]);
                countValues[i].Set(k.Count((Judgement)i).ToString(), 45 * S, y, PixelText.Align.Right, valueColor);
            }
        }

        private void Update()
        {
            if (!root.activeSelf || !record.enabled && !recordGlow.enabled) return;
            // YENI REKOR yanıp söner (sadece görünürlük; renkler paletten).
            bool on = ((int)((Time.unscaledTime - shownAt) * BlinkHz * 2f) & 1) == 0;
            record.forceRenderingOff = !on;
            recordGlow.forceRenderingOff = !on;
        }

        /// <summary>Dokunuş (ScreenRouter'dan, tıklama). Döner: işlendi mi.</summary>
        public bool OnTap(Vector2 px)
        {
            if (!IsShown || Time.unscaledTime - shownAt < TapGuardSec) return false;
            if (retry.Contains(px))
            {
                router.Retry();
                return true;
            }
            if (menu.Contains(px))
            {
                router.Back();
                return true;
            }
            return false;
        }
    }
}
