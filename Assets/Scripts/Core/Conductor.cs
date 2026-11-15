using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>
    /// Oyunun tek zaman kaynağı. Tüm zamanlama AudioSettings.dspTime üzerinden yapılır.
    /// Diğer sistemler şarkı zamanını sadece buradan okur.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Conductor : MonoBehaviour
    {
        [Header("Şarkı")]
        [SerializeField, Min(1f)] private float bpm = 120f;

        [Tooltip("Ses dosyasında ilk beat'in başladığı an (saniye). Beatmap'ten gelir.")]
        [SerializeField] private float songOffsetSec = 0f;

        [Header("Oyuncu ayarı")]
        [Tooltip("Ses/ekran/klavye gecikmesi telafisi (ms). Pozitif = ses geç duyuluyor, görüntü ve değerlendirme daha geç yapılır.")]
        [SerializeField] private float userOffsetMs = 0f;

        [Header("Başlatma")]
        [Tooltip("Play çağrısından müziğin başlamasına kadar bekleme (sn). PlayScheduled için.")]
        [SerializeField, Min(0.1f)] private float startDelaySec = 1f;

        [SerializeField] private AudioSource audioSource;

        private double dspSongStart;
        private bool isPlaying;

        public float Bpm => bpm;
        public double SecPerBeat => 60.0 / bpm;
        public bool IsPlaying => isPlaying;

        /// <summary>Şarkı pozisyonu (saniye). Başlangıç gecikmesi sırasında negatiftir.</summary>
        public double SongPositionSec
        {
            get
            {
                if (!isPlaying) return 0.0;
                return AudioSettings.dspTime - dspSongStart - songOffsetSec - userOffsetMs / 1000.0;
            }
        }

        /// <summary>Şarkı pozisyonu (beat). Başlangıç gecikmesi sırasında negatiftir.</summary>
        public double SongPositionBeats => SongPositionSec / SecPerBeat;

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        public void Play(AudioClip clip)
        {
            Stop();
            audioSource.clip = clip;
            dspSongStart = AudioSettings.dspTime + startDelaySec;
            audioSource.PlayScheduled(dspSongStart);
            isPlaying = true;
        }

        public void Stop()
        {
            audioSource.Stop();
            isPlaying = false;
        }
    }
}
