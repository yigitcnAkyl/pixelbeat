using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Perfect streak göstergesi (GDD §5): sağ üstte, can barının altında, saat yönünde ~35° dönük.
    /// Çalışma anında hiçbir şey döndürülmez: "PERFECT" ve rakamlar <see cref="StreakGlyphSet"/>'teki önceden
    /// döndürülmüş pixel sprite'lardır. Sayı, PERFECT'in üstünde aynı açıyla ve ona paralel durur; rakamlar
    /// dönük taban çizgisi boyunca dizilir. Tüm konumlar tam piksele yuvarlanır.
    /// Streak 5'ten itibaren görünür; kademe atlarken parlama + büyüme, kırılınca titreyip söner.
    /// </summary>
    public class StreakDisplay : MonoBehaviour
    {
        private const int Order = 20;
        private const int MaxDigits = 4;

        [SerializeField] private GameSession gameSession;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private HudButtons hudButtons;
        [Tooltip("Döndürülmüş sprite'lar. Açı bu asset'te ayarlanır (değiştirince 'Sprite'ları üret').")]
        [SerializeField] private StreakGlyphSet glyphs;

        [SerializeField, Min(1)] private int showFromStreak = 5;

        [Header("Konum (piksel)")]
        [Tooltip("PERFECT'in merkezi: safe area sağ kenarı ve can barının altına göre.")]
        [SerializeField] private Vector2Int labelCenterOffset = new Vector2Int(-28, -26);
        [Tooltip("Sayının merkezi, PERFECT merkezinden dönük 'yukarı' yönde bu kadar uzakta.")]
        [SerializeField] private int numberAbovePx = 14;
        [Tooltip("Sayının merkezi, dönük yazı yönünde (+ = sağ-aşağı) kaydırma.")]
        [SerializeField] private int numberAlongPx = 0;

        [Header("Animasyon (görsel, unscaled zaman)")]
        [SerializeField, Min(0.01f)] private float bumpSec = 0.15f;
        [SerializeField, Min(0.01f)] private float breakSec = 0.4f;

        [Header("Kademe renkleri (<10, 10+, 25+, 50+)")]
        [SerializeField] private PaletteRef[] tierColors =
        {
            PaletteColor.Cyan,
            PaletteColor.Lime,
            PaletteColor.Gold,
            PaletteColor.Magenta,
        };
        [SerializeField] private PaletteRef flashColor = PaletteColor.White;

        private Transform root;
        private SpriteRenderer label;
        private SpriteRenderer[] digitRenderers;

        private int lastStreak;
        private string shownNumber = "";
        private int shownTier;
        private float bumpTimer;
        private float breakTimer;

        private void Awake()
        {
            root = new GameObject("Streak").transform;
            root.SetParent(transform, false);

            label = CreateRenderer("Perfect");
            digitRenderers = new SpriteRenderer[MaxDigits];
            for (int i = 0; i < MaxDigits; i++) digitRenderers[i] = CreateRenderer($"Digit {i}");

            if (glyphs == null || !glyphs.IsBaked)
                Debug.LogWarning("[StreakDisplay] StreakGlyphSet atanmamış ya da sprite'lar üretilmemiş (PixelBeat > Bake Streak Glyphs).");
            else if (glyphs.NeedsRebake)
                Debug.LogWarning($"[StreakDisplay] Sprite'lar {glyphs.BakedAngleDeg}° ile üretilmiş, ayar {glyphs.AngleDeg}°. Yeniden üret.");

            root.gameObject.SetActive(false);
        }

        private SpriteRenderer CreateRenderer(string objName)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Order;
            return sr;
        }

        private void Update()
        {
            if (glyphs == null || !glyphs.IsBaked || gameSession.State != GameSession.SessionState.Playing)
            {
                root.gameObject.SetActive(false);
                lastStreak = 0;
                breakTimer = bumpTimer = 0f;
                return;
            }

            int streak = gameSession.Keeper.PerfectStreak;
            if (streak != lastStreak)
            {
                if (streak < lastStreak)
                {
                    // Kırıldı: görünüyorduysa son değerle titreyip söner.
                    if (lastStreak >= showFromStreak) breakTimer = breakSec;
                }
                else
                {
                    breakTimer = 0f;
                    int tier = ScoreKeeper.StreakTierFor(streak);
                    if (tier > ScoreKeeper.StreakTierFor(lastStreak)) bumpTimer = bumpSec;
                    shownTier = tier;
                    shownNumber = Mathf.Min(streak, 9999).ToString();
                }
                lastStreak = streak;
            }

            float dt = Time.unscaledDeltaTime;
            bool breaking = breakTimer > 0f;
            bool visible = breaking || streak >= showFromStreak;
            root.gameObject.SetActive(visible);
            if (!visible) return;

            Color color = tierColors[Mathf.Clamp(shownTier, 0, tierColors.Length - 1)];
            bool bump = false;
            Vector2Int jitter = Vector2Int.zero;

            if (breaking)
            {
                breakTimer -= dt;
                color.a = Mathf.Clamp01(breakTimer / breakSec);
                jitter = new Vector2Int(Random.Range(-1, 2), Random.Range(-1, 2));
            }
            else if (bumpTimer > 0f)
            {
                bumpTimer -= dt;
                bump = true;
                color = Color.Lerp(color, flashColor, Mathf.Clamp01(bumpTimer / bumpSec));
            }

            Layout(bump, color, jitter);
        }

        private void Layout(bool bump, Color color, Vector2Int jitter)
        {
            // Dönük eksenler (saat yönü θ): yazı yönü (cos, −sin) sağ-aşağı, "yukarı" (sin, cos).
            float rad = glyphs.BakedAngleDeg * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
            var up = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

            // Can barının alt kenarı: HUD sırasının 4 px altında 2 px'lik bar.
            int barBottom = hudButtons.RowBottomPx - 6;
            var labelCenter = new Vector2(screenLayout.RightPx + labelCenterOffset.x, barBottom + labelCenterOffset.y);

            root.localPosition = new Vector3(jitter.x / Playfield.PixelsPerUnit, jitter.y / Playfield.PixelsPerUnit, 0f);

            label.sprite = glyphs.Label;
            label.color = color;
            Place(label.transform, labelCenter);

            int scale = bump ? glyphs.BumpScale : glyphs.NumberScale;
            float advance = (PixelFont.GlyphWidth + PixelFont.Spacing) * scale;
            Vector2 numberCenter = labelCenter + up * numberAbovePx + along * numberAlongPx;
            int n = shownNumber.Length;

            for (int i = 0; i < digitRenderers.Length; i++)
            {
                var sr = digitRenderers[i];
                if (i >= n)
                {
                    sr.enabled = false;
                    continue;
                }

                sr.enabled = true;
                sr.sprite = glyphs.Digit(shownNumber[i] - '0', bump);
                sr.color = color;
                Place(sr.transform, numberCenter + along * ((i - (n - 1) * 0.5f) * advance));
            }
        }

        /// <summary>Merkez pivotlu (çift boyutlu) sprite'ı tam piksele oturtur.</summary>
        private static void Place(Transform t, Vector2 centerPx)
        {
            t.localPosition = new Vector3(Mathf.Round(centerPx.x) / Playfield.PixelsPerUnit,
                Mathf.Round(centerPx.y) / Playfield.PixelsPerUnit, 0f);
        }
    }
}
