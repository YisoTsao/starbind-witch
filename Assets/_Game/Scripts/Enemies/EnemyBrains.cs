using Starbind.VFX;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Per-enemy AI (GDD §10.1). Brains only steer (DesiredVelocity) and start attacks through
    /// Enemy helpers; they never touch HP directly. One brain instance per spawned enemy.</summary>
    public abstract class EnemyBrain
    {
        protected Enemy e;
        protected float readyAt;
        protected float stateUntil;
        protected Telegraph pending;
        /// <summary>False once this spawn died or went back to the pool; delayed callbacks check it
        /// so a stale telegraph never acts on a reused pooled enemy.</summary>
        protected bool alive = true;

        protected System.Action G(System.Action a) => () => { if (alive && e != null && e.Def != null && !e.Health.IsDead) a(); };

        public static EnemyBrain For(EnemyBehaviour b)
        {
            switch (b)
            {
                case EnemyBehaviour.Ranged: return new RangedBrain();
                case EnemyBehaviour.Charger: return new ChargerBrain();
                case EnemyBehaviour.Flanker: return new FlankerBrain();
                case EnemyBehaviour.Elite: return new EliteBrain();
                case EnemyBehaviour.Boss: return new BossBrain();
                default: return new ChaserBrain();
            }
        }

        public virtual void Init(Enemy enemy)
        {
            e = enemy;
            readyAt = Time.time + Random.Range(0.4f, 1.2f); // cosmetic stagger, not a gameplay roll
        }

        public abstract void Tick(float dt);

        public virtual void OnDeath()
        {
            alive = false;
            CancelPending();
        }

        public virtual void OnRelease()
        {
            alive = false;
            CancelPending();
        }

        protected void CancelPending()
        {
            if (pending != null && pending.isActiveAndEnabled)
                pending.Cancel();
            pending = null;
        }

        protected Vector2 Seek(float speedMul = 1f) => e.PlayerAlive ? e.DirToPlayer * e.Def.speed * speedMul : Vector2.zero;
    }

    /// <summary>E01 moss slime: chase, short hop on contact, attack interval.</summary>
    sealed class ChaserBrain : EnemyBrain
    {
        float hopUntil;
        Vector2 hopDir;

        public override void Tick(float dt)
        {
            float now = Time.time;
            if (now < hopUntil)
            {
                e.DesiredVelocity = hopDir * e.Def.speed * 2.6f;
                return;
            }
            e.DesiredVelocity = Seek();
            if (e.PlayerAlive && now >= readyAt && e.DistToPlayer < e.Def.attackRange + 0.35f)
            {
                readyAt = now + e.Def.attackCooldown;
                hopDir = e.DirToPlayer;
                hopUntil = now + 0.14f;
                e.HitPlayer(e.Def.damage, 1.2f);
            }
        }
    }

    /// <summary>E02 spore shroom: keeps distance, wind-up, then fires a spore at the player.</summary>
    sealed class RangedBrain : EnemyBrain
    {
        float fireAt = -1f;
        float strafe = 1f;

        public override void Init(Enemy enemy)
        {
            base.Init(enemy);
            strafe = enemy.Health.EntityId % 2 == 0 ? 1f : -1f;
        }

        public override void Tick(float dt)
        {
            float now = Time.time;
            if (fireAt > 0)
            {
                e.DesiredVelocity = Vector2.zero;
                if (now >= fireAt)
                {
                    fireAt = -1f;
                    var dir = (e.PlayerCenter - e.Center).normalized;
                    e.Shoot(dir, e.Def.projectileSpeed, e.Def.damage);
                    Sfx.Play("sfx_spore", 0.4f);
                }
                return;
            }
            float d = e.DistToPlayer;
            float pref = e.Def.preferredDistance;
            if (!e.PlayerAlive)
                e.DesiredVelocity = Vector2.zero;
            else if (d > pref + 1f)
                e.DesiredVelocity = Seek();
            else if (d < pref - 1f)
                e.DesiredVelocity = -Seek(0.8f);
            else
                e.DesiredVelocity = Vector2.Perpendicular(e.DirToPlayer) * strafe * e.Def.speed * 0.5f;
            if (e.PlayerAlive && now >= readyAt && d < pref + 3f)
            {
                readyAt = now + e.Def.attackCooldown;
                fireAt = now + 0.45f;
                e.Flash.Flash(0.4f, new Color(0.9f, 0.6f, 1f));
            }
        }
    }

    /// <summary>E03 root guard: slow approach, telegraphed straight charge, vulnerable recovery.</summary>
    sealed class ChargerBrain : EnemyBrain
    {
        enum S { Approach, Windup, Charge, Recover }
        S state;
        Vector2 dir;
        bool hit;

        public override void Tick(float dt)
        {
            float now = Time.time;
            switch (state)
            {
                case S.Approach:
                    e.DesiredVelocity = Seek();
                    if (e.PlayerAlive && now >= readyAt && e.DistToPlayer < 4.5f)
                    {
                        state = S.Windup;
                        dir = e.DirToPlayer;
                        e.DesiredVelocity = Vector2.zero;
                        pending = Telegraph.Line(e.Position + new Vector2(0, 0.3f), dir, 5.5f, 1.0f, 0.8f, G(() =>
                        {
                            pending = null;
                            state = S.Charge;
                            stateUntil = Time.time + 0.6f;
                            hit = false;
                            Sfx.Play("sfx_charge_enemy", 0.6f);
                        }));
                    }
                    break;
                case S.Windup:
                    e.DesiredVelocity = Vector2.zero;
                    break;
                case S.Charge:
                    e.DesiredVelocity = dir * 8.5f / Mathf.Max(0.5f, e.SpeedFactor);
                    if (!hit && e.PlayerAlive && Vector2.Distance(e.Position, e.PlayerPos) < 0.95f)
                        hit = e.HitPlayer(e.Def.damage, 3f, true) || e.Player.Motor.IsDashing;
                    if (now >= stateUntil)
                    {
                        state = S.Recover;
                        stateUntil = now + 1.1f;
                        e.Health.DamageTakenMultiplier = 1.3f; // guard broken (GDD: 破防需求)
                    }
                    break;
                case S.Recover:
                    e.DesiredVelocity = Vector2.zero;
                    if (now >= stateUntil)
                    {
                        e.Health.DamageTakenMultiplier = 1f;
                        state = S.Approach;
                        readyAt = now + e.Def.attackCooldown;
                    }
                    break;
            }
        }
    }

    /// <summary>E04 lost wisp: circles the player fast, then darts through on a short warning.</summary>
    sealed class FlankerBrain : EnemyBrain
    {
        float angle;
        float dartUntil = -1f;
        Vector2 dartDir;
        bool hit;
        bool winding;

        public override void Init(Enemy enemy)
        {
            base.Init(enemy);
            angle = enemy.Health.EntityId * 1.7f;
        }

        public override void Tick(float dt)
        {
            float now = Time.time;
            if (now < dartUntil)
            {
                e.DesiredVelocity = dartDir * 9f;
                if (!hit && e.PlayerAlive && Vector2.Distance(e.Position, e.PlayerPos) < 0.6f)
                    hit = e.HitPlayer(e.Def.damage, 1f);
                return;
            }
            if (winding)
            {
                e.DesiredVelocity = Vector2.zero;
                return;
            }
            if (!e.PlayerAlive)
            {
                e.DesiredVelocity = Vector2.zero;
                return;
            }
            angle += dt * 1.4f;
            var goal = e.PlayerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.8f;
            var to = goal - e.Position;
            e.DesiredVelocity = to.sqrMagnitude > 0.01f ? to.normalized * e.Def.speed : Vector2.zero;
            if (now >= readyAt && e.DistToPlayer < 3.6f)
            {
                readyAt = now + e.Def.attackCooldown;
                winding = true;
                dartDir = e.DirToPlayer;
                pending = Telegraph.Line(e.Position + new Vector2(0, 0.2f), dartDir, 4f, 0.5f, 0.45f, G(() =>
                {
                    pending = null;
                    winding = false;
                    hit = false;
                    dartUntil = Time.time + 0.4f;
                }));
            }
        }
    }

    /// <summary>EX01 spore elite: telegraphed spore fan, ground slam, summons slimes (capped).</summary>
    sealed class EliteBrain : EnemyBrain
    {
        int step;
        bool busy;
        int summoned;

        public override void Tick(float dt)
        {
            float now = Time.time;
            if (busy)
            {
                e.DesiredVelocity = Vector2.zero;
                return;
            }
            e.DesiredVelocity = e.DistToPlayer > 3.2f ? Seek() : Vector2.zero;
            if (!e.PlayerAlive || now < readyAt || e.DistToPlayer > 8f)
                return;
            readyAt = now + e.Def.attackCooldown;
            busy = true;
            switch (step++ % 3)
            {
                case 0: // spore fan: five lanes
                {
                    var dir = e.DirToPlayer;
                    for (int i = 0; i < 5; i++)
                    {
                        var d = (Vector2)(Quaternion.Euler(0, 0, (i - 2) * 15f) * dir);
                        var t = Telegraph.Line(e.Center, d, 6f, 0.4f, 0.75f, null);
                        if (i == 2)
                            pending = t;
                    }
                    e.Flash.Flash(0.7f, new Color(1f, 0.6f, 0.9f));
                    var main = Telegraph.Circle(e.Center, 0.1f, 0.75f, G(() =>
                    {
                        busy = false;
                        if (e.Health.IsDead) return;
                        for (int i = 0; i < 5; i++)
                            e.Shoot(Quaternion.Euler(0, 0, (i - 2) * 15f) * dir, 6f, e.Def.damage, 7f, 1.3f);
                        Sfx.Play("sfx_spore", 0.6f);
                    }));
                    break;
                }
                case 1: // slam
                    pending = Telegraph.Circle(e.Position, 2.1f, 0.9f, G(() =>
                    {
                        pending = null;
                        busy = false;
                        if (e.Health.IsDead) return;
                        VfxService.Instance.Play("VFX_thunder_ring", e.Position, 0, 2.6f, new Color(0.8f, 0.5f, 1f));
                        Starbind.CameraRig.CameraFollow2D.Main?.Shake(0.12f, 0.2f);
                        if (e.DistToPlayer < 2.1f)
                            e.HitPlayer(e.Def.damage * 1.3f, 2.5f, true);
                    }));
                    break;
                default: // summon
                    busy = false;
                    if (summoned < 6 && EnemySpawner.Exists)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            var p = EnemySpawner.Instance.RandomAround(e.Position, 1.4f);
                            if (EnemySpawner.Instance.SpawnAt("slime", p, true) != null)
                                summoned++;
                        }
                        VfxService.Instance.Play("VFX_portal", e.Position, 0, 1.5f, new Color(0.7f, 1f, 0.6f));
                    }
                    break;
            }
        }
    }
}
