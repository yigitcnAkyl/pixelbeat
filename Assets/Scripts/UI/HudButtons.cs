using PixelBeat.Core;
using PixelBeat.Gameplay;
using PixelBeat.UI.Menu;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Oyun ekranının üst köşelerinde (safe area içinde) iki küçük buton (GDD §11): sol ◀ = menüye (şarkı detayına) dön,
    /// sağ R = baştan başlat. Sadece oynanırken görünür. Basışta tetiklenir (oyun sırasında hızlı tepki).
    /// </summary>
    public class HudButtons : MonoBehaviour
    {
        private const int MarginPx = 4 * ScreenLayout.UiScale;

        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private LaneInput laneInput;
        [SerializeField] private SongLoader songLoader;
        [SerializeField] private GameSession gameSession;
        [SerializeField] private ScreenRouter router;

        private PixelButton backButton;
        private PixelButton restartButton;

        /// <summary>Üst buton sırasının alt kenarı (oyun pikseli). Skor yazısı ve can barı buna göre yerleşir.</summary>
        public int RowBottomPx => screenLayout.SafeTopPx - MarginPx - PixelButton.Height;

        private bool Shown => gameSession.State == GameSession.SessionState.Playing && (router == null || router.InGame);

        private void Awake()
        {
            backButton = new PixelButton(playfield, transform, "", 20, PaletteColor.Cyan, playfield.Skin != null ? playfield.Skin.IconBack : null);
            restartButton = new PixelButton(playfield, transform, "R", 20, PaletteColor.Magenta);
            Layout();
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
            int w = PixelButton.Height + 8;
            backButton.Place(screenLayout.LeftPx + MarginPx + 2, y, w);
            restartButton.Place(screenLayout.RightPx - MarginPx - 2 - w, y, w);
        }

        private void Update()
        {
            bool show = Shown;
            backButton.Visible = show;
            restartButton.Visible = show;
        }

        private void OnTouch(Vector2 px, double songTime)
        {
            if (!Shown) return;
            if (backButton.Contains(px))
            {
                if (router != null) router.Back();
            }
            else if (restartButton.Contains(px)) songLoader.Restart();
        }
    }
}
