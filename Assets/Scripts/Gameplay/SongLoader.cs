using PixelBeat.Audio;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Beatmap JSON'unu yükler, Conductor'ı yapılandırır, sistemlere chart'ı verir ve şarkıyı başlatır.
    /// audioFile boşsa test metronomu üretilir. R: baştan başlat.
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

        public Beatmap CurrentBeatmap { get; private set; }

        private void Start()
        {
            Restart();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        public void Restart()
        {
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
