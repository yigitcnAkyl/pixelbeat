using PixelBeat.Audio;
using UnityEngine;

namespace PixelBeat.UI.Menu
{
    /// <summary>Kısa buton tık sesi (efekt sesi ayarıyla). Klip kodla üretilir (metronom tıkı).</summary>
    public class UiSfx : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip click;

        /// <summary>0–1, ayarlardaki efekt sesi.</summary>
        public float Volume { get; set; } = 0.7f;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            click = MetronomeClipGenerator.CreateClick(false, AudioSettings.outputSampleRate, 0.5f);
        }

        public void Click()
        {
            if (source == null || Volume <= 0f) return;
            source.PlayOneShot(click, Volume);
        }

        private void OnDestroy()
        {
            if (click != null) Destroy(click);
        }
    }
}
