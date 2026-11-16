using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Son değerlendirmeyi kısa süre gösterir (GDD §8): NeonSkin'deki hazır kelime sprite'ları — kalın, italik, renk geçişli
    /// PERFECT! / GREAT! / GOOD! / MISS! — ve arkalarında additive hale. Skin yoksa düz pixel font yazıya düşer.
    /// Basış ve hold bırakışı ayrı satırlardadır: bırakış satırı basışın hemen üstünde.
    /// </summary>
    public class JudgementDisplay : MonoBehaviour
    {
        private const int Order = 10;

        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementSystem judgementSystem;
        [SerializeField] private NeonSkin skin;

        [Tooltip("Basış yazısının alt kenarı (piksel, ekran merkezine göre)")]
        [SerializeField] private int yPx = 0;
        [Tooltip("Bırakış yazısının basış yazısına göre yukarı kayması (piksel)")]
        [SerializeField] private int releaseLineOffsetPx = 30;
        [SerializeField, Min(0.05f)] private float displaySec = 0.4f;
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.7f;

        [Header("Skin yokken düz yazı renkleri")]
        [SerializeField] private PaletteRef perfectColor = PaletteColor.Cyan;
        [SerializeField] private PaletteRef greatColor = PaletteColor.Lime;
        [SerializeField] private PaletteRef goodColor = PaletteColor.Gold;
        [SerializeField] private PaletteRef missColor = PaletteColor.Red;

        private static readonly string[] Labels = { "PERFECT!", "GREAT!", "GOOD!", "MISS!" };

        private Sprite[] fallbackSprites;
        private Line pressLine;
        private Line releaseLine;

        private class Line
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer Glow;
            public double ShownAt;
            public bool Visible;
        }

        private bool UseSkin => skin != null && skin.WordSprite(NeonSkin.Word.Perfect) != null;

        private void Awake()
        {
            if (!UseSkin)
            {
                fallbackSprites = new Sprite[Labels.Length];
                for (int i = 0; i < Labels.Length; i++) fallbackSprites[i] = PixelFont.CreateSprite(Labels[i], Playfield.PixelsPerUnit);
            }

            pressLine = CreateLine("Judgement Text");
            releaseLine = CreateLine("Release Judgement Text");
        }

        private Line CreateLine(string objName)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Order;
            sr.enabled = false;

            var glowGo = new GameObject(objName + " Glow");
            glowGo.transform.SetParent(transform, false);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sortingOrder = Order - 1;
            if (skin != null && skin.Additive != null) glow.sharedMaterial = skin.Additive;
            glow.color = new Color(1f, 1f, 1f, glowAlpha);
            glow.enabled = false;
            return new Line { Renderer = sr, Glow = glow };
        }

        private void OnEnable()
        {
            judgementSystem.NoteJudged += OnNoteJudged;
        }

        private void OnDisable()
        {
            judgementSystem.NoteJudged -= OnNoteJudged;
        }

        private void OnNoteJudged(ChartNote note, JudgementKind kind, Judgement judgement, double diff)
        {
            // Başı kaçırılan hold'da basış + bırakış aynı anda MISS olur; tek MISS yazısı yeter.
            if (kind == JudgementKind.Release && note.Result == Judgement.Miss && judgement == Judgement.Miss) return;

            bool release = kind == JudgementKind.Release;
            Show(release ? releaseLine : pressLine, judgement, release ? yPx + releaseLineOffsetPx : yPx);
        }

        private void Show(Line line, Judgement judgement, int y)
        {
            int i = (int)judgement;
            if (UseSkin)
            {
                var sprite = skin.WordSprite((NeonSkin.Word)i);
                int w = (int)sprite.rect.width;
                int x = -w / 2;
                line.Renderer.sprite = sprite;
                line.Renderer.color = Color.white;
                line.Renderer.transform.localScale = Vector3.one;
                SetPos(line.Renderer.transform, x, y);

                var glowSprite = skin.WordGlow((NeonSkin.Word)i);
                line.Glow.sprite = glowSprite;
                line.Glow.enabled = glowSprite != null;
                if (glowSprite != null)
                {
                    int m = ((int)glowSprite.rect.width - w) / 2;
                    SetPos(line.Glow.transform, x - m, y - m);
                }
            }
            else
            {
                line.Renderer.sprite = fallbackSprites[i];
                line.Renderer.color = ColorFor(judgement);
                line.Renderer.transform.localScale = new Vector3(ScreenLayout.UiScale, ScreenLayout.UiScale, 1f);
                SetPos(line.Renderer.transform, -PixelFont.MeasureUiWidth(Labels[i]) / 2, y);
            }
            line.Renderer.enabled = true;
            line.ShownAt = conductor.SongPositionSec;
            line.Visible = true;
        }

        private static void SetPos(Transform t, int x, int y) =>
            t.localPosition = new Vector3(x / Playfield.PixelsPerUnit, y / Playfield.PixelsPerUnit, 0f);

        private void Update()
        {
            Tick(pressLine);
            Tick(releaseLine);
        }

        private void Tick(Line line)
        {
            if (!line.Visible) return;
            double age = conductor.SongPositionSec - line.ShownAt;
            if (age > displaySec || age < 0 || !conductor.IsPlaying)
            {
                line.Renderer.enabled = false;
                line.Glow.enabled = false;
                line.Visible = false;
            }
        }

        private Color ColorFor(Judgement j)
        {
            switch (j)
            {
                case Judgement.Perfect: return perfectColor;
                case Judgement.Great: return greatColor;
                case Judgement.Good: return goodColor;
                default: return missColor;
            }
        }
    }
}
