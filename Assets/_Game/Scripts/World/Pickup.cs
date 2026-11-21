using System;
using Starbind.Core;
using Starbind.Player;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    public enum PickupKind { Xp, HpPotion, MpPotion, Material, Page }

    /// <summary>Dropped item (XP star shards, potions, materials, grimoire pages). Pooled; drifts to
    /// the player inside its magnet radius (GDD §8.1 auto-collect) and reports collection once.</summary>
    public sealed class Pickup : MonoBehaviour
    {
        static ComponentPool<Pickup> pool;
        static Transform root;
        public static event Action<Pickup> Collected;

        public PickupKind Kind { get; private set; }
        public string ItemId { get; private set; }
        public int Amount { get; private set; }

        SpriteRenderer sr;
        float speed;
        float spawnAt;
        Vector2 velocity;
        bool collected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pool = null;
            root = null;
            Collected = null;
        }

        public static int ActiveCount => pool != null && root != null ? pool.ActiveCount : 0;
        public static System.Collections.Generic.IEnumerable<Pickup> Active => pool != null && root != null ? pool.Active : System.Array.Empty<Pickup>();

        public static Pickup Drop(PickupKind kind, Vector2 pos, int amount = 1, string itemId = null, Vector2? scatter = null)
        {
            if (pool == null || root == null)
            {
                root = new GameObject("[Pickups]").transform;
                pool = new ComponentPool<Pickup>(() =>
                {
                    var go = new GameObject("pickup");
                    go.transform.SetParent(root, false);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sortingLayerName = StarbindLayers.SortWorld;
                    r.spriteSortPoint = SpriteSortPoint.Pivot;
                    var db = GameDatabase.Instance;
                    if (db != null && db.unlitMaterial != null)
                        r.sharedMaterial = db.unlitMaterial;
                    go.SetActive(false);
                    return go.AddComponent<Pickup>();
                }, 400);
            }
            var p = pool.Get();
            if (p == null)
                return null;
            p.Kind = kind;
            p.Amount = amount;
            p.ItemId = itemId;
            p.collected = false;
            p.speed = 0;
            p.spawnAt = Time.time;
            p.velocity = scatter ?? Vector2.zero;
            p.transform.position = pos;
            p.sr = p.sr != null ? p.sr : p.GetComponent<SpriteRenderer>();
            string sprite = kind switch
            {
                PickupKind.Xp => "PICK_xp",
                PickupKind.HpPotion => "PICK_potion_hp",
                PickupKind.MpPotion => "PICK_potion_mp",
                PickupKind.Page => "PICK_page",
                _ => "PICK_material",
            };
            p.sr.sprite = GameDatabase.Instance.Sprite(sprite);
            p.sr.color = Color.white;
            return p;
        }

        float MagnetRadius => Kind == PickupKind.Xp ? 2.4f : 1.3f;

        void Update()
        {
            if (collected)
                return;
            var player = PlayerController.Current;
            Vector2 p = transform.position;
            velocity = Vector2.MoveTowards(velocity, Vector2.zero, 6f * Time.deltaTime);
            p += velocity * Time.deltaTime;
            if (player != null && !player.Vitals.Health.IsDead && Time.time - spawnAt > 0.25f)
            {
                var target = (Vector2)player.transform.position + new Vector2(0, 0.35f);
                float d = Vector2.Distance(p, target);
                if (d < MagnetRadius)
                {
                    speed = Mathf.Min(speed + 30f * Time.deltaTime, 14f);
                    p = Vector2.MoveTowards(p, target, speed * Time.deltaTime);
                    if (d < 0.35f)
                    {
                        Collect();
                        return;
                    }
                }
            }
            transform.position = new Vector3(p.x, p.y, 0);
        }

        void Collect()
        {
            collected = true;
            Sfx.Play(Kind == PickupKind.Xp ? "sfx_xp" : "sfx_pickup", Kind == PickupKind.Xp ? 0.25f : 0.7f, 0.15f);
            if (Kind != PickupKind.Xp)
                VfxService.Instance.Sparks(transform.position, new Color(1f, 0.95f, 0.6f), 6, 2f);
            Collected?.Invoke(this);
            pool.Release(this);
        }

        public static void ClearAll() => pool?.ReleaseAll();
    }
}
