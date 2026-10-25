using System.IO;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Oyuna girmeden önce açılan Ana Menü (Pre-Game Main Menu) ve oyun içi Duraklatma (Pause) menüsü.
    /// Kullanıcının gönderdiği 4. fotoğraftaki orijinal piksel buton takımlarını
    /// (Start, Resume, Options, Pause, Quit, Restart) birebir kullanır.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        public static MainMenuManager Instance { get; private set; }

        [Header("Menü Durumu")]
        [SerializeField] private bool isMenuOpen = true;
        private bool hasGameStarted = false;

        public bool IsMenuOpen => isMenuOpen;

        [Header("Orijinal Cyberpunk Piksel Butonları (Görsel 4)")]
        [SerializeField] private Texture2D btnStartTex;
        [SerializeField] private Texture2D btnResumeTex;
        [SerializeField] private Texture2D btnOptionsTex;
        [SerializeField] private Texture2D btnPauseTex;
        [SerializeField] private Texture2D btnQuitTex;
        [SerializeField] private Texture2D btnRestartTex;
        [SerializeField] private Texture2D frameTex;

        [Header("MP3 / Şarkı Yolu")]
        [SerializeField] private string songFilePath = "rings-of-saturn.wav";
        [SerializeField] private string songBpmText = "120";

        private bool showOptionsPanel = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Oyun başlarken önce Ana Menü açık gelsin
            isMenuOpen = true;
            hasGameStarted = false;

            LoadTextures();
        }

        private void Start()
        {
            // Ana menü açıkken şarkı başlamasın, oyuncu START'a basınca başlasın
            if (Conductor.Instance != null)
            {
                Conductor.Instance.StopSong();
            }
        }

        private void LoadTextures()
        {
#if UNITY_EDITOR
            if (btnStartTex == null)   btnStartTex   = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sliced/Buttons/Btn_Start.png");
            if (btnResumeTex == null)  btnResumeTex  = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sliced/Buttons/Btn_Resume.png");
            if (btnOptionsTex == null) btnOptionsTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sliced/Buttons/Btn_Options.png");
            if (btnPauseTex == null)   btnPauseTex   = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sliced/Buttons/Btn_Pause.png");
            if (btnQuitTex == null)    btnQuitTex    = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sliced/Buttons/Btn_Quit.png");
            if (btnRestartTex == null) btnRestartTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sliced/Buttons/Btn_Restart.png");
            if (frameTex == null)      frameTex      = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Sliced/Buttons/UI_Frame.png");
#endif
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && hasGameStarted)
            {
                if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
                {
                    TogglePauseMenu();
                }
            }
#endif
        }

        public void StartGameFromMenu()
        {
            hasGameStarted = true;
            isMenuOpen = false;
            showOptionsPanel = false;

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ResetScore();
            }

            if (Conductor.Instance != null)
            {
                Conductor.Instance.StartSongWithAutoChart();
            }
        }

        public void RestartGame()
        {
            hasGameStarted = true;
            isMenuOpen = false;
            showOptionsPanel = false;

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.ResetScore();
            }

            if (Conductor.Instance != null)
            {
                Conductor.Instance.StartSongWithAutoChart();
            }
        }

        public void TogglePauseMenu()
        {
            if (!hasGameStarted) return;

            isMenuOpen = !isMenuOpen;
            showOptionsPanel = false;

            if (Conductor.Instance != null)
            {
                if (isMenuOpen) Conductor.Instance.PauseSong();
                else Conductor.Instance.ResumeSong();
            }
        }

        private bool DrawPixelButton(Rect rect, Texture2D tex, string fallbackLabel)
        {
            if (tex != null)
            {
                bool isHovered = rect.Contains(Event.current.mousePosition);
                Rect drawRect = rect;
                if (isHovered)
                {
                    drawRect = new Rect(rect.x - 6f, rect.y - 2f, rect.width + 12f, rect.height + 4f);
                }
                GUI.color = Color.white;
                GUI.DrawTexture(drawRect, tex, ScaleMode.ScaleToFit);
                return GUI.Button(rect, GUIContent.none, GUIStyle.none);
            }
            return GUI.Button(rect, fallbackLabel);
        }

        private void OnGUI()
        {
            // Oyun esnasında sağ alt köşede orijinal piksel PAUSE butonu
            if (!isMenuOpen && hasGameStarted)
            {
                Rect pauseRect = new Rect(Screen.width - 156f, Screen.height - 52f, 140f, 40f);
                if (DrawPixelButton(pauseRect, btnPauseTex, "PAUSE"))
                {
                    TogglePauseMenu();
                }
                return;
            }

            if (!isMenuOpen) return;

            // Arka plan yarı saydam siberpunk karartma
            GUI.color = new Color(0.03f, 0.02f, 0.09f, 0.86f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelW = 460f;
            float panelH = hasGameStarted ? 460f : 390f;
            if (showOptionsPanel) panelH = 460f;

            float panelX = (Screen.width - panelW) / 2f;
            float panelY = (Screen.height - panelH) / 2f;

            // Orijinal Cyberpunk UI Çerçevesi (Artık altındaki ok tuşları tamamen kaldırıldı)
            if (frameTex != null)
            {
                GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), frameTex, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.Box(new Rect(panelX, panelY, panelW, panelH), "");
            }

            // Başlık
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0f, 0.95f, 1f, 1f);
            string titleText = !hasGameStarted ? "PIXEL BEAT" : "PAUSED";
            GUI.Label(new Rect(panelX, panelY + 28f, panelW, 36f), titleText, titleStyle);

            if (showOptionsPanel)
            {
                DrawOptionsSubPanel(panelX, panelY, panelW);
                return;
            }

            if (!hasGameStarted)
            {
                // --- OYUNA GİRMEDEN ÖNCEKİ ANA MENÜ (PRE-GAME MAIN MENU) ---
                float btnW = 236f;
                float btnH = 60f;
                float btnX = (Screen.width - btnW) / 2f;
                float currentY = panelY + 90f;
                float spacing = 74f;

                // 1. START
                if (DrawPixelButton(new Rect(btnX, currentY, btnW, btnH), btnStartTex, "START"))
                {
                    StartGameFromMenu();
                }
                currentY += spacing;

                // 2. OPTIONS
                if (DrawPixelButton(new Rect(btnX, currentY, btnW, btnH), btnOptionsTex, "OPTIONS"))
                {
                    showOptionsPanel = true;
                }
                currentY += spacing;

                // 3. QUIT (Çerçevenin içinde, taşma olmadan kusursuz konumlandı)
                if (DrawPixelButton(new Rect(btnX, currentY, btnW, btnH), btnQuitTex, "QUIT"))
                {
                    QuitGame();
                }
            }
            else
            {
                // --- OYUN İÇİ DURAKLATMA MENÜSÜ (PAUSE MENU) ---
                float btnW = 224f;
                float btnH = 56f;
                float btnX = (Screen.width - btnW) / 2f;
                float currentY = panelY + 84f;
                float spacing = 68f;

                // 1. RESUME
                if (DrawPixelButton(new Rect(btnX, currentY, btnW, btnH), btnResumeTex, "RESUME"))
                {
                    TogglePauseMenu();
                }
                currentY += spacing;

                // 2. RESTART
                if (DrawPixelButton(new Rect(btnX, currentY, btnW, btnH), btnRestartTex, "RESTART"))
                {
                    RestartGame();
                }
                currentY += spacing;

                // 3. OPTIONS
                if (DrawPixelButton(new Rect(btnX, currentY, btnW, btnH), btnOptionsTex, "OPTIONS"))
                {
                    showOptionsPanel = true;
                }
                currentY += spacing;

                // 4. QUIT
                if (DrawPixelButton(new Rect(btnX, currentY, btnW, btnH), btnQuitTex, "QUIT"))
                {
                    QuitGame();
                }
            }
        }

        private void DrawOptionsSubPanel(float px, float py, float pw)
        {
            GUILayout.BeginArea(new Rect(px + 35, py + 72, pw - 70, 370));

            GUI.color = new Color(1.0f, 0.16f, 0.52f, 1.0f);
            GUILayout.Label("<b>KONTROL ŞEMASI</b>");
            GUILayout.Space(6);

            if (InputManager.Instance != null)
            {
                GUILayout.BeginHorizontal();
                GUI.color = InputManager.Instance.CurrentScheme == ControlScheme.KeyboardASKL ? Color.cyan : Color.white;
                if (GUILayout.Button("Klavye (A, S, K, L)", GUILayout.Height(36)))
                {
                    InputManager.Instance.SetControlScheme(ControlScheme.KeyboardASKL);
                }

                GUI.color = InputManager.Instance.CurrentScheme == ControlScheme.Gamepad ? Color.cyan : Color.white;
                if (GUILayout.Button("Gamepad (□ ✕ △ ○)", GUILayout.Height(36)))
                {
                    InputManager.Instance.SetControlScheme(ControlScheme.Gamepad);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(16);
            GUI.color = new Color(0f, 0.95f, 1f, 1f);
            GUILayout.Label("<b>HARİCİ MP3 / WAV ŞARKI YÜKLE</b>");
            GUILayout.Space(4);

            GUI.color = Color.white;
            GUILayout.Label("Dosya Adı veya Tam Yolu:");
            songFilePath = GUILayout.TextField(songFilePath, GUILayout.Height(28));

            GUILayout.Space(4);
            GUILayout.Label("Şarkı BPM:");
            songBpmText = GUILayout.TextField(songBpmText, GUILayout.Height(28));

            GUILayout.Space(10);
            GUI.color = new Color(0f, 0.95f, 1f, 1f);
            if (GUILayout.Button("Şarkıyı Yükle & Oyna", GUILayout.Height(38)))
            {
                float.TryParse(songBpmText, out float bpm);
                if (bpm <= 0) bpm = 120f;

                string fullPath = songFilePath;
                if (!Path.IsPathRooted(fullPath))
                {
                    string rootPath = Path.Combine(Directory.GetCurrentDirectory(), songFilePath);
                    string assetsPath = Path.Combine(Application.dataPath, "Audio", songFilePath);
                    if (File.Exists(rootPath)) fullPath = rootPath;
                    else if (File.Exists(assetsPath)) fullPath = assetsPath;
                }

                if (SongLoader.Instance != null)
                {
                    hasGameStarted = true;
                    isMenuOpen = false;
                    showOptionsPanel = false;
                    SongLoader.Instance.LoadSongFromFile(fullPath, bpm);
                }
            }

            GUILayout.Space(14);
            GUI.color = Color.white;
            if (GUILayout.Button("◀ GERİ DÖN", GUILayout.Height(34)))
            {
                showOptionsPanel = false;
            }

            GUILayout.EndArea();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
