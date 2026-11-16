using System;
using System.Collections.Generic;

namespace PixelBeat.Core
{
    /// <summary>
    /// Beat ↔ şarkı zamanı (sn, ses dosyasının başından) dönüşümü. Unity'ye bağımlı değil.
    /// - Sabit tempo: zaman = offset + beat × 60/bpm (eski beatmap'ler).
    /// - Değişken tempo: her tam beat'in zamanı listede; aradaki kesirli beat'ler komşu iki beat arasında doğrusal.
    ///   Listenin önünde / sonunda ilk / son aralıkla devam edilir.
    /// </summary>
    public sealed class TempoMap
    {
        private readonly double[] beatTimes; // null → sabit tempo
        private readonly double offsetSec;
        private readonly double secPerBeat;

        private TempoMap(double[] beatTimes, double offsetSec, double secPerBeat)
        {
            this.beatTimes = beatTimes;
            this.offsetSec = offsetSec;
            this.secPerBeat = secPerBeat;
        }

        public static TempoMap Constant(double bpm, double offsetSec)
        {
            if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            return new TempoMap(null, offsetSec, 60.0 / bpm);
        }

        /// <summary>En az 2, kesin artan beat zamanı.</summary>
        public static TempoMap FromBeatTimes(IReadOnlyList<double> times)
        {
            if (times == null || times.Count < 2) throw new ArgumentException("En az 2 beat zamanı gerekli.", nameof(times));
            var arr = new double[times.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = times[i];
                if (i > 0 && arr[i] <= arr[i - 1]) throw new ArgumentException($"Beat zamanları kesin artan olmalı (indeks {i}).", nameof(times));
            }
            double avg = (arr[arr.Length - 1] - arr[0]) / (arr.Length - 1);
            return new TempoMap(arr, arr[0], avg);
        }

        public bool IsVariable => beatTimes != null;

        /// <summary>Değişken haritada listedeki beat sayısı; sabitte 0.</summary>
        public int BeatCount => beatTimes?.Length ?? 0;

        /// <summary>Ortalama beat süresi (sn).</summary>
        public double AverageSecPerBeat => secPerBeat;
        public double AverageBpm => 60.0 / secPerBeat;

        public double BeatToTime(double beat)
        {
            if (beatTimes == null) return offsetSec + beat * secPerBeat;

            int n = beatTimes.Length;
            if (beat <= 0) return beatTimes[0] + beat * (beatTimes[1] - beatTimes[0]);
            if (beat >= n - 1) return beatTimes[n - 1] + (beat - (n - 1)) * (beatTimes[n - 1] - beatTimes[n - 2]);

            int k = (int)Math.Floor(beat);
            double frac = beat - k;
            return beatTimes[k] + frac * (beatTimes[k + 1] - beatTimes[k]);
        }

        public double TimeToBeat(double time)
        {
            if (beatTimes == null) return (time - offsetSec) / secPerBeat;

            int n = beatTimes.Length;
            if (time <= beatTimes[0]) return (time - beatTimes[0]) / (beatTimes[1] - beatTimes[0]);
            if (time >= beatTimes[n - 1]) return (n - 1) + (time - beatTimes[n - 1]) / (beatTimes[n - 1] - beatTimes[n - 2]);

            // beatTimes[lo] <= time < beatTimes[lo + 1]
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (beatTimes[mid] <= time) lo = mid;
                else hi = mid;
            }
            return lo + (time - beatTimes[lo]) / (beatTimes[lo + 1] - beatTimes[lo]);
        }

        /// <summary>Beat'teki yerel tempo (BPM).</summary>
        public double BpmAt(double beat)
        {
            if (beatTimes == null) return 60.0 / secPerBeat;
            return 60.0 / (BeatToTime(beat + 0.5) - BeatToTime(beat - 0.5));
        }
    }
}
