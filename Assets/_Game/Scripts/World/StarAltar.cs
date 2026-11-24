using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Star altar (GDD §11.3) and the chapter wave events built on it (mine crystal hall, lake
    /// ruins): F starts a monster wave around it; clearing it grants a page and an extra upgrade choice.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class StarAltar : MonoBehaviour
    {
        [SerializeField] int waveSize = 26;
        [SerializeField] float radius = 5f;
        [SerializeField] string notice = "星痕祭壇甦醒：擊退怪潮！";
        public bool Used { get; private set; }
        public bool Cleared { get; private set; }
        SpawnDirector director;
        SpriteFlipbook glow;

        void OnEnable() => GetComponent<Interactable>().Interacted += Use;
        void OnDisable()
        {
            GetComponent<Interactable>().Interacted -= Use;
            if (director != null)
                director.WaveCleared -= OnCleared;
        }

        void Start()
        {
            glow = VfxService.Instance.Play("VFX_portal", (Vector2)transform.position + new Vector2(0, 0.9f), 0, 0.8f, new Color(1f, 0.9f, 0.6f, 0.8f), StarbindLayers.SortVfx, 0, true);
        }

        public void Use(Interactable _)
        {
            if (Used)
                return;
            director = FindAnyObjectByType<SpawnDirector>();
            if (director == null)
                return;
            Used = true;
            director.WaveCleared += OnCleared;
            director.StartWave(transform.position, radius, waveSize);
            GameSession.Instance.UI.Notice(notice);
            VfxService.Instance.Play("VFX_overdrive_aura", transform.position, 0, 2f, new Color(1f, 0.6f, 0.6f));
            Sfx.Play("sfx_boss_roar", 0.6f);
            if (glow != null)
                glow.Renderer.color = new Color(1f, 0.4f, 0.4f, 0.9f);
        }

        void OnCleared()
        {
            director.WaveCleared -= OnCleared;
            Cleared = true;
            if (glow != null)
            {
                VfxService.Instance.Release(glow);
                glow = null;
            }
            Pickup.Drop(PickupKind.Page, (Vector2)transform.position + new Vector2(0, -0.8f));
            GameSession.Instance.UI.Notice("怪潮平息！獲得魔典書頁與額外升級");
            RunManager.Current?.GrantBonusLevelUp();
            if (!director.EliteSpawned)
                director.SpawnElite(transform.position);
        }
    }
}
