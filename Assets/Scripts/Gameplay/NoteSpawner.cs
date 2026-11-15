using System.Collections.Generic;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Notaları havuzdan alıp yukarıdan akıtır. Konum her karede şarkı pozisyonundan hesaplanır (GDD §6),
    /// hareket biriktirilmez.
    /// </summary>
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private Playfield playfield;

        [Tooltip("Nota hızı: bir notanın ekranın üstünden vuruş çizgisine gelme süresi (sn). Küçük = hızlı.")]
        [SerializeField, Range(0.3f, 3f)] private float scrollTimeSec = 1.0f;

        [SerializeField] private int noteHeightPx = 4;
        [SerializeField] private Color[] laneColors =
        {
            new Color32(0x33, 0xFF, 0xFF, 0xFF),
            new Color32(0xFF, 0x40, 0xC0, 0xFF),
            new Color32(0xFF, 0x40, 0xC0, 0xFF),
            new Color32(0x33, 0xFF, 0xFF, 0xFF),
        };
        [SerializeField] private Color missedColor = new Color32(0x50, 0x50, 0x68, 0xFF);

        private ObjectPool<NoteView> pool;
        private readonly List<NoteView> active = new List<NoteView>();
        private NoteChart chart;
        private int nextIndex;

        public int ActiveCount => active.Count;
        public int PoolInactiveCount => pool?.CountInactive ?? 0;

        private void Awake()
        {
            pool = new ObjectPool<NoteView>(
                createFunc: CreateView,
                actionOnGet: v => v.gameObject.SetActive(true),
                actionOnRelease: v =>
                {
                    v.Unbind();
                    v.gameObject.SetActive(false);
                },
                actionOnDestroy: v => Destroy(v.gameObject),
                collectionCheck: true,
                defaultCapacity: 32,
                maxSize: 256);
        }

        private NoteView CreateView()
        {
            var go = new GameObject("Note");
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<NoteView>();
            view.Init(playfield.PixelSprite, 5);
            return view;
        }

        public void Load(NoteChart newChart)
        {
            for (int i = active.Count - 1; i >= 0; i--) pool.Release(active[i]);
            active.Clear();
            chart = newChart;
            nextIndex = 0;
        }

        private void Update()
        {
            if (chart == null || !conductor.IsPlaying) return;

            double songTime = conductor.SongPositionSec;
            var notes = chart.Notes;

            while (nextIndex < notes.Count && notes[nextIndex].TimeSec - songTime <= scrollTimeSec)
            {
                var note = notes[nextIndex++];
                if (note.State == NoteState.Hit) continue;
                var view = pool.Get();
                view.Bind(note, laneColors[note.Lane]);
                active.Add(view);
            }

            int laneW = playfield.LaneWidthPx;
            float travelPx = playfield.SpawnYPx - playfield.HitLineYPx;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var view = active[i];
                var note = view.Note;

                if (note.State == NoteState.Hit)
                {
                    Release(i);
                    continue;
                }

                double t = (note.TimeSec - songTime) / scrollTimeSec;
                int centerY = Mathf.RoundToInt((float)(playfield.HitLineYPx + t * travelPx));
                int bottomY = centerY - noteHeightPx / 2;

                if (bottomY + noteHeightPx < playfield.DespawnYPx)
                {
                    Release(i);
                    continue;
                }

                if (note.State == NoteState.Missed) view.SetColor(missedColor);
                Playfield.PlaceRect(view.transform, playfield.LaneLeftPx(note.Lane) + 1, bottomY, laneW - 2, noteHeightPx,
                    playfield.PixelSprite.rect.width);
            }
        }

        private void Release(int activeIndex)
        {
            var view = active[activeIndex];
            active.RemoveAt(activeIndex);
            pool.Release(view);
        }
    }
}
