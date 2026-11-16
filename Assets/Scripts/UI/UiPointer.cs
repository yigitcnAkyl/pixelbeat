using System;
using PixelBeat.Core;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace PixelBeat.UI
{
    /// <summary>
    /// Menü girişi (GDD §11): tek parmak (ilk dokunan) basış / sürükleme / bırakış, oyun pikseli konumla.
    /// Editor'de fare, LaneInput'un açtığı TouchSimulation üzerinden dokunuş olarak gelir.
    /// Bırakışta <see cref="ScrollModel.TapThresholdPx"/>'ten az kaymışsa tıklama sayılır (butonlar bırakışta tetiklenir,
    /// böylece liste kaydırırken yanlışlıkla buton basılmaz). Kapalıyken (<see cref="Blocked"/>) olay üretmez.
    /// </summary>
    public class UiPointer : MonoBehaviour
    {
        [SerializeField] private ScreenLayout screenLayout;

        private Finger finger;
        private Vector2 downPx;
        private Vector2 lastPx;
        private double lastTime;
        private float velocityY;

        /// <summary>Geçiş (fade) sırasında true: dokunuşlar yok sayılır.</summary>
        public bool Blocked { get; set; }

        public event Action<Vector2> Down;
        /// <summary>(konum, başlangıçtan beri kaydı mı)</summary>
        public event Action<Vector2> Dragged;
        /// <summary>(konum, tıklama mı, son dikey hız piksel/sn)</summary>
        public event Action<Vector2, bool, float> Up;

        public bool IsDown => finger != null;

        public void Configure(ScreenLayout layout) => screenLayout = layout;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            ETouch.onFingerDown += OnFingerDown;
            ETouch.onFingerMove += OnFingerMove;
            ETouch.onFingerUp += OnFingerUp;
        }

        private void OnDisable()
        {
            ETouch.onFingerDown -= OnFingerDown;
            ETouch.onFingerMove -= OnFingerMove;
            ETouch.onFingerUp -= OnFingerUp;
            EnhancedTouchSupport.Disable();
            finger = null;
        }

        private Vector2 Px(Finger f) => screenLayout.ScreenToPx(f.currentTouch.screenPosition);

        private void OnFingerDown(Finger f)
        {
            if (finger != null || Blocked) return;
            finger = f;
            downPx = lastPx = Px(f);
            lastTime = Time.realtimeSinceStartupAsDouble;
            velocityY = 0f;
            Down?.Invoke(downPx);
        }

        private void OnFingerMove(Finger f)
        {
            if (f != finger) return;
            var px = Px(f);
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = (float)(now - lastTime);
            if (dt > 1e-4f) velocityY = Mathf.Lerp(velocityY, (px.y - lastPx.y) / dt, 0.5f);
            lastPx = px;
            lastTime = now;
            if (!Blocked) Dragged?.Invoke(px);
        }

        private void OnFingerUp(Finger f)
        {
            if (f != finger) return;
            finger = null;
            var px = Px(f);
            // Son hareketten beri uzun süre durduysa fırlatma yok.
            if (Time.realtimeSinceStartupAsDouble - lastTime > 0.1) velocityY = 0f;
            if (!Blocked) Up?.Invoke(px, ScrollModel.IsTap(downPx, px), velocityY);
        }
    }
}
