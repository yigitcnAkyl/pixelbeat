using System;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Vuruş zamanlama pencerelerini milisaniye hassasiyetinde hesaplayan,
    /// hiçbir tıklamanın ıskalanmamasını veya boşa gitmemesini sağlayan vuruş algılama sınıfı.
    /// </summary>
    public class HitDetector : MonoBehaviour
    {
        public static HitDetector Instance { get; private set; }

        [System.Serializable]
        public struct HitResult
        {
            public int laneIndex;
            public NoteType noteType;
            public HitAccuracy accuracy;
            public float offsetMilliseconds;
            public bool isEarly;
            public bool isHoldRelease;
            public int currentCombo;
            public int totalScore;
        }

        [Header("Zamanlama Tolerans Pencereleri (Milisaniye)")]
        [SerializeField] private float perfectWindowMs = 50f;
        [SerializeField] private float greatWindowMs = 100f;
        [SerializeField] private float goodWindowMs = 165f;
        [SerializeField] private float hitWindowMaxMs = 240f;

        [Header("Skor Değerleri")]
        [SerializeField] private int scorePerfect = 1000;
        [SerializeField] private int scoreGreat = 750;
        [SerializeField] private int scoreGood = 450;

        public static event Action<HitResult> OnHitJudged;
        public static event Action<int> OnComboChanged;
        public static event Action<int> OnScoreChanged;

        public static event Action OnHitPerfect;
        public static event Action OnHitGreat;
        public static event Action OnHitGood;
        public static event Action OnHitMiss;

        public int CurrentCombo { get; private set; }
        public int MaxCombo { get; private set; }
        public int TotalScore { get; private set; }

        private NoteObject[] activeHoldNotes = new NoteObject[4];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            InputManager.OnLaneDown += HandleLaneDown;
            InputManager.OnLaneUp += HandleLaneUp;
        }

        private void OnDisable()
        {
            InputManager.OnLaneDown -= HandleLaneDown;
            InputManager.OnLaneUp -= HandleLaneUp;
        }

        private void Update()
        {
            CheckHoldNotesAutoRelease();
        }

        #region Vuruş Değerlendirme (Key Down)

        private void HandleLaneDown(int laneIndex)
        {
            if (Conductor.Instance == null || !Conductor.Instance.IsPlaying || LaneController.Instance == null) return;

            float currentSongTimeSec = Conductor.Instance.SongPositionInSeconds;
            float currentBeat = Conductor.Instance.SongPositionInBeats;
            float secondsPerBeat = Conductor.Instance.SecondsPerBeat;

            // Şeritte vurulmaya en uygun notayı bul
            NoteObject candidate = LaneController.Instance.GetEarliestHittableNote(laneIndex, currentBeat);
            if (candidate == null) return;

            float targetTimeSec = candidate.Data.targetBeat * secondsPerBeat;
            float diffMs = (currentSongTimeSec - targetTimeSec) * 1000f;
            float absDiffMs = Mathf.Abs(diffMs);

            // Vuruş penceresi kontrolü (240ms içinde herhangi bir an basıldıysa mutlaka algılanır!)
            if (absDiffMs <= hitWindowMaxMs)
            {
                HitAccuracy accuracy = EvaluateTimingAccuracy(absDiffMs);

                if (candidate.Data.noteType == NoteType.Tap)
                {
                    candidate.MarkHit();
                    RegisterHit(laneIndex, NoteType.Tap, accuracy, diffMs, false);
                }
                else if (candidate.Data.noteType == NoteType.Hold)
                {
                    candidate.StartHold();
                    activeHoldNotes[laneIndex] = candidate;
                    RegisterHit(laneIndex, NoteType.Hold, accuracy, diffMs, false);
                }
            }
        }

        #endregion

        #region Hold Bırakma Değerlendirme (Key Up)

        private void HandleLaneUp(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= activeHoldNotes.Length) return;

            NoteObject heldNote = activeHoldNotes[laneIndex];
            if (heldNote == null) return;

            float secondsPerBeat = Conductor.Instance.SecondsPerBeat;
            float endTimeSec = heldNote.Data.EndBeat * secondsPerBeat;
            float currentSongTimeSec = Conductor.Instance.SongPositionInSeconds;

            float diffMs = (currentSongTimeSec - endTimeSec) * 1000f;
            float absDiffMs = Mathf.Abs(diffMs);

            activeHoldNotes[laneIndex] = null;

            // Bitişten 165ms'den daha erken bırakıldıysa kaçırıldı say
            if (diffMs < -goodWindowMs)
            {
                heldNote.ReleaseHold(false);
                RegisterMiss(laneIndex, NoteType.Hold, diffMs, true);
            }
            else
            {
                HitAccuracy releaseAccuracy = EvaluateTimingAccuracy(absDiffMs);
                heldNote.ReleaseHold(true);
                RegisterHit(laneIndex, NoteType.Hold, releaseAccuracy, diffMs, true);
            }
        }

        private void CheckHoldNotesAutoRelease()
        {
            if (Conductor.Instance == null || !Conductor.Instance.IsPlaying) return;

            float currentSongTimeSec = Conductor.Instance.SongPositionInSeconds;
            float secondsPerBeat = Conductor.Instance.SecondsPerBeat;

            for (int i = 0; i < activeHoldNotes.Length; i++)
            {
                NoteObject heldNote = activeHoldNotes[i];
                if (heldNote != null)
                {
                    float endTimeSec = heldNote.Data.EndBeat * secondsPerBeat;
                    // Bitiş çizgisine ulaşıldığında oyuncu basılı tutmaya devam ediyorsa otomatik Perfect tamamla
                    if (currentSongTimeSec >= endTimeSec)
                    {
                        activeHoldNotes[i] = null;
                        heldNote.ReleaseHold(true);
                        RegisterHit(i, NoteType.Hold, HitAccuracy.Perfect, 0f, true);
                    }
                }
            }
        }

        #endregion

        #region Zamanlama & Skor Hesaplama

        private HitAccuracy EvaluateTimingAccuracy(float absDiffMs)
        {
            if (absDiffMs <= perfectWindowMs) return HitAccuracy.Perfect;
            if (absDiffMs <= greatWindowMs) return HitAccuracy.Great;
            return HitAccuracy.Good;
        }

        private void RegisterHit(int laneIndex, NoteType noteType, HitAccuracy accuracy, float diffMs, bool isHoldRelease)
        {
            CurrentCombo++;
            if (CurrentCombo > MaxCombo) MaxCombo = CurrentCombo;

            int points = 0;
            switch (accuracy)
            {
                case HitAccuracy.Perfect: points = scorePerfect; break;
                case HitAccuracy.Great:   points = scoreGreat; break;
                case HitAccuracy.Good:    points = scoreGood; break;
            }
            TotalScore += points;

            HitResult result = new HitResult
            {
                laneIndex = laneIndex,
                noteType = noteType,
                accuracy = accuracy,
                offsetMilliseconds = diffMs,
                isEarly = diffMs < 0f,
                isHoldRelease = isHoldRelease,
                currentCombo = CurrentCombo,
                totalScore = TotalScore
            };

            OnHitJudged?.Invoke(result);
            OnComboChanged?.Invoke(CurrentCombo);
            OnScoreChanged?.Invoke(TotalScore);

            switch (accuracy)
            {
                case HitAccuracy.Perfect: OnHitPerfect?.Invoke(); break;
                case HitAccuracy.Great:   OnHitGreat?.Invoke(); break;
                case HitAccuracy.Good:    OnHitGood?.Invoke(); break;
            }
        }

        private void RegisterMiss(int laneIndex, NoteType noteType, float diffMs, bool isHoldRelease)
        {
            CurrentCombo = 0;

            HitResult result = new HitResult
            {
                laneIndex = laneIndex,
                noteType = noteType,
                accuracy = HitAccuracy.Miss,
                offsetMilliseconds = diffMs,
                isEarly = diffMs < 0f,
                isHoldRelease = isHoldRelease,
                currentCombo = 0,
                totalScore = TotalScore
            };

            OnHitJudged?.Invoke(result);
            OnComboChanged?.Invoke(0);
            OnHitMiss?.Invoke();
        }

        #endregion
    }
}
