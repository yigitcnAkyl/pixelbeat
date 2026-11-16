using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Audio
{
    /// <summary>
    /// Tempo haritasındaki beat'lerde tık çalar. Her tık AudioSource.PlayScheduled ile örnek hassasiyetinde,
    /// Conductor'ın şarkı başlangıcına göre planlanır (birkaç yüz ms önceden, küçük bir kaynak havuzuyla).
    /// Şarkı yokken metronom olarak, şarkı varken beat takibini kulakla kontrol etmek için kullanılır.
    /// </summary>
    public class BeatClickTrack : MonoBehaviour
    {
        private const int PoolSize = 4;
        private const double LookaheadSec = 0.3;

        [SerializeField] private Conductor conductor;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField, Min(1)] private int beatsPerBar = 4;

        private AudioSource[] pool;
        private int poolIndex;
        private AudioClip accentClip;
        private AudioClip normalClip;

        private TempoMap tempo;
        private int nextBeat;
        private int lastBeat;
        private bool running;

        private void Awake()
        {
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                pool[i] = src;
            }
            int rate = AudioSettings.outputSampleRate;
            accentClip = MetronomeClipGenerator.CreateClick(true, rate);
            normalClip = MetronomeClipGenerator.CreateClick(false, rate);
        }

        public void Configure(Conductor newConductor) => conductor = newConductor;

        /// <summary>Tık ses düzeyi (ayarlardaki efekt sesi). Sonraki planlanan tıklardan itibaren geçerli.</summary>
        public float Volume
        {
            get => volume;
            set => volume = Mathf.Clamp01(value);
        }

        /// <summary>Conductor.Play'den hemen sonra çağrılır. Beat aralığı [firstBeat, lastBeat] çalınır.</summary>
        public void Begin(TempoMap map, int firstBeat, int lastBeatInclusive)
        {
            Stop();
            tempo = map;
            nextBeat = firstBeat;
            lastBeat = lastBeatInclusive;
            running = map != null;
        }

        public void Stop()
        {
            running = false;
            if (pool == null) return;
            foreach (var src in pool) src.Stop();
        }

        private void Update()
        {
            if (!running) return;
            if (conductor == null || !conductor.IsPlaying)
            {
                Stop();
                return;
            }

            double horizon = AudioSettings.dspTime + LookaheadSec;
            while (nextBeat <= lastBeat)
            {
                double songTime = tempo.BeatToTime(nextBeat);
                double dsp = conductor.DspSongStart + songTime;
                if (dsp > horizon) break;

                if (songTime >= 0 && dsp >= AudioSettings.dspTime)
                {
                    var src = pool[poolIndex];
                    poolIndex = (poolIndex + 1) % pool.Length;
                    src.clip = nextBeat % beatsPerBar == 0 ? accentClip : normalClip;
                    src.volume = volume;
                    src.PlayScheduled(dsp);
                }
                nextBeat++;
            }

            if (nextBeat > lastBeat) running = false;
        }

        private void OnDestroy()
        {
            if (accentClip != null) Destroy(accentClip);
            if (normalClip != null) Destroy(normalClip);
        }
    }
}
