using System;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// 2.5D perspektifli yolun üst kısmından alt vuruş tuşlarına doğru akan Tap ve Hold notası bileşeni.
    /// - Tap ve Hold başlığı: 1. görseldeki 2.5D perspektifli yassı oval butonlar (Note_0..3).
    /// - Hold gövdesi yukarıdan gelirken: 2. görselin üstündeki içi boş (Hollow) ray (HoldBody_Hollow_0..3).
    /// - Hold gövdesi basılı tutulurken: 2. görselin altındaki içi parıldayan neon lazer (HoldBody_Glowing_0..3) + enerji halkası!
    /// </summary>
    public class NoteObject : MonoBehaviour
    {
        public enum NoteState
        {
            Inactive,
            Approaching,
            BeingHeld,
            Completed,
            Missed
        }

        private const float TopPerspectiveScale = 0.85f;
        private const float BottomPerspectiveScale = 1.35f;

        [Header("Görsel Bileşenler")]
        [SerializeField] private SpriteRenderer headRenderer;
        [SerializeField] private SpriteRenderer tailRenderer;
        [SerializeField] private SpriteRenderer holdBodySpriteRenderer;
        [SerializeField] private SpriteRenderer holdRingRenderer;
        [SerializeField] private LineRenderer holdBodyRenderer;

        [Header("Cyberpunk Piksel Sprite Setleri")]
        [SerializeField] private Sprite[] laneSprites = new Sprite[4];
        [SerializeField] private Sprite[] holdHollowSprites = new Sprite[4];
        [SerializeField] private Sprite[] holdGlowingSprites = new Sprite[4];
        [SerializeField] private Sprite holdRingSprite;

        [Header("Tolerans Ayarları")]
        [SerializeField] private float missThresholdInBeats = 0.35f;

        public NoteData Data { get; private set; }
        public NoteState State { get; private set; } = NoteState.Inactive;

        private Vector3 spawnPosition;
        private Vector3 hitPosition;
        private float beatsShownInAdvance;
        private Action<NoteObject> despawnCallback;

        public bool IsBeingHeld => State == NoteState.BeingHeld;

        private void Awake()
        {
            EnsureChildRenderers();
            LoadDefaultNoteSpritesIfNeeded();
        }

        private void EnsureChildRenderers()
        {
            if (headRenderer == null)
            {
                Transform h = transform.Find("Head");
                if (h != null) headRenderer = h.GetComponent<SpriteRenderer>();
            }
            if (tailRenderer == null)
            {
                Transform t = transform.Find("Tail");
                if (t != null) tailRenderer = t.GetComponent<SpriteRenderer>();
            }

            Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                                 ?? Shader.Find("Sprites/Default");

            if (holdBodySpriteRenderer == null)
            {
                Transform b = transform.Find("HoldBodySprite");
                if (b == null)
                {
                    GameObject bodyObj = new GameObject("HoldBodySprite");
                    bodyObj.transform.SetParent(this.transform);
                    b = bodyObj.transform;
                }
                holdBodySpriteRenderer = b.GetComponent<SpriteRenderer>() ?? b.gameObject.AddComponent<SpriteRenderer>();
                holdBodySpriteRenderer.material = new Material(unlitShader);
                holdBodySpriteRenderer.sortingOrder = 8;
            }

            if (holdRingRenderer == null)
            {
                Transform r = transform.Find("HoldGlowRing");
                if (r == null)
                {
                    GameObject ringObj = new GameObject("HoldGlowRing");
                    ringObj.transform.SetParent(this.transform);
                    r = ringObj.transform;
                }
                holdRingRenderer = r.GetComponent<SpriteRenderer>() ?? r.gameObject.AddComponent<SpriteRenderer>();
                holdRingRenderer.material = new Material(unlitShader);
                holdRingRenderer.sortingOrder = 12;
                holdRingRenderer.enabled = false;
            }

            if (holdBodyRenderer == null)
            {
                holdBodyRenderer = GetComponent<LineRenderer>();
            }
            if (holdBodyRenderer != null)
            {
                holdBodyRenderer.enabled = false;
            }
        }

        private void LoadDefaultNoteSpritesIfNeeded()
        {
#if UNITY_EDITOR
            if (laneSprites == null || laneSprites.Length != 4) laneSprites = new Sprite[4];
            if (holdHollowSprites == null || holdHollowSprites.Length != 4) holdHollowSprites = new Sprite[4];
            if (holdGlowingSprites == null || holdGlowingSprites.Length != 4) holdGlowingSprites = new Sprite[4];

            string[] notePaths = new string[]
            {
                "Assets/Sprites/Sliced/Gameplay/Note_0_Square.png",
                "Assets/Sprites/Sliced/Gameplay/Note_1_Triangle.png",
                "Assets/Sprites/Sliced/Gameplay/Note_2_Cross.png",
                "Assets/Sprites/Sliced/Gameplay/Note_3_Circle.png"
            };
            string[] hollowPaths = new string[]
            {
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_0.png",
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_1.png",
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_2.png",
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_3.png"
            };
            string[] glowingPaths = new string[]
            {
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_0.png",
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_1.png",
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_2.png",
                "Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_3.png"
            };

            for (int i = 0; i < 4; i++)
            {
                if (laneSprites[i] == null) laneSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(notePaths[i]);
                if (holdHollowSprites[i] == null) holdHollowSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(hollowPaths[i]);
                if (holdGlowingSprites[i] == null) holdGlowingSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(glowingPaths[i]);
            }

            if (holdRingSprite == null)
            {
                holdRingSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldRing_Glow.png");
            }
#endif
        }

        public void Initialize(NoteData data, Vector3 spawn, Vector3 hit, float approachBeats, Action<NoteObject> onDespawn)
        {
            EnsureChildRenderers();
            LoadDefaultNoteSpritesIfNeeded();

            this.Data = data;
            this.spawnPosition = spawn;
            this.hitPosition = hit;
            this.beatsShownInAdvance = Mathf.Max(0.1f, approachBeats);
            this.despawnCallback = onDespawn;

            State = NoteState.Approaching;
            gameObject.SetActive(true);

            SetupVisualComponents();
            UpdatePositionAndVisuals();
        }

        private void SetupVisualComponents()
        {
            int idx = Mathf.Clamp(Data.laneIndex, 0, 3);

            if (laneSprites != null && laneSprites[idx] != null)
            {
                if (headRenderer != null)
                {
                    headRenderer.sprite = laneSprites[idx];
                    headRenderer.color = Color.white;
                    headRenderer.sortingOrder = 10;
                }
                if (tailRenderer != null)
                {
                    tailRenderer.sprite = laneSprites[idx];
                    tailRenderer.color = Color.white;
                    tailRenderer.sortingOrder = 10;
                }
            }

            if (holdRingRenderer != null)
            {
                holdRingRenderer.sprite = holdRingSprite;
                holdRingRenderer.enabled = false;
            }

            if (Data.noteType == NoteType.Hold)
            {
                if (holdBodySpriteRenderer != null)
                {
                    // Yukarıdan gelirken içi boş (Hollow) ray görünümü
                    holdBodySpriteRenderer.sprite = holdHollowSprites[idx];
                    holdBodySpriteRenderer.color = Color.white;
                    holdBodySpriteRenderer.sortingOrder = 8;
                    holdBodySpriteRenderer.enabled = true;
                }
                if (tailRenderer != null) tailRenderer.enabled = true;
            }
            else
            {
                if (holdBodySpriteRenderer != null) holdBodySpriteRenderer.enabled = false;
                if (tailRenderer != null) tailRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (State == NoteState.Inactive || Conductor.Instance == null) return;

            UpdatePositionAndVisuals();
            CheckAutoMiss();
        }

        public void UpdatePositionAndVisuals()
        {
            float currentBeat = Conductor.Instance.SongPositionInBeats;
            float tHead = (Data.targetBeat - currentBeat) / beatsShownInAdvance;

            float headScale;
            if (State == NoteState.BeingHeld)
            {
                transform.position = hitPosition;
                headScale = BottomPerspectiveScale;
                if (headRenderer != null)
                {
                    headRenderer.transform.localScale = Vector3.one * headScale;
                    headRenderer.enabled = true;
                }
            }
            else
            {
                transform.position = Vector3.LerpUnclamped(hitPosition, spawnPosition, tHead);
                headScale = Mathf.Lerp(BottomPerspectiveScale, TopPerspectiveScale, Mathf.Clamp01(tHead));
                if (headRenderer != null)
                {
                    headRenderer.transform.localScale = Vector3.one * headScale;
                    headRenderer.enabled = (tHead <= 1.02f);
                }
            }

            if (Data.noteType == NoteType.Hold)
            {
                float tTail = (Data.EndBeat - currentBeat) / beatsShownInAdvance;
                float tTailClamped = Mathf.Min(tTail, 1.0f);
                Vector3 headPos = (State == NoteState.BeingHeld)
                    ? hitPosition
                    : Vector3.LerpUnclamped(hitPosition, spawnPosition, Mathf.Min(tHead, 1.0f));
                Vector3 tailPos = Vector3.LerpUnclamped(hitPosition, spawnPosition, tTailClamped);

                float tailScale = Mathf.Lerp(BottomPerspectiveScale, TopPerspectiveScale, Mathf.Clamp01(tTailClamped));

                if (tailRenderer != null)
                {
                    tailRenderer.transform.position = tailPos;
                    tailRenderer.transform.localScale = Vector3.one * (tailScale * 0.90f);
                    tailRenderer.enabled = (tTail <= 1.02f && tTail > 0.0f);
                }

                // Hold gövde sprite'ını Head ve Tail arasında 2.5D eğimli şerit açısıyla uzat
                if (holdBodySpriteRenderer != null)
                {
                    bool visible = (tHead <= 1.02f && tTail > 0.0f);
                    holdBodySpriteRenderer.enabled = visible;

                    if (visible && holdBodySpriteRenderer.sprite != null)
                    {
                        Vector3 dir = tailPos - headPos;
                        float dist = dir.magnitude;
                        holdBodySpriteRenderer.transform.position = (headPos + tailPos) * 0.5f;

                        if (dist > 0.001f)
                        {
                            float angleZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                            holdBodySpriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angleZ);
                        }

                        float spriteH = Mathf.Max(0.05f, holdBodySpriteRenderer.sprite.bounds.size.y);
                        float scaleY = dist / spriteH;
                        float scaleX = ((headScale + tailScale) * 0.5f) * (State == NoteState.BeingHeld ? 1.25f : 1.05f);

                        // Basılı tutulurken hafif parlama titreşimi (pulse) ver
                        if (State == NoteState.BeingHeld)
                        {
                            float pulse = 1.0f + Mathf.Sin(Time.time * 28f) * 0.12f;
                            scaleX *= pulse;
                        }

                        holdBodySpriteRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                    }
                }

                // Basılı tutulurken alt tuşun etrafında 2. görseldeki parıldayan halka (HoldRing_Glow) dönsün
                if (holdRingRenderer != null)
                {
                    if (State == NoteState.BeingHeld && holdRingSprite != null)
                    {
                        holdRingRenderer.enabled = true;
                        holdRingRenderer.transform.position = hitPosition + new Vector3(0f, 0f, -0.05f);
                        float ringPulse = 1.15f + Mathf.Sin(Time.time * 22f) * 0.15f;
                        holdRingRenderer.transform.localScale = Vector3.one * ringPulse;
                        holdRingRenderer.transform.Rotate(0f, 0f, 180f * Time.deltaTime);
                    }
                    else
                    {
                        holdRingRenderer.enabled = false;
                    }
                }

                if (State == NoteState.BeingHeld && currentBeat >= Data.EndBeat)
                {
                    CompleteHoldNote();
                }
            }
        }

        private void CheckAutoMiss()
        {
            if (State != NoteState.Approaching) return;

            float currentBeat = Conductor.Instance.SongPositionInBeats;
            float overdue = currentBeat - Data.targetBeat;

            if (overdue > missThresholdInBeats)
            {
                MarkMissed();
            }
        }

        /// <summary>
        /// Hold notasına basılmaya başlandığında içi boş gövdeyi anında 2. görseldeki parıldayan neon lazere çevirir!
        /// </summary>
        public void StartHold()
        {
            if (Data.noteType != NoteType.Hold || State != NoteState.Approaching) return;
            State = NoteState.BeingHeld;

            int idx = Mathf.Clamp(Data.laneIndex, 0, 3);
            if (holdBodySpriteRenderer != null && holdGlowingSprites != null && holdGlowingSprites[idx] != null)
            {
                holdBodySpriteRenderer.sprite = holdGlowingSprites[idx];
                holdBodySpriteRenderer.sortingOrder = 9;
            }
        }

        public void ReleaseHold(bool isSuccessfulEnd = false)
        {
            if (State != NoteState.BeingHeld) return;

            if (isSuccessfulEnd || Conductor.Instance.SongPositionInBeats >= Data.EndBeat - missThresholdInBeats)
            {
                CompleteHoldNote();
            }
            else
            {
                MarkMissed();
            }
        }

        private void CompleteHoldNote()
        {
            State = NoteState.Completed;
            Recycle();
        }

        public void MarkHit()
        {
            State = NoteState.Completed;
            Recycle();
        }

        public void MarkMissed()
        {
            State = NoteState.Missed;
            Recycle();
        }

        public void Recycle()
        {
            State = NoteState.Inactive;
            if (holdRingRenderer != null) holdRingRenderer.enabled = false;
            gameObject.SetActive(false);
            despawnCallback?.Invoke(this);
        }

        public void SetColor(Color color)
        {
            // Orijinal piksel sanatının kendi renklerini koruyoruz
        }
    }
}
