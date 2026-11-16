using PixelBeat.Core;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>
    /// Havuzdan alınan nota görseli (NeonSkin sprite'ları, tam piksel). GDD §8 sanat yönü:
    /// Tap: neon elmas + additive hale. Hold: elmas baş + şerit renginde ışın gövde (döşemeli) + kuyrukta küçük elmas;
    /// gövdenin additive halesi tutulurken parlaklaşır. Kaçan notalar Ash renginde, halesiz.
    /// </summary>
    public class NoteView : MonoBehaviour
    {
        private SpriteRenderer head;
        private SpriteRenderer headGlow;
        private SpriteRenderer body;
        private SpriteRenderer bodyGlow;
        private SpriteRenderer tail;
        private SpriteRenderer tailGlow;
        private NeonSkin skin;
        private PaletteColor shownColor = (PaletteColor)(-1);
        private float glowAlpha;
        private float holdingGlowAlpha;

        public ChartNote Note { get; private set; }

        public void Init(NeonSkin neonSkin, int sortingOrder, float glowAlpha, float holdingGlowAlpha)
        {
            skin = neonSkin;
            this.glowAlpha = glowAlpha;
            this.holdingGlowAlpha = holdingGlowAlpha;
            bodyGlow = CreatePart("Body Glow", SpriteDrawMode.Tiled, sortingOrder - 2, true);
            headGlow = CreatePart("Glow", SpriteDrawMode.Simple, sortingOrder - 1, true);
            tailGlow = CreatePart("Tail Glow", SpriteDrawMode.Simple, sortingOrder - 1, true);
            body = CreatePart("Body", SpriteDrawMode.Tiled, sortingOrder, false);
            tail = CreatePart("Tail", SpriteDrawMode.Simple, sortingOrder + 1, false);
            head = CreatePart("Head", SpriteDrawMode.Simple, sortingOrder + 2, false); // baş gövdenin üstünde
        }

        private SpriteRenderer CreatePart(string partName, SpriteDrawMode mode, int order, bool additive)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.drawMode = mode;
            if (mode == SpriteDrawMode.Tiled) sr.tileMode = SpriteTileMode.Continuous;
            sr.sortingOrder = order;
            if (additive)
            {
                if (skin.Additive != null) sr.sharedMaterial = skin.Additive;
                sr.color = new Color(1f, 1f, 1f, glowAlpha);
            }
            return sr;
        }

        public void Bind(ChartNote note)
        {
            Note = note;
            transform.localPosition = Vector3.zero;
            body.enabled = bodyGlow.enabled = note.IsHold;
            tail.enabled = tailGlow.enabled = note.IsHold;
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
            headGlow.sprite = skin.NoteGlow(color);
            body.sprite = skin.HoldBody(color);
            bodyGlow.sprite = skin.HoldGlow(color);
            tail.sprite = skin.HoldTail(color);
            tailGlow.sprite = skin.TailGlow(color);
            headGlow.enabled = !missed;
            bool hold = Note != null && Note.IsHold;
            bodyGlow.enabled = hold && !missed;
            tailGlow.enabled = hold && !missed;
        }

        /// <summary>Hold tutulurken gövde halesi parlaklaşır.</summary>
        public void SetHolding(bool holding)
        {
            var c = bodyGlow.color;
            c.a = holding ? holdingGlowAlpha : glowAlpha;
            bodyGlow.color = c;
        }

        /// <summary>Şerit sol kenarı laneX, genişliği laneW; baş merkezi headCenterY. Hold için kuyruk merkezi tailY (piksel).</summary>
        public void Place(int laneX, int laneW, int headCenterY, int tailY)
        {
            int center2 = 2 * laneX + laneW; // şerit merkezinin 2 katı (tek genişlikte yarım piksel kaymasın)
            PlaceCentered(head, center2, headCenterY);
            PlaceCentered(headGlow, center2, headCenterY);
            if (!Note.IsHold) return;

            int bodyH = Mathf.Max(0, tailY - headCenterY);
            body.enabled = bodyH > 0;
            bodyGlow.enabled = bodyH > 0 && headGlow.enabled;
            if (bodyH > 0)
            {
                PlaceColumn(body, center2, headCenterY, bodyH);
                PlaceColumn(bodyGlow, center2, headCenterY, bodyH);
            }
            PlaceCentered(tail, center2, tailY);
            PlaceCentered(tailGlow, center2, tailY);
        }

        /// <summary>Sprite'ı (gerçek boyutuyla) yatay merkez center2/2 ve dikey merkez centerY'ye, sol-alt köşesi tam piksele oturacak şekilde koyar.</summary>
        private static void PlaceCentered(SpriteRenderer sr, int center2, int centerY)
        {
            if (sr.sprite == null) return;
            int w = (int)sr.sprite.rect.width, h = (int)sr.sprite.rect.height;
            Playfield.PlaceSized(sr, (center2 - w) / 2, centerY - h / 2, w, h);
        }

        /// <summary>Döşemeli dikey sütun (sprite genişliğinde), alttan y0'dan h piksel.</summary>
        private static void PlaceColumn(SpriteRenderer sr, int center2, int y0, int h)
        {
            if (sr.sprite == null) return;
            int w = (int)sr.sprite.rect.width;
            Playfield.PlaceSized(sr, (center2 - w) / 2, y0, w, h);
        }
    }
}
