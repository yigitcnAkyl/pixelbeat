using System;
using PixelBeat.Core;

namespace PixelBeat.Gameplay
{
    public enum JudgementKind
    {
        Press,
        Release
    }

    /// <summary>
    /// GDD §3-4 değerlendirme kuralları; Unity'ye bağımlı değil (birim testli). Zaman şarkı zamanı (sn).
    /// - Basış: en yakın bekleyen nota, normal pencereler. Good'dan erken basış yok sayılır.
    /// - Hold bırakış: kuyruk sonuna göre 1.5x pencereler. Good'dan erken bırakış = kırılma (MISS).
    ///   Kuyruk sonu + Good geçtiği halde tutuluyorsa bırakış otomatik GOOD.
    /// - Başı kaçırılan hold: basış MISS + bırakış MISS.
    /// </summary>
    public class ChartJudge
    {
        private readonly JudgementWindows windows;
        private readonly ChartNote[] activeHolds = new ChartNote[NoteChart.LaneCount];
        private NoteChart chart;
        private int[] laneHead = new int[NoteChart.LaneCount];

        /// <summary>(nota, tür, sonuç, fark sn [negatif = erken])</summary>
        public event Action<ChartNote, JudgementKind, Judgement, double> Judged;

        public ChartJudge(JudgementWindows windows)
        {
            this.windows = windows;
        }

        public JudgementWindows Windows => windows;

        /// <summary>Şeritte şu an tutulan hold (yoksa null).</summary>
        public ChartNote ActiveHold(int lane) => activeHolds[lane];

        public void Load(NoteChart newChart)
        {
            chart = newChart;
            laneHead = new int[NoteChart.LaneCount];
            Array.Clear(activeHolds, 0, activeHolds.Length);
        }

        /// <summary>Her kare (ve her input'tan önce) çağrılır: kaçan notalar ve geç bırakılan hold'lar.</summary>
        public void Tick(double songTime)
        {
            if (chart == null) return;

            for (int lane = 0; lane < NoteChart.LaneCount; lane++)
            {
                var hold = activeHolds[lane];
                if (hold != null)
                {
                    hold.HeldUntilSec = Clamp(songTime, hold.TimeSec, hold.EndTimeSec);
                    double late = songTime - hold.EndTimeSec;
                    if (late > windows.ReleaseGoodSec)
                    {
                        // Kuyruk bitti, Good penceresi de geçti: oyuncuyu cezalandırma.
                        activeHolds[lane] = null;
                        hold.HeldUntilSec = hold.EndTimeSec;
                        JudgeRelease(hold, Judgement.Good, late);
                    }
                }

                CheckMisses(lane, songTime);
            }
        }

        public void Press(int lane, double songTime)
        {
            if (chart == null) return;
            Tick(songTime);

            if (activeHolds[lane] != null) return; // hold sürerken aynı şeride ikinci basış (ör. ikinci parmak)

            var list = chart.LaneNotes(lane);
            if (laneHead[lane] >= list.Count) return;

            var note = list[laneHead[lane]];
            double diff = songTime - note.TimeSec;
            if (diff < -windows.GoodSec) return; // çok erken: notayı tüketmez

            laneHead[lane]++;
            var result = windows.EvaluatePress(diff);
            note.Result = result;

            if (result == Judgement.Miss)
            {
                MissEntirely(note, diff);
                return;
            }

            if (note.IsHold)
            {
                note.State = NoteState.Holding;
                note.HeldUntilSec = Clamp(songTime, note.TimeSec, note.EndTimeSec);
                activeHolds[lane] = note;
            }
            else
            {
                note.State = NoteState.Hit;
            }

            Judged?.Invoke(note, JudgementKind.Press, result, diff);
        }

        public void Release(int lane, double songTime)
        {
            if (chart == null) return;
            Tick(songTime); // geç bırakma burada otomatik GOOD olmuş olabilir

            var hold = activeHolds[lane];
            if (hold == null) return;
            activeHolds[lane] = null;

            double diff = songTime - hold.EndTimeSec;
            hold.HeldUntilSec = Clamp(songTime, hold.TimeSec, hold.EndTimeSec);

            if (diff < -windows.ReleaseGoodSec)
            {
                // Kuyruk bitmeden çok erken bırakıldı: kırıldı. Kalan gövde gri akmaya devam eder.
                hold.ReleaseResult = Judgement.Miss;
                hold.State = NoteState.Missed;
                Judged?.Invoke(hold, JudgementKind.Release, Judgement.Miss, diff);
                return;
            }

            JudgeRelease(hold, windows.EvaluateRelease(diff), diff);
        }

        private void CheckMisses(int lane, double songTime)
        {
            var list = chart.LaneNotes(lane);
            while (laneHead[lane] < list.Count)
            {
                var note = list[laneHead[lane]];
                if (note.State != NoteState.Pending)
                {
                    laneHead[lane]++;
                    continue;
                }

                double late = songTime - note.TimeSec;
                if (late <= windows.GoodSec) break;

                laneHead[lane]++;
                note.Result = Judgement.Miss;
                MissEntirely(note, late);
            }
        }

        /// <summary>Basış MISS; hold ise bırakış da MISS (GDD §5: hold = 2 değerlendirme).</summary>
        private void MissEntirely(ChartNote note, double diff)
        {
            note.State = NoteState.Missed;
            Judged?.Invoke(note, JudgementKind.Press, Judgement.Miss, diff);
            if (!note.IsHold) return;
            note.ReleaseResult = Judgement.Miss;
            Judged?.Invoke(note, JudgementKind.Release, Judgement.Miss, diff);
        }

        private void JudgeRelease(ChartNote hold, Judgement result, double diff)
        {
            hold.ReleaseResult = result;
            hold.State = NoteState.Hit;
            Judged?.Invoke(hold, JudgementKind.Release, result, diff);
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
