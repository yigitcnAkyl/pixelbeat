using System;
using UnityEngine;

namespace RhythmGame
{
    public enum NoteType
    {
        Tap,    // Standart tek vuruşluk nota
        Hold    // Belirli bir süre basılı tutulan uzun nota
    }

    public enum HitAccuracy
    {
        None,
        Perfect,
        Great,
        Good,
        Miss
    }

    /// <summary>
    /// Ritim haritasındaki tek bir notanın tüm verilerini tutan seri hale getirilebilir veri yapısı.
    /// </summary>
    [Serializable]
    public class NoteData
    {
        [Tooltip("Notanın benzersiz kimliği")]
        public int id;

        [Tooltip("Nota türü: Tap veya Hold")]
        public NoteType noteType = NoteType.Tap;

        [Range(0, 3)]
        [Tooltip("Hedef şerit indeksi (0: En Sol, 3: En Sağ)")]
        public int laneIndex;

        [Tooltip("Notanın vuruş çizgisine varması gereken müzikal vuruş (beat) zamanı")]
        public float targetBeat;

        [Tooltip("Hold notası için basılı tutulacak süre (vuruş/beat cinsinden). Tap notalarda 0'dır.")]
        public float holdDurationInBeats;

        /// <summary>
        /// Hold notasının sonlanacağı vuruş zamanı (Tap notalarda targetBeat ile aynıdır).
        /// </summary>
        public float EndBeat => noteType == NoteType.Hold ? targetBeat + holdDurationInBeats : targetBeat;

        public NoteData() { }

        public NoteData(int id, NoteType type, int lane, float beat, float holdDuration = 0f)
        {
            this.id = id;
            this.noteType = type;
            this.laneIndex = Mathf.Clamp(lane, 0, 3);
            this.targetBeat = beat;
            this.holdDurationInBeats = (type == NoteType.Hold) ? Mathf.Max(0.1f, holdDuration) : 0f;
        }

        /// <summary>
        /// Vuruş zamanının saniye cinsinden karşılığını hesaplar.
        /// </summary>
        public float GetTargetTimeInSeconds(float secondsPerBeat) => targetBeat * secondsPerBeat;

        /// <summary>
        /// Hold süresinin saniye cinsinden karşılığını hesaplar.
        /// </summary>
        public float GetHoldDurationInSeconds(float secondsPerBeat) => holdDurationInBeats * secondsPerBeat;
    }
}
