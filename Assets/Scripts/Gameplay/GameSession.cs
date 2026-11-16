using System;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Bir şarkı oturumu (GDD §5): değerlendirmeleri ScoreKeeper'a iletir, can 0 olunca FAILED,
    /// şarkı bitince sonuç ekranı (TEKRAR / MENÜ butonları sonuç ekranında, GDD §11).
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public enum SessionState
        {
            /// <summary>Şarkı yok (kalibrasyon açık).</summary>
            Idle,
            Playing,
            Failed,
            Results
        }

        [SerializeField] private Conductor conductor;
        [SerializeField] private JudgementSystem judgementSystem;
        [SerializeField] private SongLoader songLoader;

        [Tooltip("Son değerlendirmeden sonra sonuç ekranına kadar bekleme (sn, şarkı zamanı).")]
        [SerializeField, Min(0f)] private float resultsDelaySec = 1f;

        private readonly ScoreKeeper keeper = new ScoreKeeper();
        private double completedAtSongTime;

        public ScoreKeeper Keeper => keeper;
        public SessionState State { get; private set; } = SessionState.Idle;

        /// <summary>Durum değişince (FAILED / sonuç ekranı açılışından beri geçen süre için, unscaled).</summary>
        public float StateChangedAt { get; private set; }

        public event Action<SessionState> StateChanged;

        private void OnEnable()
        {
            songLoader.SongStarted += OnSongStarted;
            songLoader.SongSuspended += OnSongSuspended;
            judgementSystem.NoteJudged += OnNoteJudged;
        }

        private void OnDisable()
        {
            songLoader.SongStarted -= OnSongStarted;
            songLoader.SongSuspended -= OnSongSuspended;
            judgementSystem.NoteJudged -= OnNoteJudged;
        }

        private void OnSongStarted(NoteChart chart)
        {
            keeper.Reset(ScoreKeeper.TotalJudgementsFor(chart));
            SetState(SessionState.Playing);
        }

        private void OnSongSuspended() => SetState(SessionState.Idle);

        private void OnNoteJudged(ChartNote note, JudgementKind kind, Judgement result, double diff)
        {
            if (State != SessionState.Playing) return;
            if (!keeper.Add(result)) return;

            if (keeper.IsFailed)
            {
                songLoader.StopPlayback(); // müzik durur, notalar yerinde donar
                SetState(SessionState.Failed);
                return;
            }

            if (keeper.IsComplete) completedAtSongTime = conductor.SongPositionSec;
        }

        private void Update()
        {
            if (State == SessionState.Playing && keeper.IsComplete &&
                conductor.SongPositionSec - completedAtSongTime >= resultsDelaySec)
            {
                songLoader.StopPlayback();
                SetState(SessionState.Results);
            }
        }

        private void SetState(SessionState state)
        {
            State = state;
            StateChangedAt = Time.unscaledTime;
            StateChanged?.Invoke(state);
        }
    }
}
