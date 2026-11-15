using System.Collections.Generic;
using NUnit.Framework;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Tests
{
    /// <summary>
    /// Kalibrasyon offset'inin işaret testi: ses L sn geç duyulan bir cihazda oyuncu sesle birlikte (yani geç) dokunur.
    /// Kalibrasyon pozitif offset bulmalı ve bu offset hem değerlendirmeyi hem notaları aynı yöne (geç) kaydırmalı.
    /// </summary>
    public class OffsetSignTests
    {
        private const double DspStart = 100.0;
        private const double SongOffset = 0.0;
        private const double SecPerBeat = 60.0 / 90.0;

        /// <summary>Beat k'nin sesi duyulduğu an oyuncunun dokunduğu dspTime.</summary>
        private static double HeardDsp(int beat, double latencySec) => DspStart + SongOffset + beat * SecPerBeat + latencySec;

        /// <summary>Kalibrasyon ekranının yaptığı gibi: dokunuşları ölçüp yeni offset'i hesaplar.</summary>
        private static float Calibrate(float currentOffsetMs, double latencySec, double jitterSec = 0)
        {
            var diffs = new List<double>();
            for (int k = 4; k < 20; k++)
            {
                double jitter = jitterSec * ((k % 3) - 1); // -j, 0, +j
                double songTime = Conductor.SongTimeFromDsp(HeardDsp(k, latencySec) + jitter, DspStart, SongOffset, currentOffsetMs);
                diffs.Add(OffsetCalibration.DiffToBeat(songTime, SecPerBeat));
            }
            return OffsetCalibration.NewUserOffsetMs(currentOffsetMs, diffs, 2, out _);
        }

        [TestCase(0.080)]
        [TestCase(0.250)] // Bluetooth kulaklık
        [TestCase(0.450)] // çok yüksek gecikme, asimetrik pencere sayesinde hâlâ doğru
        [TestCase(-0.030)] // erken dokunan oyuncu
        public void LateTapsGivePositiveOffset_WithSameMagnitude(double latencySec)
        {
            float offset = Calibrate(0f, latencySec);
            Assert.AreEqual(latencySec * 1000.0, offset, 0.5, "Offset gecikmeyle aynı işarette ve büyüklükte olmalı");
        }

        [Test]
        public void CalibratedOffset_JudgesLateTapAsPerfect()
        {
            const double latency = 0.120;
            float offset = Calibrate(0f, latency);

            var windows = ScriptableObject.CreateInstance<JudgementWindows>();
            try
            {
                for (int k = 0; k < 8; k++)
                {
                    double noteTime = k * SecPerBeat;

                    // Offset yokken sesle birlikte dokunuş 120 ms geç sayılır (GOOD/MISS sınırında).
                    double rawTap = Conductor.SongTimeFromDsp(HeardDsp(k, latency), DspStart, SongOffset, 0);
                    Assert.Greater(rawTap - noteTime, 0.1, "Offset yokken dokunuş geç görünmeli");

                    // Kalibre offset ile aynı dokunuş tam zamanında → PERFECT.
                    double tap = Conductor.SongTimeFromDsp(HeardDsp(k, latency), DspStart, SongOffset, offset);
                    Assert.AreEqual(0.0, tap - noteTime, 1e-3);
                    Assert.AreEqual(Judgement.Perfect, windows.EvaluatePress(tap - noteTime));
                }
            }
            finally
            {
                Object.DestroyImmediate(windows);
            }
        }

        [Test]
        public void PositiveOffset_DelaysNotesOnScreen()
        {
            const double latency = 0.120;
            float offset = Calibrate(0f, latency);

            // Herhangi bir dsp anında pozitif offset şarkı zamanını geri çeker → nota vuruş çizgisinin daha yukarısında
            // (NoteSpawner: y ∝ nota zamanı − şarkı zamanı). Yani notalar da geç gelir.
            double dsp = DspStart + 3.0;
            double without = Conductor.SongTimeFromDsp(dsp, DspStart, SongOffset, 0);
            double with = Conductor.SongTimeFromDsp(dsp, DspStart, SongOffset, offset);
            Assert.AreEqual(-latency, with - without, 1e-6);

            // Nota, sesi duyulduğu anda tam vuruş çizgisinde: (nota zamanı − şarkı zamanı) = 0.
            const int beat = 5;
            double songAtHeard = Conductor.SongTimeFromDsp(HeardDsp(beat, latency), DspStart, SongOffset, offset);
            Assert.AreEqual(beat * SecPerBeat, songAtHeard, 1e-3);
        }

        [Test]
        public void Recalibration_IsStable()
        {
            const double latency = 0.180;
            float first = Calibrate(0f, latency);
            float second = Calibrate(first, latency);
            Assert.AreEqual(first, second, 0.5, "Doğru offset ile tekrar ölçüm aynı sonucu vermeli (fark ~0)");
        }

        [Test]
        public void TrimmedMean_IgnoresOutliersAndJitter()
        {
            float offset = Calibrate(0f, 0.100, jitterSec: 0.015);
            Assert.AreEqual(100.0, offset, 2.0);

            var values = new List<double> { 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, -0.2, 0.45, 0.4, -0.15 };
            Assert.AreEqual(0.1, OffsetCalibration.TrimmedMean(values, 2, out _), 1e-9);
        }
    }
}
