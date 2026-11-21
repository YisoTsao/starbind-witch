using Starbind.Combat;
using Starbind.Core;
using UnityEngine;

namespace Starbind.Spells
{
    /// <summary>Scene-scoped pools for spell objects (projectiles, strikes, fields, tornadoes).
    /// Created lazily; owns the pooled GameObjects so a scene change cleans them up.</summary>
    public sealed class SpellPools : MonoBehaviour
    {
        public const int MaxProjectiles = 300;
        static SpellPools instance;

        public ComponentPool<Projectile> Projectiles { get; private set; }
        public ComponentPool<AreaStrike> Strikes { get; private set; }
        public ComponentPool<PullField> Pulls { get; private set; }
        public ComponentPool<FireTornadoZone> Tornadoes { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static SpellPools Instance
        {
            get
            {
                if (instance == null)
                    instance = new GameObject("[SpellPools]").AddComponent<SpellPools>();
                return instance;
            }
        }

        void Awake()
        {
            Projectiles = new ComponentPool<Projectile>(() => Make<Projectile>("projectile", true), MaxProjectiles);
            Strikes = new ComponentPool<AreaStrike>(() => Make<AreaStrike>("strike", false), 64);
            Pulls = new ComponentPool<PullField>(() => Make<PullField>("pull", false), 8);
            Tornadoes = new ComponentPool<FireTornadoZone>(() => Make<FireTornadoZone>("tornado", true), CombatWorld.MaxTornadoes);
            CombatWorld.Instance.SpawnTornado = (p, target) =>
            {
                var t = Tornadoes.Get();
                if (t != null)
                    t.Launch(p);
            };
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        T Make<T>(string name, bool withSprite) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            if (withSprite)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                var db = GameDatabase.Instance;
                if (db != null && db.unlitMaterial != null)
                    sr.sharedMaterial = db.unlitMaterial;
                sr.sortingLayerName = StarbindLayers.SortVfx;
                go.AddComponent<VFX.SpriteFlipbook>();
            }
            var c = go.AddComponent<T>();
            go.SetActive(false);
            return c;
        }

        public Projectile Spawn() => Projectiles.Get();
    }
}
