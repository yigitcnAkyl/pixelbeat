using System;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Ritim oyununun ana zaman yöneticisi (Master Audio Clock).
    /// AudioSettings.dspTime ve AudioSource.timeSamples donanım sayacını kullanarak
    /// donanım seviyesinde gecikmesiz, kaymasız zaman takibi sağlar.
    /// </summary>
    public class Conductor : MonoBehaviour
    {
        public static Conductor Instance { get; private set; }

        [Header("Şarkı & Ses Ayarları")]
        [Tooltip("Şarkıyı çalacak AudioSource")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Şarkının temposu (Beats Per Minute)")]
        [SerializeField] private float bpm = 120f;

        [Tooltip("Ses gecikmesi veya şarkı başındaki sessizlik toleransı (saniye)")]
        [SerializeField] private float audioOffset = 0f;

        [Tooltip("Oyun başladığında şarkının çalmaya başlaması için bekleme süresi (saniye)")]
        [SerializeField] private float songStartDelay = 1.0f;

        [Tooltip("Sahne açıldığında otomatik başlasın mı?")]
        [SerializeField] private bool playOnStart = false;

        [Header("Yaklaşma / Görünürlük Ayarları")]
        [Tooltip("Bir notanın vuruş çizgisine varmadan kaç beat önce görünür olacağı (Approach Rate)")]
        [SerializeField] private float beatsShownInAdvance = 2.5f;

        // Zamanlama Değişkenleri
        private double dspSongStartTime;
        private double pauseDspTime;
        private bool isPlaying = false;
        private bool isPaused = false;
        private int lastReportedBeat = -1;

        // Döngü takibi
        private int lastTimeSamples = 0;
        private int completedLoops = 0;

        // Public Özellikler (Properties)
        public float Bpm => bpm;
        public float SecondsPerBeat => 60f / bpm;
        public float BeatsShownInAdvance => beatsShownInAdvance;
        public bool IsPlaying => isPlaying && !isPaused;
        public AudioSource AudioSource => audioSource;

        public float SongPositionInSeconds { get; private set; }
        public float SongPositionInBeats { get; private set; }

        // Eventler
        public event Action<int> OnBeat;
        public event Action OnSongStarted;
        public event Action OnSongCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.playOnAwake = false;

            if (beatsShownInAdvance > 3.0f || beatsShownInAdvance < 1.5f)
            {
                beatsShownInAdvance = 2.5f;
            }

#if UNITY_EDITOR
            if (audioSource.clip == null)
            {
                AudioClip localClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rings-of-saturn.wav");
                if (localClip != null)
                {
                    audioSource.clip = localClip;
                    audioSource.loop = true;
                    bpm = 120f;
                }
            }
#endif
        }

        private void Start()
        {
            if (MainMenuManager.Instance != null && MainMenuManager.Instance.IsMenuOpen)
            {
                StopSong();
                return;
            }

            if (playOnStart)
            {
                StartSongWithAutoChart();
            }
        }

        private void Update()
        {
            if (!isPlaying || isPaused) return;

            double currentDsp = AudioSettings.dspTime;
            double dspElapsed = currentDsp - dspSongStartTime;

            if (dspElapsed < 0.0)
            {
                SongPositionInSeconds = (float)(dspElapsed - audioOffset);
            }
            else if (audioSource != null && audioSource.clip != null && audioSource.isPlaying)
            {
                int currentSamples = audioSource.timeSamples;
                int frequency = audioSource.clip.frequency;
                double clipLen = audioSource.clip.length;

                // Loop başa sardıysa tespit et
                if (currentSamples < lastTimeSamples - (frequency / 2))
                {
                    completedLoops++;
                }
                lastTimeSamples = currentSamples;

                double exactHardwareTime = (completedLoops * clipLen) + ((double)currentSamples / frequency);

                double drift = exactHardwareTime - dspElapsed;
                if (Math.Abs(drift) > 0.015)
                {
                    dspSongStartTime = currentDsp - exactHardwareTime;
                    dspElapsed = exactHardwareTime;
                }
                else
                {
                    dspSongStartTime -= drift * 0.12;
                    dspElapsed = currentDsp - dspSongStartTime;
                }

                SongPositionInSeconds = (float)(dspElapsed - audioOffset);
            }
            else
            {
                SongPositionInSeconds = (float)(dspElapsed - audioOffset);
            }

            SongPositionInBeats = SongPositionInSeconds / SecondsPerBeat;

            int currentBeat = Mathf.FloorToInt(SongPositionInBeats);
            if (currentBeat > lastReportedBeat)
            {
                lastReportedBeat = currentBeat;
                OnBeat?.Invoke(currentBeat);
            }

            if (audioSource.clip != null && !audioSource.loop && SongPositionInSeconds >= audioSource.clip.length + 1f)
            {
                StopSong();
                OnSongCompleted?.Invoke();
            }
        }

        public void StartSongWithAutoChart()
        {
            if (audioSource != null && audioSource.clip != null)
            {
                BeatDetector detector = BeatDetector.Instance;
                if (detector == null)
                {
                    detector = FindFirstObjectByType<BeatDetector>();
                    if (detector == null)
                    {
                        detector = gameObject.AddComponent<BeatDetector>();
                    }
                }
                detector.GenerateChartFromClip(audioSource.clip);
            }
            StartSong();
        }

        public void StartSong()
        {
            StopSong();

            lastTimeSamples = 0;
            completedLoops = 0;
            dspSongStartTime = AudioSettings.dspTime + songStartDelay;
            SongPositionInSeconds = -songStartDelay;
            SongPositionInBeats = SongPositionInSeconds / SecondsPerBeat;

            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.PlayScheduled(dspSongStartTime);
            }

            isPlaying = true;
            isPaused = false;
            lastReportedBeat = -1;

            OnSongStarted?.Invoke();
        }

        public void PauseSong()
        {
            if (!isPlaying || isPaused) return;

            if (audioSource != null) audioSource.Pause();
            pauseDspTime = AudioSettings.dspTime;
            isPaused = true;
        }

        public void ResumeSong()
        {
            if (!isPlaying || !isPaused) return;

            double pauseDuration = AudioSettings.dspTime - pauseDspTime;
            dspSongStartTime += pauseDuration;

            if (audioSource != null) audioSource.UnPause();
            isPaused = false;
        }

        public void StopSong()
        {
            isPlaying = false;
            isPaused = false;
            lastTimeSamples = 0;
            completedLoops = 0;
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }

        public void SetExactBpm(float exactBpm)
        {
            if (exactBpm > 30f && exactBpm < 300f)
            {
                this.bpm = exactBpm;
            }
        }

        public void SetTrack(AudioClip clip, float newBpm, float offset = 0f)
        {
            if (audioSource != null) audioSource.clip = clip;
            this.bpm = newBpm;
            this.audioOffset = offset;
        }
    }
}
