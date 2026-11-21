using UnityEngine;

namespace Starbind.VFX
{
    /// <summary>0.08-0.12 s white flash on hit (GDD §4.4) through the Starbind/SpriteFlash material.
    /// Also exposes a tint for status colours (chill blue, burn orange).</summary>
    public sealed class HitFlash : MonoBehaviour
    {
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        [SerializeField] SpriteRenderer target;
        MaterialPropertyBlock block;
        float flashUntil;
        float flashStart;
        Color flashColor = Color.white;

        public void Bind(SpriteRenderer sr) => target = sr;

        public void Flash(float seconds = 0.1f, Color? color = null)
        {
            flashStart = Time.time;
            flashUntil = Time.time + seconds;
            flashColor = color ?? Color.white;
            Apply(1f);
        }

        void OnDisable()
        {
            flashUntil = 0;
            Apply(0);
        }

        void LateUpdate()
        {
            if (flashUntil <= 0)
                return;
            if (Time.time >= flashUntil)
            {
                flashUntil = 0;
                Apply(0);
                return;
            }
            float k = 1f - (Time.time - flashStart) / Mathf.Max(0.01f, flashUntil - flashStart);
            Apply(k > 0.5f ? 1f : 0.6f);
        }

        void Apply(float amount)
        {
            if (target == null)
                return;
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetFloat(FlashId, amount);
            block.SetColor(FlashColorId, flashColor);
            target.SetPropertyBlock(block);
        }
    }
}
