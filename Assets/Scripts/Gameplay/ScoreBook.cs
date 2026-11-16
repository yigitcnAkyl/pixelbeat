using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>Bir harita (şarkı + tür + zorluk) için en iyi sonuç.</summary>
    [Serializable]
    public class ScoreRecord
    {
        public string key;
        public int score;
        public Grade grade = Grade.D;
        public int maxCombo;
        public bool fullCombo;
        public bool allPerfect;
    }

    /// <summary>
    /// Skor kaydı (GDD §11): şarkı + harita türü + zorluk başına en yüksek skor, derece, max combo, FC/AP.
    /// JSON dosyası (persistentDataPath/scores.json). Bozuk/eksik dosya boş kayıt olarak açılır.
    /// Kurallar: daha yüksek skor → skor ve derece güncellenir (YENİ REKOR); max combo en iyisi tutulur;
    /// FC / AP bir kez kazanılınca kalır. Fail edilen oyunlar kaydedilmez (çağıran taraf).
    /// </summary>
    public sealed class ScoreBook
    {
        public const string FileName = "scores.json";

        [Serializable]
        private class Data
        {
            public List<ScoreRecord> records = new List<ScoreRecord>();
        }

        private readonly Dictionary<string, ScoreRecord> records = new Dictionary<string, ScoreRecord>();

        public int Count => records.Count;

        public static string Key(string songId, MapType type, SongDifficulty difficulty) => $"{songId}|{type}|{difficulty}";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public ScoreRecord Get(string key) => records.TryGetValue(key, out var r) ? r : null;

        public ScoreRecord Get(string songId, MapType type, SongDifficulty difficulty) => Get(Key(songId, type, difficulty));

        /// <summary>Sonucu işler. Döner: yeni rekor mu (ilk kayıt ya da daha yüksek skor).</summary>
        public bool Submit(string key, int score, Grade grade, int maxCombo, bool fullCombo, bool allPerfect)
        {
            if (!records.TryGetValue(key, out var r))
            {
                records[key] = new ScoreRecord
                {
                    key = key, score = score, grade = grade, maxCombo = maxCombo, fullCombo = fullCombo, allPerfect = allPerfect,
                };
                return true;
            }

            bool record = score > r.score;
            if (record)
            {
                r.score = score;
                r.grade = grade;
            }
            r.maxCombo = Math.Max(r.maxCombo, maxCombo);
            r.fullCombo |= fullCombo;
            r.allPerfect |= allPerfect;
            return record;
        }

        /// <summary>Şarkının tüm haritaları arasında en iyi derece (S en iyi); kayıt yoksa null.</summary>
        public Grade? BestGrade(string songId)
        {
            Grade? best = null;
            string prefix = songId + "|";
            foreach (var kv in records)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (best == null || kv.Value.grade < best.Value) best = kv.Value.grade;
            }
            return best;
        }

        public string ToJson()
        {
            var data = new Data();
            var keys = new List<string>(records.Keys);
            keys.Sort(StringComparer.Ordinal); // belirleyici dosya
            foreach (var k in keys) data.records.Add(records[k]);
            return JsonUtility.ToJson(data, true);
        }

        public static ScoreBook FromJson(string json)
        {
            var book = new ScoreBook();
            if (string.IsNullOrWhiteSpace(json)) return book;
            try
            {
                var data = JsonUtility.FromJson<Data>(json);
                if (data?.records == null) return book;
                foreach (var r in data.records)
                    if (r != null && !string.IsNullOrEmpty(r.key)) book.records[r.key] = r;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScoreBook] Skor dosyası okunamadı, boş kayıtla devam: {e.Message}");
            }
            return book;
        }

        public static ScoreBook Load(string path)
        {
            try
            {
                return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new ScoreBook();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScoreBook] {path} okunamadı: {e.Message}");
                return new ScoreBook();
            }
        }

        /// <summary>Önce geçici dosyaya yazar, sonra yerine koyar (yarım kalan yazım kaydı bozmasın).</summary>
        public void Save(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, ToJson());
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ScoreBook] {path} yazılamadı: {e.Message}");
            }
        }
    }
}
