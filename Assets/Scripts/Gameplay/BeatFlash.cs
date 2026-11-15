using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Her beat'te yanıp sönen test karesi. Renk her karede şarkı pozisyonundan hesaplanır, durum biriktirilmez.
    /// </summary>
    public class BeatFlash : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [SerializeField] private Color baseColor = new Color(0.16f, 0.16f, 0.24f);
        [SerializeField] private Color flashColor = new Color(0.2f, 1f, 1f);
        [Tooltip("Her ölçünün ilk beat'i")]
        [SerializeField] private Color accentColor = new Color(1f, 0.25f, 0.75f);

        [SerializeField, Range(0.05f, 1f)] private float flashDurationBeats = 0.25f;
        [SerializeField, Min(1)] private int beatsPerBar = 4;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (conductor == null || spriteRenderer == null) return;

            double beat = conductor.SongPositionBeats;
            if (!conductor.IsPlaying || beat < 0)
            {
                spriteRenderer.color = baseColor;
                return;
            }

            long beatIndex = (long)System.Math.Floor(beat);
            float phase = (float)(beat - beatIndex);
            Color peak = beatIndex % beatsPerBar == 0 ? accentColor : flashColor;
            spriteRenderer.color = Color.Lerp(peak, baseColor, phase / flashDurationBeats);
        }
    }
}
