using UnityEngine;

namespace Starbind.CameraRig
{
    /// <summary>Smoothly follows a target and keeps the view inside the map bounds.
    /// Pixel snapping is done by the URP Pixel Perfect Camera on the same object.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 targetOffset = new Vector2(0f, 0.6f);
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] Rect bounds = new Rect(0, 0, 28, 20);

        Camera cam;
        Vector3 velocity;
        Vector3 smoothed;
        float shakeAmp;
        float shakeUntil;
        float shakeDuration;

        /// <summary>The scene's gameplay camera (for screen shake requests from combat feedback).</summary>
        public static CameraFollow2D Main { get; private set; }

        public Rect Bounds => bounds;
        public Transform Target => target;

        /// <summary>Optional second point of interest (the boss): the camera frames both by leaning
        /// toward it by <see cref="SecondaryWeight"/>, so big attacks stay on screen.</summary>
        public Transform SecondaryFocus { get; set; }
        public float SecondaryWeight { get; set; } = 0.4f;

        public void Configure(Transform followTarget, Rect mapBounds)
        {
            target = followTarget;
            bounds = mapBounds;
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            Main = this;
            smoothed = transform.position;
        }

        void OnDestroy()
        {
            if (Main == this)
                Main = null;
        }

        void Start() => SnapToTarget();

        public void SetTarget(Transform t) => target = t;

        /// <summary>Short screen shake (GDD §4.4). Amplitude in world units; stronger calls win.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (Time.unscaledTime < shakeUntil && amplitude < shakeAmp * ((shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, shakeDuration)))
                return;
            shakeAmp = amplitude;
            shakeDuration = duration;
            shakeUntil = Time.unscaledTime + duration;
        }

        void LateUpdate()
        {
            if (target == null)
                return;
            var goal = Clamp(Goal());
            smoothed = Clamp(Vector3.SmoothDamp(smoothed, goal, ref velocity, smoothTime));
            var p = smoothed;
            if (Time.unscaledTime < shakeUntil)
            {
                float k = (shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, shakeDuration);
                p += (Vector3)(Random.insideUnitCircle * shakeAmp * k); // cosmetic only
            }
            transform.position = p;
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;
            velocity = Vector3.zero;
            smoothed = Clamp(Goal());
            transform.position = smoothed;
        }

        /// <summary>World-space rectangle currently visible.</summary>
        public Rect ViewRect()
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            var p = transform.position;
            return new Rect(p.x - w, p.y - h, 2 * w, 2 * h);
        }

        Vector3 Goal()
        {
            var p = target.position + (Vector3)targetOffset;
            if (SecondaryFocus != null && SecondaryFocus.gameObject.activeInHierarchy)
                p = Vector3.Lerp(p, SecondaryFocus.position + new Vector3(0, 1.2f, 0), SecondaryWeight);
            p.z = transform.position.z;
            return p;
        }

        Vector3 Clamp(Vector3 p)
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            p.x = bounds.width <= 2 * w ? bounds.center.x : Mathf.Clamp(p.x, bounds.xMin + w, bounds.xMax - w);
            p.y = bounds.height <= 2 * h ? bounds.center.y : Mathf.Clamp(p.y, bounds.yMin + h, bounds.yMax - h);
            return p;
        }
    }
}
