using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Player;
using Starbind.Spells;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>One pooled enemy: physics body, Health, animation, status visuals and the shared attack
    /// helpers its brain uses. Behaviour lives in EnemyBrain subclasses; spawning in EnemySpawner.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class Enemy : MonoBehaviour, IKnockbackReceiver
    {
        public const float ChillSlow = 0.7f;

        public EnemyDefinition Def { get; private set; }
        public Health Health { get; private set; }
        public EnemyBrain Brain { get; private set; }
        public SpriteRenderer Visual { get; private set; }
        public SpriteFlipbook Flip { get; private set; }
        public HitFlash Flash { get; private set; }
        public Vector2 DesiredVelocity { get; set; }
        public float StunUntil { get; set; }
        public bool FaceLock { get; set; }
        public float SpawnedAt { get; private set; }
        public static Enemy ActiveBoss { get; private set; }
        public event Action<Enemy> Released;

        Rigidbody2D body;
        CircleCollider2D col;
        Vector2 knock;
        Vector2 pull;
        SpriteFlipbook burnFx;
        float releaseAt = -1f;
        Color baseTint = Color.white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ActiveBoss = null;

        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public Vector2 Center => Health.Center;

        public static Enemy Create(Transform parent)
        {
            var go = new GameObject("enemy") { layer = LayerMask.NameToLayer(StarbindLayers.Enemy) };
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = 0;
            go.AddComponent<CircleCollider2D>();
            go.AddComponent<Health>();
            var vis = new GameObject("Visual");
            vis.transform.SetParent(go.transform, false);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = StarbindLayers.SortWorld;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var db = GameDatabase.Instance;
            if (db != null && db.characterMaterial != null)
                sr.sharedMaterial = db.characterMaterial;
            vis.AddComponent<SpriteFlipbook>();
            var flash = go.AddComponent<HitFlash>();
            flash.Bind(sr);
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(go.transform, false);
            var ssr = shadow.AddComponent<SpriteRenderer>();
            ssr.sprite = db != null ? db.Sprite("VFX_shadow") : null;
            ssr.sortingLayerName = StarbindLayers.SortGroundDecal;
            if (db != null && db.unlitMaterial != null)
                ssr.sharedMaterial = db.unlitMaterial;
            var e = go.AddComponent<Enemy>();
            go.SetActive(false);
            return e;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();
            Health = GetComponent<Health>();
            Visual = transform.Find("Visual").GetComponent<SpriteRenderer>();
            Flip = Visual.GetComponent<SpriteFlipbook>();
            Flash = GetComponent<HitFlash>();
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (Health != null)
            {
                Health.Damaged -= OnDamaged;
                Health.Died -= OnDied;
            }
        }

        public void Spawn(EnemyDefinition def, Vector2 pos, float hpMultiplier = 1f)
        {
            Def = def;
            float scale = def.isBoss ? 1f : 1f;
            Health.Configure(Team.Enemy, def.hp * hpMultiplier, def.radius, new Vector2(0, def.radius + 0.1f));
            col.radius = def.radius;
            col.offset = new Vector2(0, def.radius * 0.6f);
            body.position = pos;
            transform.position = pos;
            body.linearVelocity = Vector2.zero;
            body.mass = def.isBoss ? 100f : def.isElite ? 8f : 1f;
            body.bodyType = def.isBoss ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            col.enabled = true;
            knock = pull = Vector2.zero;
            StunUntil = 0;
            FaceLock = false;
            releaseAt = -1f;
            SpawnedAt = Time.time;
            transform.localScale = Vector3.one * scale;
            Visual.color = baseTint = Color.white;
            Visual.transform.localPosition = Vector3.zero;
            Visual.sortingLayerName = StarbindLayers.SortWorld;
            var shadow = transform.Find("Shadow");
            shadow.localScale = new Vector3(def.radius * 5.2f, def.radius * 4f, 1);
            Flip.Play(GameDatabase.Instance.Sprites(def.sprite), true, def.spriteFps, 0, def.isBoss ? 4 : -1);
            Brain = EnemyBrain.For(def.behaviour);
            Brain.Init(this);
            if (def.isBoss)
                ActiveBoss = this;
            VfxService.Instance.Play("VFX_poof", pos + new Vector2(0, 0.2f), 0, def.isBoss ? 2.5f : 0.8f, new Color(0.8f, 0.7f, 1f));
        }

        // ------------------------------------------------------------------ helpers for brains
        public PlayerController Player => PlayerController.Current;
        public Vector2 PlayerPos => Player != null ? (Vector2)Player.transform.position : Position;
        public Vector2 PlayerCenter => Player != null ? Player.Vitals.Health.Center : Center;
        public float DistToPlayer => Vector2.Distance(Position, PlayerPos);
        public Vector2 DirToPlayer => (PlayerPos - Position).sqrMagnitude > 0.0001f ? (PlayerPos - Position).normalized : Vector2.down;
        public bool PlayerAlive => Player != null && !Player.Vitals.Health.IsDead;
        public bool Stunned => Time.time < StunUntil;
        public float SpeedFactor
        {
            get
            {
                float f = Health.Status.Has(ElementType.Ice, Time.time) ? ChillSlow : 1f;
                return Def.isBoss ? Mathf.Max(f, 0.85f) : f;
            }
        }

        /// <summary>Melee/area hit on the player; a dash-invulnerable player gets a perfect dodge.</summary>
        public bool HitPlayer(float damage, float knockback, bool important = false)
        {
            if (!PlayerAlive)
                return false;
            var ph = Player.Vitals.Health;
            if (Player.Motor.IsDashing)
            {
                Player.Combat.NotifyPerfectDodge(important || Def.isElite || Def.isBoss);
                return false;
            }
            var e = new DamageEvent
            {
                SourceEntityId = Health.EntityId,
                Amount = damage,
                Source = HitSource.Enemy,
                Knockback = knockback,
                Direction = DirToPlayer,
                CannotTriggerReaction = true,
            };
            return CombatWorld.Instance.Damage(e, ph).Applied;
        }

        public Projectile Shoot(Vector2 dir, float speed, float damage, float range = 9f, float scale = 1f, string sprite = null)
        {
            if (string.IsNullOrEmpty(sprite))
                sprite = Def != null && !string.IsNullOrEmpty(Def.projectileSprite) ? Def.projectileSprite : "VFX_spore";
            var spec = new ProjectileSpec
            {
                Team = Team.Enemy,
                Hit = new DamageEvent { SourceEntityId = Health.EntityId, Amount = damage, Source = HitSource.Enemy, Knockback = 0.6f, CannotTriggerReaction = true },
                Position = Center,
                Direction = dir,
                Speed = speed,
                MaxDistance = range,
                Radius = 0.18f * scale,
                Sprite = sprite,
                Rotate = sprite != "VFX_spore",
                Scale = scale,
                ImpactVfx = "VFX_poof",
                ImpactColor = new Color(0.8f, 0.5f, 1f),
            };
            spec.Hit.CastId = CombatIds.NewCast();
            return Projectile.Launch(spec);
        }

        public void Knockback(Vector2 direction, float strength)
        {
            float resist = Mathf.Clamp01(Def != null ? Def.knockbackResist : 0f);
            if (resist >= 1f)
                return;
            knock = direction.normalized * strength * 3f * (1f - resist);
            if (strength >= 2f && !Def.isBoss)
                StunUntil = Mathf.Max(StunUntil, Time.time + 0.25f * (1f - resist)); // finisher stagger
        }

        public void Pull(Vector2 velocity)
        {
            if (Def != null && Def.knockbackResist < 1f)
                pull += velocity * (1f - Def.knockbackResist * 0.8f);
        }

        // ------------------------------------------------------------------ lifecycle
        void OnDamaged(Health h, DamageEvent e, DamageResult r)
        {
            if (!r.Applied)
                return;
            Flash.Flash(0.09f);
        }

        void OnDied(Health h)
        {
            col.enabled = false;
            body.linearVelocity = Vector2.zero;
            Brain?.OnDeath();
            var v = VfxService.Instance;
            v.Play("VFX_poof", Center, 0, Def.isBoss ? 3f : Def.isElite ? 1.6f : 1f);
            v.Sparks(Center, new Color(0.9f, 0.85f, 1f), Def.isBoss ? 40 : 8, 4f);
            Sfx.Play(Def.isBoss ? "sfx_boss_die" : "sfx_enemy_die", 0.6f);
            if (Def.isBoss)
            {
                CameraRig.CameraFollow2D.Main?.Shake(0.25f, 0.8f);
                v.Light(Center, new Color(1f, 0.85f, 0.6f), 8f, 2.5f, 1.2f);
            }
            releaseAt = Time.time + 0.06f;
            Visual.enabled = false;
        }

        void Update()
        {
            if (releaseAt > 0)
            {
                if (Time.time >= releaseAt)
                    Release();
                return;
            }
            if (Health.IsDead || Def == null)
                return;
            if (!Stunned)
                Brain.Tick(Time.deltaTime);
            else
                DesiredVelocity = Vector2.zero;
            if (!FaceLock)
            {
                float vx = DesiredVelocity.x;
                if (Mathf.Abs(vx) > 0.05f)
                    Visual.flipX = Def.behaviour == EnemyBehaviour.Charger ? vx > 0 : vx < 0;
            }
            UpdateStatusVisuals();
        }

        void FixedUpdate()
        {
            if (Def == null || Health.IsDead)
                return;
            var v = (Stunned ? Vector2.zero : DesiredVelocity * SpeedFactor) + knock + pull;
            if (body.bodyType == RigidbodyType2D.Kinematic)
                body.MovePosition(body.position + v * Time.fixedDeltaTime);
            else
                body.linearVelocity = v;
            knock = Vector2.MoveTowards(knock, Vector2.zero, 25f * Time.fixedDeltaTime);
            pull = Vector2.zero;
        }

        void UpdateStatusVisuals()
        {
            float now = Time.time;
            var st = Health.Status;
            Color tint = baseTint;
            if (st.Has(ElementType.Ice, now)) tint = new Color(0.65f, 0.85f, 1f);
            else if (st.Has(ElementType.Lightning, now)) tint = new Color(1f, 1f, 0.7f);
            else if (st.Has(ElementType.Wind, now)) tint = new Color(0.8f, 1f, 0.9f);
            else if (st.Has(ElementType.Nature, now)) tint = new Color(0.78f, 1f, 0.62f);
            else if (st.Has(ElementType.Moon, now)) tint = new Color(0.8f, 0.7f, 1f);
            Visual.color = tint;
            bool burning = st.Has(ElementType.Fire, now);
            if (burning && burnFx == null)
            {
                burnFx = VfxService.Instance.Play("VFX_burn", Center, 0, 1f, null, StarbindLayers.SortVfx, 1, true, transform);
                if (burnFx != null)
                    burnFx.FollowOffset = new Vector2(0, Def.radius * 2f + 0.2f);
            }
            else if (!burning && burnFx != null)
            {
                VfxService.Instance.Release(burnFx);
                burnFx = null;
            }
        }

        public void Release()
        {
            if (burnFx != null)
            {
                VfxService.Instance.Release(burnFx);
                burnFx = null;
            }
            if (ActiveBoss == this)
                ActiveBoss = null;
            Brain?.OnRelease();
            Brain = null;
            Visual.enabled = true;
            releaseAt = -1f;
            Def = null;
            Released?.Invoke(this);
        }
    }
}
