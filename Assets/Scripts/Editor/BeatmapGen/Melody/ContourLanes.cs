using System;
using System.Collections.Generic;

namespace PixelBeat.EditorTools.BeatmapGen.Melody
{
    /// <summary>
    /// Şerit = perde konturu (GDD §7.1). Unity'ye bağımlı değil.
    /// - Tercih edilen şerit: perde, kayan penceredeki (±<see cref="DefaultWindowSec"/> sn) yerel aralığa (%10–%90) göre 0–3'e eşlenir;
    ///   böylece melodi register değiştirse de 4 şeridin hepsi kullanılır.
    /// - Adım: önceki notaya göre yön (tekrar / yukarı / aşağı / büyük atlama); <see cref="LaneAssigner"/> bunu maliyete katar.
    /// </summary>
    public static class ContourLanes
    {
        public const double DefaultWindowSec = 4.0;
        /// <summary>Yerel aralık bundan dar olamaz (yarım ses); dar aralıkta küçük oynamalar uç şeritlere savrulmasın.</summary>
        public const int MinSpan = 7;
        public const int JumpSemitones = 5;

        public static ContourStep StepBetween(int prevPitch, int pitch)
        {
            int d = pitch - prevPitch;
            if (d == 0) return ContourStep.Repeat;
            if (d >= JumpSemitones) return ContourStep.JumpUp;
            if (d <= -JumpSemitones) return ContourStep.JumpDown;
            return d > 0 ? ContourStep.Up : ContourStep.Down;
        }

        public static void Compute(IReadOnlyList<double> times, IReadOnlyList<int> pitches,
            out double[] preferredLanes, out ContourStep[] steps, double windowSec = DefaultWindowSec)
        {
            int n = times.Count;
            preferredLanes = new double[n];
            steps = new ContourStep[n];
            var window = new List<int>();
            int lo = 0, hi = 0;
            for (int i = 0; i < n; i++)
            {
                while (lo < n && times[lo] < times[i] - windowSec) lo++;
                while (hi < n && times[hi] <= times[i] + windowSec) hi++;
                window.Clear();
                for (int k = lo; k < hi; k++) window.Add(pitches[k]);
                window.Sort();
                double low = window[(int)((window.Count - 1) * 0.1)];
                double high = window[(int)Math.Ceiling((window.Count - 1) * 0.9)];
                if (high - low < MinSpan)
                {
                    double c = (low + high) / 2;
                    low = c - MinSpan / 2.0;
                    high = c + MinSpan / 2.0;
                }
                double norm = Math.Max(0, Math.Min(1, (pitches[i] - low) / (high - low)));
                preferredLanes[i] = norm * (LaneAssigner.LaneCount - 1);
                steps[i] = i == 0 ? ContourStep.None : StepBetween(pitches[i - 1], pitches[i]);
            }
        }
    }
}
