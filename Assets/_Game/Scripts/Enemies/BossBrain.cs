using Starbind.CameraRig;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Eclipse Mushroom King, three phases (GDD §10.3):
    /// P1 100-70%: aimed spore volleys and poison circles. P2 70-35%: adds summons and a radial spore
    /// ring. P3 35-0%: faster, staggered warning zones, then an exposed window (takes extra damage).
    /// Every big attack has a ground warning of at least 0.7 s; phase changes give 1 s of invulnerability.</summary>
    public sealed class BossBrain : BossBrainBase
    {
        protected override int AttackCount(int phase) => phase == 1 ? 2 : phase == 2 ? 4 : 5;

        protected override void Attack(int pick)
        {
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
                    After(delay, (() =>
                    {
                        if (e.Health.IsDead) return;
                        var dir = (e.PlayerCenter - e.Center).normalized;
                        for (int i = -2; i <= 2; i++)
                            e.Shoot(Quaternion.Euler(0, 0, i * 12f) * dir, 5.5f + Phase * 0.5f, e.Def.damage * 0.8f, 10f, 1.4f);
                        Sfx.Play("sfx_spore", 0.6f);
                    }));
                }
                After(waves * 0.25f, () => busy = false);
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
            if (summoned < SummonCap && EnemySpawner.Exists)
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
                After(delay, (() =>
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
                }));
            }
        }

    }
}
