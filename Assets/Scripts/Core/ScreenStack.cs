using System;
using System.Collections.Generic;

namespace PixelBeat.Core
{
    /// <summary>
    /// Ekran geçmişi (GDD §11): ileri gidince itilir, geri tuşu bir öncekine döner. Kökte geri = çıkış isteği.
    /// Aynı ekran tekrar açılırsa yığındaki eski kopyasına dönülür (döngü birikmez).
    /// </summary>
    public sealed class ScreenStack<T>
    {
        private readonly List<T> items = new List<T>();
        private readonly IEqualityComparer<T> cmp = EqualityComparer<T>.Default;

        public int Count => items.Count;
        public T Current => items.Count > 0 ? items[items.Count - 1] : throw new InvalidOperationException("Yığın boş.");
        public bool IsEmpty => items.Count == 0;

        /// <summary>Kök ekranla başlatır (geçmiş silinir).</summary>
        public void Reset(T root)
        {
            items.Clear();
            items.Add(root);
        }

        /// <summary>Yeni ekrana geçer. Ekran zaten yığındaysa onun üstündekiler atılır.</summary>
        public void Push(T screen)
        {
            int existing = items.FindIndex(x => cmp.Equals(x, screen));
            if (existing >= 0) items.RemoveRange(existing + 1, items.Count - existing - 1);
            else items.Add(screen);
        }

        /// <summary>Bir önceki ekrana döner. Kökteyse false (uygulamadan çıkış).</summary>
        public bool TryBack(out T previous)
        {
            if (items.Count <= 1)
            {
                previous = items.Count == 1 ? items[0] : default;
                return false;
            }
            items.RemoveAt(items.Count - 1);
            previous = items[items.Count - 1];
            return true;
        }

        public bool Contains(T screen) => items.Exists(x => cmp.Equals(x, screen));
    }
}
