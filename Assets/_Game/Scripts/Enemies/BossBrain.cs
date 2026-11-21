using Starbind.CameraRig;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Eclipse Mushroom King, three phases (GDD §10.3):
    /// P1 100-70%: aimed spore volleys and poison circles. P2 70-35%: adds summons and a radial spore
    /// ring. P3 35-0%: faster, staggered warning zones, then an exposed window (takes extra damage).
    /// Every big attack has a ground warning of at least 0.7 s; phase changes give 1 s of invulnerability.</summary>
    public sealed class BossBrain : EnemyBrain
    {
        public const float MinWindup = 0.7f;
        public int Phase { get; private set; } = 1;
        public bool Exposed => Time.time < exposedUntil;
        public int AttacksStarted { get; private set; }

        Vector2 home;
        bool busy;
        int cycle;
        float exposedUntil;
        int summoned;

        public override void Init(Enemy enemy)
        {
            base.Init(enemy);
            home = enemy.Position;
            readyAt = Time.time + 1.5f;
            Phase = 1;
        }

        float CdScale => Phase == 3 ? 0.65f : Phase == 2 ? 0.85f : 1f;

        void Windup(bool on)
        {
            if (on) e.Flip.SetRange(4, 2, 6f);
            else e.Flip.SetRange(0, 4, e.Def.spriteFps);
        }

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
            }
            e.Health.DamageTakenMultiplier = Exposed ? 1.5f : 1f;
            if (busy)
            {
                e.DesiredVelocity = Vector2.zero;
                return;
            }
            // slow drift toward the player, leashed to the arena centre
            var toPlayer = e.PlayerPos - e.Position;
            var v = toPlayer.magnitude > 4.5f && e.PlayerAlive ? toPlayer.normalized * e.Def.speed : Vector2.zero;
            if ((e.Position + v * dt - home).magnitude > 5.5f)
                v = (home - e.Position).normalized * e.Def.speed;
            e.DesiredVelocity = v;
            if (!e.PlayerAlive || now < readyAt)
                return;
            readyAt = now + e.Def.attackCooldown * CdScale;
            AttacksStarted++;
            int pick = cycle++ % (Phase == 1 ? 2 : Phase == 2 ? 4 : 5);
            switch (pick)
            {
                case 0: SporeVolley(); break;
                case 1: PoisonCircles(); break;
                case 2: Summon(); break;
                case 3: SporeRing(); break;
                default: ZoneBarrage(); break;
            }
        }

        void SporeVolley()
        {
            busy = true;
            Windup(true);
            int waves = Phase == 3 ? 4 : 3;
            pending = Telegraph.Line(e.Center, (e.PlayerCenter - e.Center).normalized, 7f, 1.2f, MinWindup, G(() =>
            {
                pending = null;
                Windup(false);
                for (int w = 0; w < waves; w++)
                {
                    float delay = w * 0.25f;
                    e.StartCoroutine(Delayed(delay, G(() =>
                    {
                        if (e.Health.IsDead) return;
                        var dir = (e.PlayerCenter - e.Center).normalized;
                        for (int i = -2; i <= 2; i++)
                            e.Shoot(Quaternion.Euler(0, 0, i * 12f) * dir, 5.5f + Phase * 0.5f, e.Def.damage * 0.8f, 10f, 1.4f);
                        Sfx.Play("sfx_spore", 0.6f);
                    })));
                }
                e.StartCoroutine(Delayed(waves * 0.25f, G(() => busy = false)));
            }));
        }

        void PoisonCircles()
        {
            busy = true;
            Windup(true);
            int n = Phase == 1 ? 3 : 4;
            var center = e.PlayerPos;
            for (int i = 0; i < n; i++)
            {
                var p = i == 0 ? center : EnemySpawner.Instance.RandomAround(center, 2.2f);
                bool last = i == n - 1;
                var t = Telegraph.Circle(p, 1.2f, 1.0f, G(() =>
                {
                    HazardZone.Spawn(p, 1.2f, 4f, 6f);
                    VfxService.Instance.Play("VFX_steam", p, 0, 1.2f, new Color(0.8f, 0.6f, 1f));
                    if (Vector2.Distance(e.PlayerPos, p) < 1.2f)
                        e.HitPlayer(e.Def.damage, 1f, true);
                    if (last)
                    {
                        Windup(false);
                        busy = false;
                    }
                }));
                if (i == 0)
                    pending = t;
            }
        }

        void Summon()
        {
            VfxService.Instance.Play("VFX_portal", e.Position, 0, 2.5f, new Color(0.7f, 1f, 0.6f));
            Sfx.Play("sfx_summon", 0.8f);
            if (summoned < 16 && EnemySpawner.Exists)
                for (int i = 0; i < 4; i++)
                {
                    var p = EnemySpawner.Instance.RandomAround(e.Position, 2.5f);
                    if (EnemySpawner.Instance.SpawnAt(i % 2 == 0 ? "slime" : "spore_shroom", p, true) != null)
                        summoned++;
                }
        }

        void SporeRing()
        {
            busy = true;
            Windup(true);
            pending = Telegraph.Circle(e.Position, 3.2f, 0.9f, G(() =>
            {
                pending = null;
                Windup(false);
                busy = false;
                for (int i = 0; i < 16; i++)
                    e.Shoot(Quaternion.Euler(0, 0, i * 22.5f) * Vector2.right, 4.5f, e.Def.damage * 0.8f, 9f, 1.3f);
                if (e.DistToPlayer < 3.2f)
                    e.HitPlayer(e.Def.damage, 2.5f, true);
                VfxService.Instance.Play("VFX_thunder_ring", e.Position, 0, 4f, new Color(0.8f, 0.5f, 1f));
                CameraFollow2D.Main?.Shake(0.15f, 0.3f);
            }));
        }

        void ZoneBarrage()
        {
            busy = true;
            Windup(true);
            var c = e.PlayerPos;
            int done = 0;
            const int total = 6;
            for (int i = 0; i < total; i++)
            {
                var p = c + new Vector2((i % 3 - 1) * 2.2f, (i / 3 - 0.5f) * 2.2f);
                float delay = (i % 2) * 0.45f; // staggered: two alternating sets
                e.StartCoroutine(Delayed(delay, G(() =>
                {
                    Telegraph.Circle(p, 1.0f, 0.9f, G(() =>
                    {
                        VfxService.Instance.Play("VFX_explosion", p, 0, 1.3f, new Color(0.9f, 0.6f, 1f));
                        if (Vector2.Distance(e.PlayerPos, p) < 1.0f)
                            e.HitPlayer(e.Def.damage * 1.2f, 2f, true);
                        if (++done == total)
                        {
                            Windup(false);
                            busy = false;
                            exposedUntil = Time.time + 2f; // weak point exposed after the big attack
                            e.Flash.Flash(0.3f, new Color(1f, 1f, 0.6f));
                            VfxService.Instance.Play("VFX_levelup", e.Center, 0, 2.5f, new Color(1f, 1f, 0.6f));
                        }
                    }));
                })));
            }
        }

        static System.Collections.IEnumerator Delayed(float seconds, System.Action a)
        {
            if (seconds > 0)
                yield return new WaitForSeconds(seconds);
            a();
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
