using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace RhythmGame
{
    /// <summary>
    /// Yerel depolamadan çalışma anında (runtime) harici .mp3, .wav ve .ogg dosyalarını
    /// UnityWebRequestMultimedia ile belleğe AudioClip olarak yükleyen ve Conductor'a aktaran yükleyici.
    /// </summary>
    public class SongLoader : MonoBehaviour
    {
        public static SongLoader Instance { get; private set; }

        [Header("Yükleme Ayarları")]
        [Tooltip("Varsayılan BPM (Yüklenen şarkının BPM değeri bilinmiyorsa)")]
        [SerializeField] private float defaultBpm = 125f;

        [Tooltip("Şarkı yüklenir yüklenmez otomatik başlasın mı")]
        [SerializeField] private bool autoPlayOnLoaded = true;

        [Header("Son Yüklenen Şarkı Bilgisi")]
        public string lastLoadedSongName = "";
        public bool isCurrentlyLoading = false;
        public float loadingProgress = 0f;

        // Olaylar (UI ve Otomatik Nota Üretimi için)
        public static event Action<AudioClip> OnSongLoaded;
        public static event Action<float> OnLoadingProgress;
        public static event Action<string> OnLoadingFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Dosya yolundan (C:/... veya D:/...) harici müzik dosyasını asenkron olarak yükler.
        /// </summary>
        public void LoadSongFromFile(string filePath, float bpm = -1f)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("[SongLoader] Dosya yolu boş olamaz!");
                OnLoadingFailed?.Invoke("Dosya yolu belirtilmedi!");
                return;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[SongLoader] Dosya bulunamadı: {filePath}");
                OnLoadingFailed?.Invoke($"Dosya bulunamadı: {filePath}");
                return;
            }

            float finalBpm = bpm > 0 ? bpm : defaultBpm;
            StartCoroutine(LoadAudioClipRoutine(filePath, finalBpm));
        }

        private IEnumerator LoadAudioClipRoutine(string filePath, float bpm)
        {
            isCurrentlyLoading = true;
            loadingProgress = 0f;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            AudioType audioType = AudioType.UNKNOWN;

            switch (extension)
            {
                case ".mp3":
                    audioType = AudioType.MPEG;
                    break;
                case ".wav":
                    audioType = AudioType.WAV;
                    break;
                case ".ogg":
                    audioType = AudioType.OGGVORBIS;
                    break;
                case ".aiff":
                case ".aif":
                    audioType = AudioType.AIFF;
                    break;
                default:
                    Debug.LogError($"[SongLoader] Desteklenmeyen ses formatı: {extension}. (.mp3, .wav, .ogg desteklenir)");
                    isCurrentlyLoading = false;
                    OnLoadingFailed?.Invoke($"Desteklenmeyen ses formatı: {extension}");
                    yield break;
            }

            // file:// protokolünü formatla
            string uri = filePath;
            if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                uri = "file:///" + uri.Replace("\\", "/");
            }

            Debug.Log($"<color=#00e5ff><b>[SongLoader]</b> Harici müzik yükleniyor: {uri}</color>");

            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
            {
                var asyncOp = uwr.SendWebRequest();

                while (!asyncOp.isDone)
                {
                    loadingProgress = uwr.downloadProgress;
                    OnLoadingProgress?.Invoke(loadingProgress);
                    yield return null;
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[SongLoader] Ses yüklenirken hata oluştu: {uwr.error}");
                    isCurrentlyLoading = false;
                    OnLoadingFailed?.Invoke(uwr.error);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr);
                if (clip == null || clip.length <= 0)
                {
                    Debug.LogError("[SongLoader] Ses verisi çözülemedi (Clip boş)!");
                    isCurrentlyLoading = false;
                    OnLoadingFailed?.Invoke("Ses verisi okunamadı.");
                    yield break;
                }

                clip.name = Path.GetFileNameWithoutExtension(filePath);
                lastLoadedSongName = clip.name;
                isCurrentlyLoading = false;
                loadingProgress = 1f;

                Debug.Log($"<color=#00ff88><b>[SongLoader]</b> Şarkı Başarıyla Yüklendi! '{clip.name}' (Süre: {clip.length:F1}s)</color>");

                // Conductor'a aktar
                if (Conductor.Instance != null)
                {
                    Conductor.Instance.SetTrack(clip, bpm);
                    if (autoPlayOnLoaded)
                    {
                        Conductor.Instance.StartSong();
                    }
                }

                OnSongLoaded?.Invoke(clip);
            }
        }
    }
}
