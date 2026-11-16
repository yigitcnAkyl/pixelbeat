using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Son değerlendirmeyi (PERFECT / GREAT / GOOD / MISS) kısa süre gösterir.
    /// Basış ve hold bırakışı ayrı satırlardadır: bırakış satırı basışın hemen üstünde.
    /// </summary>
    public class JudgementDisplay : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementSystem judgementSystem;

        [Tooltip("Basış yazısının alt kenarı (piksel, ekran merkezine göre)")]
        [SerializeField] private int yPx = 0;
        [Tooltip("Bırakış yazısının basış yazısına göre yukarı kayması (piksel)")]
        [SerializeField] private int releaseLineOffsetPx = 10;
        [SerializeField, Min(0.05f)] private float displaySec = 0.4f;

        [SerializeField] private Color perfectColor = new Color32(0x33, 0xFF, 0xFF, 0xFF);
        [SerializeField] private Color greatColor = new Color32(0x66, 0xFF, 0x66, 0xFF);
        [SerializeField] private Color goodColor = new Color32(0xFF, 0xD0, 0x40, 0xFF);
        [SerializeField] private Color missColor = new Color32(0xFF, 0x40, 0x60, 0xFF);

        private static readonly string[] Labels = { "PERFECT", "GREAT", "GOOD", "MISS" };

        private Sprite[] sprites;
        private Line pressLine;
        private Line releaseLine;

        private class Line
        {
            public SpriteRenderer Renderer;
            public double ShownAt;
            public bool Visible;
        }

        private void Awake()
        {
            sprites = new Sprite[Labels.Length];
            for (int i = 0; i < Labels.Length; i++) sprites[i] = PixelFont.CreateSprite(Labels[i], Playfield.PixelsPerUnit);

            pressLine = CreateLine("Judgement Text");
            releaseLine = CreateLine("Release Judgement Text");
        }

        private Line CreateLine(string objName)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            sr.enabled = false;
            return new Line { Renderer = sr };
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
            line.Renderer.sprite = sprites[i];
            line.Renderer.color = ColorFor(judgement);
            int x = -PixelFont.MeasureWidth(Labels[i]) / 2;
            line.Renderer.transform.localPosition = new Vector3(x / Playfield.PixelsPerUnit, y / Playfield.PixelsPerUnit, 0f);
            line.Renderer.enabled = true;
            line.ShownAt = conductor.SongPositionSec;
            line.Visible = true;
        }

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
