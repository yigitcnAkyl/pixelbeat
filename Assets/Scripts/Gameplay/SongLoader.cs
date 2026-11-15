using PixelBeat.Audio;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Beatmap JSON'unu yükler, Conductor'ı yapılandırır, sistemlere chart'ı verir ve şarkıyı başlatır.
    /// audioFile boşsa test metronomu üretilir. R / ekrandaki R butonu: baştan başlat.
    /// </summary>
    public class SongLoader : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private TextAsset beatmapJson;
        [SerializeField] private JudgementSystem judgementSystem;
        [SerializeField] private NoteSpawner noteSpawner;

        [Tooltip("Son notadan sonra metronomun kaç beat daha çalacağı.")]
        [SerializeField, Min(0)] private int tailBeats = 8;

        private AudioClip generatedClip;
        private bool suspended;

        public Beatmap CurrentBeatmap { get; private set; }

        /// <summary>Kalibrasyon ekranı açıkken true: oynanış durur, Conductor başkasınındır.</summary>
        public bool IsSuspended => suspended;

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
            conductor.Stop();
            judgementSystem.Load(null);
            noteSpawner.Load(null);
        }

        public void Restart()
        {
            suspended = false;
            if (beatmapJson == null)
            {
                Debug.LogError("[SongLoader] Beatmap atanmamış.");
                return;
            }

            CurrentBeatmap = Beatmap.FromJson(beatmapJson.text);
            conductor.Configure(CurrentBeatmap.bpm, CurrentBeatmap.offset);

            var chart = NoteChart.FromBeatmap(CurrentBeatmap);
            judgementSystem.Load(chart);
            noteSpawner.Load(chart);

            if (!string.IsNullOrEmpty(CurrentBeatmap.audioFile))
                Debug.LogWarning($"[SongLoader] Ses dosyası yükleme henüz yok ('{CurrentBeatmap.audioFile}'), metronom çalınıyor.");

            if (generatedClip != null) Destroy(generatedClip);
            int beats = Mathf.CeilToInt((float)chart.LastBeat) + tailBeats;
            generatedClip = MetronomeClipGenerator.Create(CurrentBeatmap.bpm, beats, AudioSettings.outputSampleRate);
            conductor.Play(generatedClip);
        }

        private void OnDestroy()
        {
            if (generatedClip != null) Destroy(generatedClip);
        }
    }
}
