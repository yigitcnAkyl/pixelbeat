using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>
    /// GDD §7 beatmap formatı. Assets/Data/Beatmaps/&lt;şarkı&gt;.json olarak tutulur.
    /// Alanlar JsonUtility için public; Inspector'da gösterilmez, sadece veri taşır.
    /// Tempo: <see cref="beatTimes"/> doluysa (değişken tempo) beat zamanları oradan, değilse bpm + offset'ten.
    /// </summary>
    [Serializable]
    public class Beatmap
    {
        public string title;
        public string artist;
        [Tooltip("Bilgi amaçlı (ör. Audio/Music/x.mp3). Çalınacak klip SongLoader'da seçilir.")]
        public string audioFile;
        [Tooltip("Sabit tempoda BPM; beatTimes varsa ortalama BPM (bilgi).")]
        public float bpm;
        [Tooltip("Sabit tempoda ses dosyasında ilk beat'in başladığı an (sn). beatTimes varsa kullanılmaz.")]
        public float offset;
        public string difficulty;
        [Tooltip("Değişken tempo: her tam beat'in ses dosyasındaki zamanı (sn), kesin artan. Boşsa bpm + offset.")]
        public List<double> beatTimes = new List<double>();
        public List<BeatmapNote> notes = new List<BeatmapNote>();

        public bool HasBeatTimes => beatTimes != null && beatTimes.Count >= 2;

        public TempoMap CreateTempoMap() =>
            HasBeatTimes ? TempoMap.FromBeatTimes(beatTimes) : TempoMap.Constant(bpm, offset);

        public static Beatmap FromJson(string json)
        {
            var map = JsonUtility.FromJson<Beatmap>(json);
            if (map.notes == null) map.notes = new List<BeatmapNote>();
            if (map.beatTimes == null) map.beatTimes = new List<double>();
            return map;
        }
    }

    [Serializable]
    public class BeatmapNote
    {
        [Tooltip("Şerit 0-3")]
        public int lane;
        [Tooltip("Başlangıç (beat). Kesirli olabilir: 12.5 = 1/2, 12.25 = 1/4.")]
        public float beat;
        [Tooltip("0 = tap, > 0 = hold (beat)")]
        public float lengthBeats;
    }
}
