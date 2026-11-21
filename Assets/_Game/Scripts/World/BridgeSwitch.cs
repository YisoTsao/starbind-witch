using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Broken bridge lever (GDD §11.3): F repairs the stone bridge (sprite swap + gap blocker off).</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class BridgeSwitch : MonoBehaviour
    {
        [SerializeField] SpriteRenderer lever;
        [SerializeField] SpriteRenderer bridge;
        [SerializeField] Collider2D gap;
        public bool Repaired { get; private set; }

        public void Configure(SpriteRenderer leverSr, SpriteRenderer bridgeSr, Collider2D gapCollider)
        {
            lever = leverSr;
            bridge = bridgeSr;
            gap = gapCollider;
        }

        void OnEnable() => GetComponent<Interactable>().Interacted += Pull;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Pull;

        void Pull(Interactable _)
        {
            if (Repaired)
                return;
            Repaired = true;
            var db = GameDatabase.Instance;
            if (lever != null) lever.sprite = db.Sprite("PROP_lever_on_draft");
            if (bridge != null) bridge.sprite = db.Sprite("PROP_bridge_stone_draft");
            if (gap != null) gap.enabled = false;
            if (bridge != null)
            {
                VfxService.Instance.Play("VFX_portal", bridge.transform.position, 0, 2f, new Color(0.7f, 0.9f, 1f));
                VfxService.Instance.Sparks(bridge.transform.position, new Color(0.8f, 0.9f, 1f), 24, 4f);
            }
            Sfx.Play("sfx_portal", 0.8f);
            GameSession.Instance.UI.Notice("斷橋修復了");
        }
    }
}
