using System;
using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>A blocker that opens when its id is signalled (crystal circuits, mine-cart rubble, beam
    /// doors, the citadel hall). Collider off + sprites swapped/hidden; never closes again in a run.</summary>
    public sealed class PowerGate : MonoBehaviour
    {
        [SerializeField] string gateId;
        [SerializeField] Collider2D blocker;
        [SerializeField] SpriteRenderer[] visuals;
        [SerializeField] string openSprite;
        [SerializeField] string notice;
        static readonly Dictionary<string, List<PowerGate>> byId = new Dictionary<string, List<PowerGate>>();

        public string Id => gateId;
        public bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => byId.Clear();

        public void Configure(string id, Collider2D block, SpriteRenderer[] sprites, string spriteWhenOpen, string text)
        {
            gateId = id;
            blocker = block;
            visuals = sprites;
            openSprite = spriteWhenOpen;
            notice = text;
        }

        void OnEnable()
        {
            if (!byId.TryGetValue(gateId, out var l))
                byId[gateId] = l = new List<PowerGate>();
            l.Add(this);
        }

        void OnDisable()
        {
            if (byId.TryGetValue(gateId, out var l))
                l.Remove(this);
        }

        /// <summary>Opens every gate with <paramref name="id"/>; returns how many changed.</summary>
        public static int Open(string id)
        {
            if (string.IsNullOrEmpty(id) || !byId.TryGetValue(id, out var l))
                return 0;
            int n = 0;
            foreach (var g in l.ToArray())
                if (g.OpenNow())
                    n++;
            return n;
        }

        public static bool IsGateOpen(string id)
        {
            if (!byId.TryGetValue(id, out var l) || l.Count == 0)
                return false;
            foreach (var g in l)
                if (!g.IsOpen)
                    return false;
            return true;
        }

        bool OpenNow()
        {
            if (IsOpen)
                return false;
            IsOpen = true;
            if (blocker != null)
                blocker.enabled = false;
            var db = GameDatabase.Instance;
            foreach (var sr in visuals ?? Array.Empty<SpriteRenderer>())
            {
                if (sr == null) continue;
                var s = string.IsNullOrEmpty(openSprite) ? null : db.Sprite(openSprite);
                if (s != null) sr.sprite = s;
                else sr.enabled = false;
            }
            var v = VfxService.Instance;
            v.Play("VFX_portal", transform.position, 0, 1.8f, new Color(0.8f, 0.9f, 1f));
            v.Sparks(transform.position, new Color(0.85f, 0.9f, 1f), 18, 4f);
            Sfx.Play("sfx_portal", 0.8f);
            if (!string.IsNullOrEmpty(notice) && GameSession.Exists)
                GameSession.Instance.UI.Notice(notice);
            return true;
        }
    }
}
