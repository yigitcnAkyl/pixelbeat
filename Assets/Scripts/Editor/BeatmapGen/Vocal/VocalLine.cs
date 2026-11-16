using System;
using System.Collections.Generic;
using PixelBeat.EditorTools.BeatmapGen.Melody;

namespace PixelBeat.EditorTools.BeatmapGen.Vocal
{
    public sealed class VocalLineSettings
    {
        /// <summary>Bu süre içinde başlayan notalar "aynı anda" sayılır.</summary>
        public double SimultaneousSec = 0.06;
        /// <summary>Yerel medyandan (±<see cref="OutlierWindowSec"/>) bu kadar yarım ses sapan nota aykırıdır (oktav / harmonik hatası).</summary>
        public int OutlierSemitones = 12;
        public double OutlierWindowSec = 3.0;
    }

    /// <summary>
    /// Vokal MIDI'si → tek sesli vokal hattı (GDD §7.2). Unity'ye bağımlı değil, belirleyici.
    /// Kapı (başlangıcı vokal bölümünde olmayan nota atılır) → aynı anda başlayanlardan önceki notaya perdece en yakını → aykırı perdeler.
    /// </summary>
    public static class VocalLine
    {
        public static List<MidiNote> Gate(IReadOnlyList<MidiNote> notes, IReadOnlyList<Span> sections)
        {
            var result = new List<MidiNote>();
            foreach (var n in notes)
                if (VocalGate.Inside(sections, n.Start)) result.Add(n);
            return result;
        }

        /// <summary>Aynı anda başlayan notalardan önceki seçilen notaya perdece en yakını (eşitlikte velocity'si yüksek); ilk grupta en güçlüsü.</summary>
        public static List<MelodyNote> Monophonic(IReadOnlyList<MidiNote> notes, VocalLineSettings s = null)
        {
            s = s ?? new VocalLineSettings();
            var sorted = new List<MidiNote>(notes);
            sorted.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Pitch.CompareTo(b.Pitch));

            var result = new List<MelodyNote>();
            int i = 0;
            while (i < sorted.Count)
            {
                double groupStart = sorted[i].Start;
                int j = i;
                MidiNote best = null;
                int prevPitch = result.Count > 0 ? result[result.Count - 1].Pitch : -1;
                while (j < sorted.Count && sorted[j].Start - groupStart < s.SimultaneousSec)
                {
                    var c = sorted[j];
                    if (best == null || Better(c, best, prevPitch)) best = c;
                    j++;
                }
                result.Add(new MelodyNote { Time = best.Start, Duration = best.Duration, Pitch = best.Pitch, Velocity = best.Velocity });
                i = j;
            }
            return result;
        }

        private static bool Better(MidiNote c, MidiNote best, int prevPitch)
        {
            if (prevPitch >= 0)
            {
                int dc = Math.Abs(c.Pitch - prevPitch), db = Math.Abs(best.Pitch - prevPitch);
                if (dc != db) return dc < db;
            }
            if (c.Velocity != best.Velocity) return c.Velocity > best.Velocity;
            return c.Pitch > best.Pitch;
        }

        /// <summary>Yerel medyandan bir oktavdan fazla sapan perdeler atılır.</summary>
        public static List<MelodyNote> RemoveOutliers(List<MelodyNote> line, VocalLineSettings s = null)
        {
            s = s ?? new VocalLineSettings();
            var result = new List<MelodyNote>();
            var window = new List<int>();
            int lo = 0, hi = 0;
            foreach (var n in line)
            {
                while (lo < line.Count && line[lo].Time < n.Time - s.OutlierWindowSec) lo++;
                while (hi < line.Count && line[hi].Time <= n.Time + s.OutlierWindowSec) hi++;
                window.Clear();
                for (int k = lo; k < hi; k++) window.Add(line[k].Pitch);
                window.Sort();
                int median = window[window.Count / 2];
                if (Math.Abs(n.Pitch - median) > s.OutlierSemitones) continue;
                result.Add(n);
            }
            return result;
        }
    }
}
