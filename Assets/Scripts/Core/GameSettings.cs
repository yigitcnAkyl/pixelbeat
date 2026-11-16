using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>Ayar deposu (PlayerPrefs ya da testte bellek).</summary>
    public interface ISettingsStore
    {
        bool Has(string key);
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        void Save();
    }

    public sealed class PlayerPrefsStore : ISettingsStore
    {
        public bool Has(string key) => PlayerPrefs.HasKey(key);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>
    /// Oyuncu ayarları (GDD §11): nota hızı, müzik sesi, efekt sesi. Değerler okunurken ve yazılırken sınırlara kırpılır.
    /// Ses offset'i Conductor'da, CRT CrtOverlay'de tutulur (mevcut anahtarlarıyla).
    /// </summary>
    public sealed class GameSettings
    {
        public const string ScrollKey = "noteScrollSec";
        public const string MusicKey = "musicVolume";
        public const string SfxKey = "sfxVolume";

        /// <summary>Nota hızı: notanın ekranın üstünden vuruş çizgisine gelme süresi (sn). Küçük = hızlı.</summary>
        public const float MinScrollSec = 0.4f, MaxScrollSec = 2.0f, DefaultScrollSec = 1.0f;
        public const float DefaultMusicVolume = 0.8f, DefaultSfxVolume = 0.7f;

        private float scrollSec = DefaultScrollSec;
        private float music = DefaultMusicVolume;
        private float sfx = DefaultSfxVolume;

        public float NoteScrollSec
        {
            get => scrollSec;
            set => scrollSec = ClampScroll(value);
        }

        public float MusicVolume
        {
            get => music;
            set => music = Clamp01(value);
        }

        public float SfxVolume
        {
            get => sfx;
            set => sfx = Clamp01(value);
        }

        public static float ClampScroll(float v) => float.IsNaN(v) ? DefaultScrollSec : Mathf.Clamp(v, MinScrollSec, MaxScrollSec);
        private static float Clamp01(float v) => float.IsNaN(v) ? 1f : Mathf.Clamp01(v);

        public static GameSettings Load(ISettingsStore store) => new GameSettings
        {
            NoteScrollSec = store.GetFloat(ScrollKey, DefaultScrollSec),
            MusicVolume = store.GetFloat(MusicKey, DefaultMusicVolume),
            SfxVolume = store.GetFloat(SfxKey, DefaultSfxVolume),
        };

        public void Save(ISettingsStore store)
        {
            store.SetFloat(ScrollKey, scrollSec);
            store.SetFloat(MusicKey, music);
            store.SetFloat(SfxKey, sfx);
            store.Save();
        }
    }
}
