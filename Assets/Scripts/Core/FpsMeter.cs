using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>
    /// Development build'de (cihazda) kare süresi istatistiği: her <see cref="WindowSec"/> sn'de bir loga yazar
    /// (Android'de logcat'ten okunur: "[FPS] ..."). Editor'de ve release build'de çalışmaz.
    /// </summary>
    public class FpsMeter : MonoBehaviour
    {
        private const float WindowSec = 5f;
        private const float SlowFrameMs = 1000f / 60f + 1.5f; // 60 FPS bütçesi + titreme payı

        private float elapsed;
        private int frames;
        private int slowFrames;
        private float worstMs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Application.isEditor || !Debug.isDebugBuild) return;
            var go = new GameObject("FPS Meter");
            DontDestroyOnLoad(go);
            go.AddComponent<FpsMeter>();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float ms = dt * 1000f;
            elapsed += dt;
            frames++;
            if (ms > SlowFrameMs) slowFrames++;
            if (ms > worstMs) worstMs = ms;

            if (elapsed < WindowSec) return;
            Debug.Log($"[FPS] ort={frames / elapsed:0.0} en_kotu_kare={worstMs:0.0}ms yavas_kare={slowFrames}/{frames} " +
                      $"hedef={Application.targetFrameRate} ekran={Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRateRatio.value:0}Hz");
            elapsed = 0f;
            frames = 0;
            slowFrames = 0;
            worstMs = 0f;
        }
    }
}
