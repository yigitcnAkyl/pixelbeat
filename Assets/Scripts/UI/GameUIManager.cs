using UnityEngine;
using TMPro;

namespace RhythmGame
{
    /// <summary>
    /// Ekranın sağ üstündeki çapraz Kombo & Çarpan arayüzünü ve sol üstteki
    /// animasyonlu vuruş değerlendirme (PERFECT, GREAT, GOOD, MISS) metinlerini yöneten UI sınıfı.
    /// Ana menü açıkken gizlenir ve hiçbir başka UI ile üst üste binmez.
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        public static GameUIManager Instance { get; private set; }

        [Header("TextMeshPro UI Referansları (Opsiyonel Canvas Bağlantısı)")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text multiplierText;
        [SerializeField] private TMP_Text judgementText;

        [Header("Görsel Renk Paleti (Cyberpunk UI Renkleri)")]
        [SerializeField] private Color colorPerfect = new Color(1.0f, 0.16f, 0.52f, 1.0f); // #FF2A85: Neon Magenta
        [SerializeField] private Color colorGreat = new Color(0.0f, 0.95f, 1.0f, 1.0f);   // #00F2FF: Neon Cyan
        [SerializeField] private Color colorGood = new Color(0.72f, 0.27f, 1.0f, 1.0f);   // #B844FF: Elektrik Mor
        [SerializeField] private Color colorMiss = new Color(0.45f, 0.55f, 0.75f, 1.0f);  // Siber Çelik Mavi/Gri

        [Header("Dahili Arayüz Gösterimi")]
        [SerializeField] private bool enableBuiltInOverlay = true;

        private string currentJudgementStr = "";
        private Color currentJudgementColor = Color.white;
        private float judgementTimer = 0f;
        private float judgementScale = 1.0f;
        private float judgementYOffset = 0f;

        private int displayedScore = 0;
        private int displayedCombo = 0;
        private int displayedMultiplier = 1;
        private float comboPulseScale = 1.0f;

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
            ScoreManager.OnScoreUpdated += HandleScoreUpdated;
            ScoreManager.OnComboUpdated += HandleComboUpdated;
            ScoreManager.OnHitProcessed += HandleHitProcessed;
        }

        private void OnDisable()
        {
            ScoreManager.OnScoreUpdated -= HandleScoreUpdated;
            ScoreManager.OnComboUpdated -= HandleComboUpdated;
            ScoreManager.OnHitProcessed -= HandleHitProcessed;
        }

        private void Update()
        {
            UpdateJudgementAnimation();
            comboPulseScale = Mathf.Lerp(comboPulseScale, 1.0f, Time.deltaTime * 12f);
        }

        private void HandleScoreUpdated(int totalScore, int addedPoints)
        {
            displayedScore = totalScore;
            if (scoreText != null)
            {
                scoreText.text = $"SKOR: {displayedScore:N0}";
            }
        }

        private void HandleComboUpdated(int currentCombo, int multiplier)
        {
            if (currentCombo > displayedCombo)
            {
                comboPulseScale = 1.38f; // Başarılı vuruşta büyüme (pulse) efekti!
            }
            else if (currentCombo == 0 && displayedCombo > 0)
            {
                comboPulseScale = 0.85f; // Kombo kaçırılınca hafif küçülme
            }

            displayedCombo = currentCombo;
            displayedMultiplier = multiplier;

            if (comboText != null)
            {
                comboText.text = currentCombo > 0 ? $"{currentCombo} COMBO" : "";
            }

            if (multiplierText != null)
            {
                multiplierText.text = multiplier > 1 ? $"x{multiplier}" : "";
            }
        }

        private void HandleHitProcessed(HitAccuracy accuracy, int addedPoints, int currentCombo)
        {
            judgementTimer = 0.85f;
            judgementYOffset = 0f;

            switch (accuracy)
            {
                case HitAccuracy.Perfect:
                    currentJudgementStr = "PERFECT!";
                    currentJudgementColor = colorPerfect;
                    judgementScale = 1.35f;
                    break;

                case HitAccuracy.Great:
                    currentJudgementStr = "GREAT";
                    currentJudgementColor = colorGreat;
                    judgementScale = 1.18f;
                    break;

                case HitAccuracy.Good:
                    currentJudgementStr = "GOOD";
                    currentJudgementColor = colorGood;
                    judgementScale = 1.08f;
                    break;

                case HitAccuracy.Miss:
                    currentJudgementStr = "MISS";
                    currentJudgementColor = colorMiss;
                    judgementScale = 1.0f;
                    break;
            }

            if (judgementText != null)
            {
                judgementText.text = currentJudgementStr;
                judgementText.color = currentJudgementColor;
                judgementText.gameObject.SetActive(true);
            }
        }

        private void UpdateJudgementAnimation()
        {
            if (judgementTimer > 0f)
            {
                judgementTimer -= Time.deltaTime;

                if (currentJudgementStr == "PERFECT!")
                {
                    judgementScale = Mathf.Lerp(judgementScale, 1.0f, Time.deltaTime * 10f);
                }
                else if (currentJudgementStr == "MISS")
                {
                    judgementYOffset += Time.deltaTime * 30f;
                }

                if (judgementText != null)
                {
                    judgementText.transform.localScale = Vector3.one * judgementScale;
                    Color c = currentJudgementColor;
                    c.a = Mathf.Clamp01(judgementTimer * 2f);
                    judgementText.color = c;

                    if (judgementTimer <= 0f)
                    {
                        judgementText.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!enableBuiltInOverlay) return;

            // Ana Menü veya Pause Menüsü açıkken oyun içi HUD'u gizle
            if (MainMenuManager.Instance != null && MainMenuManager.Instance.IsMenuOpen) return;

            // 1. Sağ Üst Köşe: Çapraz Kombo & Çarpan
            DrawTopRightComboHUD();

            // 2. Sol Üst Köşe: Vuruş Değerlendirme (PERFECT / GREAT / GOOD / MISS)
            DrawJudgementFeedback();

            // 3. Sol Alt Köşe: Minimal Tuş Bilgisi (Yolun üstünü kapatmaz)
            DrawBottomLeftKeyHint();
        }

        private void DrawTopRightComboHUD()
        {
            float screenW = Screen.width;
            float marginX = 28f;
            float marginY = 22f;
            float boxW = 220f;
            float boxH = 96f;

            Vector2 pivot = new Vector2(screenW - boxW * 0.5f - marginX, marginY + boxH * 0.5f);

            Matrix4x4 oldMatrix = GUI.matrix;
            // Çapraz açı (-9 derece) ve her vuruşta büyüyüp küçülen canlı animasyon
            GUIUtility.RotateAroundPivot(-9f, pivot);
            GUIUtility.ScaleAroundPivot(Vector2.one * comboPulseScale, pivot);

            Rect hudRect = new Rect(screenW - boxW - marginX, marginY, boxW, boxH);

            // 1. Çarpan (x1 .. x5)
            if (displayedMultiplier > 1)
            {
                GUIStyle multStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 32,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperRight
                };
                Color mCol = displayedMultiplier >= 4 ? colorPerfect : (displayedMultiplier >= 2 ? colorGreat : Color.white);
                DrawTextWithShadow(new Rect(hudRect.x, hudRect.y - 4f, hudRect.width, 36f), $"x{displayedMultiplier}", multStyle, mCol);
            }

            // 2. Kombo Sayacı ("124 COMBO" veya "0 COMBO")
            GUIStyle comboStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            Color cCol = displayedCombo > 0 ? (displayedMultiplier >= 3 ? colorPerfect : colorGreat) : new Color(0.6f, 0.7f, 0.9f, 0.75f);
            DrawTextWithShadow(new Rect(hudRect.x, hudRect.y + 28f, hudRect.width, 36f), $"{displayedCombo} COMBO", comboStyle, cCol);

            // 3. Skor Göstergesi
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight
            };
            DrawTextWithShadow(new Rect(hudRect.x, hudRect.y + 64f, hudRect.width, 26f), $"SCORE: {displayedScore:N0}", scoreStyle, new Color(0.92f, 0.95f, 1f, 0.95f));

            GUI.matrix = oldMatrix;
        }

        private void DrawTextWithShadow(Rect rect, string text, GUIStyle style, Color textColor)
        {
            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0.02f, 0.01f, 0.06f, 0.9f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, shadowStyle);
            style.normal.textColor = textColor;
            GUI.Label(rect, text, style);
        }

        private void DrawJudgementFeedback()
        {
            if (judgementTimer <= 0f) return;

            float alpha = Mathf.Clamp01(judgementTimer * 2f);
            Color renderColor = currentJudgementColor;
            renderColor.a = alpha;

            int baseFontSize = (int)(38 * judgementScale);
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = baseFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            style.normal.textColor = renderColor;

            // Sol üst köşede temiz ve okunaklı konum
            float w = 360f;
            float h = 65f;
            float x = 28f;
            float y = 28f + judgementYOffset;

            GUIStyle shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
            GUI.Label(new Rect(x + 3, y + 3, w, h), currentJudgementStr, shadowStyle);
            GUI.Label(new Rect(x, y, w, h), currentJudgementStr, style);
        }

        private void DrawBottomLeftKeyHint()
        {
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft
            };
            hintStyle.normal.textColor = new Color(0.55f, 0.75f, 1f, 0.8f);

            string schemeText = (InputManager.Instance != null && InputManager.Instance.CurrentScheme == ControlScheme.Gamepad)
                ? "Gamepad: [□] [✕] [△] [○]"
                : "Klavye: [A]  [S]  [K]  [L]";

            GUI.Label(new Rect(18f, Screen.height - 34f, 360f, 24f), $"{schemeText}   |   ESC: Menü", hintStyle);
        }
    }
}
