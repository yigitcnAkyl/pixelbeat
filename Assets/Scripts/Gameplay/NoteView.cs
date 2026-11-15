using UnityEngine;

namespace PixelBeat.Gameplay
{
    /// <summary>Havuzdan alınan nota görseli.</summary>
    public class NoteView : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;

        public ChartNote Note { get; private set; }

        public void Init(Sprite sprite, int sortingOrder)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        public void Bind(ChartNote note, Color color)
        {
            Note = note;
            spriteRenderer.color = color;
        }

        public void Unbind()
        {
            Note = null;
        }

        public void SetColor(Color color)
        {
            spriteRenderer.color = color;
        }
    }
}
