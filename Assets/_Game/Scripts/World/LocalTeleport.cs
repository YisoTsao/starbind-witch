using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Teleport within the scene (village star plate -> cottage door).</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class LocalTeleport : MonoBehaviour
    {
        [SerializeField] string spawnId;
        public void Configure(string id) => spawnId = id;
        void OnEnable() => GetComponent<Interactable>().Interacted += Use;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Use;

        void Use(Interactable _)
        {
            var sp = SpawnPoint.Find(spawnId);
            var p = Player.PlayerController.Current;
            if (sp == null || p == null)
                return;
            VfxService.Instance.Play("VFX_portal", p.transform.position + new Vector3(0, 0.4f), 0, 1.2f);
            p.Teleport(sp.transform.position);
            CameraRig.CameraFollow2D.Main?.SnapToTarget();
            VfxService.Instance.Play("VFX_portal", sp.transform.position + new Vector3(0, 0.4f), 0, 1.2f);
            Sfx.Play("sfx_portal", 0.8f);
        }
    }
}
