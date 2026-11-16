using System.Collections.Generic;
using PixelBeat.EditorTools.BeatmapGen.Melody;

namespace PixelBeat.EditorTools.BeatmapGen.Vocal
{
    public sealed class SyllableSplitSettings
    {
        /// <summary>Bundan kısa notalar bölünmez.</summary>
        public double MinSplitDurationSec = 0.3;
        /// <summary>Notanın ilk / son bu kadar süresindeki onset'ler sayılmaz (notanın kendi başlangıcı, sonraki hece).</summary>
        public double EdgeGuardSec = 0.08;
        /// <summary>Parçalar bundan kısa olamaz; kısa parça öncekine katılır.</summary>
        public double MinPieceSec = 0.12;
    }

    /// <summary>
    /// Hece bölme (GDD §7.2): aynı perdede art arda söylenen heceleri tek uzun nota yapan Basic Pitch çıktısını,
    /// vokal stem'indeki onset'lerde parçalara böler. Son olmayan parçalar tap (NoHold), son parça yeterince uzunsa hold kalabilir.
    /// Unity'ye bağımlı değil.
    /// </summary>
    public static class SyllableSplitter
    {
        /// <param name="onsetTimes">Vokal stem'indeki onset zamanları (sn, artan).</param>
        public static List<MelodyNote> Split(IReadOnlyList<MelodyNote> line, IReadOnlyList<double> onsetTimes,
            out int splitNotes, SyllableSplitSettings s = null)
        {
            s = s ?? new SyllableSplitSettings();
            splitNotes = 0;
            var result = new List<MelodyNote>();
            int k = 0;
            foreach (var n in line)
            {
                if (n.Duration < s.MinSplitDurationSec)
                {
                    result.Add(n);
                    continue;
                }

                // Notanın içindeki onset'ler → kesim noktaları
                var cuts = new List<double>();
                while (k < onsetTimes.Count && onsetTimes[k] < n.Time) k++;
                for (int j = k; j < onsetTimes.Count && onsetTimes[j] < n.End; j++)
                {
                    double t = onsetTimes[j];
                    if (t < n.Time + s.EdgeGuardSec || t > n.End - s.EdgeGuardSec) continue;
                    double prevCut = cuts.Count > 0 ? cuts[cuts.Count - 1] : n.Time;
                    if (t - prevCut < s.MinPieceSec) continue; // önceki parça çok kısa kalırdı
                    cuts.Add(t);
                }
                // Son parça çok kısa kalırsa son kesim kalkar.
                while (cuts.Count > 0 && n.End - cuts[cuts.Count - 1] < s.MinPieceSec) cuts.RemoveAt(cuts.Count - 1);

                if (cuts.Count == 0)
                {
                    result.Add(n);
                    continue;
                }

                splitNotes++;
                double start = n.Time;
                for (int c = 0; c <= cuts.Count; c++)
                {
                    double end = c < cuts.Count ? cuts[c] : n.End;
                    result.Add(new MelodyNote
                    {
                        Time = start,
                        Duration = end - start,
                        Pitch = n.Pitch,
                        Velocity = n.Velocity,
                        NoHold = c < cuts.Count,
                    });
                    start = end;
                }
            }
            return result;
        }
    }
}
