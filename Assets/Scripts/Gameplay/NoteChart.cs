using System.Collections.Generic;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    public enum NoteState
    {
        /// <summary>Henüz basılmadı.</summary>
        Pending,
        /// <summary>Hold: başı basıldı, tutuluyor.</summary>
        Holding,
        /// <summary>Tamamlandı (tap vuruldu / hold bırakışı değerlendirildi).</summary>
        Hit,
        /// <summary>Kaçırıldı ya da hold kırıldı. Gri olarak akmaya devam eder.</summary>
        Missed
    }

    /// <summary>Oyun sırasında bir notanın durumu.</summary>
    public class ChartNote
    {
        public int Lane { get; }
        public double Beat { get; }
        public double LengthBeats { get; }
        public double TimeSec { get; }
        /// <summary>Hold kuyruğunun sonu (sn). Tap için TimeSec.</summary>
        public double EndTimeSec { get; }
        public bool IsHold => LengthBeats > 0;

        public NoteState State { get; set; }
        /// <summary>Basış sonucu (tap'in tek sonucu).</summary>
        public Judgement Result { get; set; }
        /// <summary>Hold bırakış sonucu.</summary>
        public Judgement ReleaseResult { get; set; }

        /// <summary>
        /// Hold gövdesinin bu şarkı zamanına kadarki kısmı "yendi" (çizilmez). Basılmadıysa TimeSec;
        /// tutulurken şimdiki zaman; bırakıldı/kırıldıysa bırakış anı.
        /// </summary>
        public double HeldUntilSec { get; set; }

        public ChartNote(int lane, double beat, double lengthBeats, TempoMap tempo)
        {
            Lane = lane;
            Beat = beat;
            LengthBeats = lengthBeats > 0 ? lengthBeats : 0;
            TimeSec = tempo.BeatToTime(beat);
            EndTimeSec = tempo.BeatToTime(beat + LengthBeats);
            HeldUntilSec = TimeSec;
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

        /// <summary>Haritanın beat ↔ zaman dönüşümü (sabit ya da değişken tempo).</summary>
        public TempoMap Tempo { get; private set; }

        public IReadOnlyList<ChartNote> LaneNotes(int lane) => byLane[lane];

        public static NoteChart FromBeatmap(Beatmap map, bool logWarnings = true)
        {
            var chart = new NoteChart();
            for (int i = 0; i < LaneCount; i++) chart.byLane[i] = new List<ChartNote>();

            var tempo = map.CreateTempoMap();
            chart.Tempo = tempo;
            foreach (var n in map.notes)
            {
                if (n.lane < 0 || n.lane >= LaneCount)
                {
                    if (logWarnings) Debug.LogWarning($"[NoteChart] Geçersiz şerit {n.lane} (beat {n.beat}), atlandı.");
                    continue;
                }

                var note = new ChartNote(n.lane, n.beat, n.lengthBeats, tempo);
                chart.notes.Add(note);
                chart.byLane[n.lane].Add(note);
                if (n.beat + note.LengthBeats > chart.LastBeat) chart.LastBeat = n.beat + note.LengthBeats;
            }

            chart.notes.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));
            foreach (var list in chart.byLane) list.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));

            if (logWarnings)
                foreach (var issue in chart.Validate())
                    Debug.LogWarning($"[NoteChart] {issue}");

            return chart;
        }

        /// <summary>
        /// Harita hatalarını döndürür (silmez). Şimdilik: aynı şeritte hold sürerken (bitişi dahil) başlayan nota.
        /// </summary>
        public List<string> Validate()
        {
            var issues = new List<string>();
            for (int lane = 0; lane < LaneCount; lane++)
            {
                ChartNote activeHold = null;
                foreach (var note in byLane[lane])
                {
                    if (activeHold != null && note.Beat <= activeHold.Beat + activeHold.LengthBeats)
                    {
                        issues.Add($"Şerit {lane}: beat {note.Beat} notası, beat {activeHold.Beat} hold'u " +
                                   $"(bitiş {activeHold.Beat + activeHold.LengthBeats}) sürerken başlıyor.");
                    }

                    if (note.IsHold && (activeHold == null || note.Beat + note.LengthBeats > activeHold.Beat + activeHold.LengthBeats))
                        activeHold = note;
                }
            }
            return issues;
        }
    }
}
