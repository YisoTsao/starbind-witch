using UnityEngine;

namespace Starbind.Player
{
    /// <summary>NPC pose: sets the CharacterAnimator facing; turns toward the witch while she is near.
    /// With an NpcWander on the NPC root it follows the walk direction and plays the walk cycle.</summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public sealed class NpcFacing : MonoBehaviour
    {
        [SerializeField] string facing = "down";
        CharacterAnimator anim;
        NpcWander wander;
        Vector2 rest;

        public void Configure(string f) => facing = string.IsNullOrEmpty(f) ? "down" : f;

        void Awake()
        {
            anim = GetComponent<CharacterAnimator>();
            wander = GetComponentInParent<NpcWander>();
            rest = facing switch { "up" => Vector2.up, "left" => Vector2.left, "right" => Vector2.right, _ => Vector2.down };
            anim.Facing = rest;
        }

        void Update()
        {
            if (wander != null && wander.Walking)
            {
                anim.Moving = true;
                anim.Facing = wander.Direction;
                rest = wander.Direction;
                return;
            }
            anim.Moving = false;
            var p = PlayerController.Current;
            if (p == null)
            {
                anim.Facing = rest;
                return;
            }
            var to = (Vector2)(p.transform.position - transform.position);
            anim.Facing = to.sqrMagnitude < 4f ? to.normalized : rest;
        }
    }
}
