using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// 2.5D perspektifli Cyberpunk yol görselini (Track_Highway.png) ve yol üzerindeki
    /// 4 adet basılmamış/basılmış vuruş butonlarını yöneten görsel sınıf.
    /// Eski sürümlerden kalan tüm çizgileri, quad'ları ve çakışan objeleri sahnede otomatik temizler.
    /// </summary>
    public class HighwayVisuals : MonoBehaviour
    {
        // 2.5D Yol Şerit Koordinatları (Track_Highway.png 300x264, PPU=32)
        public static readonly float[] TopLaneX = new float[] { -1.41f, -0.47f, 0.47f, 1.41f };
        public static readonly float[] BottomLaneX = new float[] { -2.70f, -0.91f, 0.92f, 2.70f };
        public const float TopLaneY = 3.50f;
        public const float BottomLaneY = -2.50f;

        [Header("Orijinal Cyberpunk Piksel Görselleri")]
        [SerializeField] private Sprite highwaySprite;
        [SerializeField] private Sprite[] unpressedPadSprites = new Sprite[4];
        [SerializeField] private Sprite[] pressedPadSprites = new Sprite[4];

        private SpriteRenderer highwayRenderer;
        private SpriteRenderer[] padRenderers = new SpriteRenderer[4];

        private void Awake()
        {
            CleanLeftoverSceneObjects();
            LoadSpritesIfNeeded();
        }

        private void Start()
        {
            CleanLeftoverSceneObjects();
            ConfigureCameraFor25DPixelRoad();
            SetupHighway();

            if (LaneController.Instance != null)
            {
                LaneController.Instance.OnLaneStateChanged += HandleLaneInputVisual;
            }

            AudioVisualizer.OnBassPulse += HandleBassPulse;
        }

        private void OnDestroy()
        {
            if (LaneController.Instance != null)
            {
                LaneController.Instance.OnLaneStateChanged -= HandleLaneInputVisual;
            }
            AudioVisualizer.OnBassPulse -= HandleBassPulse;
        }

        /// <summary>
        /// Sahnede önceden kaydedilmiş eski çizgi (LineRenderer), 3D quad ve KeyLabel kalıntılarını tamamen siler.
        /// </summary>
        public static void CleanLeftoverSceneObjects()
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go == null) continue;
                string n = go.name;
                if (n.StartsWith("Divider_") || n == "HitLine" || n == "TrackGround" || n == "KeyLabel" || n == "ProceduralPixelBurst")
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
            }
        }

        private void LoadSpritesIfNeeded()
        {
#if UNITY_EDITOR
            if (highwaySprite == null)
            {
                highwaySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Track_Highway.png");
            }

            string[] unpressedPaths = new string[]
            {
                "Assets/Sprites/Sliced/Gameplay/HitPad_0_Square.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_1_Triangle.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_2_Cross.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_3_Circle.png"
            };

            string[] pressedPaths = new string[]
            {
                "Assets/Sprites/Sliced/Gameplay/HitPad_Pressed_0_Square.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_Pressed_1_Triangle.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_Pressed_2_Cross.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_Pressed_3_Circle.png"
            };

            if (unpressedPadSprites == null || unpressedPadSprites.Length != 4) unpressedPadSprites = new Sprite[4];
            if (pressedPadSprites == null || pressedPadSprites.Length != 4) pressedPadSprites = new Sprite[4];

            for (int i = 0; i < 4; i++)
            {
                if (unpressedPadSprites[i] == null)
                    unpressedPadSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(unpressedPaths[i]);
                if (pressedPadSprites[i] == null)
                    pressedPadSprites[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(pressedPaths[i]);
            }
#endif
        }

        public static void ConfigureCameraFor25DPixelRoad()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            cam.orthographicSize = 4.2f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.16f, 1f);
        }

        public void SetupHighway()
        {
            CleanLeftoverSceneObjects();
            LoadSpritesIfNeeded();

            Transform existing = transform.Find("GeneratedHighway");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            GameObject highwayRoot = new GameObject("GeneratedHighway");
            highwayRoot.transform.SetParent(this.transform);
            highwayRoot.transform.localPosition = Vector3.zero;
            highwayRoot.transform.localRotation = Quaternion.identity;

            Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                                  ?? Shader.Find("Sprites/Default");
            Material spriteMat = new Material(spriteShader);

            // 1. 2.5D Cyberpunk Yol (Track_Highway.png)
            GameObject groundObj = new GameObject("TrackHighway_25D");
            groundObj.transform.SetParent(highwayRoot.transform);
            groundObj.transform.position = new Vector3(0f, 0f, 1f);
            groundObj.transform.rotation = Quaternion.identity;
            groundObj.transform.localScale = Vector3.one;

            highwayRenderer = groundObj.AddComponent<SpriteRenderer>();
            highwayRenderer.sprite = highwaySprite;
            highwayRenderer.material = spriteMat;
            highwayRenderer.color = Color.white;
            highwayRenderer.sortingOrder = -10;

            // 2. Yolun Üzerindeki 4 Vuruş Butonu (Image 2)
            padRenderers = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject padObj = new GameObject($"HitPad_{i}");
                padObj.transform.SetParent(highwayRoot.transform);
                padObj.transform.position = new Vector3(BottomLaneX[i], BottomLaneY, 0f);
                padObj.transform.rotation = Quaternion.identity;
                padObj.transform.localScale = Vector3.one;

                SpriteRenderer sr = padObj.AddComponent<SpriteRenderer>();
                sr.sprite = (unpressedPadSprites != null && i < unpressedPadSprites.Length) ? unpressedPadSprites[i] : null;
                sr.material = spriteMat;
                sr.color = Color.white;
                sr.sortingOrder = 5;

                padRenderers[i] = sr;
            }
        }

        private void HandleLaneInputVisual(int laneIndex, bool isPressed)
        {
            if (padRenderers == null || laneIndex < 0 || laneIndex >= padRenderers.Length) return;
            SpriteRenderer sr = padRenderers[laneIndex];
            if (sr == null) return;

            if (isPressed)
            {
                if (pressedPadSprites != null && laneIndex < pressedPadSprites.Length && pressedPadSprites[laneIndex] != null)
                {
                    sr.sprite = pressedPadSprites[laneIndex];
                }
            }
            else
            {
                if (unpressedPadSprites != null && laneIndex < unpressedPadSprites.Length && unpressedPadSprites[laneIndex] != null)
                {
                    sr.sprite = unpressedPadSprites[laneIndex];
                }
            }
        }

        private void HandleBassPulse(float intensity)
        {
            if (highwayRenderer != null)
            {
                float b = Mathf.Lerp(0.95f, 1.05f, intensity);
                highwayRenderer.color = new Color(b, b, 1f, 1f);
            }
        }
    }
}
