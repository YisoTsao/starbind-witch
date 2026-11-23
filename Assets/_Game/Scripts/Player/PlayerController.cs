using Starbind.Combat;
using Starbind.Controls;
using Starbind.Core;
using Starbind.Spells;
using Starbind.VFX;
using Starbind.World;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>Wires input to movement, interaction and combat, places the player at the spawn point,
    /// and handles death (the run manager decides what happens next; outside a run the witch revives).</summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] string defaultSpawnId = "spawn_square";

        PlayerMotor motor;
        PlayerCombat combat;
        PlayerVitals vitals;
        PlayerBuild build;
        CharacterAnimator anim;
        HitFlash flash;

        public static PlayerController Current { get; private set; }

        /// <summary>Tests and cutscenes turn device input off and drive the motor directly.</summary>
        public bool InputEnabled { get; set; } = true;

        public PlayerMotor Motor => motor;
        public PlayerInteractor Interactor => interactor;
        public PlayerCombat Combat => combat;
        public PlayerVitals Vitals => vitals;
        public PlayerBuild Build => build;
        public PlayerInputReader Input => input;
        public System.Action<PlayerController> DeathHandler { get; set; }

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            combat = GetComponent<PlayerCombat>();
            vitals = GetComponent<PlayerVitals>();
            build = GetComponent<PlayerBuild>();
            anim = GetComponentInChildren<CharacterAnimator>();
            flash = GetComponentInChildren<HitFlash>();
            OcclusionFader.Viewer = transform;
            Current = this;
        }

        void Start()
        {
            var spawnId = SceneTransition.ConsumePendingSpawnId() ?? defaultSpawnId;
            var spawn = SpawnPoint.Find(spawnId);
            if (spawn != null)
                Teleport(spawn.transform.position);
            GameSession.Instance.BindPlayer(this);
        }

        void OnEnable()
        {
            if (input != null)
            {
                input.InteractPressed += OnInteractPressed;
                input.AttackPressed += OnAttackPressed;
                input.AttackReleased += OnAttackReleased;
                input.ChargeAltPressed += OnChargePressed;
                input.ChargeAltReleased += OnChargeReleased;
                input.DashPressed += OnDash;
                input.Spell1Pressed += OnSpell1;
                input.Spell2Pressed += OnSpell2;
                input.OverdrivePressed += OnOverdrive;
                input.ItemPressed += OnItem;
                input.GrimoirePressed += OnGrimoire;
                input.PausePressed += OnPause;
                input.MapPressed += OnMap;
            }
            if (vitals != null)
            {
                vitals.Health.Died += OnDied;
                vitals.Health.Damaged += OnDamaged;
            }
        }

        void OnDisable()
        {
            if (input != null)
            {
                input.InteractPressed -= OnInteractPressed;
                input.AttackPressed -= OnAttackPressed;
                input.AttackReleased -= OnAttackReleased;
                input.ChargeAltPressed -= OnChargePressed;
                input.ChargeAltReleased -= OnChargeReleased;
                input.DashPressed -= OnDash;
                input.Spell1Pressed -= OnSpell1;
                input.Spell2Pressed -= OnSpell2;
                input.OverdrivePressed -= OnOverdrive;
                input.ItemPressed -= OnItem;
                input.GrimoirePressed -= OnGrimoire;
                input.PausePressed -= OnPause;
                input.MapPressed -= OnMap;
            }
            if (vitals != null)
            {
                vitals.Health.Died -= OnDied;
                vitals.Health.Damaged -= OnDamaged;
            }
        }

        void OnDestroy()
        {
            if (OcclusionFader.Viewer == transform)
                OcclusionFader.Viewer = null;
            if (Current == this)
                Current = null;
        }

        bool Accepting => InputEnabled && input != null && !GamePause.IsPaused && !vitals.Health.IsDead && !UIBlocks();

        static bool UIBlocks() => GameSession.Exists && GameSession.Instance.UI != null && GameSession.Instance.UI.BlocksGameplay;

        Vector2 Aim => combat.AimPoint(input);

        float lastAttackLock;

        void Update()
        {
            if (InputEnabled && input != null)
                motor.SetMoveInput(Accepting ? input.Move : Vector2.zero);
            motor.Locked = vitals.Health.IsDead;
            if (anim != null)
            {
                bool attacking = Time.time < combat.AttackLockedUntil || combat.IsCharging;
                anim.Facing = attacking ? combat.AimDirection : motor.Facing;
                anim.Moving = motor.Velocity.sqrMagnitude > 0.2f;
                anim.Charging = combat.IsCharging;
                bool newSwing = combat.AttackLockedUntil > lastAttackLock + 0.001f;
                lastAttackLock = combat.AttackLockedUntil;
                if (Time.time < combat.AttackLockedUntil)
                    anim.PlayCast(0.05f, newSwing); // each combo step / cast restarts the action frames
            }
            if (motor.IsDashing)
                vitals.Health.InvulnerableUntil = Mathf.Max(vitals.Health.InvulnerableUntil, Time.time + 0.02f);
        }

        public void Teleport(Vector2 position)
        {
            var body = GetComponent<Rigidbody2D>();
            body.position = position;
            body.linearVelocity = Vector2.zero;
            transform.position = position;
        }

        void OnInteractPressed()
        {
            if (Accepting && interactor != null)
                interactor.TryInteract();
        }

        void OnAttackPressed() { if (Accepting) combat.AttackDown(); }
        void OnAttackReleased() { if (InputEnabled) combat.AttackUp(Aim); }
        void OnChargePressed() { if (Accepting) combat.ChargeDown(); }
        void OnChargeReleased() { if (InputEnabled) combat.ChargeUp(Aim); }

        void OnDash()
        {
            if (!Accepting || !motor.TryDash())
                return;
            vitals.Health.InvulnerableUntil = Time.time + PlayerMotor.DashInvulnerable;
            VfxService.Instance.Play("VFX_dust", transform.position, 0, 1f, null, StarbindLayers.SortGroundDecal);
            Sfx.Play("sfx_dash", 0.6f);
        }

        void OnSpell1() { if (Accepting) combat.CastSlot(0, Aim); }
        void OnSpell2() { if (Accepting) combat.CastSlot(1, Aim); }
        void OnOverdrive() { if (Accepting) combat.TryOverdrive(); }
        void OnItem(int i) { if (Accepting) combat.UseItem(i); }

        void OnGrimoire()
        {
            if (InputEnabled && GameSession.Exists)
                GameSession.Instance.UI.ToggleGrimoire();
        }

        void OnMap()
        {
            if (InputEnabled && GameSession.Exists)
                GameSession.Instance.UI.ToggleMap();
        }

        void OnPause()
        {
            if (InputEnabled && GameSession.Exists)
                GameSession.Instance.UI.TogglePause();
        }

        void OnDamaged(Health h, DamageEvent e, DamageResult r)
        {
            if (!r.Applied)
                return;
            flash?.Flash(0.12f, new Color(1f, 0.4f, 0.4f));
            CameraRig.CameraFollow2D.Main?.Shake(0.1f, 0.18f);
            Sfx.Play("sfx_player_hurt", 0.8f);
        }

        void OnDied(Health h)
        {
            motor.SetMoveInput(Vector2.zero);
            combat.InputEnabled = false;
            VfxService.Instance.Play("VFX_poof", transform.position + new Vector3(0, 0.4f), 0, 1.5f);
            if (DeathHandler != null)
                DeathHandler(this);
            else
                Invoke(nameof(Revive), 1.5f);
        }

        public void Revive()
        {
            vitals.FullRestore();
            combat.InputEnabled = true;
        }
    }
}
