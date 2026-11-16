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

        public const string UserOffsetPrefsKey = "userOffsetMs";

        [Header("Şarkı (beatmap'ten gelir)")]
        [SerializeField, Min(1f)] private float bpm = 120f;

        [Tooltip("Ses dosyasında ilk beat'in başladığı an (saniye).")]
        [SerializeField] private float songOffsetSec = 0f;

        [Header("Oyuncu ayarı")]
        [Tooltip("Ses/ekran/dokunma gecikmesi telafisi (ms). Pozitif = ses geç duyuluyor, görüntü ve değerlendirme daha geç yapılır. " +
                 "Gerçek cihazda PlayerPrefs'ten okunur (kalibrasyon ekranı yazar).")]
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
        private TempoMap tempoMap;

        // Yumuşatma durumu
        private double lastDsp;
        private double lastDspUnscaled;
        private double smoothedDsp;
        private double lastFrameUnscaled;
        private double lastUpdateRealtime;

        /// <summary>Müzik (ya da kalibrasyon metronomu) ses düzeyi 0–1.</summary>
        public float Volume
        {
            get => audioSource != null ? audioSource.volume : 1f;
            set
            {
                if (audioSource == null) audioSource = GetComponent<AudioSource>();
                audioSource.volume = Mathf.Clamp01(value);
            }
        }

        public float Bpm => bpm;
        public double SecPerBeat => 60.0 / bpm;
        public bool IsPlaying => isPlaying;

        /// <summary>Yumuşatılmış şarkı pozisyonu (saniye). Başlangıç gecikmesi sırasında negatiftir.</summary>
        public double SongPositionSec => isPlaying ? ToSongTime(smoothedDsp) : 0.0;

        /// <summary>Yumuşatılmış şarkı pozisyonu (beat). Tempo haritası varsa (değişken tempo) ondan.</summary>
        public double SongPositionBeats => tempoMap != null ? tempoMap.TimeToBeat(SongPositionSec) : SongPositionSec / SecPerBeat;

        /// <summary>Şarkı zamanı 0'ın (ses dosyasının başı + şarkı offset'i) denk geldiği dspTime. Ek sesleri planlamak için.</summary>
        public double DspSongStart => dspSongStart + songOffsetSec;

        public TempoMap TempoMap => tempoMap;

        /// <summary>Ham (yumuşatılmamış) dspTime'dan şarkı pozisyonu. Hata ayıklama için.</summary>
        public double RawSongPositionSec => isPlaying ? ToSongTime(AudioSettings.dspTime) : 0.0;

        public float UserOffsetMs => userOffsetMs;

        /// <summary>
        /// Kalibrasyon sadece gerçek cihaz build'inde kaydedilir. Editor / Unity Remote'ta ölçülen değer
        /// Remote'un kendi gecikmesini içerdiği için kaydedilmez.
        /// </summary>
        public static bool CanPersistUserOffset => !Application.isEditor;

        private double ToSongTime(double dsp) => SongTimeFromDsp(dsp, dspSongStart, songOffsetSec, userOffsetMs);

        /// <summary>
        /// dspTime → şarkı zamanı. userOffsetMs pozitifse şarkı zamanı geride kalır: notalar ve değerlendirme
        /// o kadar geç olur (ses geç duyulan cihazı telafi eder).
        /// </summary>
        public static double SongTimeFromDsp(double dsp, double dspSongStart, double songOffsetSec, double userOffsetMs) =>
            dsp - dspSongStart - songOffsetSec - userOffsetMs / 1000.0;

        /// <summary>Kullanıcı offset'ini değiştirir. persist = true ise (ve gerçek cihazdaysa) PlayerPrefs'e yazar.</summary>
        public void SetUserOffsetMs(float ms, bool persist)
        {
            userOffsetMs = ms;
            if (!persist || !CanPersistUserOffset) return;
            PlayerPrefs.SetFloat(UserOffsetPrefsKey, ms);
            PlayerPrefs.Save();
        }

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

        /// <summary>Sabit tempo (kalibrasyon metronomu gibi). Tempo haritasını temizler.</summary>
        public void Configure(float newBpm, float newSongOffsetSec)
        {
            bpm = Mathf.Max(1f, newBpm);
            songOffsetSec = newSongOffsetSec;
            tempoMap = null;
        }

        /// <summary>
        /// Beatmap'in tempo haritası. Harita zamanları ses dosyasının başından ölçüldüğü için şarkı offset'i 0 olur;
        /// BPM ortalama tempoya ayarlanır (SecPerBeat bilgi amaçlı).
        /// </summary>
        public void SetTempoMap(TempoMap map)
        {
            tempoMap = map;
            songOffsetSec = 0f;
            if (map != null) bpm = Mathf.Max(1f, (float)map.AverageBpm);
        }

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            if (CanPersistUserOffset && PlayerPrefs.HasKey(UserOffsetPrefsKey))
                userOffsetMs = PlayerPrefs.GetFloat(UserOffsetPrefsKey);
            ResetSmoothing();
        }

        /// <param name="loop">Kalibrasyon metronomu için. Klip tam sayı beat uzunluğunda olmalı.</param>
        public void Play(AudioClip clip, bool loop = false)
        {
            Stop();
            audioSource.clip = clip;
            audioSource.loop = loop;
            dspSongStart = AudioSettings.dspTime + startDelaySec;
            // Klip yoksa (ör. sadece planlanmış tıklar) zaman yine dspSongStart'tan akar.
            if (clip != null) audioSource.PlayScheduled(dspSongStart);
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
