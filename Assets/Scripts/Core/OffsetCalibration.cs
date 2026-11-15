using System;
using System.Collections.Generic;

namespace PixelBeat.Core
{
    /// <summary>
    /// Offset kalibrasyonunun saf hesapları (test edilebilir, Unity'ye bağımlı değil).
    /// İşaret kuralı Conductor ile aynı: pozitif fark = dokunuş beat'ten geç → kullanıcı offset'i artar.
    /// </summary>
    public static class OffsetCalibration
    {
        /// <summary>
        /// Fark penceresi asimetrik: [-EarlyWindowBeats, 1 - EarlyWindowBeats) beat.
        /// Gecikme hep pozitif yönde olduğu için (Bluetooth'ta yüzlerce ms) geç tarafa daha çok yer ayrılır.
        /// </summary>
        public const double EarlyWindowBeats = 0.25;

        /// <summary>Dokunuşun (şarkı zamanı) en yakın beat'e göre farkı (sn). Pozitif = geç.</summary>
        public static double DiffToBeat(double songTimeSec, double secPerBeat)
        {
            double k = Math.Floor(songTimeSec / secPerBeat + EarlyWindowBeats);
            return songTimeSec - k * secPerBeat;
        }

        /// <summary>Uçlardan trimEachSide kadar atılmış ortalama ve kalanların standart sapması.</summary>
        public static double TrimmedMean(IReadOnlyList<double> values, int trimEachSide, out double stdDev)
        {
            if (values == null || values.Count == 0) throw new ArgumentException("Boş liste.", nameof(values));

            var sorted = new List<double>(values);
            sorted.Sort();
            int trim = Math.Max(0, Math.Min(trimEachSide, (sorted.Count - 1) / 2));
            int count = sorted.Count - 2 * trim;

            double sum = 0;
            for (int i = trim; i < trim + count; i++) sum += sorted[i];
            double mean = sum / count;

            double sq = 0;
            for (int i = trim; i < trim + count; i++) sq += (sorted[i] - mean) * (sorted[i] - mean);
            stdDev = Math.Sqrt(sq / count);
            return mean;
        }

        /// <summary>
        /// Ölçüm, o anki offset uygulanmış şarkı zamanıyla yapılır; bu yüzden yeni offset = eski + ortalama fark.
        /// </summary>
        public static float NewUserOffsetMs(float currentOffsetMs, IReadOnlyList<double> diffsSec, int trimEachSide, out double stdDevMs)
        {
            double mean = TrimmedMean(diffsSec, trimEachSide, out double sd);
            stdDevMs = sd * 1000.0;
            return (float)(currentOffsetMs + mean * 1000.0);
        }
    }
}
