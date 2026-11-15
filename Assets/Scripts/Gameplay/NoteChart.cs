using System.Collections.Generic;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    public enum NoteState
    {
        Pending,
        Hit,
        Missed
    }

    /// <summary>Oyun sırasında bir notanın durumu.</summary>
    public class ChartNote
    {
        public int Lane { get; }
        public double Beat { get; }
        public double TimeSec { get; }
        public NoteState State { get; set; }
        public Judgement Result { get; set; }

        public ChartNote(int lane, double beat, double timeSec)
        {
            Lane = lane;
            Beat = beat;
            TimeSec = timeSec;
            State = NoteState.Pending;
        }
    }

    /// <summary>Beatmap'ten üretilen, zamana göre sıralı nota listesi.</summary>
    public class NoteChart
    {
        public const int LaneCount = 4;

        private readonly List<ChartNote> notes = new List<ChartNote>();
        private readonly List<ChartNote>[] byLane = new List<ChartNote>[LaneCount];

        public IReadOnlyList<ChartNote> Notes => notes;
        public double LastBeat { get; private set; }

        public IReadOnlyList<ChartNote> LaneNotes(int lane) => byLane[lane];

        public static NoteChart FromBeatmap(Beatmap map)
        {
            var chart = new NoteChart();
            for (int i = 0; i < LaneCount; i++) chart.byLane[i] = new List<ChartNote>();

            double secPerBeat = 60.0 / map.bpm;
            foreach (var n in map.notes)
            {
                if (n.lane < 0 || n.lane >= LaneCount)
                {
                    Debug.LogWarning($"[NoteChart] Geçersiz şerit {n.lane} (beat {n.beat}), atlandı.");
                    continue;
                }

                // Hold notalar (lengthBeats > 0) adım 3'e kadar tap gibi işlenir.
                var note = new ChartNote(n.lane, n.beat, n.beat * secPerBeat);
                chart.notes.Add(note);
                chart.byLane[n.lane].Add(note);
                if (n.beat + n.lengthBeats > chart.LastBeat) chart.LastBeat = n.beat + n.lengthBeats;
            }

            chart.notes.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));
            foreach (var list in chart.byLane) list.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));
            return chart;
        }
    }
}
