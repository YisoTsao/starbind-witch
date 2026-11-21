using Starbind.Combat;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>Eight-direction movement and dash on a gravity-free Rigidbody2D (GDD §4.2). Dash: 2.6
    /// units in 0.2 s, 2.5 s cooldown, 0.15 s invulnerability; walls stop it (continuous collision).
    /// No damage logic here.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : MonoBehaviour, IKnockbackReceiver
    {
        [SerializeField] float moveSpeed = 4.5f; // GAME_DESIGN_V3 §4.2
        public const float DashDistance = 2.6f;
        public const float DashTime = 0.2f;
        public const float DashCooldown = 2.5f;
        public const float DashInvulnerable = 0.15f;

        Rigidbody2D body;
        Vector2 input;
        Vector2 dashDir;
        float dashUntil;
        Vector2 knock;

        public float MoveSpeed => moveSpeed;
        public float SpeedMultiplier { get; set; } = 1f;
        public float AttackSlow { get; set; } = 1f;
        public Vector2 Facing { get; private set; } = Vector2.down;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool IsMoving => input.sqrMagnitude > 0.01f;
        public bool IsDashing => Time.time < dashUntil;
        public float DashReadyAt { get; private set; }
        public float DashCooldownFraction => Mathf.Clamp01((DashReadyAt - Time.time) / DashCooldown);
        public bool Locked { get; set; }

        /// <summary>Stick-style input; magnitude is clamped to 1 so diagonals are not faster.</summary>
        public void SetMoveInput(Vector2 value)
        {
            input = Vector2.ClampMagnitude(value, 1f);
            if (input.sqrMagnitude > 0.01f)
                Facing = input.normalized;
        }

        public void Face(Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.01f)
                Facing = dir.normalized;
        }

        /// <summary>Starts a dash in the move direction (or facing). Returns false while on cooldown.</summary>
        public bool TryDash()
        {
            if (Locked || Time.time < DashReadyAt)
                return false;
            dashDir = input.sqrMagnitude > 0.01f ? input.normalized : Facing;
            dashUntil = Time.time + DashTime;
            DashReadyAt = Time.time + DashCooldown;
            return true;
        }

        public void Knockback(Vector2 direction, float strength)
        {
            if (!IsDashing)
                knock = direction.normalized * strength * 2.2f;
        }

        public void Pull(Vector2 velocity) { }

        void Awake() => body = GetComponent<Rigidbody2D>();

        void FixedUpdate()
        {
            if (IsDashing)
            {
                body.linearVelocity = dashDir * (DashDistance / DashTime);
                return;
            }
            var v = Locked ? Vector2.zero : input * moveSpeed * SpeedMultiplier * AttackSlow;
            body.linearVelocity = v + knock;
            knock = Vector2.MoveTowards(knock, Vector2.zero, 30f * Time.fixedDeltaTime);
        }

        void OnDisable()
        {
            input = Vector2.zero;
            knock = Vector2.zero;
            dashUntil = 0;
            if (body != null)
                body.linearVelocity = Vector2.zero;
        }
    }
}
