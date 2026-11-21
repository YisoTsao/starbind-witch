using System.Collections.Generic;
using Starbind.Core;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Finds the nearest Interactable in reach, shows the F prompt over it, and uses it.</summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] float reach = 0.75f;
        [SerializeField] Vector2 probeOffset = new Vector2(0f, 0.3f);
        [SerializeField] SpriteRenderer prompt;

        readonly List<Collider2D> hits = new List<Collider2D>(8);
        ContactFilter2D filter;

        public Interactable Current { get; private set; }

        void Awake()
        {
            filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
            filter.SetLayerMask(LayerMask.GetMask(StarbindLayers.Interactable));
            if (prompt != null)
                prompt.enabled = false;
        }

        void Update()
        {
            Current = FindNearest();
            if (prompt == null)
                return;
            prompt.enabled = Current != null;
            if (Current != null)
                prompt.transform.position = Current.PromptPosition;
        }

        public bool TryInteract()
        {
            Current = FindNearest();
            if (Current == null)
                return false;
            Current.Interact();
            return true;
        }

        Interactable FindNearest()
        {
            Vector2 origin = (Vector2)transform.position + probeOffset;
            Physics2D.OverlapCircle(origin, reach, filter, hits);
            Interactable best = null;
            float bestDist = float.MaxValue;
            foreach (var c in hits)
            {
                if (!c.TryGetComponent(out Interactable it))
                    continue;
                float d = ((Vector2)c.transform.position - origin).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = it;
                }
            }
            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + (Vector3)probeOffset, reach);
        }
    }
}
