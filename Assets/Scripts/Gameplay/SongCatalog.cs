using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Şarkı kataloğu (GDD §11): şarkı listesi buradan gelir; yeni şarkı eklemek = listeye satır eklemek.
    /// Her şarkının haritaları tür (Vokal / Melodi) + zorluk (Kolay / Normal / Zor) ile tanımlanır.
    /// </summary>
    [CreateAssetMenu(fileName = "SongCatalog", menuName = "PixelBeat/Song Catalog")]
    public class SongCatalog : ScriptableObject
    {
        [SerializeField] private List<SongEntry> songs = new List<SongEntry>();

        public IReadOnlyList<SongEntry> Songs => songs;

        public SongEntry Find(string id)
        {
            foreach (var s in songs)
                if (s != null && s.Id == id) return s;
            return null;
        }

#if UNITY_EDITOR
        public void SetSongs(List<SongEntry> newSongs) => songs = newSongs;
#endif
    }

    [Serializable]
    public class SongEntry
    {
        /// <summary>Şarkı listesinde ve skor kaydında kullanılan sabit kimlik (değiştirme: rekorlar buna bağlı).</summary>
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private string artist;
        [SerializeField] private AudioClip clip;
        [SerializeField] private List<MapEntry> maps = new List<MapEntry>();

        /// <summary>Harita türleri gösterim sırası (Vokal önce, GDD §7.2 vokalli şarkılarda tercih edilen).</summary>
        public static readonly MapType[] TypeOrder = { MapType.Vocal, MapType.Melody };
        public static readonly SongDifficulty[] Difficulties = { SongDifficulty.Easy, SongDifficulty.Normal, SongDifficulty.Hard };

        public SongEntry() { }

        public SongEntry(string id, string title, string artist, AudioClip clip, List<MapEntry> maps)
        {
            this.id = id;
            this.title = title;
            this.artist = artist;
            this.clip = clip;
            this.maps = maps ?? new List<MapEntry>();
        }

        public string Id => id;
        public string Title => title;
        public string Artist => artist;
        public AudioClip Clip => clip;
        public IReadOnlyList<MapEntry> Maps => maps;
        public double DurationSec => clip != null ? clip.length : 0;

        /// <summary>Katalogda haritası olan türler (sabit sırada).</summary>
        public List<MapType> AvailableTypes()
        {
            var list = new List<MapType>();
            foreach (var t in TypeOrder)
                foreach (var m in maps)
                    if (m != null && m.Type == t && m.Beatmap != null)
                    {
                        list.Add(t);
                        break;
                    }
            return list;
        }

        public MapEntry Get(MapType type, SongDifficulty difficulty)
        {
            foreach (var m in maps)
                if (m != null && m.Type == type && m.Difficulty == difficulty && m.Beatmap != null) return m;
            return null;
        }

        public bool Has(MapType type, SongDifficulty difficulty) => Get(type, difficulty) != null;

        /// <summary>"3:42" biçiminde süre.</summary>
        public static string FormatDuration(double sec)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt((float)sec));
            return $"{s / 60}:{s % 60:00}";
        }
    }

    [Serializable]
    public class MapEntry
    {
        [SerializeField] private MapType type;
        [SerializeField] private SongDifficulty difficulty = SongDifficulty.Normal;
        [SerializeField] private TextAsset beatmap;

        public MapEntry() { }

        public MapEntry(MapType type, SongDifficulty difficulty, TextAsset beatmap)
        {
            this.type = type;
            this.difficulty = difficulty;
            this.beatmap = beatmap;
        }

        public MapType Type => type;
        public SongDifficulty Difficulty => difficulty;
        public TextAsset Beatmap => beatmap;
    }
}
