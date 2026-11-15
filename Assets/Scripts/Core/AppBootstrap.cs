using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>Sahne yüklenmeden önce uygulama geneli ayarlar (GDD §8).</summary>
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            QualitySettings.vSyncCount = 0; // targetFrameRate'in geçerli olması için
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep; // oynarken ekran kararmasın
        }
    }
}
