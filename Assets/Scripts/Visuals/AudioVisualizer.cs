using System;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// AudioSource üzerinden FFT spektrum verisi alarak bas (kick) ve tiz frekansları analiz eden,
    /// ritme duyarlı (Audio Reactive) arka plan ışıkları ve şerit parlama efektlerini yöneten sınıf.
    /// </summary>
    public class AudioVisualizer : MonoBehaviour
    {
        public static AudioVisualizer Instance { get; private set; }

        [Header("Spektrum Analiz Ayarları")]
        [SerializeField] private FFTWindow fftWindow = FFTWindow.BlackmanHarris;
        [SerializeField] private float bassSensitivity = 1.35f;
        [SerializeField] private float decaySpeed = 6.0f;

        [Header("Görsel Tepki Referansları")]
        [Tooltip("Ritme göre rengi ve parlaklığı titreşecek arka plan veya ışık")]
        [SerializeField] private Light backgroundLight;
        [SerializeField] private Camera targetCamera;

        [Header("Renk Paleti (Cyberpunk Işıkları)")]
        [SerializeField] private Color baseBgColor = new Color(0.04f, 0.04f, 0.09f, 1f);
        [SerializeField] private Color bassPulseColor = new Color(0.18f, 0.05f, 0.25f, 1f); // Neon Mor/Magenta vuruş

        // Spektrum Verisi (128 örnek frekans)
        private float[] spectrum = new float[128];
        private float currentBassEnergy = 0f;
        private float smoothedBassEnergy = 0f;
        private float dynamicThreshold = 0.02f;

        // Olaylar (Diğer görsel efektler dinleyebilir)
        public static event Action<float> OnBassPulse; // (intensity 0..1)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            AnalyzeAudio();
            UpdateVisuals();
        }

        private void AnalyzeAudio()
        {
            AudioSource source = Conductor.Instance != null ? Conductor.Instance.AudioSource : null;
            if (source == null || !source.isPlaying)
            {
                // Müzik çalmıyorsa enerjiyi sıfıra söndür
                smoothedBassEnergy = Mathf.Lerp(smoothedBassEnergy, 0f, Time.deltaTime * decaySpeed);
                return;
            }

            // Donanım ses spektrumunu al
            source.GetSpectrumData(spectrum, 0, fftWindow);

            // Bas frekansları (0-5 arasındaki düşük frekans bantları: kick drum ve sub-bass)
            float bassSum = 0f;
            int bassBins = 6;
            for (int i = 0; i < bassBins; i++)
            {
                bassSum += spectrum[i];
            }
            currentBassEnergy = (bassSum / bassBins) * 50f; // Görselleştirme için ölçekle

            // Dinamik eşik takibi
            dynamicThreshold = Mathf.Lerp(dynamicThreshold, currentBassEnergy, Time.deltaTime * 2f);

            // Eğer anlık bas dinamik eşiği aştıysa beat tetikle
            if (currentBassEnergy > dynamicThreshold * bassSensitivity && currentBassEnergy > 0.05f)
            {
                float intensity = Mathf.Clamp01((currentBassEnergy - dynamicThreshold) * 2f);
                smoothedBassEnergy = Mathf.Max(smoothedBassEnergy, intensity);
                OnBassPulse?.Invoke(intensity);
            }
            else
            {
                smoothedBassEnergy = Mathf.Lerp(smoothedBassEnergy, 0f, Time.deltaTime * decaySpeed);
            }
        }

        private void UpdateVisuals()
        {
            // 1. Kamera Arka Plan Rengi Titreşimi (Kick vurdukça Cyberpunk Magenta neon parlaması)
            if (targetCamera != null)
            {
                targetCamera.backgroundColor = Color.Lerp(baseBgColor, bassPulseColor, smoothedBassEnergy);
            }

            // 2. Varsa Işık Yoğunluğu
            if (backgroundLight != null)
            {
                backgroundLight.intensity = Mathf.Lerp(0.5f, 2.5f, smoothedBassEnergy);
            }
        }

        /// <summary>
        /// O anki müzik bas yoğunluğunu döner (0.0 ile 1.0 arası).
        /// </summary>
        public float GetCurrentBassIntensity()
        {
            return smoothedBassEnergy;
        }
    }
}
