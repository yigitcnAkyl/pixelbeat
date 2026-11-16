using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI
{
    /// <summary>PixelFont ile tek satır yazı. Metin değişince eski sprite/texture yok edilir.</summary>
    public class PixelText : MonoBehaviour
    {
        public enum Align { Left, Center, Right }

        private SpriteRenderer sr;
        private string text;

        public static PixelText Create(string objName, Transform parent, int sortingOrder)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<PixelText>();
            t.sr = go.AddComponent<SpriteRenderer>();
            t.sr.sortingOrder = sortingOrder;
            return t;
        }

        /// <param name="xPx">Hizalama noktası (oyun pikseli)</param>
        /// <param name="yPx">Yazının alt kenarı (oyun pikseli)</param>
        /// <param name="scale">Tam sayı büyütme (pixel art bozulmaz). Hizalama büyütülmüş genişliğe göre.</param>
        public void Set(string newText, int xPx, int yPx, Align align, Color color, int scale = 1)
        {
            if (newText != text)
            {
                DestroySprite();
                text = newText;
                sr.sprite = string.IsNullOrEmpty(text) ? null : PixelFont.CreateSprite(text, Playfield.PixelsPerUnit);
            }

            scale = Mathf.Max(1, scale);
            transform.localScale = new Vector3(scale, scale, 1f);
            int w = PixelFont.MeasureWidth(text) * scale;
            int x = align == Align.Left ? xPx : align == Align.Center ? xPx - w / 2 : xPx - w;
            transform.localPosition = new Vector3(x / Playfield.PixelsPerUnit, yPx / Playfield.PixelsPerUnit, 0f);
            sr.color = color;
        }

        public bool Visible
        {
            get => sr.enabled;
            set => sr.enabled = value;
        }

        private void DestroySprite()
        {
            if (sr.sprite == null) return;
            Destroy(sr.sprite.texture);
            Destroy(sr.sprite);
            sr.sprite = null;
        }

        private void OnDestroy()
        {
            if (sr != null) DestroySprite();
        }
    }
}
