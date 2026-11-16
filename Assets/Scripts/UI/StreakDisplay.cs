using System.Collections.Generic;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Perfect streak göstergesi (GDD §5): sağ üstte, can barının altında, 45° çapraz.
    /// Pixel art bozulmasın diye döndürülmez; her karakter ayrı sprite olup bir öncekinden genişliği kadar
    /// yukarıda durur (merdiven). Son karakter sağ üst köşeye hizalı, yazı sol alta uzanır.
    /// Büyük sayı (2x) + altında ona paralel küçük "PERFECT". Streak 5'ten itibaren görünür.
    /// Kademe atlarken kısa parlama + büyüme, kırılınca titreyip söner.
    /// </summary>
    public class StreakDisplay : MonoBehaviour
    {
        private const int Order = 20;
        private const string Label = "PERFECT";
        private const int MaxDigits = 4;

        [SerializeField] private GameSession gameSession;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private HudButtons hudButtons;

        [SerializeField, Min(1)] private int showFromStreak = 5;
        [SerializeField, Range(1, 4)] private int numberScale = 2;
        [Tooltip("Can barının altından ve sağ kenardan boşluk (piksel).")]
        [SerializeField, Min(0)] private int marginPx = 4;
        [Tooltip("Sayı ile PERFECT satırı arası (piksel).")]
        [SerializeField, Min(0)] private int lineGapPx = 3;

        [Header("Animasyon (görsel, unscaled zaman)")]
        [SerializeField, Min(0.01f)] private float bumpSec = 0.15f;
        [SerializeField, Min(0.01f)] private float breakSec = 0.4f;

        [Header("Kademe renkleri (<10, 10+, 25+, 50+)")]
        [SerializeField] private Color[] tierColors =
        {
            new Color32(0x33, 0xFF, 0xFF, 0xFF),
            new Color32(0x66, 0xFF, 0x66, 0xFF),
            new Color32(0xFF, 0xD0, 0x40, 0xFF),
            new Color32(0xFF, 0x40, 0xC0, 0xFF),
        };
        [SerializeField] private Color flashColor = Color.white;

        private static readonly Dictionary<char, Sprite> GlyphCache = new Dictionary<char, Sprite>();

        private Transform root;
        private SpriteRenderer[] numberGlyphs;
        private SpriteRenderer[] labelGlyphs;

        private int lastStreak;
        private string shownNumber = "";
        private int shownTier;
        private float bumpTimer;
        private float breakTimer;

        private void Awake()
        {
            root = new GameObject("Streak").transform;
            root.SetParent(transform, false);

            numberGlyphs = new SpriteRenderer[MaxDigits];
            for (int i = 0; i < MaxDigits; i++) numberGlyphs[i] = CreateGlyph($"Digit {i}");

            labelGlyphs = new SpriteRenderer[Label.Length];
            for (int i = 0; i < Label.Length; i++) labelGlyphs[i] = CreateGlyph($"Label {i}");

            root.gameObject.SetActive(false);
        }

        private SpriteRenderer CreateGlyph(string objName)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Order;
            return sr;
        }

        private static Sprite Glyph(char c)
        {
            if (!GlyphCache.TryGetValue(c, out var sprite) || sprite == null)
            {
                sprite = PixelFont.CreateSprite(c.ToString(), Playfield.PixelsPerUnit);
                GlyphCache[c] = sprite;
            }
            return sprite;
        }

        private void Update()
        {
            if (gameSession.State != GameSession.SessionState.Playing)
            {
                root.gameObject.SetActive(false);
                lastStreak = 0;
                breakTimer = bumpTimer = 0f;
                return;
            }

            var k = gameSession.Keeper;
            int streak = k.PerfectStreak;

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
            int scale = numberScale;
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
                scale = numberScale + 1;
                color = Color.Lerp(color, flashColor, Mathf.Clamp01(bumpTimer / bumpSec));
            }

            Layout(scale, color, jitter);
        }

        private void Layout(int scale, Color color, Vector2Int jitter)
        {
            // Sağ üst köşe: can barının altı.
            int anchorX = screenLayout.RightPx - marginPx;
            int anchorY = hudButtons.RowBottomPx - marginPx - 2 - marginPx;
            root.localPosition = new Vector3(jitter.x / Playfield.PixelsPerUnit, jitter.y / Playfield.PixelsPerUnit, 0f);

            PlaceStair(numberGlyphs, shownNumber, anchorX, anchorY, scale, color);

            // 45° satırlar arası dikey mesafe: sayı yüksekliği + küçük yazının bir karakter genişliği + boşluk
            // (yoksa sayının sol-alttaki rakamları PERFECT'in harflerine biner). Büyüme animasyonunda zıplamasın
            // diye temel ölçek kullanılır.
            int labelDrop = PixelFont.GlyphHeight * numberScale + PixelFont.GlyphWidth + lineGapPx;
            PlaceStair(labelGlyphs, Label, anchorX, anchorY - labelDrop, 1, color);
        }

        /// <summary>
        /// Karakterleri 45° merdivende dizer: i. karakter son karaktere göre (n-1-i) adım sol-aşağıda.
        /// Adım = karakter genişliği + boşluk (yatay ve dikey aynı → 45°). (topRightX, topRightY) son karakterin sağ üstü.
        /// </summary>
        private static void PlaceStair(SpriteRenderer[] glyphs, string text, int topRightX, int topRightY, int scale, Color color)
        {
            int n = text.Length;
            int step = (PixelFont.GlyphWidth + PixelFont.Spacing) * scale;
            int glyphW = PixelFont.GlyphWidth * scale;
            int glyphH = PixelFont.GlyphHeight * scale;

            for (int i = 0; i < glyphs.Length; i++)
            {
                var sr = glyphs[i];
                if (i >= n)
                {
                    sr.enabled = false;
                    continue;
                }

                sr.enabled = true;
                sr.sprite = Glyph(text[i]); // önbellekten
                sr.color = color;

                int back = n - 1 - i;
                int x = topRightX - glyphW - back * step;
                int y = topRightY - glyphH - back * step;
                sr.transform.localPosition = new Vector3(x / Playfield.PixelsPerUnit, y / Playfield.PixelsPerUnit, 0f);
                sr.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
