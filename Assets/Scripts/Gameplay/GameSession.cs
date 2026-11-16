using System;
using PixelBeat.Core;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Bir şarkı oturumu (GDD §5): değerlendirmeleri ScoreKeeper'a iletir, can 0 olunca FAILED,
    /// şarkı bitince sonuç ekranı. Bu ekranlarda dokunmak (ya da R) şarkıyı baştan başlatır.
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
        [SerializeField] private LaneInput laneInput;

        [Tooltip("Son değerlendirmeden sonra sonuç ekranına kadar bekleme (sn, şarkı zamanı).")]
        [SerializeField, Min(0f)] private float resultsDelaySec = 1f;
        [Tooltip("FAILED / sonuç ekranı açıldıktan sonra bu süre dokunuşlar yok sayılır (yanlışlıkla yeniden başlatmayı önler).")]
        [SerializeField, Min(0f)] private float restartGuardSec = 0.6f;

        private readonly ScoreKeeper keeper = new ScoreKeeper();
        private double completedAtSongTime;
        private float stateChangedAt;

        public ScoreKeeper Keeper => keeper;
        public SessionState State { get; private set; } = SessionState.Idle;

        public event Action<SessionState> StateChanged;

        /// <summary>Dokunuşun yeniden başlatma saymayacağı alanlar (ör. üstteki CAL / R butonları).</summary>
        public Func<Vector2, bool> IgnoreTouchAt { get; set; }

        private void OnEnable()
        {
            songLoader.SongStarted += OnSongStarted;
            songLoader.SongSuspended += OnSongSuspended;
            judgementSystem.NoteJudged += OnNoteJudged;
            laneInput.TouchStarted += OnTouch;
        }

        private void OnDisable()
        {
            songLoader.SongStarted -= OnSongStarted;
            songLoader.SongSuspended -= OnSongSuspended;
            judgementSystem.NoteJudged -= OnNoteJudged;
            laneInput.TouchStarted -= OnTouch;
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

        private void OnTouch(Vector2 px, double songTime)
        {
            if (State != SessionState.Failed && State != SessionState.Results) return;
            if (Time.unscaledTime - stateChangedAt < restartGuardSec) return;
            if (IgnoreTouchAt != null && IgnoreTouchAt(px)) return;
            songLoader.Restart();
        }

        private void SetState(SessionState state)
        {
            State = state;
            stateChangedAt = Time.unscaledTime;
            StateChanged?.Invoke(state);
        }
    }
}
