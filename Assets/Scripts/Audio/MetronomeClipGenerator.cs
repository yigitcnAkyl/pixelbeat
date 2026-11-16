using System;
using UnityEngine;

namespace PixelBeat.Audio
{
    /// <summary>Test için kodla metronom (tık) AudioClip'i üretir.</summary>
    public static class MetronomeClipGenerator
    {
        private const float ClickDurationSec = 0.03f;
        private const float AccentFrequency = 1500f;
        private const float NormalFrequency = 1000f;
        private const float Volume = 0.6f;

        /// <summary>Tek bir tık sesi (planlanarak çalınmak için).</summary>
        public static AudioClip CreateClick(bool accent, int sampleRate, float volume = Volume)
        {
            int clickSamples = Mathf.CeilToInt(ClickDurationSec * sampleRate);
            var data = new float[clickSamples];
            float freq = accent ? AccentFrequency : NormalFrequency;
            for (int i = 0; i < clickSamples; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t / (ClickDurationSec * 0.25f)) * volume;
            }
            var clip = AudioClip.Create(accent ? "Click_Accent" : "Click", clickSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <param name="beatsPerBar">Her ölçünün ilk beat'i vurgulu (daha tiz) çalınır.</param>
        public static AudioClip Create(float bpm, int beats, int sampleRate, int beatsPerBar = 4)
        {
            double secPerBeat = 60.0 / bpm;
            // Round: döngüde çalan klipte kayan nokta hatasından fazladan 1 örnek eklenip beat'ler kaymasın.
            int totalSamples = (int)Math.Round(beats * secPerBeat * sampleRate);
            int clickSamples = Mathf.CeilToInt(ClickDurationSec * sampleRate);
            var data = new float[totalSamples];

            for (int beat = 0; beat < beats; beat++)
            {
                // Her beat'in başlangıcı mutlak olarak hesaplanır, yuvarlama hatası birikmez.
                int start = (int)Math.Round(beat * secPerBeat * sampleRate);
                float freq = beat % beatsPerBar == 0 ? AccentFrequency : NormalFrequency;

                for (int i = 0; i < clickSamples && start + i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float envelope = Mathf.Exp(-t / (ClickDurationSec * 0.25f));
                    data[start + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * Volume;
                }
            }

            var clip = AudioClip.Create($"Metronome_{bpm}bpm", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
