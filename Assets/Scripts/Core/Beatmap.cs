using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>
    /// GDD §7 beatmap formatı. Assets/Data/Beatmaps/&lt;şarkı&gt;.json olarak tutulur.
    /// Alanlar JsonUtility için public; Inspector'da gösterilmez, sadece veri taşır.
    /// </summary>
    [Serializable]
    public class Beatmap
    {
        public string title;
        public string artist;
        [Tooltip("Boşsa test metronomu üretilir.")]
        public string audioFile;
        public float bpm;
        [Tooltip("Ses dosyasında ilk beat'in başladığı an (sn).")]
        public float offset;
        public string difficulty;
        public List<BeatmapNote> notes = new List<BeatmapNote>();

        public static Beatmap FromJson(string json)
        {
            var map = JsonUtility.FromJson<Beatmap>(json);
            if (map.notes == null) map.notes = new List<BeatmapNote>();
            return map;
        }
    }

    [Serializable]
    public class BeatmapNote
    {
        [Tooltip("Şerit 0-3")]
        public int lane;
        [Tooltip("Başlangıç (beat)")]
        public float beat;
        [Tooltip("0 = tap, > 0 = hold (beat)")]
        public float lengthBeats;
    }
}
