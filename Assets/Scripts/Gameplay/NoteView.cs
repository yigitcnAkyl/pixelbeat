using PixelBeat.Core;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Havuzdan alınan nota görseli (NeonSkin sprite'ları, 9-slice ile tam piksel boyut).
    /// Tap: baş + hale. Hold: baş + gövde + kuyruk ucu + hale. Kaçan notalar Ash renginde, halesiz.
    /// </summary>
    public class NoteView : MonoBehaviour
    {
        private SpriteRenderer head;
        private SpriteRenderer body;
        private SpriteRenderer tail;
        private SpriteRenderer glow;
        private NeonSkin skin;
        private PaletteColor shownColor = (PaletteColor)(-1);

        public ChartNote Note { get; private set; }

        public void Init(NeonSkin neonSkin, int sortingOrder, float glowAlpha)
        {
            skin = neonSkin;
            glow = CreatePart("Glow", SpriteDrawMode.Tiled, sortingOrder - 1);
            if (skin.Additive != null) glow.sharedMaterial = skin.Additive;
            glow.color = new Color(1f, 1f, 1f, glowAlpha);
            body = CreatePart("Body", SpriteDrawMode.Sliced, sortingOrder);
            tail = CreatePart("Tail", SpriteDrawMode.Sliced, sortingOrder);
            head = CreatePart("Head", SpriteDrawMode.Sliced, sortingOrder + 1); // baş gövdenin üstünde
        }

        private SpriteRenderer CreatePart(string partName, SpriteDrawMode mode, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.drawMode = mode;
            if (mode == SpriteDrawMode.Tiled) sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = order;
            return sr;
        }

        public void Bind(ChartNote note)
        {
            Note = note;
            transform.localPosition = Vector3.zero;
            body.enabled = note.IsHold;
            tail.enabled = note.IsHold;
            shownColor = (PaletteColor)(-1);
        }

        public void Unbind()
        {
            Note = null;
        }

        /// <summary>Renk (paletten) değişince sprite'ları değiştirir; kaçan nota halesiz.</summary>
        public void SetColor(PaletteColor color, bool missed)
        {
            if (color == shownColor) return;
            shownColor = color;
            head.sprite = skin.NoteHead(color);
            body.sprite = skin.HoldBody(color);
            tail.sprite = skin.HoldTail(color);
            glow.sprite = skin.NoteGlow(color);
            glow.enabled = !missed;
        }

        /// <summary>Sol kenar laneX, baş merkezi headCenterY. Hold için kuyruk ucu tailY (piksel).</summary>
        public void Place(int laneX, int laneW, int headCenterY, int headH, int tailY, int bodyInsetPx, int tailH)
        {
            int headW = laneW - 4;
            int headX = laneX + 2;
            int headY = headCenterY - headH / 2;
            Playfield.PlaceSized(head, headX, headY, headW, headH);
            Playfield.PlaceSized(glow, headX - 2, headY - 2, headW + 4, headH + 4);
            if (!Note.IsHold) return;

            int bodyW = Mathf.Max(4, laneW - 2 * bodyInsetPx);
            int bodyH = Mathf.Max(0, tailY - headCenterY);
            body.enabled = bodyH > 0;
            if (bodyH > 0) Playfield.PlaceSized(body, laneX + bodyInsetPx, headCenterY, bodyW, bodyH);

            int tailW = Mathf.Max(4, bodyW + 6);
            Playfield.PlaceSized(tail, laneX + bodyInsetPx - 3, tailY - tailH, tailW, tailH);
        }
    }
}
