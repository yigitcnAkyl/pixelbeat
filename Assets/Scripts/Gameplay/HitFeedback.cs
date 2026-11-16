using PixelBeat.Core;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Vuruş geri bildirimi (GDD §8): şeritte vuruş çizgisinden yukarı additive parlama + küçük pixel parçacıkları.
    /// Perfect'te parlama beyaz, daha güçlü ve uzun; parçacıklar daha çok (beyaz + şerit rengi). Miss'te yok.
    /// Parçacıklar havuzlu SpriteRenderer'lardır (en fazla <see cref="MaxParticles"/>), konumları tam piksele yuvarlanır,
    /// renkleri sadece paletten (sönerken saydamlaşmaz, yanıp sönerek kaybolur).
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        public const int MaxParticles = 64;

        [SerializeField] private JudgementSystem judgementSystem;
        [SerializeField] private Playfield playfield;
        [SerializeField] private NoteSpawner noteSpawner;

        [Header("Şerit parlaması")]
        [SerializeField, Min(0.01f)] private float perfectFlashSec = 0.18f;
        [SerializeField, Min(0.01f)] private float flashSec = 0.12f;
        [SerializeField, Range(0f, 1f)] private float perfectFlashAlpha = 0.9f;
        [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.5f;
        [SerializeField, Min(8)] private int flashHeightPx = 48;

        [Header("Parçacıklar (piksel / sn)")]
        [SerializeField] private Vector2 upSpeed = new Vector2(40f, 110f);
        [SerializeField, Min(0f)] private float sideSpeed = 45f;
        [SerializeField, Min(0f)] private float gravity = 260f;
        [SerializeField] private Vector2 lifeSec = new Vector2(0.3f, 0.55f);

        private const int FlashOrder = 2;
        private const int ParticleOrder = 7;

        private SpriteRenderer[] flashes;
        private float[] flashTimers;
        private float[] flashDurations;
        private float[] flashPeak;

        private struct Particle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;
            public float Life;
        }

        private Particle[] particles;
        private SpriteRenderer[] particleRenderers;
        private int particleCursor;
        private System.Random rnd = new System.Random(1234);

        /// <summary>Değerlendirmeye göre parçacık sayısı (bırakışta yarısı).</summary>
        public static int ParticleCountFor(Judgement j, JudgementKind kind)
        {
            int n;
            switch (j)
            {
                case Judgement.Perfect: n = 10; break;
                case Judgement.Great: n = 6; break;
                case Judgement.Good: n = 3; break;
                default: n = 0; break;
            }
            return kind == JudgementKind.Release ? n / 2 : n;
        }

        private void Awake()
        {
            var skin = playfield.Skin;
            flashes = new SpriteRenderer[NoteChart.LaneCount];
            flashTimers = new float[flashes.Length];
            flashDurations = new float[flashes.Length];
            flashPeak = new float[flashes.Length];
            for (int i = 0; i < flashes.Length; i++)
            {
                flashes[i] = playfield.CreateSkinned($"Lane Flash {i}", transform, skin.LaneFlash(PaletteColor.White),
                    SpriteDrawMode.Tiled, 0, 0, 8, flashHeightPx, FlashOrder, additive: true);
                flashes[i].enabled = false;
            }

            particles = new Particle[MaxParticles];
            particleRenderers = new SpriteRenderer[MaxParticles];
            for (int i = 0; i < MaxParticles; i++)
            {
                var go = new GameObject($"Particle {i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = ParticleOrder;
                sr.enabled = false;
                particleRenderers[i] = sr;
            }
        }

        private void OnEnable() => judgementSystem.NoteJudged += OnJudged;

        private void OnDisable() => judgementSystem.NoteJudged -= OnJudged;

        private void OnJudged(ChartNote note, JudgementKind kind, Judgement result, double diff)
        {
            if (result == Judgement.Miss) return;
            int lane = note.Lane;
            bool perfect = result == Judgement.Perfect;
            var laneColor = noteSpawner.LanePalette(lane);

            // Şerit parlaması
            var skin = playfield.Skin;
            var flash = flashes[lane];
            flash.sprite = skin.LaneFlash(perfect ? PaletteColor.White : laneColor);
            Playfield.PlaceSized(flash, playfield.LaneLeftPx(lane) + 1, playfield.HitLineYPx, playfield.LaneWidthPx - 1, flashHeightPx);
            flashDurations[lane] = perfect ? perfectFlashSec : flashSec;
            flashTimers[lane] = flashDurations[lane];
            flashPeak[lane] = perfect ? perfectFlashAlpha : flashAlpha;
            flash.enabled = true;

            if (perfect || result == Judgement.Great) playfield.FlashHitLine(0.08f);

            // Parçacıklar
            int count = ParticleCountFor(result, kind);
            var origin = new Vector2(playfield.LaneCenterPx(lane), playfield.HitLineYPx + 1);
            for (int k = 0; k < count; k++)
            {
                var c = perfect && (k & 1) == 0 ? PaletteColor.White : laneColor;
                Spawn(origin, c, perfect ? 1.25f : 1f);
            }
        }

        private void Spawn(Vector2 origin, PaletteColor color, float speedScale)
        {
            int i = particleCursor;
            particleCursor = (particleCursor + 1) % MaxParticles;

            float Rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            particles[i] = new Particle
            {
                Pos = origin + new Vector2(Rand(-12f, 12f), 0f),
                Vel = new Vector2(Rand(-sideSpeed, sideSpeed), Rand(upSpeed.x, upSpeed.y)) * speedScale,
                Age = 0f,
                Life = Rand(lifeSec.x, lifeSec.y),
            };
            var sr = particleRenderers[i];
            sr.sprite = rnd.Next(3) == 0 ? playfield.Skin.Particle2 : playfield.Skin.Particle1;
            sr.color = Palette.Get(color);
            sr.forceRenderingOff = false;
            sr.enabled = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < flashes.Length; i++)
            {
                if (flashTimers[i] <= 0f) continue;
                flashTimers[i] -= dt;
                if (flashTimers[i] <= 0f)
                {
                    flashes[i].enabled = false;
                    continue;
                }
                flashes[i].color = new Color(1f, 1f, 1f, flashPeak[i] * flashTimers[i] / flashDurations[i]);
            }

            for (int i = 0; i < MaxParticles; i++)
            {
                var sr = particleRenderers[i];
                if (!sr.enabled) continue;

                ref var p = ref particles[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    sr.enabled = false;
                    continue;
                }
                p.Vel.y -= gravity * dt;
                p.Pos += p.Vel * dt;

                // Son %30'da yanıp sönerek kaybolur (saydamlık yok → sadece palet rengi).
                float t = p.Age / p.Life;
                bool visible = t < 0.7f || ((int)(p.Age * 30f) & 1) == 0;
                sr.forceRenderingOff = !visible;
                // Merkez pivotlu tek sayılı (1 px) sprite yarım piksel kaydırılır ki piksel ızgarasına otursun.
                float half = ((int)sr.sprite.rect.width & 1) == 1 ? 0.5f : 0f;
                sr.transform.localPosition = new Vector3((Mathf.Round(p.Pos.x) + half) / Playfield.PixelsPerUnit,
                    (Mathf.Round(p.Pos.y) + half) / Playfield.PixelsPerUnit, 0f);
            }
        }
    }
}
