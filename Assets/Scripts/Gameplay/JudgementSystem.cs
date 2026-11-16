using System;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// <see cref="ChartJudge"/>'ı input'a ve Conductor'a bağlar. Kurallar ChartJudge'da (GDD §3-4).
    /// </summary>
    public class JudgementSystem : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementWindows windows;
        [SerializeField] private LaneInput laneInput;

        private ChartJudge judge;
        private bool loaded;

        /// <summary>(nota, tür, sonuç, fark sn [negatif = erken])</summary>
        public event Action<ChartNote, JudgementKind, Judgement, double> NoteJudged;

        public JudgementWindows Windows => windows;

        /// <summary>Şeritte şu an tutulan hold var mı (şerit parlaması için).</summary>
        public bool IsHolding(int lane) => judge != null && judge.ActiveHold(lane) != null;

        private void Awake()
        {
            judge = new ChartJudge(windows);
            judge.Judged += (note, kind, result, diff) => NoteJudged?.Invoke(note, kind, result, diff);
        }

        private void OnEnable()
        {
            laneInput.LanePressed += OnLanePressed;
            laneInput.LaneReleased += OnLaneReleased;
        }

        private void OnDisable()
        {
            laneInput.LanePressed -= OnLanePressed;
            laneInput.LaneReleased -= OnLaneReleased;
        }

        public void Load(NoteChart newChart)
        {
            judge.Load(newChart);
            loaded = newChart != null;
        }

        private void Update()
        {
            if (!loaded || !conductor.IsPlaying) return;
            judge.Tick(conductor.SongPositionSec);
        }

        private void OnLanePressed(int lane, double songTime)
        {
            if (!loaded || !conductor.IsPlaying) return;
            judge.Press(lane, songTime);
        }

        private void OnLaneReleased(int lane, double songTime)
        {
            if (!loaded || !conductor.IsPlaying) return;
            judge.Release(lane, songTime);
        }
    }
}
