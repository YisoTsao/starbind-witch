using System;
using System.Collections;
using Starbind.CameraRig;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Shared boss rules (GDD §10.3): three phases at 70% / 35% health with 1 s of
    /// invulnerability on each change, every big attack telegraphed on the ground for at least
    /// <see cref="MinWindup"/> seconds, a leash to the arena centre, a weak-point window (damage taken
    /// x1.5) and capped summons. Subclasses only pick and run attacks.</summary>
    public abstract class BossBrainBase : EnemyBrain
    {
        public const float MinWindup = 0.7f;
        public const float ExposedDamageTaken = 1.5f;
        public int Phase { get; private set; } = 1;
        public bool Exposed => Time.time < exposedUntil;
        public int AttacksStarted { get; private set; }

        protected Vector2 home;
        protected bool busy;
        protected int cycle;
        protected float exposedUntil;
        protected int summoned;

        protected virtual float Leash => 5.5f;
        protected virtual float KeepDistance => 4.5f;
        protected virtual int SummonCap => 16;
        protected float CdScale => Phase == 3 ? 0.65f : Phase == 2 ? 0.85f : 1f;

        public override void Init(Enemy enemy)
        {
            base.Init(enemy);
            home = enemy.Position;
            readyAt = Time.time + 1.5f;
            Phase = 1;
            OnInit();
        }

        protected virtual void OnInit() { }

        /// <summary>How many attacks the rotation has in <paramref name="phase"/> (attack i = cycle % count).</summary>
        protected abstract int AttackCount(int phase);

        protected abstract void Attack(int pick);

        protected virtual void OnPhaseChanged(int phase) { }

        /// <summary>Extra per-frame logic (charge meters, teleports); runs before the attack choice.</summary>
        protected virtual void OnTick(float dt) { }

        public override void Tick(float dt)
        {
            float now = Time.time;
            float hp = e.Health.Normalized;
            int want = hp > 0.7f ? 1 : hp > 0.35f ? 2 : 3;
            if (want > Phase)
            {
                Phase = want;
                e.Health.InvulnerableUntil = now + 1f;
                e.Flash.Flash(0.6f, new Color(1f, 0.6f, 0.9f));
                CameraFollow2D.Main?.Shake(0.2f, 0.6f);
                VfxService.Instance.Play("VFX_overdrive_aura", e.Position, 0, 3f, new Color(1f, 0.5f, 0.8f));
                Sfx.Play("sfx_boss_roar", 1f);
                readyAt = now + 1.2f;
                OnPhaseChanged(Phase);
            }
            e.Health.DamageTakenMultiplier = Exposed ? ExposedDamageTaken : 1f;
            OnTick(dt);
            if (busy)
            {
                e.DesiredVelocity = Vector2.zero;
                return;
            }
            e.DesiredVelocity = Steer(dt);
            if (!e.PlayerAlive || now < readyAt)
                return;
            readyAt = now + e.Def.attackCooldown * CdScale;
            AttacksStarted++;
            Attack(cycle++ % Mathf.Max(1, AttackCount(Phase)));
        }

        /// <summary>Slow drift toward the player, leashed to the arena centre.</summary>
        protected virtual Vector2 Steer(float dt)
        {
            var toPlayer = e.PlayerPos - e.Position;
            var v = toPlayer.magnitude > KeepDistance && e.PlayerAlive ? toPlayer.normalized * e.Def.speed : Vector2.zero;
            if ((e.Position + v * dt - home).magnitude > Leash)
                v = (home - e.Position).normalized * e.Def.speed;
            return v;
        }

        // ------------------------------------------------------------------ helpers
        protected void Windup(bool on)
        {
            if (on) e.Flip.SetRange(4, 2, 6f);
            else e.Flip.SetRange(0, 4, e.Def.spriteFps);
        }

        protected void Begin()
        {
            busy = true;
            Windup(true);
        }

        protected void Finish()
        {
            Windup(false);
            busy = false;
        }

        protected void Expose(float seconds, Color tint)
        {
            exposedUntil = Time.time + seconds;
            e.Health.DamageTakenMultiplier = ExposedDamageTaken;
            e.Flash.Flash(0.3f, tint);
            VfxService.Instance.Play("VFX_levelup", e.Center, 0, 2.5f, tint);
        }

        /// <summary>Runs <paramref name="a"/> after <paramref name="seconds"/> if this spawn is still alive.</summary>
        protected void After(float seconds, Action a) => e.StartCoroutine(Delayed(seconds, G(a)));

        static IEnumerator Delayed(float seconds, Action a)
        {
            if (seconds > 0)
                yield return new WaitForSeconds(seconds);
            a();
        }

        protected int Summon(int count, float radius, string id = null)
        {
            if (summoned >= SummonCap || !EnemySpawner.Exists)
                return 0;
            int n = 0;
            for (int i = 0; i < count && summoned < SummonCap; i++)
            {
                string pick = id ?? (i % 2 == 1 && !string.IsNullOrEmpty(e.Def.summonId2) ? e.Def.summonId2 : e.Def.summonId);
                if (string.IsNullOrEmpty(pick))
                    pick = "slime";
                var p = EnemySpawner.Instance.RandomAround(e.Position, radius);
                if (EnemySpawner.Instance.SpawnAt(pick, p, true) != null)
                {
                    summoned++;
                    n++;
                }
            }
            if (n > 0)
            {
                VfxService.Instance.Play("VFX_portal", e.Position, 0, 2.5f, new Color(0.75f, 0.8f, 1f));
                Sfx.Play("sfx_summon", 0.8f);
            }
            return n;
        }

        /// <summary>True when the player's feet are within <paramref name="halfWidth"/> of the segment.</summary>
        protected bool PlayerOnLine(Vector2 from, Vector2 dir, float length, float halfWidth)
        {
            var to = e.PlayerPos - from;
            float t = Mathf.Clamp(Vector2.Dot(to, dir), 0, length);
            return (from + dir * t - e.PlayerPos).sqrMagnitude <= halfWidth * halfWidth;
        }

        /// <summary>Telegraphed straight beam/line hit; calls <paramref name="then"/> after it lands.</summary>
        protected Telegraph LineAttack(Vector2 from, Vector2 dir, float length, float width, float windup, float damageMul,
            string vfx, Color col, Action then = null)
        {
            return Telegraph.Line(from, dir, length, width, Mathf.Max(MinWindup, windup), G(() =>
            {
                int steps = Mathf.Max(2, Mathf.RoundToInt(length / 1.2f));
                for (int i = 0; i <= steps; i++)
                    VfxService.Instance.Play(vfx, from + dir * (length * i / steps), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 0.9f, col);
                if (PlayerOnLine(from, dir, length, width * 0.5f + 0.2f))
                    e.HitPlayer(e.Def.damage * damageMul, 2f, true);
                then?.Invoke();
            }));
        }

        /// <summary>Telegraphed circle blast; calls <paramref name="then"/> after it lands.</summary>
        protected Telegraph CircleAttack(Vector2 p, float radius, float windup, float damageMul, string vfx, Color col, Action then = null)
        {
            return Telegraph.Circle(p, radius, Mathf.Max(MinWindup, windup), G(() =>
            {
                VfxService.Instance.Play(vfx, p, 0, radius * 1.1f, col);
                if (Vector2.Distance(e.PlayerPos, p) < radius)
                    e.HitPlayer(e.Def.damage * damageMul, 2f, true);
                then?.Invoke();
            }));
        }

        protected void Ring(int count, float speed, float damageMul, float offsetDeg = 0, string sprite = null, float scale = 1.3f)
        {
            for (int i = 0; i < count; i++)
                e.Shoot(Quaternion.Euler(0, 0, offsetDeg + i * 360f / count) * Vector2.right, speed, e.Def.damage * damageMul, 10f, scale, sprite);
        }

        public override void OnDeath()
        {
            base.OnDeath();
            e.StopAllCoroutines();
        }

        public override void OnRelease()
        {
            base.OnRelease();
            e.StopAllCoroutines();
        }
    }
}
