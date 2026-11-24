using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Ch.2 Crystal Core Golem (STORY_WORLD_QUESTS §2.3): its body is runaway star core, so
    /// lightning hits deal no damage and charge it instead. A full charge (or phase 3 on a timer) releases
    /// a telegraphed overcharge nova, after which the cracked core is exposed. Crystal spike lines,
    /// ground slam, shard rain and crystal mites.</summary>
    public sealed class GolemBrain : BossBrainBase
    {
        public const float ChargePerLightningHit = 12f;
        public const float MaxCharge = 100f;
        public float Charge { get; private set; }
        public int Novas { get; private set; }
        static readonly Color Crystal = new Color(0.7f, 0.6f, 1f);

        protected override void OnInit() => e.Health.IncomingScale = Absorb;

        float Absorb(DamageEvent hit)
        {
            if (hit.Element != ElementType.Lightning)
                return 1f;
            Charge = Mathf.Min(MaxCharge, Charge + ChargePerLightningHit);
            VfxService.Instance.Play("VFX_shock", e.Center, 0, 1.4f, new Color(1f, 0.95f, 0.5f));
            return 0f;
        }

        protected override void OnTick(float dt)
        {
            if (Phase == 3 && !busy)
                Charge = Mathf.Min(MaxCharge, Charge + dt * 6f);
            if (Charge >= MaxCharge && !busy)
                Nova();
        }

        protected override int AttackCount(int phase) => phase == 1 ? 2 : phase == 2 ? 4 : 4;

        protected override void Attack(int pick)
        {
            switch (pick)
            {
                case 0: Spikes(); break;
                case 1: Slam(); break;
                case 2: ShardRain(); break;
                default: Summon(Phase == 3 ? 4 : 3, 2.5f); break;
            }
        }

        void Spikes()
        {
            Begin();
            var dir = (e.PlayerPos - e.Position).normalized;
            int lanes = Phase == 1 ? 3 : 5;
            for (int i = 0; i < lanes; i++)
            {
                var d = (Vector2)(Quaternion.Euler(0, 0, (i - (lanes - 1) * 0.5f) * 18f) * dir);
                bool last = i == lanes - 1;
                var t = LineAttack(e.Position, d, 7.5f, 0.9f, 0.85f, 0.9f, "VFX_shatter", Crystal, last ? (System.Action)Finish : null);
                if (i == 0) pending = t;
            }
        }

        void Slam()
        {
            Begin();
            pending = CircleAttack(e.Position, 2.6f, 0.95f, 1.2f, "VFX_shockwave", Crystal, () =>
            {
                CameraFollow2D.Main?.Shake(0.15f, 0.3f);
                if (Phase >= 2)
                    Ring(10, 4.5f, 0.7f, cycle * 9f, "VFX_crystal_shard");
                Finish();
            });
        }

        void ShardRain()
        {
            Begin();
            int n = Phase == 3 ? 6 : 5;
            var c = e.PlayerPos;
            int done = 0;
            for (int i = 0; i < n; i++)
            {
                var p = i == 0 ? c : EnemySpawner.Instance.RandomAround(c, 2.6f);
                var t = CircleAttack(p, 1.0f, 0.8f + i * 0.08f, 1f, "VFX_shatter", Crystal, () => { if (++done == n) Finish(); });
                if (i == 0) pending = t;
            }
        }

        void Nova()
        {
            Begin();
            Charge = 0;
            Novas++;
            GameSession_Notice("晶核過載！遠離魔像");
            pending = CircleAttack(e.Position, 4.2f, 1.3f, 1.6f, "VFX_thunder_ring", new Color(1f, 0.95f, 0.5f), () =>
            {
                CameraFollow2D.Main?.Shake(0.25f, 0.5f);
                Finish();
                Expose(3f, new Color(1f, 1f, 0.6f)); // cracked core
            });
        }

        static void GameSession_Notice(string text)
        {
            if (Core.GameSession.Exists)
                Core.GameSession.Instance.UI.Notice(text);
        }
    }

    /// <summary>Ch.3 Lake Reflection (STORY_WORLD_QUESTS §2.3): mirrors the witch's current build. Its
    /// volleys use the element of her Q spell, its ring the element of her E spell; it dives into the
    /// water and resurfaces at a telegraphed spot, and from phase 2 splits off shadow clones.</summary>
    public sealed class ShadowBrain : BossBrainBase
    {
        public ElementType LastMirrored { get; private set; }
        protected override float KeepDistance => 3.5f;
        static readonly Color Moon = new Color(0.62f, 0.5f, 1f);

        protected override int AttackCount(int phase) => phase == 1 ? 2 : phase == 2 ? 4 : 5;

        protected override void Attack(int pick)
        {
            switch (pick)
            {
                case 0: MirrorVolley(); break;
                case 1: Dive(); break;
                case 2: Summon(2, 1.8f); break;
                case 3: MirrorRing(); break;
                default: MirrorVolley(); break;
            }
        }

        ElementType Mirror(int slot)
        {
            var s = e.Player != null ? e.Player.Build.Active(slot) : null;
            var el = s != null ? s.Element : ElementType.None;
            LastMirrored = el;
            return el;
        }

        static string SpriteFor(ElementType el) => el switch
        {
            ElementType.Fire => "VFX_fireball",
            ElementType.Ice => "VFX_ice_shard",
            ElementType.Lightning => "VFX_beam_seg",
            ElementType.Nature => "VFX_thorn",
            ElementType.Star => "VFX_star_shot",
            _ => "VFX_shadow_bolt",
        };

        void MirrorVolley()
        {
            Begin();
            var el = Mirror(0);
            var dir = (e.PlayerCenter - e.Center).normalized;
            pending = Telegraph.Line(e.Center, dir, 7f, 1.1f, MinWindup, G(() =>
            {
                pending = null;
                int waves = Phase == 3 ? 3 : 2;
                for (int w = 0; w < waves; w++)
                    After(w * 0.3f, () =>
                    {
                        var d = (e.PlayerCenter - e.Center).normalized;
                        for (int i = -1; i <= 1; i++)
                            e.Shoot(Quaternion.Euler(0, 0, i * 14f) * d, 6f + Phase * 0.4f, e.Def.damage * 0.8f, 10f, 1.2f, SpriteFor(el));
                        SpellFx.Cast(el, e.Position, e.Center, d);
                    });
                After(waves * 0.3f, Finish);
            }));
        }

        void Dive()
        {
            Begin();
            var to = EnemySpawner.Instance.RandomAround(e.PlayerPos, 2.2f);
            VfxService.Instance.Play("VFX_portal", e.Position, 0, 2f, Moon);
            e.Visual.enabled = false;
            e.Health.InvulnerableUntil = Time.time + 1.1f;
            pending = CircleAttack(to, 1.7f, 1.0f, 1.1f, "VFX_shockwave", Moon, () =>
            {
                e.Visual.enabled = true;
                var body = e.GetComponent<Rigidbody2D>();
                body.position = to;
                e.transform.position = to;
                VfxService.Instance.Play("VFX_portal", to, 0, 2f, Moon);
                Finish();
            });
        }

        void MirrorRing()
        {
            Begin();
            var el = Mirror(1);
            pending = Telegraph.Circle(e.Position, 3f, 0.9f, G(() =>
            {
                pending = null;
                Ring(12, 4.5f, 0.75f, 0, SpriteFor(el));
                After(0.35f, () => Ring(12, 4.5f, 0.75f, 15f, SpriteFor(el)));
                After(0.4f, () =>
                {
                    Finish();
                    if (Phase == 3)
                        Expose(2.2f, Moon);
                });
            }));
        }

        public override void OnRelease()
        {
            if (e != null && e.Visual != null)
                e.Visual.enabled = true;
            base.OnRelease();
        }
    }

    /// <summary>Ch.4 Observatory Guardian (STORY_WORLD_QUESTS §2.3): attacks with refracted light. A beam
    /// is fired at the witch and bends at its end toward her new position; from phase 2 three beams
    /// rotate around the guardian; star meteors and lens turrets. After the triple sweep in phase 3 the
    /// lens is exposed.</summary>
    public sealed class GuardianBrain : BossBrainBase
    {
        static readonly Color Light = new Color(1f, 0.92f, 0.6f);
        float spin;

        protected override int AttackCount(int phase) => phase == 1 ? 2 : phase == 2 ? 4 : 4;

        protected override void Attack(int pick)
        {
            switch (pick)
            {
                case 0: Refraction(); break;
                case 1: Meteors(); break;
                case 2: TriBeam(); break;
                default: Summon(2, 2.5f); break;
            }
        }

        void Refraction()
        {
            Begin();
            var dir = (e.PlayerPos - e.Position).normalized;
            const float len = 6f;
            var bend = e.Position + dir * len;
            pending = LineAttack(e.Position, dir, len, 0.8f, 0.85f, 1f, "VFX_beam_seg", Light, () =>
            {
                var d2 = (e.PlayerPos - bend).sqrMagnitude > 0.01f ? (e.PlayerPos - bend).normalized : -dir;
                VfxService.Instance.Play("VFX_flash", bend, 0, 1.2f, Light);
                LineAttack(bend, d2, 7f, 0.8f, 0.75f, 1f, "VFX_beam_seg", Light, Finish);
            });
        }

        void TriBeam()
        {
            Begin();
            int sweeps = Phase == 3 ? 3 : 2;
            for (int s = 0; s < sweeps; s++)
            {
                float a0 = spin + s * 40f;
                bool lastSweep = s == sweeps - 1;
                After(s * 0.55f, () =>
                {
                    for (int k = 0; k < 3; k++)
                    {
                        var d = (Vector2)(Quaternion.Euler(0, 0, a0 + k * 120f) * Vector2.right);
                        bool last = lastSweep && k == 2;
                        LineAttack(e.Position, d, 9f, 0.9f, 0.8f, 1f, "VFX_beam_seg", Light, last ? () =>
                        {
                            Finish();
                            if (Phase == 3)
                                Expose(2.5f, Light);
                        } : (System.Action)null);
                    }
                });
            }
            spin += 25f;
        }

        void Meteors()
        {
            Begin();
            int n = Phase == 1 ? 4 : 6;
            int done = 0;
            for (int i = 0; i < n; i++)
            {
                var p = i == 0 ? e.PlayerPos : EnemySpawner.Instance.RandomAround(e.PlayerPos, 3f);
                var t = CircleAttack(p, 1.1f, 0.9f + i * 0.1f, 1.1f, "VFX_blast", Light, () => { if (++done == n) Finish(); });
                if (i == 0) pending = t;
            }
        }
    }

    /// <summary>Finale: the Twisted Grimoire, three phases that each use one school (STORY_WORLD_QUESTS
    /// §2.3). P1 Nature: thorn lanes and poison blooms. P2 Moon: shadow hands under the witch, blink and
    /// void wisps. P3 Star: rewriting star rings that copy her Q element, a zone barrage and an exposed
    /// spine after it.</summary>
    public sealed class GrimoireBrain : BossBrainBase
    {
        static readonly Color NatureC = new Color(0.55f, 0.9f, 0.4f);
        static readonly Color MoonC = new Color(0.62f, 0.5f, 1f);
        static readonly Color StarC = new Color(1f, 0.9f, 0.5f);
        public string School => Phase == 1 ? "nature" : Phase == 2 ? "moon" : "star";

        protected override int AttackCount(int phase) => 3;

        protected override void OnPhaseChanged(int phase)
        {
            if (Core.GameSession.Exists)
                Core.GameSession.Instance.UI.Notice(phase == 2 ? "魔典翻頁：月影之章" : "魔典翻頁：星痕之章");
        }

        protected override void Attack(int pick)
        {
            if (Phase == 1)
            {
                if (pick == 0) ThornLanes(); else if (pick == 1) PoisonBloom(); else Summon(3, 2.5f, e.Def.summonId);
            }
            else if (Phase == 2)
            {
                if (pick == 0) ShadowHands(); else if (pick == 1) Blink(); else Summon(3, 2.5f, e.Def.summonId2);
            }
            else
            {
                if (pick == 0) StarRings(); else if (pick == 1) Rewrite(); else Barrage();
            }
        }

        void ThornLanes()
        {
            Begin();
            var dir = (e.PlayerPos - e.Position).normalized;
            for (int i = 0; i < 4; i++)
            {
                var d = (Vector2)(Quaternion.Euler(0, 0, (i - 1.5f) * 22f) * dir);
                bool last = i == 3;
                var from = e.Position;
                var t = LineAttack(from, d, 8f, 0.8f, 0.85f, 0.9f, "VFX_thorn", NatureC, () =>
                {
                    HazardZone.Spawn(from + d * 5f, 0.9f, 3f, 5f);
                    if (last) Finish();
                });
                if (i == 0) pending = t;
            }
        }

        void PoisonBloom()
        {
            Begin();
            int done = 0;
            for (int i = 0; i < 4; i++)
            {
                var p = i == 0 ? e.PlayerPos : EnemySpawner.Instance.RandomAround(e.PlayerPos, 2.4f);
                var t = CircleAttack(p, 1.2f, 1f, 1f, "VFX_bloom", NatureC, () =>
                {
                    HazardZone.Spawn(p, 1.2f, 4f, 6f);
                    if (++done == 4) Finish();
                });
                if (i == 0) pending = t;
            }
        }

        void ShadowHands()
        {
            Begin();
            int n = 5, done = 0;
            for (int i = 0; i < n; i++)
            {
                float delay = i * 0.22f;
                After(delay, () =>
                {
                    var p = e.PlayerPos;
                    CircleAttack(p, 1.1f, 0.8f, 1.1f, "VFX_shadow_hand", MoonC, () => { if (++done == n) Finish(); });
                });
            }
        }

        void Blink()
        {
            Begin();
            var to = home + (Vector2)(Quaternion.Euler(0, 0, cycle * 97f) * Vector2.right) * 3.5f;
            e.Health.InvulnerableUntil = Time.time + 0.9f;
            VfxService.Instance.Play("VFX_portal", e.Position, 0, 2.2f, MoonC);
            pending = CircleAttack(to, 2f, 0.8f, 1.1f, "VFX_shockwave", MoonC, () =>
            {
                var body = e.GetComponent<Rigidbody2D>();
                body.position = to;
                e.transform.position = to;
                Ring(10, 4f, 0.7f, 0, "VFX_shadow_bolt");
                Finish();
            });
        }

        void StarRings()
        {
            Begin();
            pending = Telegraph.Circle(e.Position, 3.2f, 0.9f, G(() =>
            {
                pending = null;
                for (int w = 0; w < 3; w++)
                {
                    float off = w * 10f;
                    After(w * 0.3f, () => Ring(16, 4.2f, 0.7f, off, "VFX_star_shot"));
                }
                After(0.9f, Finish);
            }));
        }

        void Rewrite()
        {
            Begin();
            var s = e.Player != null ? e.Player.Build.Active(0) : null;
            var el = s != null ? s.Element : ElementType.Star;
            string sprite = el == ElementType.Fire ? "VFX_fireball" : el == ElementType.Ice ? "VFX_ice_shard" : el == ElementType.Lightning ? "VFX_beam_seg" : "VFX_star_shot";
            var dir = (e.PlayerCenter - e.Center).normalized;
            pending = Telegraph.Line(e.Center, dir, 8f, 1.4f, MinWindup, G(() =>
            {
                pending = null;
                for (int w = 0; w < 3; w++)
                    After(w * 0.22f, () =>
                    {
                        var d = (e.PlayerCenter - e.Center).normalized;
                        for (int i = -2; i <= 2; i++)
                            e.Shoot(Quaternion.Euler(0, 0, i * 11f) * d, 6.5f, e.Def.damage * 0.8f, 11f, 1.3f, sprite);
                    });
                After(0.7f, Finish);
            }));
        }

        void Barrage()
        {
            Begin();
            var c = e.PlayerPos;
            int done = 0;
            const int total = 6;
            for (int i = 0; i < total; i++)
            {
                var p = c + new Vector2((i % 3 - 1) * 2.2f, (i / 3 - 0.5f) * 2.2f);
                float delay = (i % 2) * 0.45f;
                After(delay, () => CircleAttack(p, 1.0f, 0.9f, 1.2f, "VFX_blast", StarC, () =>
                {
                    if (++done == total)
                    {
                        Finish();
                        Expose(2.5f, StarC);
                    }
                }));
            }
        }
    }

    /// <summary>Crystal mite / void spark: runs at the witch, stops, flashes a 0.6 s ground warning and
    /// bursts (killing itself). Dodging out of the circle avoids all damage.</summary>
    public sealed class BomberBrain : EnemyBrain
    {
        public const float FuseSeconds = 0.6f;
        public const float BlastRadius = 1.3f;
        bool lit;

        public override void Tick(float dt)
        {
            if (lit)
            {
                e.DesiredVelocity = Vector2.zero;
                return;
            }
            e.DesiredVelocity = Seek();
            if (e.PlayerAlive && Time.time >= readyAt && e.DistToPlayer < 1.1f)
            {
                lit = true;
                e.Flash.Flash(FuseSeconds, new Color(1f, 0.85f, 0.5f));
                var at = e.Position;
                pending = Telegraph.Circle(at, BlastRadius, FuseSeconds, G(() =>
                {
                    pending = null;
                    VfxService.Instance.Play("VFX_blast", at, 0, 1.4f, new Color(0.8f, 0.7f, 1f));
                    if (Vector2.Distance(e.PlayerPos, at) < BlastRadius)
                        e.HitPlayer(e.Def.damage, 1.5f);
                    e.Health.Kill();
                }));
            }
        }
    }
}
