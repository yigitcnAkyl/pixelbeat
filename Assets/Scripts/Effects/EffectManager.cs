using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Vuruş çizgisi üzerinde şerit bazlı piksel alevleri (orijinal flame burst'ler)
    /// ve neon kıvılcım efektlerini nesne havuzlama (Object Pooling) ile yöneten efekt yöneticisi.
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance { get; private set; }

        [Header("Cyberpunk Orijinal Alev Spriteları")]
        [SerializeField] private Sprite burstPerfectSprite;
        [SerializeField] private Sprite burstGreatSprite;
        [SerializeField] private Sprite burstGoodSprite;
        [SerializeField] private Sprite burstMissSprite;

        [Header("Efekt Prefabları (Opsiyonel)")]
        [SerializeField] private ParticleSystem customHitParticlePrefab;

        [Header("Havuzlama Ayarları")]
        [SerializeField] private int initialPoolSize = 12;

        private Queue<ParticleSystem> particlePool = new Queue<ParticleSystem>();
        private Transform poolContainer;
        private ParticleSystem defaultParticleTemplate;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadFlameSprites();
            InitPool();
        }

        private void LoadFlameSprites()
        {
#if UNITY_EDITOR
            if (burstPerfectSprite == null) burstPerfectSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Burst_Perfect_Pink.png");
            if (burstGreatSprite == null) burstGreatSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Burst_Great_Cyan.png");
            if (burstGoodSprite == null) burstGoodSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Burst_Good_Purple.png");
            if (burstMissSprite == null) burstMissSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Burst_Miss_Blue.png");
#endif
        }

        private void OnEnable()
        {
            HitDetector.OnHitJudged += HandleHitJudged;
        }

        private void OnDisable()
        {
            HitDetector.OnHitJudged -= HandleHitJudged;
        }

        private void InitPool()
        {
            poolContainer = new GameObject("[EffectPool]").transform;
            poolContainer.SetParent(this.transform);

            if (customHitParticlePrefab == null)
            {
                defaultParticleTemplate = CreateProceduralPixelBurstTemplate();
                defaultParticleTemplate.transform.SetParent(poolContainer);
                defaultParticleTemplate.gameObject.SetActive(false);
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                ParticleSystem ps = CreateNewParticleInstance();
                particlePool.Enqueue(ps);
            }
        }

        private ParticleSystem CreateNewParticleInstance()
        {
            ParticleSystem source = customHitParticlePrefab != null ? customHitParticlePrefab : defaultParticleTemplate;
            ParticleSystem instance = Instantiate(source, poolContainer);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void HandleHitJudged(HitDetector.HitResult result)
        {
            Vector3 hitPosition = GetLaneHitPosition(result.laneIndex);

            SpawnFlameSprite(hitPosition, result.accuracy);

            if (result.accuracy != HitAccuracy.Miss)
            {
                Color effectColor = GetEffectColor(result.accuracy);
                SpawnHitEffect(hitPosition, effectColor, result.accuracy == HitAccuracy.Perfect);
            }
        }

        private void SpawnFlameSprite(Vector3 position, HitAccuracy accuracy)
        {
            Sprite flame = null;
            switch (accuracy)
            {
                case HitAccuracy.Perfect: flame = burstPerfectSprite; break;
                case HitAccuracy.Great:   flame = burstGreatSprite; break;
                case HitAccuracy.Good:    flame = burstGoodSprite; break;
                case HitAccuracy.Miss:    flame = burstMissSprite; break;
            }
            if (flame == null) return;

            GameObject flameObj = new GameObject("HitFlame");
            flameObj.transform.position = position + new Vector3(0f, 0.35f, -0.1f);
            flameObj.transform.rotation = Quaternion.identity;
            flameObj.transform.localScale = Vector3.one * 1.15f;

            SpriteRenderer sr = flameObj.AddComponent<SpriteRenderer>();
            sr.sprite = flame;
            sr.sortingOrder = 15;
            Shader unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                           ?? Shader.Find("Sprites/Default");
            sr.material = new Material(unlit);

            StartCoroutine(AnimateFlameRoutine(flameObj, sr));
        }

        private IEnumerator AnimateFlameRoutine(GameObject obj, SpriteRenderer sr)
        {
            float elapsed = 0f;
            float duration = 0.25f;
            Vector3 initScale = obj.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (obj != null)
                {
                    obj.transform.localScale = initScale * Mathf.Lerp(1.0f, 1.35f, t);
                    Color c = Color.white;
                    c.a = Mathf.Lerp(1.0f, 0.0f, t);
                    sr.color = c;
                }
                yield return null;
            }
            if (obj != null) Destroy(obj);
        }

        public void SpawnHitEffect(Vector3 position, Color color, bool isPerfect)
        {
            if (particlePool.Count == 0)
            {
                particlePool.Enqueue(CreateNewParticleInstance());
            }

            ParticleSystem ps = particlePool.Dequeue();
            ps.transform.position = position;

            var main = ps.main;
            main.startColor = color;
            main.startSizeMultiplier = isPerfect ? 1.2f : 0.85f;

            ps.gameObject.SetActive(true);
            ps.Clear();
            ps.Play();

            StartCoroutine(ReturnToPoolRoutine(ps, main.duration));
        }

        private IEnumerator ReturnToPoolRoutine(ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay + 0.1f);
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
                particlePool.Enqueue(ps);
            }
        }

        private Vector3 GetLaneHitPosition(int laneIndex)
        {
            int idx = Mathf.Clamp(laneIndex, 0, 3);
            return new Vector3(HighwayVisuals.BottomLaneX[idx], HighwayVisuals.BottomLaneY, -0.1f);
        }

        private Color GetEffectColor(HitAccuracy accuracy)
        {
            switch (accuracy)
            {
                case HitAccuracy.Perfect:
                    return new Color(1.0f, 0.16f, 0.52f, 1.0f);
                case HitAccuracy.Great:
                    return new Color(0.0f, 0.95f, 1.0f, 1.0f);
                case HitAccuracy.Good:
                    return new Color(0.72f, 0.27f, 1.0f, 1.0f);
                default:
                    return Color.white;
            }
        }

        private ParticleSystem CreateProceduralPixelBurstTemplate()
        {
            GameObject obj = new GameObject("ProceduralPixelBurst");
            ParticleSystem ps = obj.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.30f;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Sprites/Default");
            renderer.material = new Material(unlit);
            renderer.sortingOrder = 16;

            return ps;
        }
    }
}
