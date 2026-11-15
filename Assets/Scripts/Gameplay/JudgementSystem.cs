using System;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// GDD §4: basışları en yakın bekleyen notaya göre değerlendirir, kaçan notaları MISS yapar.
    /// </summary>
    public class JudgementSystem : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementWindows windows;
        [SerializeField] private LaneInput laneInput;

        private NoteChart chart;
        private int[] laneHead;

        /// <summary>(nota, sonuç, fark sn [negatif = erken])</summary>
        public event Action<ChartNote, Judgement, double> NoteJudged;

        public JudgementWindows Windows => windows;

        private void OnEnable()
        {
            laneInput.LanePressed += OnLanePressed;
        }

        private void OnDisable()
        {
            laneInput.LanePressed -= OnLanePressed;
        }

        public void Load(NoteChart newChart)
        {
            chart = newChart;
            laneHead = new int[NoteChart.LaneCount];
        }

        private void Update()
        {
            if (chart == null || !conductor.IsPlaying) return;
            CheckMisses(conductor.SongPositionSec);
        }

        private void CheckMisses(double songTime)
        {
            for (int lane = 0; lane < NoteChart.LaneCount; lane++)
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

                    Judge(note, Judgement.Miss, late);
                    laneHead[lane]++;
                }
            }
        }

        private void OnLanePressed(int lane, double songTime)
        {
            if (chart == null || !conductor.IsPlaying) return;

            CheckMisses(songTime);

            var list = chart.LaneNotes(lane);
            if (laneHead[lane] >= list.Count) return;

            var note = list[laneHead[lane]];
            double diff = songTime - note.TimeSec;

            // Good penceresinden erken basış notayı tüketmez.
            if (diff < -windows.GoodSec) return;

            Judge(note, windows.EvaluatePress(diff), diff);
            laneHead[lane]++;
        }

        private void Judge(ChartNote note, Judgement result, double diff)
        {
            note.Result = result;
            note.State = result == Judgement.Miss ? NoteState.Missed : NoteState.Hit;
            NoteJudged?.Invoke(note, result, diff);
        }
    }
}
