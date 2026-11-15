using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.Audio
{
    /// <summary>
    /// Şarkı yokken senkron testi: Conductor'ın BPM'iyle metronom üretip çalar.
    /// R tuşu: güncel BPM ile yeniden üretip baştan başlatır.
    /// </summary>
    public class MetronomeTest : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField, Min(4)] private int beatCount = 256;

        private AudioClip clip;

        private void Start()
        {
            Restart();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Restart();
            }
        }

        private void Restart()
        {
            if (clip != null) Destroy(clip);
            clip = MetronomeClipGenerator.Create(conductor.Bpm, beatCount, AudioSettings.outputSampleRate);
            conductor.Play(clip);
        }

        private void OnDestroy()
        {
            if (clip != null) Destroy(clip);
        }
    }
}
