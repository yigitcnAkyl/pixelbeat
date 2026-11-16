using System.Collections.Generic;
using System.IO;
using PixelBeat.Core;
using PixelBeat.EditorTools.BeatmapGen.Melody;
using PixelBeat.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelBeat.EditorTools.BeatmapGen
{
    /// <summary>
    /// PixelBeat > Beatmap Üretici: GDD §7 formatında (değişken tempolu) beatmap üretir.
    /// - Ritim modu: sadece ses dosyasından (onset'ler beat ızgarasına).
    /// - Melodi modu (GDD §7.1): ses + MIDI; notalar MIDI melodisinden, beat zamanları sesten. Kolay/Normal/Zor ayrı dosyalar.
    /// </summary>
    public class BeatmapGeneratorWindow : EditorWindow
    {
        private const string OutputFolder = "Assets/Data/Beatmaps";

        private enum Mode
        {
            Ritim,
            Melodi,
        }

        [SerializeField] private Mode mode = Mode.Melodi;
        [SerializeField] private AudioClip clip;
        [SerializeField] private int difficulty = 6;
        [SerializeField] private float bpmHint;
        [SerializeField] private string songTitle = "";
        [SerializeField] private string artist = "";
        [SerializeField] private string fileName = "";

        [SerializeField] private Object midiAsset;
        [SerializeField] private float offsetMs;
        [SerializeField] private int minPitch = 48;
        [SerializeField] private bool makeEasy = true, makeNormal = true, makeHard = true;

        private string summary = "";
        private string lastPath;
        private readonly Dictionary<MelodyDifficulty, string> lastMelodyPaths = new Dictionary<MelodyDifficulty, string>();
        private Vector2 scroll;

        [MenuItem("PixelBeat/Beatmap Üretici")]
        public static void Open() => GetWindow<BeatmapGeneratorWindow>("Beatmap Üretici");

        private void OnGUI()
        {
            mode = (Mode)EditorGUILayout.EnumPopup(new GUIContent("Mod", "Melodi: MIDI melodisinden (tercih edilen). Ritim: sadece sesten, beat ızgarasına."), mode);

            EditorGUI.BeginChangeCheck();
            clip = (AudioClip)EditorGUILayout.ObjectField("Ses dosyası", clip, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck() && clip != null)
            {
                if (string.IsNullOrEmpty(fileName)) fileName = clip.name;
                if (string.IsNullOrEmpty(songTitle)) songTitle = clip.name;
            }

            bool midiOk = true;
            if (mode == Mode.Melodi)
            {
                midiAsset = EditorGUILayout.ObjectField(new GUIContent("MIDI dosyası", ".mid (ör. Basic Pitch çıktısı, kayıtla hizalı)"), midiAsset, typeof(Object), false);
                midiOk = IsMidi(midiAsset);
                if (midiAsset != null && !midiOk) EditorGUILayout.HelpBox("Seçilen dosya .mid / .midi değil.", MessageType.Warning);
                offsetMs = EditorGUILayout.FloatField(new GUIContent("Offset (ms)", "Tüm notalara eklenir (MIDI kayıtla tam hizalı değilse)."), offsetMs);
                minPitch = EditorGUILayout.IntSlider(new GUIContent("Alt perde sınırı", "Bunun altındaki MIDI notaları melodi olamaz (48 = C3)."), minPitch, 0, 127);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Zorluklar");
                    makeEasy = GUILayout.Toggle(makeEasy, "Kolay");
                    makeNormal = GUILayout.Toggle(makeNormal, "Normal");
                    makeHard = GUILayout.Toggle(makeHard, "Zor");
                }
            }
            else
            {
                difficulty = EditorGUILayout.IntSlider(new GUIContent("Zorluk (yoğunluk)", "1 = seyrek, 10 = yoğun (1/4 notalar 8'den itibaren)"), difficulty, 1, 10);
            }

            bpmHint = EditorGUILayout.FloatField(new GUIContent("BPM ipucu", "0 = otomatik. Yarım / iki kat tempo karışıklığını önler."), bpmHint);
            songTitle = EditorGUILayout.TextField("Başlık", songTitle);
            artist = EditorGUILayout.TextField("Sanatçı", artist);
            string nameTip = mode == Mode.Melodi ? $"{OutputFolder}/<ad>_easy|normal|hard.json" : $"{OutputFolder}/<ad>.json";
            fileName = EditorGUILayout.TextField(new GUIContent("Dosya adı", nameTip), fileName);

            EditorGUILayout.Space();
            bool anyDifficulty = makeEasy || makeNormal || makeHard;
            bool canGenerate = clip != null && !string.IsNullOrWhiteSpace(fileName)
                               && (mode == Mode.Ritim || (midiAsset != null && midiOk && anyDifficulty));
            using (new EditorGUI.DisabledScope(!canGenerate))
            {
                if (GUILayout.Button("Üret", GUILayout.Height(30)))
                {
                    if (mode == Mode.Melodi) GenerateMelody();
                    else GenerateRhythm();
                }
            }

            bool hasOutput = mode == Mode.Melodi ? lastMelodyPaths.Count > 0 : !string.IsNullOrEmpty(lastPath);
            using (new EditorGUI.DisabledScope(!hasOutput))
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

        private static bool IsMidi(Object asset)
        {
            if (asset == null) return false;
            string ext = Path.GetExtension(AssetDatabase.GetAssetPath(asset)).ToLowerInvariant();
            return ext == ".mid" || ext == ".midi";
        }

        // ---------- Ritim modu ----------

        private void GenerateRhythm()
        {
            try
            {
                if (!ReadSamples(clip, out var samples, out int channels, out int rate)) return;

                var settings = new GeneratorSettings { Difficulty = difficulty, BpmHint = bpmHint };
                var result = BeatmapGenerator.Generate(samples, channels, rate, settings,
                    (p, label) => EditorUtility.DisplayProgressBar("Beatmap Üretici", label, p));

                lastPath = $"{OutputFolder}/{fileName.Trim()}.json";
                var issues = Write(lastPath, ToBeatmap(result, $"Auto {difficulty}"));
                summary = BuildSummary(result, issues, null);
                Debug.Log($"[Beatmap Üretici] {lastPath}\n{summary}");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(lastPath));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------- Melodi modu ----------

        private void GenerateMelody()
        {
            try
            {
                if (!ReadSamples(clip, out var samples, out int channels, out int rate)) return;
                var beatTimes = BeatmapGenerator.AnalyzeBeatTimes(samples, channels, rate, bpmHint,
                    (p, label) => EditorUtility.DisplayProgressBar("Beatmap Üretici (melodi)", label, p * 0.8f));

                EditorUtility.DisplayProgressBar("Beatmap Üretici (melodi)", "MIDI", 0.85f);
                string midiPath = AssetDatabase.GetAssetPath(midiAsset);
                var midi = MidiReader.Read(File.ReadAllBytes(midiPath));
                var melody = MelodyExtractor.Extract(midi.Notes, new MelodyExtractSettings { MinPitch = minPitch });

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"MIDI: {midi.Notes.Count} nota → melodi hattı {melody.Count} nota");
                lastMelodyPaths.Clear();
                string first = null;

                foreach (var d in new[] { MelodyDifficulty.Easy, MelodyDifficulty.Normal, MelodyDifficulty.Hard })
                {
                    if ((d == MelodyDifficulty.Easy && !makeEasy) || (d == MelodyDifficulty.Normal && !makeNormal) ||
                        (d == MelodyDifficulty.Hard && !makeHard))
                        continue;
                    EditorUtility.DisplayProgressBar("Beatmap Üretici (melodi)", d.ToString(), 0.9f);

                    var preset = MelodyPreset.For(d);
                    var result = MelodyGenerator.Generate(melody, beatTimes, preset, offsetMs / 1000.0);
                    string path = $"{OutputFolder}/{fileName.Trim()}_{preset.Suffix}.json";
                    var issues = Write(path, ToBeatmap(result, preset.Label));
                    lastMelodyPaths[d] = path;
                    first = first ?? path;

                    sb.AppendLine();
                    sb.AppendLine($"== {preset.Label} → {path}");
                    sb.Append(BuildSummary(result, issues, preset));
                }

                summary = sb.ToString().TrimEnd();
                Debug.Log($"[Beatmap Üretici] Melodi modu\n{summary}");
                if (first != null) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(first));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------- Ortak ----------

        /// <summary>JSON'u yazar, içe aktarır ve oyunun yükleyeceği haliyle doğrular.</summary>
        private static List<string> Write(string path, Beatmap map)
        {
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllText(path, JsonUtility.ToJson(map, true));
            AssetDatabase.ImportAsset(path);
            var chart = NoteChart.FromBeatmap(Beatmap.FromJson(File.ReadAllText(path)), logWarnings: false);
            return chart.Validate();
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

        private Beatmap ToBeatmap(GeneratorResult r, string difficultyLabel)
        {
            var map = new Beatmap
            {
                title = songTitle,
                artist = artist,
                audioFile = AssetDatabase.GetAssetPath(clip).Replace("Assets/", ""),
                bpm = (float)System.Math.Round(r.MedianBpm, 2),
                offset = 0,
                difficulty = difficultyLabel,
                beatTimes = new List<double>(),
                notes = new List<BeatmapNote>(),
            };
            foreach (double t in r.BeatTimes) map.beatTimes.Add(System.Math.Round(t, 4));
            foreach (var n in r.Notes)
                map.notes.Add(new BeatmapNote { lane = n.Lane, beat = (float)n.Beat, lengthBeats = (float)n.LengthBeats });
            return map;
        }

        private static string BuildSummary(GeneratorResult r, List<string> chartIssues, MelodyPreset preset)
        {
            int taps = r.Notes.Count - r.Holds;
            double activeSec = r.Notes.Count > 1 ? r.Notes[r.Notes.Count - 1].Time - r.Notes[0].Time : 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Beat: {r.BeatTimes.Count}, tempo medyan {r.MedianBpm:0.0} BPM (aralık %5–95: {r.MinBpm:0.0}–{r.MaxBpm:0.0})");
            if (preset == null) sb.AppendLine($"Süre: {r.DurationSec:0.0} sn, onset: {r.OnsetCount}, ızgara adayı: {r.CandidateCount}");
            else sb.AppendLine($"Melodi: {r.CandidateCount} → seyreltme sonrası {r.OnsetCount} (hedef {preset.TargetNps:0.0} nota/sn)");
            sb.AppendLine($"Nota: {r.Notes.Count} (tap {taps}, hold {r.Holds}, akor {r.Chords}), " +
                          $"{(activeSec > 0 ? r.Notes.Count / activeSec : 0):0.00} nota/sn");
            if (r.Notes.Count > 0)
            {
                var firsts = new List<string>();
                for (int i = 0; i < r.Notes.Count && i < 4; i++) firsts.Add($"{r.Notes[i].Time:0.00}");
                sb.AppendLine($"İlk notalar (sn): {string.Join(", ", firsts)}");
            }
            sb.AppendLine($"Şerit dağıtımında: düşen {r.LaneStats.Dropped}, teke inen akor {r.LaneStats.DemotedChords}, tap'e inen hold {r.LaneStats.DemotedHolds}");

            var all = new List<string>(r.Warnings);
            all.AddRange(chartIssues);
            if (all.Count == 0) sb.AppendLine("Denetim: sorun yok (iki parmak + GDD §7 kuralları).");
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
            if (mode == Mode.Melodi)
            {
                Set(so, "easyBeatmap", MelodyDifficulty.Easy);
                Set(so, "normalBeatmap", MelodyDifficulty.Normal);
                Set(so, "hardBeatmap", MelodyDifficulty.Hard);
                so.FindProperty("difficulty").enumValueIndex = lastMelodyPaths.ContainsKey(MelodyDifficulty.Normal)
                    ? (int)SongDifficulty.Normal
                    : (int)SongDifficulty.Easy + (int)FirstKey();
            }
            else
            {
                so.FindProperty("beatmapJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(lastPath);
                so.FindProperty("difficulty").enumValueIndex = (int)SongDifficulty.Custom;
            }
            so.FindProperty("musicClip").objectReferenceValue = clip;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(loader.gameObject.scene);
            Debug.Log($"[Beatmap Üretici] SongLoader → {(mode == Mode.Melodi ? string.Join(", ", lastMelodyPaths.Values) : lastPath)} + {clip.name}. Sahneyi kaydetmeyi unutma.");
        }

        private void Set(SerializedObject so, string property, MelodyDifficulty d)
        {
            if (lastMelodyPaths.TryGetValue(d, out var path))
                so.FindProperty(property).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        private MelodyDifficulty FirstKey()
        {
            foreach (var d in new[] { MelodyDifficulty.Easy, MelodyDifficulty.Normal, MelodyDifficulty.Hard })
                if (lastMelodyPaths.ContainsKey(d)) return d;
            return MelodyDifficulty.Easy;
        }
    }
}
