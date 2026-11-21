using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Exit portal after the boss: F ends the run as a victory.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class ReturnPortal : MonoBehaviour
    {
        void OnEnable() => GetComponent<Interactable>().Interacted += Use;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Use;
        void Use(Interactable _) => RunManager.Current?.Finish(true);
    }
}
