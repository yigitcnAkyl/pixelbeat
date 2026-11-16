using System;
using System.Collections.Generic;

namespace PixelBeat.EditorTools.BeatmapGen.Melody
{
    /// <summary>Melodi hattındaki nota (zamanlar MIDI'deki gerçek zaman, sn).</summary>
    public sealed class MelodyNote
    {
        public double Time;
        public double Duration;
        public int Pitch;
        public int Velocity;
        /// <summary>Aynı anda vurulan en güçlü ikinci nota (akor adayı); yoksa -1.</summary>
        public int ChordPitch = -1;
        public int ChordVelocity;
        /// <summary>Uzun sessizlikten sonra gelen ilk nota (seyreltmede korunur).</summary>
        public bool PhraseStart;
        /// <summary>Hold olamaz (vokal: bölünmüş hecenin son olmayan parçası).</summary>
        public bool NoHold;
        public double End => Time + Duration;
    }

    public sealed class MelodyExtractSettings
    {
        /// <summary>Bu süre içinde başlayan notalar "aynı anda" sayılır (skyline grubu).</summary>
        public double ChordWindowSec = 0.06;
        /// <summary>Alt perde sınırı (MIDI). Altındaki notalar melodi olamaz.</summary>
        public int MinPitch = 48;
        /// <summary>Bundan kısa notalar hayalet sayılır.</summary>
        public double GhostMinDurationSec = 0.06;
        /// <summary>Velocity'si çevresindeki (±<see cref="LocalWindowSec"/>) notaların medyanının bu oranının altındaysa hayalet.</summary>
        public double GhostVelocityRatio = 0.5;
        /// <summary>Süsleme: bundan kısa, hemen ardından (≤ <see cref="OrnamentFollowSec"/>) ±2 yarım ses içinde nota gelen nota.</summary>
        public double OrnamentMaxDurationSec = 0.11;
        public double OrnamentFollowSec = 0.15;
        public int OrnamentMaxInterval = 2;
        /// <summary>Daha tiz bir melodi notası sürerken altta başlayan nota eşliktir (tiz notanın ilk bu kadar saniyesi için).</summary>
        public double SustainShadowSec = 0.5;
        /// <summary>Yerel melodi seviyesinin (çevredeki skyline notalarının %65'liği) bu kadar yarım ses altındaki notalar eşliktir.</summary>
        public int RegisterDropSemitones = 7;
        public double RegisterPercentile = 0.65;
        public double LocalWindowSec = 3.0;
        /// <summary>Akor adayı: ikinci nota en tiz notadan en az bu kadar yarım ses aşağıda olmalı.</summary>
        public int ChordMinInterval = 3;
    }

    /// <summary>
    /// Çok sesli MIDI → tek sesli melodi hattı (GDD §7.1). Unity'ye bağımlı değil, belirleyici.
    /// Sıra: alt perde + hayalet temizliği → skyline (60 ms grupta en tiz) → tiz nota gölgesindeki eşlik →
    /// yerel register'ın çok altındaki eşlik/bas → süslemeler.
    /// </summary>
    public static class MelodyExtractor
    {
        public static List<MelodyNote> Extract(IReadOnlyList<MidiNote> notes, MelodyExtractSettings s = null)
        {
            s = s ?? new MelodyExtractSettings();
            var sorted = new List<MidiNote>(notes);
            sorted.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Pitch.CompareTo(a.Pitch));

            var clean = RemoveGhosts(sorted, s);
            var line = Skyline(clean, s);
            line = RemoveShadowed(line, s);
            line = RemoveBelowRegister(line, s);
            line = RemoveOrnaments(line, s);
            return line;
        }

        /// <summary>Alt perde sınırı, çok kısa ve çevresine göre çok zayıf notalar.</summary>
        public static List<MidiNote> RemoveGhosts(List<MidiNote> sorted, MelodyExtractSettings s)
        {
            var result = new List<MidiNote>();
            int lo = 0, hi = 0;
            var window = new List<int>();
            for (int i = 0; i < sorted.Count; i++)
            {
                var n = sorted[i];
                if (n.Pitch < s.MinPitch || n.Duration < s.GhostMinDurationSec) continue;

                while (lo < sorted.Count && sorted[lo].Start < n.Start - s.LocalWindowSec) lo++;
                while (hi < sorted.Count && sorted[hi].Start <= n.Start + s.LocalWindowSec) hi++;
                window.Clear();
                for (int k = lo; k < hi; k++) window.Add(sorted[k].Velocity);
                window.Sort();
                double median = window.Count > 0 ? window[window.Count / 2] : n.Velocity;
                if (n.Velocity < median * s.GhostVelocityRatio) continue;
                result.Add(n);
            }
            return result;
        }

        /// <summary>~60 ms içinde başlayan notalardan en tizi; zamanı kendi başlangıcı. İkinci güçlü nota akor adayı olur.</summary>
        public static List<MelodyNote> Skyline(List<MidiNote> sorted, MelodyExtractSettings s)
        {
            var result = new List<MelodyNote>();
            int i = 0;
            while (i < sorted.Count)
            {
                double groupStart = sorted[i].Start;
                int j = i;
                MidiNote top = null;
                while (j < sorted.Count && sorted[j].Start - groupStart < s.ChordWindowSec)
                {
                    var c = sorted[j];
                    if (top == null || c.Pitch > top.Pitch || (c.Pitch == top.Pitch && c.Velocity > top.Velocity)) top = c;
                    j++;
                }

                var m = new MelodyNote { Time = top.Start, Duration = top.Duration, Pitch = top.Pitch, Velocity = top.Velocity };
                for (int k = i; k < j; k++)
                {
                    var c = sorted[k];
                    if (c == top || top.Pitch - c.Pitch < s.ChordMinInterval) continue;
                    if (c.Velocity > m.ChordVelocity || (c.Velocity == m.ChordVelocity && c.Pitch > m.ChordPitch))
                    {
                        m.ChordPitch = c.Pitch;
                        m.ChordVelocity = c.Velocity;
                    }
                }
                result.Add(m);
                i = j;
            }
            return result;
        }

        /// <summary>Daha tiz bir melodi notası çalarken (ilk <see cref="MelodyExtractSettings.SustainShadowSec"/> sn) altta başlayan notalar eşliktir.</summary>
        public static List<MelodyNote> RemoveShadowed(List<MelodyNote> line, MelodyExtractSettings s)
        {
            var result = new List<MelodyNote>();
            foreach (var n in line)
            {
                if (result.Count > 0)
                {
                    var prev = result[result.Count - 1];
                    double shadowEnd = prev.Time + Math.Min(prev.Duration, s.SustainShadowSec);
                    if (prev.Pitch > n.Pitch && n.Time < shadowEnd) continue;
                }
                result.Add(n);
            }
            return result;
        }

        /// <summary>Çevredeki melodi seviyesinin (%65'lik) bir beşliden fazla altındaki notalar bas / eşliktir.</summary>
        public static List<MelodyNote> RemoveBelowRegister(List<MelodyNote> line, MelodyExtractSettings s)
        {
            var result = new List<MelodyNote>();
            var window = new List<int>();
            int lo = 0, hi = 0;
            foreach (var n in line)
            {
                while (lo < line.Count && line[lo].Time < n.Time - s.LocalWindowSec) lo++;
                while (hi < line.Count && line[hi].Time <= n.Time + s.LocalWindowSec) hi++;
                window.Clear();
                for (int k = lo; k < hi; k++) window.Add(line[k].Pitch);
                window.Sort();
                int level = window[Math.Min(window.Count - 1, (int)(window.Count * s.RegisterPercentile))];
                if (n.Pitch < level - s.RegisterDropSemitones) continue;
                result.Add(n);
            }
            return result;
        }

        /// <summary>Kısa süsleme notaları (vurgu notası / çarpma): hemen ardından yakın perdede nota geliyorsa atılır.</summary>
        public static List<MelodyNote> RemoveOrnaments(List<MelodyNote> line, MelodyExtractSettings s)
        {
            var result = new List<MelodyNote>();
            for (int i = 0; i < line.Count; i++)
            {
                var n = line[i];
                if (n.Duration < s.OrnamentMaxDurationSec && i + 1 < line.Count)
                {
                    var next = line[i + 1];
                    if (next.Time - n.Time <= s.OrnamentFollowSec && Math.Abs(next.Pitch - n.Pitch) <= s.OrnamentMaxInterval
                        && next.Pitch != n.Pitch)
                        continue;
                }
                result.Add(n);
            }
            return result;
        }
    }
}
