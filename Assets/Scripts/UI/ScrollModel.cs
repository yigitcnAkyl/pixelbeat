using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>
    /// Dikey liste kaydırma (Unity'den bağımsız mantık): parmakla sürükleme, bırakınca sürtünmeyle yavaşlayan kayma,
    /// sınırlar [0, içerik − görünür]. Offset 0 = listenin başı; parmak yukarı → offset artar (aşağıdaki satırlar gelir).
    /// </summary>
    public sealed class ScrollModel
    {
        /// <summary>Bu kadar pikselden az kayan dokunuş tıklamadır.</summary>
        public const float TapThresholdPx = 8f;
        /// <summary>Saniyede hızın bu oranı kalır (üstel sönüm).</summary>
        public const float FrictionPerSec = 0.04f;

        private float dragStartOffset;
        private float dragStartY;

        public float Offset { get; private set; }
        public float Velocity { get; private set; }
        public float ContentHeight { get; private set; }
        public float ViewHeight { get; private set; }
        public bool Dragging { get; private set; }
        public float MaxOffset => Mathf.Max(0f, ContentHeight - ViewHeight);

        public void SetSizes(float content, float view)
        {
            ContentHeight = Mathf.Max(0f, content);
            ViewHeight = Mathf.Max(0f, view);
            Offset = Mathf.Clamp(Offset, 0f, MaxOffset);
        }

        public void BeginDrag(float y)
        {
            Dragging = true;
            Velocity = 0f;
            dragStartY = y;
            dragStartOffset = Offset;
        }

        public void Drag(float y)
        {
            if (!Dragging) return;
            Offset = Mathf.Clamp(dragStartOffset + (y - dragStartY), 0f, MaxOffset);
        }

        /// <param name="velocity">Parmağın son hızı (piksel/sn, yukarı +).</param>
        public void EndDrag(float velocity)
        {
            Dragging = false;
            Velocity = velocity;
        }

        public void Tick(float dt)
        {
            if (Dragging || Mathf.Approximately(Velocity, 0f)) return;
            Offset += Velocity * dt;
            if (Offset <= 0f || Offset >= MaxOffset)
            {
                Offset = Mathf.Clamp(Offset, 0f, MaxOffset);
                Velocity = 0f;
                return;
            }
            Velocity *= Mathf.Pow(FrictionPerSec, dt);
            if (Mathf.Abs(Velocity) < 5f) Velocity = 0f;
        }

        public static bool IsTap(Vector2 down, Vector2 up) => (up - down).sqrMagnitude < TapThresholdPx * TapThresholdPx;
    }
}
