using System.Collections.Generic;
using System.IO;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelBeat.EditorTools.BeatmapGen
{
    /// <summary>PixelBeat > Beatmap Üretici: ses dosyasından GDD §7 formatında (değişken tempolu) beatmap üretir.</summary>
    public class BeatmapGeneratorWindow : EditorWindow
    {
        private const string OutputFolder = "Assets/Data/Beatmaps";

        [SerializeField] private AudioClip clip;
        [SerializeField] private int difficulty = 6;
        [SerializeField] private float bpmHint;
        [SerializeField] private string songTitle = "";
        [SerializeField] private string artist = "";
        [SerializeField] private string fileName = "";

        private string summary = "";
        private string lastPath;
        private Vector2 scroll;

        [MenuItem("PixelBeat/Beatmap Üretici")]
        public static void Open() => GetWindow<BeatmapGeneratorWindow>("Beatmap Üretici");

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            clip = (AudioClip)EditorGUILayout.ObjectField("Ses dosyası", clip, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck() && clip != null)
            {
                if (string.IsNullOrEmpty(fileName)) fileName = clip.name;
                if (string.IsNullOrEmpty(songTitle)) songTitle = clip.name;
            }

            difficulty = EditorGUILayout.IntSlider(new GUIContent("Zorluk (yoğunluk)", "1 = seyrek, 10 = yoğun (1/4 notalar 8'den itibaren)"), difficulty, 1, 10);
            bpmHint = EditorGUILayout.FloatField(new GUIContent("BPM ipucu", "0 = otomatik. Yarım / iki kat tempo karışıklığını önler."), bpmHint);
            songTitle = EditorGUILayout.TextField("Başlık", songTitle);
            artist = EditorGUILayout.TextField("Sanatçı", artist);
            fileName = EditorGUILayout.TextField(new GUIContent("Dosya adı", $"{OutputFolder}/<ad>.json"), fileName);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(clip == null || string.IsNullOrWhiteSpace(fileName)))
            {
                if (GUILayout.Button("Üret", GUILayout.Height(30))) Generate();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastPath)))
            {
                if (GUILayout.Button("Sahneye ata (SongLoader: beatmap + şarkı)")) AssignToScene();
            }

            if (!string.IsNullOrEmpty(summary))
            {
                EditorGUILayout.Space();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.HelpBox(summary, MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
        }

        private void Generate()
        {
            try
            {
                if (!ReadSamples(clip, out var samples, out int channels, out int rate)) return;

                var settings = new GeneratorSettings { Difficulty = difficulty, BpmHint = bpmHint };
                var result = BeatmapGenerator.Generate(samples, channels, rate, settings,
                    (p, label) => EditorUtility.DisplayProgressBar("Beatmap Üretici", label, p));

                var map = ToBeatmap(result);
                Directory.CreateDirectory(OutputFolder);
                lastPath = $"{OutputFolder}/{fileName.Trim()}.json";
                File.WriteAllText(lastPath, JsonUtility.ToJson(map, true));
                AssetDatabase.ImportAsset(lastPath);

                // Oyunun yükleyeceği haliyle de doğrula (aynı şeritte hold sürerken nota vb.).
                var chart = NoteChart.FromBeatmap(Beatmap.FromJson(File.ReadAllText(lastPath)), logWarnings: false);
                var issues = chart.Validate();

                summary = BuildSummary(result, issues);
                Debug.Log($"[Beatmap Üretici] {lastPath}\n{summary}");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(lastPath));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Klibin örneklerini okur; gerekirse Load Type'ı Decompress On Load yapar (GetData için gerekli).</summary>
        private static bool ReadSamples(AudioClip audioClip, out float[] samples, out int channels, out int rate)
        {
            samples = null;
            channels = audioClip.channels;
            rate = audioClip.frequency;

            string path = AssetDatabase.GetAssetPath(audioClip);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var s = importer.defaultSampleSettings;
                if (s.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = s;
                    importer.SaveAndReimport();
                    Debug.Log($"[Beatmap Üretici] {path}: Load Type = Decompress On Load yapıldı (örnek okumak için).");
                    audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    channels = audioClip.channels;
                    rate = audioClip.frequency;
                }
            }

            audioClip.LoadAudioData();
            samples = new float[audioClip.samples * audioClip.channels];
            if (!audioClip.GetData(samples, 0))
            {
                EditorUtility.DisplayDialog("Beatmap Üretici", "Ses verisi okunamadı (Load Type = Decompress On Load olmalı).", "Tamam");
                return false;
            }
            return true;
        }

        private Beatmap ToBeatmap(GeneratorResult r)
        {
            var map = new Beatmap
            {
                title = songTitle,
                artist = artist,
                audioFile = AssetDatabase.GetAssetPath(clip).Replace("Assets/", ""),
                bpm = (float)System.Math.Round(r.MedianBpm, 2),
                offset = 0,
                difficulty = $"Auto {difficulty}",
                beatTimes = new List<double>(),
                notes = new List<BeatmapNote>(),
            };
            foreach (double t in r.BeatTimes) map.beatTimes.Add(System.Math.Round(t, 4));
            foreach (var n in r.Notes)
                map.notes.Add(new BeatmapNote { lane = n.Lane, beat = (float)n.Beat, lengthBeats = (float)n.LengthBeats });
            return map;
        }

        private static string BuildSummary(GeneratorResult r, List<string> chartIssues)
        {
            int taps = r.Notes.Count - r.Holds;
            double activeSec = r.Notes.Count > 1 ? r.Notes[r.Notes.Count - 1].Time - r.Notes[0].Time : 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Süre: {r.DurationSec:0.0} sn, beat: {r.BeatTimes.Count}");
            sb.AppendLine($"Tempo: medyan {r.MedianBpm:0.0} BPM (aralık %5–95: {r.MinBpm:0.0}–{r.MaxBpm:0.0})");
            sb.AppendLine($"Onset: {r.OnsetCount}, ızgara adayı: {r.CandidateCount}");
            sb.AppendLine($"Nota: {r.Notes.Count} (tap {taps}, hold {r.Holds}, akor {r.Chords}), " +
                          $"{(activeSec > 0 ? r.Notes.Count / activeSec : 0):0.00} nota/sn");
            sb.AppendLine($"Şerit dağıtımında: düşen {r.LaneStats.Dropped}, teke inen akor {r.LaneStats.DemotedChords}, tap'e inen hold {r.LaneStats.DemotedHolds}");

            var all = new List<string>(r.Warnings);
            all.AddRange(chartIssues);
            if (all.Count == 0) sb.Append("Denetim: sorun yok (iki parmak + GDD §7 kuralları).");
            else
            {
                sb.AppendLine($"Denetim: {all.Count} sorun");
                for (int i = 0; i < all.Count && i < 20; i++) sb.AppendLine(" - " + all[i]);
            }
            return sb.ToString();
        }

        private void AssignToScene()
        {
            var loader = FindFirstObjectByType<SongLoader>();
            if (loader == null)
            {
                EditorUtility.DisplayDialog("Beatmap Üretici", "Açık sahnede SongLoader yok.", "Tamam");
                return;
            }

            var so = new SerializedObject(loader);
            so.FindProperty("beatmapJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(lastPath);
            so.FindProperty("musicClip").objectReferenceValue = clip;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(loader.gameObject.scene);
            Debug.Log($"[Beatmap Üretici] SongLoader → {lastPath} + {clip.name}. Sahneyi kaydetmeyi unutma.");
        }
    }
}
