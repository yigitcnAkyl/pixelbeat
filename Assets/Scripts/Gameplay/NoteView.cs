using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Havuzdan alınan nota görseli. Tap: sadece baş. Hold: baş + gövde + kuyruk ucu.
    /// Konumlar oyun pikseli; NoteSpawner her kare şarkı zamanından hesaplar.
    /// </summary>
    public class NoteView : MonoBehaviour
    {
        private SpriteRenderer head;
        private SpriteRenderer body;
        private SpriteRenderer tail;
        private float spriteSizePx;

        public ChartNote Note { get; private set; }

        public void Init(Sprite sprite, int sortingOrder)
        {
            spriteSizePx = sprite.rect.width;
            body = CreatePart("Body", sprite, sortingOrder);
            tail = CreatePart("Tail", sprite, sortingOrder);
            head = CreatePart("Head", sprite, sortingOrder + 1); // baş gövdenin üstünde
        }

        private SpriteRenderer CreatePart(string partName, Sprite sprite, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public void Bind(ChartNote note)
        {
            Note = note;
            transform.localPosition = Vector3.zero;
            body.enabled = note.IsHold;
            tail.enabled = note.IsHold;
        }

        public void Unbind()
        {
            Note = null;
        }

        public void SetColors(Color headColor, Color bodyColor)
        {
            head.color = headColor;
            tail.color = headColor;
            body.color = bodyColor;
        }

        /// <summary>Sol kenar laneX, baş merkezi headCenterY. Hold için kuyruk ucu tailY (piksel).</summary>
        public void Place(int laneX, int laneW, int headCenterY, int headH, int tailY, int bodyInsetPx, int tailH)
        {
            int headW = laneW - 2;
            Playfield.PlaceRect(head.transform, laneX + 1, headCenterY - headH / 2, headW, headH, spriteSizePx);
            if (!Note.IsHold) return;

            int bodyW = Mathf.Max(2, laneW - 2 * bodyInsetPx);
            int bodyH = Mathf.Max(0, tailY - headCenterY);
            body.enabled = bodyH > 0;
            if (bodyH > 0)
                Playfield.PlaceRect(body.transform, laneX + bodyInsetPx, headCenterY, bodyW, bodyH, spriteSizePx);

            int tailW = Mathf.Max(2, laneW - 2 * (bodyInsetPx - 3));
            Playfield.PlaceRect(tail.transform, laneX + bodyInsetPx - 3, tailY - tailH, tailW, tailH, spriteSizePx);
        }
    }
}
