using UnityEngine;

namespace Starbind.World
{
    /// <summary>Fades a tall sprite (tree, stall) while the player stands behind it, so the player
    /// stays readable. Y-sorting already puts the player behind; this only changes alpha.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class OcclusionFader : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float occludedAlpha = 0.45f;
        [SerializeField] float fadeSpeed = 5f;

        public static Transform Viewer { get; set; }

        SpriteRenderer sprite;

        public float CurrentAlpha => sprite.color.a;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Viewer = null;

        void Awake() => sprite = GetComponent<SpriteRenderer>();

        void LateUpdate()
        {
            float target = IsViewerBehind() ? occludedAlpha : 1f;
            var c = sprite.color;
            c.a = Mathf.MoveTowards(c.a, target, fadeSpeed * Time.deltaTime);
            sprite.color = c;
        }

        bool IsViewerBehind()
        {
            if (Viewer == null)
                return false;
            var b = sprite.bounds;
            var p = Viewer.position;
            return p.y > transform.position.y && p.y < b.max.y - 0.3f && p.x > b.min.x + 0.2f && p.x < b.max.x - 0.2f;
        }
    }
}
