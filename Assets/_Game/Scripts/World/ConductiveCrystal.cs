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
    /// <summary>Conductive crystal / lake pylon (GDD §11.3 "魔法水晶：雷擊/互動"): a lightning hit lights
    /// it from range, F lights it by hand (so every build can pass). When every crystal of its circuit is
    /// lit, the circuit's gate opens.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class ConductiveCrystal : MonoBehaviour
    {
        [SerializeField] string circuit;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] string litSprite;
        static readonly Dictionary<string, List<ConductiveCrystal>> circuits = new Dictionary<string, List<ConductiveCrystal>>();
        Health health;

        public bool Lit { get; private set; }
        public string Circuit => circuit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => circuits.Clear();

        public void Configure(string id, SpriteRenderer sr, string spriteWhenLit)
        {
            circuit = id;
            sprite = sr;
            litSprite = spriteWhenLit;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            health.AutoTargetable = false;
            health.Damaged += OnDamaged;
            var it = GetComponentInChildren<Interactable>();
            if (it != null)
                it.Interacted += _ => Light();
        }

        void Start() => health.DamageTakenMultiplier = 0f;

        void OnEnable()
        {
            if (!circuits.TryGetValue(circuit, out var l))
                circuits[circuit] = l = new List<ConductiveCrystal>();
            l.Add(this);
        }

        void OnDisable()
        {
            if (circuits.TryGetValue(circuit, out var l))
                l.Remove(this);
        }

        void OnDestroy() => health.Damaged -= OnDamaged;

        void OnDamaged(Health h, DamageEvent e, DamageResult r)
        {
            if (e.Element == ElementType.Lightning)
                Light();
        }

        public void Light()
        {
            if (Lit)
                return;
            Lit = true;
            if (sprite != null && !string.IsNullOrEmpty(litSprite))
                sprite.sprite = GameDatabase.Instance.Sprite(litSprite);
            var v = VfxService.Instance;
            v.Play("VFX_shock", (Vector2)transform.position + new Vector2(0, 0.7f), 0, 1.6f, new Color(1f, 0.95f, 0.6f));
            v.Light(transform.position, new Color(0.7f, 0.8f, 1f), 3f, 1.2f, 0.6f);
            Sfx.Play("sfx_thunder", 0.6f);
            int lit = 0, total = 0;
            foreach (var c in circuits[circuit])
            {
                total++;
                if (c.Lit) lit++;
            }
            if (lit < total)
            {
                GameSession.Instance.UI.Notice($"導電水晶 {lit}/{total}");
                return;
            }
            foreach (var c in circuits[circuit])
                if (c != this)
                    v.Bolt((Vector2)transform.position + new Vector2(0, 0.7f), (Vector2)c.transform.position + new Vector2(0, 0.7f), new Color(0.75f, 0.85f, 1f), 0.4f, 2, 4f);
            PowerGate.Open(circuit);
        }
    }
}
