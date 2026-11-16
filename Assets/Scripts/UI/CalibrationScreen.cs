using System.Collections.Generic;
using PixelBeat.Audio;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Offset kalibrasyonu (GDD §9): metronom çalar, oyuncu sesle birlikte ekrana dokunur. Isınma dokunuşlarından sonra
    /// her dokunuşun en yakın beat'e farkı ölçülür, uçlar atılarak ortalanır → yeni kullanıcı offset'i.
    /// Sadece ses ölçülür (ekranda beat gösterilmez), yoksa oyuncu sese değil görüntüye senkron olur.
    /// Gerçek cihazda KAYDET PlayerPrefs'e yazar; Editor / Unity Remote'ta sonuç sadece gösterilir.
    /// </summary>
    public class CalibrationScreen : MonoBehaviour
    {
        private enum Phase { Closed, Measuring, Result }

        [SerializeField] private Conductor conductor;
        [SerializeField] private Playfield playfield;
        [SerializeField] private ScreenLayout screenLayout;
        [SerializeField] private LaneInput laneInput;
        [SerializeField] private SongLoader songLoader;
        [Tooltip("CRT aç/kapat butonu için (ayarlar ekranı gelene kadar burada).")]
        [SerializeField] private CrtOverlay crtOverlay;

        [Tooltip("Yarım beat'ten uzun gecikmeler karışmasın diye yavaş tempo. 90 BPM'de pencere -167..+500 ms.")]
        [SerializeField, Range(60f, 140f)] private float bpm = 90f;
        [SerializeField, Min(0)] private int warmupTaps = 4;
        [SerializeField, Min(4)] private int measuredTaps = 16;
        [Tooltip("Ortalamadan önce her iki uçtan atılan dokunuş sayısı.")]
        [SerializeField, Min(0)] private int trimEachSide = 2;

        [SerializeField] private PaletteRef overlayColor = PaletteColor.Void;
        [SerializeField] private PaletteRef titleColor = PaletteColor.Magenta;
        [SerializeField] private PaletteRef textColor = PaletteColor.Haze;
        [SerializeField] private PaletteRef valueColor = PaletteColor.Cyan;
        [SerializeField] private PaletteRef warnColor = PaletteColor.Gold;

        private const int Order = 50;
        private const int LineGap = 11;

        private readonly List<double> diffs = new List<double>();
        private Phase phase = Phase.Closed;
        private int tapCount;
        private float resultOffsetMs;
        private double resultStdDevMs;
        private float tapFlash;
        private int openedFrame = -1;

        private GameObject root;
        private SpriteRenderer overlay;
        private SpriteRenderer tapIndicator;
        private PixelText title, line1, line2, status, detail, note;
        private PixelButton saveButton, retryButton, closeButton, crtButton;
        private AudioClip clip;

        private string CrtLabel => crtOverlay != null && crtOverlay.IsOn ? "CRT: ACIK" : "CRT: KAPALI";

        public bool IsOpen => phase != Phase.Closed;

        private void Awake()
        {
            root = new GameObject("Calibration UI");
            root.transform.SetParent(transform, false);
            var t = root.transform;

            overlay = playfield.CreateRect("Overlay", t, 0, 0, 1, 1, overlayColor, Order);
            tapIndicator = playfield.CreateRect("Tap", t, 0, 0, 1, 1, valueColor, Order + 1);
            title = PixelText.Create("Title", t, Order + 2);
            line1 = PixelText.Create("Line1", t, Order + 2);
            line2 = PixelText.Create("Line2", t, Order + 2);
            status = PixelText.Create("Status", t, Order + 2);
            detail = PixelText.Create("Detail", t, Order + 2);
            note = PixelText.Create("Note", t, Order + 2);
            saveButton = new PixelButton(playfield, t, "KAYDET", Order + 2);
            retryButton = new PixelButton(playfield, t, "TEKRAR", Order + 2);
            closeButton = new PixelButton(playfield, t, "KAPAT", Order + 2);
            crtButton = new PixelButton(playfield, t, "CRT: KAPALI", Order + 2);

            root.SetActive(false);
        }

        private void OnEnable()
        {
            screenLayout.Changed += Layout;
            laneInput.TouchStarted += OnTouch;
            laneInput.LanePressed += OnKey;
        }

        private void OnDisable()
        {
            screenLayout.Changed -= Layout;
            laneInput.TouchStarted -= OnTouch;
            laneInput.LanePressed -= OnKey;
        }

        private void OnDestroy()
        {
            if (clip != null) Destroy(clip);
        }

        /// <summary>Uygulama arka plandan dönünce ölçüm baştan başlar (zaman güvenilir değil).</summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused && phase == Phase.Measuring) StartMeasuring();
        }

        public void Open()
        {
            if (IsOpen) return;
            openedFrame = Time.frameCount;
            songLoader.Suspend();
            laneInput.LaneTouchesEnabled = false;
            root.SetActive(true);
            StartMeasuring();
        }

        private void Close()
        {
            conductor.Stop();
            phase = Phase.Closed;
            root.SetActive(false);
            laneInput.LaneTouchesEnabled = true;
            songLoader.Restart(); // songLoader beatmap BPM/offset'ini Conductor'a geri yükler
        }

        private void StartMeasuring()
        {
            diffs.Clear();
            tapCount = 0;
            phase = Phase.Measuring;

            // 8 beat'lik klip döngüde çalar; 90 BPM'de beat başına örnek sayısı tam sayı olduğundan kayma olmaz.
            if (clip == null) clip = MetronomeClipGenerator.Create(bpm, 8, AudioSettings.outputSampleRate);
            conductor.Configure(bpm, 0f);
            conductor.Play(clip, loop: true);
            Layout();
        }

        private void OnKey(int lane, double songTime)
        {
            // Kalibrasyonda dokunuşlar şerit üretmez; buraya sadece tuşlar (Editor testi) gelir.
            if (phase == Phase.Measuring) RegisterTap(songTime);
        }

        private void OnTouch(Vector2 px, double songTime)
        {
            if (!IsOpen) return;

            if (closeButton.Contains(px)) { Close(); return; }
            if (crtButton.Contains(px))
            {
                if (crtOverlay != null) crtOverlay.Toggle();
                Layout();
                return; // dokunuş ölçüme sayılmaz
            }

            if (phase == Phase.Result)
            {
                if (retryButton.Contains(px)) StartMeasuring();
                else if (saveButton.Contains(px)) Save();
                return;
            }

            RegisterTap(songTime);
        }

        private void RegisterTap(double songTime)
        {
            // Ekranı açan dokunuş (aynı kare) ve metronom başlamadan önceki dokunuşlar sayılmaz.
            if (Time.frameCount == openedFrame || songTime < 0) return;

            tapCount++;
            tapFlash = 1f;
            if (tapCount > warmupTaps) diffs.Add(OffsetCalibration.DiffToBeat(songTime, conductor.SecPerBeat));

            if (diffs.Count >= measuredTaps)
            {
                resultOffsetMs = OffsetCalibration.NewUserOffsetMs(conductor.UserOffsetMs, diffs, trimEachSide, out resultStdDevMs);
                resultOffsetMs = Mathf.Round(resultOffsetMs);
                phase = Phase.Result;
                conductor.Stop();
                Debug.Log($"[Calibration] eski {conductor.UserOffsetMs:+0;-0;0} ms → yeni {resultOffsetMs:+0;-0;0} ms, sapma {resultStdDevMs:0} ms");
            }

            Layout();
        }

        private void Save()
        {
            if (!Conductor.CanPersistUserOffset) return;
            conductor.SetUserOffsetMs(resultOffsetMs, persist: true);
            Close();
        }

        private static string Ms(float ms) => $"{(ms >= 0 ? "+" : "-")}{Mathf.Abs(Mathf.RoundToInt(ms))} MS";

        private void Layout()
        {
            if (!IsOpen) return;
            var l = screenLayout;
            Playfield.PlaceRect(overlay.transform, l.LeftPx, l.BottomPx, l.RightPx - l.LeftPx, l.HeightPx, playfield.PixelSprite.rect.width);

            int top = l.SafeTopPx - 20;
            title.Set("KALIBRASYON", 0, top, PixelText.Align.Center, titleColor);
            line1.Set("SESLE BIRLIKTE", 0, top - 2 * LineGap, PixelText.Align.Center, textColor);
            line2.Set("EKRANA DOKUN", 0, top - 3 * LineGap, PixelText.Align.Center, textColor);

            crtButton.SetText(CrtLabel);
            int crtW = PixelButton.WidthFor(crtButton.Text);
            crtButton.Place(-crtW / 2, top - 5 * LineGap - PixelButton.Height);

            Playfield.PlaceRect(tapIndicator.transform, -6, 18, 12, 12, playfield.PixelSprite.rect.width);

            bool canSave = Conductor.CanPersistUserOffset;
            if (phase == Phase.Measuring)
            {
                bool warming = tapCount < warmupTaps;
                status.Set(warming ? $"ISINMA {tapCount}/{warmupTaps}" : $"{diffs.Count}/{measuredTaps}",
                    0, 0, PixelText.Align.Center, valueColor);
                detail.Set($"SIMDIKI {Ms(conductor.UserOffsetMs)}", 0, -LineGap, PixelText.Align.Center, textColor);
                note.Set("", 0, 0, PixelText.Align.Center, textColor);
            }
            else
            {
                status.Set($"OFFSET {Ms(resultOffsetMs)}", 0, 0, PixelText.Align.Center, valueColor);
                bool noisy = resultStdDevMs > 40.0;
                detail.Set($"SAPMA {Mathf.RoundToInt((float)resultStdDevMs)} MS{(noisy ? " TEKRAR DENE" : "")}",
                    0, -LineGap, PixelText.Align.Center, noisy ? warnColor : textColor);
                note.Set(canSave ? $"ESKI {Ms(conductor.UserOffsetMs)}" : "EDITORDE KAYDEDILMEZ",
                    0, -2 * LineGap, PixelText.Align.Center, canSave ? textColor : warnColor);
            }

            // Butonlar safe area altında, tek sıra ortalı.
            int by = l.SafeBottomPx + 8;
            bool result = phase == Phase.Result;
            saveButton.Visible = result && canSave;
            retryButton.Visible = result;
            closeButton.Visible = true;

            const int gap = 6;
            int total = PixelButton.WidthFor("KAPAT");
            if (retryButton.Visible) total += gap + PixelButton.WidthFor("TEKRAR");
            if (saveButton.Visible) total += gap + PixelButton.WidthFor("KAYDET");
            int x = -total / 2;
            if (saveButton.Visible) { saveButton.Place(x, by); x += PixelButton.WidthFor("KAYDET") + gap; }
            if (retryButton.Visible) { retryButton.Place(x, by); x += PixelButton.WidthFor("TEKRAR") + gap; }
            closeButton.Place(x, by);
        }

        private void Update()
        {
            if (!IsOpen) return;
            // Dokunuş geri bildirimi (beat değil, dokunuşun kendisi).
            tapFlash = Mathf.Max(0f, tapFlash - Time.unscaledDeltaTime * 6f);
            Color c = valueColor;
            c.a = tapFlash;
            tapIndicator.color = c;
        }
    }
}
