using System;
using UnityEngine;

namespace PixelBeat.Core
{
    /// <summary>
    /// Dikey ekran ölçüleri (oyun pikseli, ekran merkezi = 0,0). Genişlik 180 px referanstır, yükseklik telefona göre uzar.
    /// Pixel Perfect Camera ile aynı tam sayı büyütmeyi (zoom) hesaplar; Screen.safeArea boşluklarını oyun pikseline çevirir.
    /// Ekran boyutu ya da safe area değişirse <see cref="Changed"/> yayınlanır (Editor'de Game view boyutu değişince).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ScreenLayout : MonoBehaviour
    {
        public const int ReferenceWidthPx = 180;
        public const int ReferenceHeightPx = 320;

        private int screenW = -1;
        private int screenH = -1;
        private Rect safeArea;

        public event Action Changed;

        /// <summary>Ekran pikseli / oyun pikseli (tam sayı).</summary>
        public int Zoom { get; private set; } = 1;

        /// <summary>Görünen alanın kenarları (oyun pikseli, merkeze göre).</summary>
        public int LeftPx { get; private set; }
        public int RightPx { get; private set; }
        public int BottomPx { get; private set; }
        public int TopPx { get; private set; }

        /// <summary>Safe area'nın alt/üst kenarı (oyun pikseli). Önemli öğeler bunların arasında kalır.</summary>
        public int SafeBottomPx { get; private set; }
        public int SafeTopPx { get; private set; }

        public int HeightPx => TopPx - BottomPx;
        public int SafeHeightPx => SafeTopPx - SafeBottomPx;

        private void Awake() => Refresh(true);

        private void Update() => Refresh(false);

        /// <summary>Ekran koordinatını (piksel, sol-alt köken) oyun pikseline çevirir.</summary>
        public Vector2 ScreenToPx(Vector2 screenPos)
        {
            Refresh(false);
            return new Vector2((screenPos.x - screenW * 0.5f) / Zoom, (screenPos.y - screenH * 0.5f) / Zoom);
        }

        /// <summary>Değerleri gerekirse yeniler. Başka bileşenler Awake'te çağırabilir (çalışma sırasından bağımsız).</summary>
        public void Refresh(bool force)
        {
            int w = Screen.width;
            int h = Screen.height;
            Rect safe = Screen.safeArea;
            if (!force && w == screenW && h == screenH && safe == safeArea) return;

            screenW = w;
            screenH = h;
            safeArea = safe;

            // Pixel Perfect Camera (crop yok) ile aynı: iki eksende de sığan en büyük tam sayı büyütme.
            Zoom = Mathf.Max(1, Mathf.Min(w / ReferenceWidthPx, h / ReferenceHeightPx));

            float halfW = w * 0.5f / Zoom;
            float halfH = h * 0.5f / Zoom;
            LeftPx = Mathf.FloorToInt(-halfW);
            RightPx = Mathf.CeilToInt(halfW);
            BottomPx = Mathf.FloorToInt(-halfH);
            TopPx = Mathf.CeilToInt(halfH);

            SafeBottomPx = Mathf.CeilToInt(-halfH + safe.yMin / Zoom);
            SafeTopPx = Mathf.FloorToInt(halfH - (h - safe.yMax) / Zoom);

            Changed?.Invoke();
        }
    }
}
