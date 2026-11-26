using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Burnable vines (GDD §11.3): block a shortcut; any Fire hit burns them away. The blocker
    /// opens at once; the sprites play the optional burn frames, then show the charred state.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class BurnableVine : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Collider2D blocker;
        [SerializeField] string burntSprite = "PROP_vine_burnt_draft";
        [SerializeField] string[] burnFrames = System.Array.Empty<string>();
        const float BurnFrameTime = 0.12f;
        public bool Burnt { get; private set; }
        Health health;

        public void Configure(SpriteRenderer sr, Collider2D block)
        {
            sprite = sr;
            blocker = block;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            health.AutoTargetable = false;
            health.Damaged += OnDamaged;
        }

        void Start() => health.DamageTakenMultiplier = 0f;

        void OnDestroy() => health.Damaged -= OnDamaged;

        void OnDamaged(Health h, DamageEvent e, DamageResult r)
        {
            if (Burnt || (e.Element != ElementType.Fire && !h.Status.Has(ElementType.Fire, Time.time)))
                return;
            Burn();
        }

        public void Burn()
        {
            Burnt = true;
            if (sprite != null)
            {
                if (burnFrames != null && burnFrames.Length > 0 && isActiveAndEnabled)
                    StartCoroutine(PlayBurn());
                else
                    ShowSprite(burntSprite);
            }
            if (blocker != null)
                blocker.enabled = false;
            health.enabled = false;
            var v = VfxService.Instance;
            v.Play("VFX_explosion", (Vector2)transform.position + new Vector2(0, 0.6f), 0, 1.4f);
            v.Sparks((Vector2)transform.position + new Vector2(0, 0.6f), new Color(1f, 0.6f, 0.2f), 20, 4f);
            v.Light(transform.position, new Color(1f, 0.6f, 0.2f), 3f, 1.6f, 0.6f);
            Sfx.Play("sfx_fire_hit", 0.9f);
            GameSession.Instance.UI.Notice("藤蔓燒盡，捷徑開啟");
        }

        System.Collections.IEnumerator PlayBurn()
        {
            foreach (var f in burnFrames)
            {
                ShowSprite(f);
                yield return new WaitForSeconds(BurnFrameTime);
            }
            ShowSprite(burntSprite);
        }

        void ShowSprite(string name)
        {
            var s = GameDatabase.Instance.Sprite(name);
            if (s == null)
                return;
            foreach (var sr in sprite.GetComponentsInChildren<SpriteRenderer>())
                sr.sprite = s;
        }
    }
}
