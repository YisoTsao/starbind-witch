using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starbind.Core
{
    /// <summary>Pool of inactive component instances (enemies, projectiles, VFX). Get() activates,
    /// Release() deactivates; nothing is destroyed during play. Optional hard cap.</summary>
    public sealed class ComponentPool<T> where T : Component
    {
        readonly Func<T> create;
        readonly Stack<T> free = new Stack<T>();
        readonly HashSet<T> live = new HashSet<T>();
        readonly int cap;

        public ComponentPool(Func<T> create, int cap = int.MaxValue)
        {
            this.create = create;
            this.cap = cap;
        }

        public int ActiveCount => live.Count;
        public int TotalCreated { get; private set; }
        public IEnumerable<T> Active => live;

        public T Get()
        {
            T item = null;
            while (free.Count > 0 && item == null)
                item = free.Pop();
            if (item == null)
            {
                if (TotalCreated >= cap)
                    return null;
                item = create();
                TotalCreated++;
            }
            live.Add(item);
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null || !live.Remove(item))
                return;
            item.gameObject.SetActive(false);
            free.Push(item);
        }

        public void ReleaseAll()
        {
            foreach (var item in new List<T>(live))
                Release(item);
        }
    }
}
