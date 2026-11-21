using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Treasure chest (F): potions, star sand and (first chest of a run) a page.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class Chest : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] bool givesPage = true;
        public bool Opened { get; private set; }

        public void Configure(SpriteRenderer sr, bool page)
        {
            sprite = sr;
            givesPage = page;
        }

        void OnEnable() => GetComponent<Interactable>().Interacted += Open;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Open;

        void Open(Interactable i)
        {
            if (Opened)
                return;
            Opened = true;
            if (sprite != null)
                sprite.sprite = GameDatabase.Instance.Sprite("PROP_chest_open_draft");
            Vector2 p = transform.position;
            Pickup.Drop(PickupKind.HpPotion, p + new Vector2(-0.5f, -0.6f), 1, null, new Vector2(-1.5f, -1.5f));
            Pickup.Drop(PickupKind.MpPotion, p + new Vector2(0.5f, -0.6f), 1, null, new Vector2(1.5f, -1.5f));
            Pickup.Drop(PickupKind.Material, p + new Vector2(0, -0.7f), 5, Items.Inventory.StarSand, new Vector2(0, -2f));
            if (givesPage)
                Pickup.Drop(PickupKind.Page, p + new Vector2(0, -1f));
            VfxService.Instance.Play("VFX_levelup", p + new Vector2(0, 0.4f), 0, 1.2f);
            VfxService.Instance.Light(p, new Color(1f, 0.9f, 0.5f), 3f, 1.5f, 0.5f);
            Sfx.Play("sfx_chest", 0.9f);
            GetComponent<Interactable>().enabled = false;
            GetComponent<Collider2D>().enabled = false;
        }
    }
}
