using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>Son değerlendirmeyi (PERFECT / GREAT / GOOD / MISS) kısa süre gösterir.</summary>
    public class JudgementDisplay : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementSystem judgementSystem;

        [Tooltip("Yazının alt kenarı (piksel, ekran merkezine göre)")]
        [SerializeField] private int yPx = -36;
        [SerializeField, Min(0.05f)] private float displaySec = 0.4f;

        [SerializeField] private Color perfectColor = new Color32(0x33, 0xFF, 0xFF, 0xFF);
        [SerializeField] private Color greatColor = new Color32(0x66, 0xFF, 0x66, 0xFF);
        [SerializeField] private Color goodColor = new Color32(0xFF, 0xD0, 0x40, 0xFF);
        [SerializeField] private Color missColor = new Color32(0xFF, 0x40, 0x60, 0xFF);

        private static readonly string[] Labels = { "PERFECT", "GREAT", "GOOD", "MISS" };

        private SpriteRenderer textRenderer;
        private Sprite[] sprites;
        private double shownAtSongTime;
        private bool visible;

        private void Awake()
        {
            sprites = new Sprite[Labels.Length];
            for (int i = 0; i < Labels.Length; i++) sprites[i] = PixelFont.CreateSprite(Labels[i], Playfield.PixelsPerUnit);

            var go = new GameObject("Judgement Text");
            go.transform.SetParent(transform, false);
            textRenderer = go.AddComponent<SpriteRenderer>();
            textRenderer.sortingOrder = 10;
            textRenderer.enabled = false;
        }

        private void OnEnable()
        {
            judgementSystem.NoteJudged += OnNoteJudged;
        }

        private void OnDisable()
        {
            judgementSystem.NoteJudged -= OnNoteJudged;
        }

        private void OnNoteJudged(ChartNote note, Judgement judgement, double diff)
        {
            int i = (int)judgement;
            textRenderer.sprite = sprites[i];
            textRenderer.color = ColorFor(judgement);
            int x = -PixelFont.MeasureWidth(Labels[i]) / 2;
            textRenderer.transform.localPosition = new Vector3(x / Playfield.PixelsPerUnit, yPx / Playfield.PixelsPerUnit, 0f);
            textRenderer.enabled = true;
            shownAtSongTime = conductor.SongPositionSec;
            visible = true;
        }

        private void Update()
        {
            if (!visible) return;
            double age = conductor.SongPositionSec - shownAtSongTime;
            if (age > displaySec || age < 0 || !conductor.IsPlaying)
            {
                textRenderer.enabled = false;
                visible = false;
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
