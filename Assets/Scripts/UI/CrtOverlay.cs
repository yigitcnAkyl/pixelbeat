using PixelBeat.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PixelBeat.UI
{
    /// <summary>
    /// Hafif CRT hissi (GDD §8): ekran çözünürlüğünde scanline (her oyun pikseli satırının alt ~1/3'ü hafif koyu) + hafif vinyet.
    /// Screen Space Overlay canvas'ta çizilir (Pixel Perfect büyütmesinden sonra → ince kalır). Tek tam ekran quad.
    /// Açık/kapalı PlayerPrefs'te ("crtEnabled").
    /// </summary>
    public class CrtOverlay : MonoBehaviour
    {
        public const string PrefsKey = "crtEnabled";

        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private bool enabledByDefault = true;
        [SerializeField, Range(0f, 1f)] private float scanlineStrength = 0.22f;
        [SerializeField, Range(0f, 1f)] private float vignetteStrength = 0.3f;

        private Canvas canvas;
        private RawImage scanlines;
        private RawImage vignette;
        private Texture2D scanTex;
        private Texture2D vignetteTex;
        private int builtZoom = -1;

        public bool IsOn { get; private set; }

        private void Awake()
        {
            var go = new GameObject("CRT Canvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            scanlines = CreateImage("Scanlines");
            vignette = CreateImage("Vignette");
            vignetteTex = BuildVignette(64, vignetteStrength);
            vignette.texture = vignetteTex;

            IsOn = PlayerPrefs.GetInt(PrefsKey, enabledByDefault ? 1 : 0) == 1;
            canvas.enabled = IsOn;
        }

        private RawImage CreateImage(string objName)
        {
            var go = new GameObject(objName, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        public void SetOn(bool on)
        {
            IsOn = on;
            canvas.enabled = on;
            PlayerPrefs.SetInt(PrefsKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void Toggle() => SetOn(!IsOn);

        private void Update()
        {
            if (!IsOn) return;
            int zoom = screenLayout.Zoom;
            if (zoom != builtZoom)
            {
                if (scanTex != null) Destroy(scanTex);
                scanTex = BuildScanline(zoom, scanlineStrength);
                scanlines.texture = scanTex;
                builtZoom = zoom;
            }
            // Doku yüksekliği = 1 oyun pikseli; ekran boyunca oyun pikseli satırı kadar tekrar.
            scanlines.uvRect = new Rect(0, 0, 1, Screen.height / (float)zoom);
        }

        /// <summary>Bir oyun pikseli satırı içindeki ekran satırlarının karartma alfası: alt ~1/3 koyu (zoom &lt; 3'te yok).</summary>
        public static float[] ScanlineAlphas(int zoom, float strength)
        {
            int h = Mathf.Max(1, zoom);
            int dark = h >= 3 ? Mathf.Max(1, h / 3) : 0;
            var a = new float[h];
            for (int y = 0; y < h; y++) a[y] = y < dark ? strength : 0f;
            return a;
        }

        /// <summary>1 x zoom doku (tekrarlı, Point).</summary>
        public static Texture2D BuildScanline(int zoom, float strength)
        {
            var alphas = ScanlineAlphas(zoom, strength);
            var tex = new Texture2D(1, alphas.Length, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = "CRT Scanline",
            };
            for (int y = 0; y < alphas.Length; y++) tex.SetPixel(0, y, new Color(0, 0, 0, alphas[y]));
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildVignette(int size, float strength)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "CRT Vignette",
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.6f) / 0.8f);
                tex.SetPixel(x, y, new Color(0, 0, 0, d * d * strength));
            }
            tex.Apply(false, true);
            return tex;
        }

        private void OnDestroy()
        {
            if (scanTex != null) Destroy(scanTex);
            if (vignetteTex != null) Destroy(vignetteTex);
        }
    }
}
