using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RhythmGame
{
    /// <summary>
    /// 4 şeridin (Lane 0-3) 2.5D perspektifli yol üzerindeki koordinatlarını yöneten,
    /// notaları havuza alan/dağıtan ve tıklamaların asla boşa gitmemesini sağlayan şerit kontrolcüsü.
    /// </summary>
    public class LaneController : MonoBehaviour
    {
        public static LaneController Instance { get; private set; }

        [System.Serializable]
        public class Lane
        {
            public int laneIndex;
            public Transform spawnPoint;
            public Transform hitPoint;
            public Color laneColor = Color.cyan;

            [NonSerialized]
            public List<NoteObject> activeNotes = new List<NoteObject>();

            [NonSerialized]
            public NoteObject currentlyHeldNote = null;
        }

        [Header("Şerit Tanımlamaları (4 Şerit)")]
        [SerializeField] private Lane[] lanes = new Lane[4];

        [Header("Nota Prefab & Havuzlama (Pooling)")]
        [SerializeField] private NoteObject notePrefab;
        [SerializeField] private int initialPoolSize = 32;

        [Header("Vuruş Toleransı (Beat Cinsinden)")]
        [SerializeField] private float hitLookaheadBeats = 1.0f;

        private Queue<NoteObject> notePool = new Queue<NoteObject>();
        private Transform poolContainer;
        private Queue<NoteData> pendingNotesQueue = new Queue<NoteData>();

        public event Action<int, bool> OnLaneStateChanged;
        public event Action<NoteObject, HitAccuracy> OnNoteJudged;
        public event Action<NoteObject> OnNoteMissed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ValidateLanes();
            SyncLanesWith25DHighway();
            InitPool();
        }

        private void Start()
        {
            SetupDefaultCyberpunkColors();

            if (pendingNotesQueue.Count == 0)
            {
                if (Conductor.Instance != null && Conductor.Instance.AudioSource != null && Conductor.Instance.AudioSource.clip != null && BeatDetector.Instance != null)
                {
                    BeatDetector.Instance.GenerateChartFromClip(Conductor.Instance.AudioSource.clip);
                }
                else
                {
                    LoadDefaultTestChart();
                }
            }
        }

        private void Update()
        {
            CheckAndSpawnUpcomingNotes();
        }

        public void SyncLanesWith25DHighway()
        {
            for (int i = 0; i < 4; i++)
            {
                if (lanes[i].spawnPoint != null)
                {
                    lanes[i].spawnPoint.position = new Vector3(HighwayVisuals.TopLaneX[i], HighwayVisuals.TopLaneY, 0f);
                }
                if (lanes[i].hitPoint != null)
                {
                    lanes[i].hitPoint.position = new Vector3(HighwayVisuals.BottomLaneX[i], HighwayVisuals.BottomLaneY, 0f);
                }
            }
        }

        #region Havuzlama (Object Pooling)

        private void InitPool()
        {
            poolContainer = new GameObject("[NotePool]").transform;
            poolContainer.SetParent(this.transform);

            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewPoolNote();
            }
        }

        private NoteObject CreateNewPoolNote()
        {
            if (notePrefab == null)
            {
                Debug.LogError("[LaneController] NotePrefab atanmamış! Lütfen bir NoteObject prefabı bağlayın.");
                return null;
            }

            NoteObject note = Instantiate(notePrefab, poolContainer);
            note.gameObject.SetActive(false);
            notePool.Enqueue(note);
            return note;
        }

        private NoteObject GetNoteFromPool()
        {
            if (notePool.Count == 0)
            {
                CreateNewPoolNote();
            }

            return notePool.Dequeue();
        }

        private void ReturnNoteToPool(NoteObject note)
        {
            int laneIdx = note.Data.laneIndex;
            if (laneIdx >= 0 && laneIdx < lanes.Length)
            {
                lanes[laneIdx].activeNotes.Remove(note);
                if (lanes[laneIdx].currentlyHeldNote == note)
                {
                    lanes[laneIdx].currentlyHeldNote = null;
                }
            }

            notePool.Enqueue(note);
        }

        public void ClearActiveNotes()
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                var copy = new List<NoteObject>(lanes[i].activeNotes);
                foreach (var note in copy)
                {
                    if (note != null) note.Recycle();
                }
                lanes[i].activeNotes.Clear();
                lanes[i].currentlyHeldNote = null;
            }
        }

        #endregion

        #region Nota Doğurma (Spawning)

        public void LoadChart(List<NoteData> chart)
        {
            ClearActiveNotes();
            pendingNotesQueue.Clear();
            chart.Sort((a, b) => a.targetBeat.CompareTo(b.targetBeat));
            foreach (var note in chart)
            {
                pendingNotesQueue.Enqueue(note);
            }
        }

        private void CheckAndSpawnUpcomingNotes()
        {
            if (Conductor.Instance == null || !Conductor.Instance.IsPlaying || pendingNotesQueue.Count == 0) return;

            float currentBeat = Conductor.Instance.SongPositionInBeats;
            float approachBeats = Conductor.Instance.BeatsShownInAdvance;

            while (pendingNotesQueue.Count > 0 && currentBeat >= pendingNotesQueue.Peek().targetBeat - approachBeats)
            {
                NoteData nextNote = pendingNotesQueue.Dequeue();
                SpawnNote(nextNote);
            }
        }

        private void SpawnNote(NoteData noteData)
        {
            int laneIdx = noteData.laneIndex;
            if (laneIdx < 0 || laneIdx >= lanes.Length) return;

            Lane lane = lanes[laneIdx];
            Vector3 spawnPos = new Vector3(HighwayVisuals.TopLaneX[laneIdx], HighwayVisuals.TopLaneY, 0f);
            Vector3 hitPos = new Vector3(HighwayVisuals.BottomLaneX[laneIdx], HighwayVisuals.BottomLaneY, 0f);

            if (lane.spawnPoint != null) lane.spawnPoint.position = spawnPos;
            if (lane.hitPoint != null) lane.hitPoint.position = hitPos;

            NoteObject note = GetNoteFromPool();
            if (note == null) return;

            note.SetColor(lane.laneColor);
            note.Initialize(noteData, spawnPos, hitPos, Conductor.Instance.BeatsShownInAdvance, (recycledNote) =>
            {
                ReturnNoteToPool(recycledNote);
            });

            lane.activeNotes.Add(note);
        }

        #endregion

        #region Girdi & Vurulabilir Nota Arama

        public void HandleLanePress(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= lanes.Length) return;
            OnLaneStateChanged?.Invoke(laneIndex, true);
        }

        public void HandleLaneRelease(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= lanes.Length) return;
            OnLaneStateChanged?.Invoke(laneIndex, false);
        }

        /// <summary>
        /// Şeritte vurulmaya hazır (Approaching) ve o anki vuruşa en yakın notayı döner.
        /// Zaten tutulmakta olan (BeingHeld) veya çoktan kaçmış notaları kesinlikle atlar!
        /// </summary>
        public NoteObject GetEarliestHittableNote(int laneIndex, float currentBeat)
        {
            if (laneIndex < 0 || laneIndex >= lanes.Length) return null;

            var list = lanes[laneIndex].activeNotes;
            NoteObject bestNote = null;
            float bestDiff = float.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                NoteObject note = list[i];
                if (note == null || note.State != NoteObject.NoteState.Approaching) continue;

                // Vuruş çizgisini 0.40 beat'ten fazla geçmişse artık gecikmiştir, otomatik kaçır
                float overdue = currentBeat - note.Data.targetBeat;
                if (overdue > 0.40f)
                {
                    note.MarkMissed();
                    continue;
                }

                float diff = Mathf.Abs(note.Data.targetBeat - currentBeat);
                if (diff < bestDiff && diff <= hitLookaheadBeats)
                {
                    bestDiff = diff;
                    bestNote = note;
                }
            }

            return bestNote;
        }

        #endregion

        #region Yardımcı Metotlar

        private void ValidateLanes()
        {
            if (lanes == null || lanes.Length != 4)
            {
                lanes = new Lane[4];
                for (int i = 0; i < 4; i++)
                {
                    lanes[i] = new Lane { laneIndex = i };
                }
            }
        }

        private void SetupDefaultCyberpunkColors()
        {
            lanes[0].laneColor = new Color(1.0f, 0.16f, 0.52f, 1f);
            lanes[1].laneColor = new Color(0.0f, 0.95f, 1.0f, 1f);
            lanes[2].laneColor = new Color(0.72f, 0.27f, 1.0f, 1f);
            lanes[3].laneColor = new Color(0.25f, 0.55f, 1.0f, 1f);
        }

        private void LoadDefaultTestChart()
        {
            List<NoteData> testChart = new List<NoteData>
            {
                new NoteData(1, NoteType.Tap, 0, 2.0f),
                new NoteData(2, NoteType.Tap, 1, 3.0f),
                new NoteData(3, NoteType.Tap, 2, 4.0f),
                new NoteData(4, NoteType.Tap, 3, 5.0f),
                new NoteData(5, NoteType.Hold, 0, 6.0f, 2.0f),
                new NoteData(6, NoteType.Tap, 1, 9.0f),
                new NoteData(7, NoteType.Tap, 2, 10.0f),
                new NoteData(8, NoteType.Hold, 3, 11.0f, 2.0f)
            };
            LoadChart(testChart);
        }

        #endregion
    }
}
