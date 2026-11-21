using UnityEngine;

namespace Starbind.Player
{
    /// <summary>Static NPC pose: sets the CharacterAnimator facing; turns toward the witch while she is near.</summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public sealed class NpcFacing : MonoBehaviour
    {
        [SerializeField] string facing = "down";
        CharacterAnimator anim;
        Vector2 rest;

        public void Configure(string f) => facing = string.IsNullOrEmpty(f) ? "down" : f;

        void Awake()
        {
            anim = GetComponent<CharacterAnimator>();
            rest = facing switch { "up" => Vector2.up, "left" => Vector2.left, "right" => Vector2.right, _ => Vector2.down };
            anim.Facing = rest;
        }

        void Update()
        {
            var p = PlayerController.Current;
            if (p == null)
                return;
            var to = (Vector2)(p.transform.position - transform.position);
            anim.Facing = to.sqrMagnitude < 4f ? to.normalized : rest;
        }
    }
}
