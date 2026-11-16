using PixelBeat.Audio;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.Gameplay
{
    /// <summary>SongLoader'ın çalacağı harita: Özel (tek beatmap) ya da melodi modu zorlukları.</summary>
    public enum SongDifficulty
    {
        Custom,
        Easy,
        Normal,
        Hard,
    }

    /// <summary>
    /// Beatmap JSON'unu yükler, Conductor'ı tempo haritasıyla yapılandırır, sistemlere chart'ı verir ve şarkıyı başlatır.
    /// Çalınacak beatmap ve şarkı Inspector'dan seçilir. Şarkı yoksa beatmap'in beat'lerinde metronom tıkı çalar.
    /// R / ekrandaki R butonu: baştan başlat.
    /// </summary>
    public class SongLoader : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementSystem judgementSystem;
        [SerializeField] private NoteSpawner noteSpawner;

        [Header("Çalınacak şarkı")]
        [Tooltip("Özel: tek beatmap (ritim modu / elle yazılmış). Kolay/Normal/Zor: melodi modu haritaları.")]
        [SerializeField] private SongDifficulty difficulty = SongDifficulty.Custom;
        [SerializeField] private TextAsset beatmapJson;
        [SerializeField] private TextAsset easyBeatmap;
        [SerializeField] private TextAsset normalBeatmap;
        [SerializeField] private TextAsset hardBeatmap;
        [Tooltip("Şarkı. Boşsa beatmap'in beat'lerinde tık çalan metronom kullanılır.")]
        [SerializeField] private AudioClip musicClip;
        [Tooltip("Şarkının üstüne beatmap'in beat'lerinde tık çal (beat takibini kulakla kontrol etmek için).")]
        [SerializeField] private bool addClickTrack;
        [SerializeField] private BeatClickTrack clickTrack;

        [Tooltip("Şarkı yokken son notadan sonra metronomun kaç beat daha çalacağı.")]
        [SerializeField, Min(0)] private int tailBeats = 8;

        private bool suspended;

        public Beatmap CurrentBeatmap { get; private set; }

        public SongDifficulty Difficulty => difficulty;

        private TextAsset SelectedBeatmap
        {
            get
            {
                switch (difficulty)
                {
                    case SongDifficulty.Easy: return easyBeatmap;
                    case SongDifficulty.Normal: return normalBeatmap;
                    case SongDifficulty.Hard: return hardBeatmap;
                    default: return beatmapJson;
                }
            }
        }

        /// <summary>Şarkı (yeniden) başladı: yeni chart.</summary>
        public event System.Action<NoteChart> SongStarted;

        /// <summary>Oynanış askıya alındı (kalibrasyon).</summary>
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
            Restart();
        }

        private void Update()
        {
            if (suspended) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        /// <summary>
        /// Uygulama arka plandan dönünce şarkı baştan başlar (prototip; gerçek pause sonra).
        /// Arka planda ses durduğu için dspTime ile şarkı pozisyonu artık güvenilir değil.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused && !suspended) Restart();
        }

        /// <summary>Oynanışı durdurur (notalar temizlenir). Tekrar başlatmak için <see cref="Restart"/>.</summary>
        public void Suspend()
        {
            suspended = true;
            clickTrack.Stop();
            conductor.Stop();
            judgementSystem.Load(null);
            noteSpawner.Load(null);
            SongSuspended?.Invoke();
        }

        public void Restart()
        {
            suspended = false;
            var json = SelectedBeatmap;
            if (json == null)
            {
                Debug.LogError($"[SongLoader] {difficulty} beatmap'i atanmamış.");
                return;
            }

            CurrentBeatmap = Beatmap.FromJson(json.text);
            var tempo = CurrentBeatmap.CreateTempoMap();
            conductor.SetTempoMap(tempo);

            var chart = NoteChart.FromBeatmap(CurrentBeatmap);
            judgementSystem.Load(chart);
            noteSpawner.Load(chart);

            conductor.Play(musicClip);

            // Tık: şarkı yoksa metronom olarak, varsa istenirse kontrol için.
            if (musicClip == null || addClickTrack)
            {
                int last = musicClip != null
                    ? Mathf.FloorToInt((float)tempo.TimeToBeat(musicClip.length))
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
