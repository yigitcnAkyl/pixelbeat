using System;
using System.Collections.Generic;
using PixelBeat.Core;

namespace PixelBeat.EditorTools.BeatmapGen
{
    public sealed class GeneratedNote
    {
        public int Lane;
        public double Beat;
        public double LengthBeats;
        public double Time;
        /// <summary>Hold sonu; tap için Time.</summary>
        public double EndTime;
        public bool IsHold => LengthBeats > 0;
    }

    public struct LaneAssignStats
    {
        public int Dropped;
        public int DemotedChords;
        public int DemotedHolds;
    }

    /// <summary>
    /// Şerit dağıtımı (GDD §7) — iki başparmakla oynanabilirlik:
    /// - Şerit 0–1 sol el, 2–3 sağ el. Aynı anda en fazla 2 parmak.
    /// - Akor sadece 2'li ve el başına bir nota; hold sürerken akor yok, ikinci hold yok.
    /// - Hold sürerken (bitişi dahil) o şeride nota yok; diğer notalar sadece serbest elin şeritlerine.
    /// - Aynı el art arda en az <see cref="MinHandGapSec"/> bekler (hold'da bitişten itibaren).
    /// - Aynı şeride art arda en fazla <see cref="MaxSameLaneRun"/> tekli nota.
    /// Kısıtlar içinde şerit ses perdesine göre seçilir (bas → sol, tiz → sağ). Tamamen belirleyicidir.
    /// </summary>
    public static class LaneAssigner
    {
        public const int LaneCount = 4;
        public const double MinHandGapSec = 0.15;
        public const int MaxSameLaneRun = 2;
        private const double Eps = 1e-6;

        public static int HandOf(int lane) => lane < 2 ? 0 : 1;

        public static List<GeneratedNote> Assign(IReadOnlyList<PlannedNote> notes, TempoMap tempo, out LaneAssignStats stats)
        {
            stats = default;
            var result = new List<GeneratedNote>();

            // Ses perdesi yüzdeliği → tercih edilen şerit.
            var centroids = new List<double>();
            foreach (var n in notes) centroids.Add(n.Centroid);
            centroids.Sort();

            var handFreeAt = new[] { double.NegativeInfinity, double.NegativeInfinity };
            var laneBlockedUntil = new double[LaneCount]; // hold bitişi (dahil)
            for (int i = 0; i < LaneCount; i++) laneBlockedUntil[i] = double.NegativeInfinity;
            double holdEnd = double.NegativeInfinity;
            int holdHand = -1;
            int lastLane = -1, run = 0, lastHand = -1;

            foreach (var note in notes)
            {
                double t = note.Time;
                bool holdActive = t <= holdEnd + Eps;
                if (!holdActive) holdHand = -1;

                double pref = PreferredLane(centroids, note.Centroid);

                bool HandOk(int h) => handFreeAt[h] <= t + Eps && !(holdActive && holdHand == h);
                bool LaneOk(int lane) => HandOk(HandOf(lane)) && t > laneBlockedUntil[lane] + Eps
                                         && !(lane == lastLane && run >= MaxSameLaneRun);

                bool wantChord = note.Chord;
                if (wantChord && (holdActive || !HandOk(0) || !HandOk(1)))
                {
                    wantChord = false;
                    stats.DemotedChords++;
                }

                if (wantChord)
                {
                    int left = BestLane(0, 1, pref, LaneOk, lastHand, lastLane);
                    int right = BestLane(2, 3, pref, LaneOk, lastHand, lastLane);
                    if (left >= 0 && right >= 0)
                    {
                        Add(result, note, left, 0, tempo);
                        Add(result, note, right, 0, tempo);
                        handFreeAt[0] = handFreeAt[1] = t + MinHandGapSec;
                        lastLane = -1; run = 0; lastHand = -1; // akor tekli seriyi sıfırlar
                        continue;
                    }
                    stats.DemotedChords++;
                }

                int lane = BestLane(0, LaneCount - 1, pref, LaneOk, lastHand, lastLane);
                if (lane < 0)
                {
                    stats.Dropped++;
                    continue;
                }

                double length = note.HoldBeats;
                if (length > 0 && holdActive)
                {
                    length = 0; // aynı anda ikinci hold yok
                    stats.DemotedHolds++;
                }

                var g = Add(result, note, lane, length, tempo);
                int hand = HandOf(lane);
                if (g.IsHold)
                {
                    holdEnd = g.EndTime;
                    holdHand = hand;
                    laneBlockedUntil[lane] = g.EndTime;
                }
                handFreeAt[hand] = g.EndTime + MinHandGapSec;

                run = lane == lastLane ? run + 1 : 1;
                lastLane = lane;
                lastHand = hand;
            }

            return result;
        }

        private static GeneratedNote Add(List<GeneratedNote> list, PlannedNote n, int lane, double length, TempoMap tempo)
        {
            var g = new GeneratedNote
            {
                Lane = lane,
                Beat = n.Beat,
                LengthBeats = length,
                Time = n.Time,
                EndTime = length > 0 ? tempo.BeatToTime(n.Beat + length) : n.Time,
            };
            list.Add(g);
            return g;
        }

        /// <summary>Ses perdesi yüzdeliğinden 0–3 arası (kesirli) tercih edilen şerit.</summary>
        private static double PreferredLane(List<double> sortedCentroids, double centroid)
        {
            if (sortedCentroids.Count <= 1) return 1.5;
            int idx = sortedCentroids.BinarySearch(centroid);
            if (idx < 0) idx = ~idx;
            double pct = idx / (double)(sortedCentroids.Count - 1);
            return Math.Max(0, Math.Min(3, pct * 4 - 0.5));
        }

        /// <summary>[from, to] aralığında kısıtlara uyan, tercihe en yakın şerit (el değiştirmeyi ve şerit değiştirmeyi hafif ödüllendirir).</summary>
        private static int BestLane(int from, int to, double pref, Func<int, bool> ok, int lastHand, int lastLane)
        {
            int best = -1;
            double bestCost = double.MaxValue;
            for (int lane = from; lane <= to; lane++)
            {
                if (!ok(lane)) continue;
                double cost = Math.Abs(lane - pref);
                if (HandOf(lane) == lastHand) cost += 0.3;
                if (lane == lastLane) cost += 0.5;
                if (cost < bestCost - 1e-9)
                {
                    bestCost = cost;
                    best = lane;
                }
            }
            return best;
        }
    }

    /// <summary>Üretilen haritanın iki parmak / GDD §7 kurallarına uygunluk denetimi (testler ve Editor raporu için).</summary>
    public static class PlayabilityRules
    {
        private const double Eps = 1e-6;

        public static List<string> Check(IReadOnlyList<GeneratedNote> notes, double minHandGapSec = LaneAssigner.MinHandGapSec,
            int maxSameLaneRun = LaneAssigner.MaxSameLaneRun)
        {
            var problems = new List<string>();
            var sorted = new List<GeneratedNote>(notes);
            sorted.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Lane.CompareTo(b.Lane));

            // Aynı anda başlayan gruplar
            var groups = new List<List<GeneratedNote>>();
            foreach (var n in sorted)
            {
                if (groups.Count > 0 && Math.Abs(groups[groups.Count - 1][0].Time - n.Time) < Eps) groups[groups.Count - 1].Add(n);
                else groups.Add(new List<GeneratedNote> { n });
            }

            int lastLane = -1, run = 0;
            var handLastEnd = new[] { double.NegativeInfinity, double.NegativeInfinity };
            var laneLastEnd = new double[LaneAssigner.LaneCount];
            for (int i = 0; i < laneLastEnd.Length; i++) laneLastEnd[i] = double.NegativeInfinity;

            foreach (var g in groups)
            {
                double t = g[0].Time;
                if (g.Count > 2) problems.Add($"beat {g[0].Beat}: {g.Count}'li akor");
                if (g.Count == 2 && LaneAssigner.HandOf(g[0].Lane) == LaneAssigner.HandOf(g[1].Lane))
                    problems.Add($"beat {g[0].Beat}: akor aynı elde");

                // Aktif hold sayısı + yeni basışlar ≤ 2 parmak
                int active = 0;
                foreach (var other in sorted)
                    if (other.IsHold && other.Time < t - Eps && other.EndTime >= t - Eps) active++;
                if (active + g.Count > 2) problems.Add($"beat {g[0].Beat}: {active + g.Count} parmak gerekiyor");

                foreach (var n in g)
                {
                    if (t <= laneLastEnd[n.Lane] + Eps)
                        problems.Add($"beat {n.Beat}: şerit {n.Lane}'de önceki nota/hold bitmeden");
                    int h = LaneAssigner.HandOf(n.Lane);
                    if (t < handLastEnd[h] + minHandGapSec - Eps)
                        problems.Add($"beat {n.Beat}: el {h} için çok sık ({(t - handLastEnd[h]) * 1000:0} ms)");
                }

                foreach (var n in g)
                {
                    laneLastEnd[n.Lane] = Math.Max(laneLastEnd[n.Lane], n.EndTime);
                    int h = LaneAssigner.HandOf(n.Lane);
                    handLastEnd[h] = Math.Max(handLastEnd[h], n.EndTime);
                }

                if (g.Count == 1)
                {
                    run = g[0].Lane == lastLane ? run + 1 : 1;
                    lastLane = g[0].Lane;
                    if (run > maxSameLaneRun) problems.Add($"beat {g[0].Beat}: şerit {lastLane}'e art arda {run} nota");
                }
                else
                {
                    lastLane = -1;
                    run = 0;
                }
            }

            return problems;
        }
    }
}
