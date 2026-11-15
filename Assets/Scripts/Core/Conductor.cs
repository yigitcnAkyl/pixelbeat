using System;
using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>
    /// Oyunun tek zaman kaynağı. Ana saat AudioSettings.dspTime'dır.
    /// dspTime ses buffer'ı başına adım adım ilerlediği için aradaki kareler unscaled zamanla enterpole edilir;
    /// yumuşatılmış değer dspTime'dan maxDriftMs'ten fazla saparsa doğrudan dspTime'a geri senkronlanır.
    /// Diğer sistemler şarkı zamanını sadece buradan okur.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioSource))]
    public class Conductor : MonoBehaviour
    {
        /// <summary>dspTime donarsa (ör. pencere odağı kaybı) en fazla bu kadar ileri tahmin yapılır.</summary>
        private const double MaxExtrapolationSec = 0.1;

        [Header("Şarkı (beatmap'ten gelir)")]
        [SerializeField, Min(1f)] private float bpm = 120f;

        [Tooltip("Ses dosyasında ilk beat'in başladığı an (saniye).")]
        [SerializeField] private float songOffsetSec = 0f;

        [Header("Oyuncu ayarı")]
        [Tooltip("Ses/ekran/klavye gecikmesi telafisi (ms). Pozitif = ses geç duyuluyor, görüntü ve değerlendirme daha geç yapılır.")]
        [SerializeField] private float userOffsetMs = 0f;

        [Header("Başlatma")]
        [Tooltip("Play çağrısından müziğin başlamasına kadar bekleme (sn). PlayScheduled için.")]
        [SerializeField, Min(0.1f)] private float startDelaySec = 1f;

        [Header("Yumuşatma")]
        [Tooltip("Yumuşatılmış zaman dspTime tahmininden bundan fazla saparsa doğrudan ona atlar (ms).")]
        [SerializeField, Min(1f)] private float maxDriftMs = 50f;

        [Tooltip("Her karede sapmanın ne kadarının düzeltileceği (0-1).")]
        [SerializeField, Range(0.01f, 1f)] private float correctionRate = 0.1f;

        [SerializeField] private AudioSource audioSource;

        private double dspSongStart;
        private bool isPlaying;

        // Yumuşatma durumu
        private double lastDsp;
        private double lastDspUnscaled;
        private double smoothedDsp;
        private double lastFrameUnscaled;
        private double lastUpdateRealtime;

        public float Bpm => bpm;
        public double SecPerBeat => 60.0 / bpm;
        public bool IsPlaying => isPlaying;

        /// <summary>Yumuşatılmış şarkı pozisyonu (saniye). Başlangıç gecikmesi sırasında negatiftir.</summary>
        public double SongPositionSec => isPlaying ? ToSongTime(smoothedDsp) : 0.0;

        /// <summary>Yumuşatılmış şarkı pozisyonu (beat).</summary>
        public double SongPositionBeats => SongPositionSec / SecPerBeat;

        /// <summary>Ham (yumuşatılmamış) dspTime'dan şarkı pozisyonu. Hata ayıklama için.</summary>
        public double RawSongPositionSec => isPlaying ? ToSongTime(AudioSettings.dspTime) : 0.0;

        private double ToSongTime(double dsp) => dsp - dspSongStart - songOffsetSec - userOffsetMs / 1000.0;

        /// <summary>
        /// realtimeSinceStartup tabanındaki bir anı (ör. input event zaman damgası) şarkı zamanına çevirir.
        /// </summary>
        public double SongTimeAtRealtime(double realtime)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double age = now - realtime;
            if (age < 0 || age > 0.25) age = 0; // geçersiz/çok eski damga: şimdiki anı kullan

            double sinceUpdate = Math.Min(now - lastUpdateRealtime, MaxExtrapolationSec);
            return SongPositionSec + sinceUpdate - age;
        }

        public void Configure(float newBpm, float newSongOffsetSec)
        {
            bpm = Mathf.Max(1f, newBpm);
            songOffsetSec = newSongOffsetSec;
        }

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            ResetSmoothing();
        }

        public void Play(AudioClip clip)
        {
            Stop();
            audioSource.clip = clip;
            dspSongStart = AudioSettings.dspTime + startDelaySec;
            audioSource.PlayScheduled(dspSongStart);
            ResetSmoothing();
            isPlaying = true;
        }

        public void Stop()
        {
            audioSource.Stop();
            isPlaying = false;
        }

        private void ResetSmoothing()
        {
            lastDsp = AudioSettings.dspTime;
            lastDspUnscaled = Time.unscaledTimeAsDouble;
            lastFrameUnscaled = lastDspUnscaled;
            smoothedDsp = lastDsp;
            lastUpdateRealtime = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            double now = Time.unscaledTimeAsDouble;
            double dsp = AudioSettings.dspTime;

            if (dsp != lastDsp)
            {
                lastDsp = dsp;
                lastDspUnscaled = now;
            }

            double estimate = lastDsp + Math.Min(now - lastDspUnscaled, MaxExtrapolationSec);
            double next = smoothedDsp + (now - lastFrameUnscaled);
            double error = estimate - next;

            if (Math.Abs(error) > maxDriftMs / 1000.0)
                next = estimate; // çok saptı: dspTime'a geri senkronla
            else
                next += error * correctionRate;

            smoothedDsp = Math.Max(smoothedDsp, next); // zaman geri gitmez
            lastFrameUnscaled = now;
            lastUpdateRealtime = Time.realtimeSinceStartupAsDouble;
        }
    }
}
