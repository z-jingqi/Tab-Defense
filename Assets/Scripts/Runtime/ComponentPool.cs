using System.Collections.Generic;
using UnityEngine;

namespace TapDefense
{
    // Fixed capacity: overload drops cosmetic effects or delays spawning, never grows on a phone.
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly Stack<T> free;
        private readonly List<T> all;
        public ComponentPool(T prefab, int capacity, Transform parent, System.Action<T> prepare = null)
        {
            free = new Stack<T>(capacity); all = new List<T>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                T item = Object.Instantiate(prefab, parent);
                item.gameObject.SetActive(false); prepare?.Invoke(item);
                all.Add(item); free.Push(item);
            }
        }
        public T Rent()
        {
            if (free.Count == 0) return null;
            T item = free.Pop(); item.gameObject.SetActive(true); return item;
        }
        public void Return(T item)
        {
            if (!item || !item.gameObject.activeSelf) return;
            item.gameObject.SetActive(false); free.Push(item);
        }
        public void ReturnAll()
        {
            free.Clear();
            foreach (T item in all) { item.gameObject.SetActive(false); free.Push(item); }
        }
    }
}
