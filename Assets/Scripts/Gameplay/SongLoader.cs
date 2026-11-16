using PixelBeat.Audio;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.Gameplay
{
    /// <summary>Harita zorluğu. Custom = katalog dışı tek beatmap (Editor hızlı test).</summary>
    public enum SongDifficulty
    {
        Custom,
        Easy,
        Normal,
        Hard,
    }

    /// <summary>Üretilmiş harita türü: melodi modu (GDD §7.1) ya da vokal modu (§7.2).</summary>
    public enum MapType
    {
        Melody,
        Vocal,
    }

    /// <summary>
    /// Beatmap JSON'unu yükler, Conductor'ı tempo haritasıyla yapılandırır, sistemlere chart'ı verir ve şarkıyı başlatır.
    /// Şarkı ve harita şarkı listesinden (<see cref="SongCatalog"/>) seçilir → <see cref="Play"/>. Menüye dönerken <see cref="Unload"/>.
    /// Editor hızlı testi: "Özel" beatmap + şarkı ve <c>autoStartCustom</c> açıkken sahne doğrudan oyunla başlar.
    /// Şarkı yoksa beatmap'in beat'lerinde metronom tıkı çalar. R: baştan başlat.
    /// </summary>
    public class SongLoader : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementSystem judgementSystem;
        [SerializeField] private NoteSpawner noteSpawner;

        [Header("Editor hızlı test (katalog dışı)")]
        [Tooltip("Açıksa Play'de menü yerine doğrudan aşağıdaki beatmap + şarkı çalar.")]
        [SerializeField] private bool autoStartCustom;
        [SerializeField] private TextAsset beatmapJson;
        [Tooltip("Şarkı. Boşsa beatmap'in beat'lerinde tık çalan metronom kullanılır.")]
        [SerializeField] private AudioClip musicClip;

        [Space]
        [Tooltip("Şarkının üstüne beatmap'in beat'lerinde tık çal (beat takibini kulakla kontrol etmek için).")]
        [SerializeField] private bool addClickTrack;
        [SerializeField] private BeatClickTrack clickTrack;

        [Tooltip("Şarkı yokken son notadan sonra metronomun kaç beat daha çalacağı.")]
        [SerializeField, Min(0)] private int tailBeats = 8;

        private bool suspended;
        private TextAsset currentJson;
        private AudioClip currentClip;

        public Beatmap CurrentBeatmap { get; private set; }

        /// <summary>Katalogdan çalınan şarkı ve harita (özel test haritasında null).</summary>
        public SongEntry CurrentSong { get; private set; }
        public MapEntry CurrentMap { get; private set; }

        /// <summary>Bir harita yüklü (oyun ekranı).</summary>
        public bool IsLoaded => currentJson != null;
        public bool AutoStartCustom => autoStartCustom && beatmapJson != null;
        public BeatClickTrack ClickTrack => clickTrack;

        /// <summary>Şarkı (yeniden) başladı: yeni chart.</summary>
        public event System.Action<NoteChart> SongStarted;

        /// <summary>Oynanış askıya alındı ya da harita boşaltıldı (kalibrasyon / menü).</summary>
        public event System.Action SongSuspended;

        /// <summary>Kalibrasyon ekranı açıkken true: oynanış durur, Conductor başkasınındır.</summary>
        public bool IsSuspended => suspended;

        private void Awake()
        {
            if (clickTrack == null)
            {
                clickTrack = gameObject.AddComponent<BeatClickTrack>();
                clickTrack.Configure(conductor);
            }
        }

        private void Start()
        {
            if (AutoStartCustom) PlayCustom();
        }

        private void Update()
        {
            if (suspended || !IsLoaded) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        /// <summary>
        /// Uygulama arka plandan dönünce şarkı baştan başlar (prototip; gerçek pause sonra).
        /// Arka planda ses durduğu için dspTime ile şarkı pozisyonu artık güvenilir değil.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused && !suspended && IsLoaded) Restart();
        }

        /// <summary>Katalogdaki bir haritayı çalar.</summary>
        public void Play(SongEntry song, MapEntry map)
        {
            CurrentSong = song;
            CurrentMap = map;
            currentJson = map?.Beatmap;
            currentClip = song?.Clip;
            Restart();
        }

        /// <summary>Inspector'daki özel beatmap + şarkı (Editor hızlı test).</summary>
        public void PlayCustom()
        {
            CurrentSong = null;
            CurrentMap = null;
            currentJson = beatmapJson;
            currentClip = musicClip;
            Restart();
        }

        /// <summary>Oynanışı durdurur (notalar temizlenir). Tekrar başlatmak için <see cref="Restart"/>.</summary>
        public void Suspend()
        {
            suspended = true;
            StopAndClear();
            SongSuspended?.Invoke();
        }

        /// <summary>Haritayı boşaltır (menüye dönüş): müzik durur, notalar temizlenir, oturum boşta.</summary>
        public void Unload()
        {
            suspended = false;
            currentJson = null;
            currentClip = null;
            CurrentSong = null;
            CurrentMap = null;
            CurrentBeatmap = null;
            StopAndClear();
            SongSuspended?.Invoke();
        }

        private void StopAndClear()
        {
            clickTrack.Stop();
            conductor.Stop();
            judgementSystem.Load(null);
            noteSpawner.Load(null);
        }

        public void Restart()
        {
            suspended = false;
            if (currentJson == null)
            {
                Debug.LogError("[SongLoader] Çalınacak beatmap yok (şarkı listesinden seçilmeli ya da Özel beatmap atanmalı).");
                return;
            }

            CurrentBeatmap = Beatmap.FromJson(currentJson.text);
            var tempo = CurrentBeatmap.CreateTempoMap();
            conductor.SetTempoMap(tempo);

            var chart = NoteChart.FromBeatmap(CurrentBeatmap);
            judgementSystem.Load(chart);
            noteSpawner.Load(chart);

            conductor.Play(currentClip);

            // Tık: şarkı yoksa metronom olarak, varsa istenirse kontrol için.
            if (currentClip == null || addClickTrack)
            {
                int last = currentClip != null
                    ? Mathf.FloorToInt((float)tempo.TimeToBeat(currentClip.length))
                    : Mathf.CeilToInt((float)chart.LastBeat) + tailBeats;
                clickTrack.Begin(tempo, 0, last);
            }
            else
            {
                clickTrack.Stop();
            }

            SongStarted?.Invoke(chart);
        }

        /// <summary>Müziği ve oynanışı durdurur, notalar yerinde donar (FAILED / sonuç ekranı).</summary>
        public void StopPlayback()
        {
            clickTrack.Stop();
            conductor.Stop();
        }
    }
}
