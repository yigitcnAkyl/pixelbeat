using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelBeat.UI
{
    /// <summary>
    /// Üst köşelerde (safe area içinde) iki küçük buton: sol CAL = kalibrasyon, sağ R = baştan başlat.
    /// Editor'de C tuşu da kalibrasyonu açar (R tuşunu SongLoader dinler).
    /// </summary>
    public class HudButtons : MonoBehaviour
    {
        private const int MarginPx = 4 * ScreenLayout.UiScale;

        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private LaneInput laneInput;
        [SerializeField] private SongLoader songLoader;
        [SerializeField] private CalibrationScreen calibration;
        [SerializeField] private GameSession gameSession;

        private PixelButton calButton;
        private PixelButton restartButton;

        /// <summary>Üst buton sırasının alt kenarı (oyun pikseli). Skor yazısı ve can barı buna göre yerleşir.</summary>
        public int RowBottomPx => screenLayout.SafeTopPx - MarginPx - PixelButton.Height;

        /// <summary>Konum bir HUD butonunun üstünde mi (sonuç ekranında yeniden başlatma saymamak için).</summary>
        public bool Contains(Vector2 px) => calButton.Contains(px) || restartButton.Contains(px);

        private void Awake()
        {
            calButton = new PixelButton(playfield, transform, "CAL", 20);
            restartButton = new PixelButton(playfield, transform, "R", 20, PaletteColor.Magenta);
            Layout();
            if (gameSession != null) gameSession.IgnoreTouchAt = Contains;
        }

        private void OnEnable()
        {
            screenLayout.Changed += Layout;
            laneInput.TouchStarted += OnTouch;
        }

        private void OnDisable()
        {
            screenLayout.Changed -= Layout;
            laneInput.TouchStarted -= OnTouch;
        }

        private void Layout()
        {
            int y = RowBottomPx;
            calButton.Place(screenLayout.LeftPx + MarginPx + 2, y);
            int rw = PixelButton.Height; // kare
            restartButton.Place(screenLayout.RightPx - MarginPx - 2 - rw, y, rw);
        }

        private void Update()
        {
            bool show = !calibration.IsOpen;
            calButton.Visible = show;
            restartButton.Visible = show;

            var keyboard = Keyboard.current;
            if (show && keyboard != null && keyboard.cKey.wasPressedThisFrame) calibration.Open();
        }

        private void OnTouch(Vector2 px, double songTime)
        {
            if (calibration.IsOpen) return;
            if (calButton.Contains(px)) calibration.Open();
            else if (restartButton.Contains(px)) songLoader.Restart();
        }
    }
}
