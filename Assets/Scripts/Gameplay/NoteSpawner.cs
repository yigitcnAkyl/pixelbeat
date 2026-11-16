using System.Collections.Generic;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Notaları havuzdan alıp yukarıdan akıtır. Konum her karede şarkı pozisyonundan hesaplanır (GDD §6),
    /// hareket biriktirilmez. Hold gövdesi tutulurken vuruş çizgisinde "yenir"; kırılırsa kalan parça gri akar.
    /// </summary>
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] private Conductor conductor;
        [SerializeField] private Playfield playfield;

        [Tooltip("Nota hızı: bir notanın ekranın üstünden vuruş çizgisine gelme süresi (sn). Küçük = hızlı.")]
        [SerializeField, Range(0.3f, 3f)] private float scrollTimeSec = 1.0f;

        [Tooltip("Nota başı yüksekliği (piksel) — NeonSkin nota sprite'ı 6 px.")]
        [SerializeField] private int noteHeightPx = 6;
        [Tooltip("Hold gövdesinin şerit kenarlarından içeri girintisi (piksel).")]
        [SerializeField] private int holdBodyInsetPx = 9;
        [SerializeField] private int holdTailHeightPx = 3;
        [Tooltip("Şerit renkleri (paletten; NeonSkin'de bu renklerin sprite'ları üretilmiş olmalı).")]
        [SerializeField] private PaletteColor[] laneColors =
        {
            PaletteColor.Cyan,
            PaletteColor.Magenta,
            PaletteColor.Magenta,
            PaletteColor.Cyan,
        };
        [SerializeField] private PaletteColor missedColor = PaletteColor.Ash;
        [Tooltip("Nota halesinin (additive) gücü.")]
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.35f;

        private ObjectPool<NoteView> pool;
        private readonly List<NoteView> active = new List<NoteView>();
        private NoteChart chart;
        private int nextIndex;

        public int ActiveCount => active.Count;
        public int PoolInactiveCount => pool?.CountInactive ?? 0;
        public float ScrollTimeSec => scrollTimeSec;

        public Color LaneColor(int lane) => Palette.Get(laneColors[lane]);
        public PaletteColor LanePalette(int lane) => laneColors[lane];

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
            view.Init(playfield.Skin, 5, glowAlpha);
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
                view.Bind(note);
                active.Add(view);
            }

            int laneW = playfield.LaneWidthPx;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var view = active[i];
                var note = view.Note;

                // Tamamlanan notalar (vurulan tap, doğru bırakılan hold) kaybolur.
                if (note.State == NoteState.Hit)
                {
                    Release(i);
                    continue;
                }

                // Hold'da baş, gövdenin henüz yenmemiş alt ucundadır (tutulurken vuruş çizgisinde).
                // Tutulurken bu karenin zamanı kullanılır (JudgementSystem'in Update sırasından bağımsız).
                double headTime = !note.IsHold ? note.TimeSec
                    : note.State == NoteState.Holding ? System.Math.Min(System.Math.Max(songTime, note.TimeSec), note.EndTimeSec)
                    : note.HeldUntilSec;
                int headY = YAt(headTime, songTime);
                int tailY = note.IsHold ? YAt(note.EndTimeSec, songTime) : headY;

                if (Mathf.Max(headY, tailY) + noteHeightPx < playfield.DespawnYPx)
                {
                    Release(i);
                    continue;
                }

                bool missed = note.State == NoteState.Missed;
                view.SetColor(missed ? missedColor : laneColors[note.Lane], missed);
                view.Place(playfield.LaneLeftPx(note.Lane), laneW, headY, noteHeightPx, tailY, holdBodyInsetPx, holdTailHeightPx);
            }
        }

        private int YAt(double noteTimeSec, double songTime)
        {
            float travelPx = playfield.SpawnYPx - playfield.HitLineYPx;
            double t = (noteTimeSec - songTime) / scrollTimeSec;
            return Mathf.RoundToInt((float)(playfield.HitLineYPx + t * travelPx));
        }

        private void Release(int activeIndex)
        {
            var view = active[activeIndex];
            active.RemoveAt(activeIndex);
            pool.Release(view);
        }
    }
}
