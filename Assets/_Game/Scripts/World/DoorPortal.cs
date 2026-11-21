using UnityEngine;

namespace Starbind.World
{
    /// <summary>Door that sends the player to another scene's spawn point when interacted with.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class DoorPortal : MonoBehaviour
    {
        [SerializeField] string targetScene = "Workshop";
        [SerializeField] string targetSpawnId = "spawn_from_village";

        Interactable interactable;

        public string TargetScene => targetScene;

        public void Configure(string scene, string spawnId)
        {
            targetScene = scene;
            targetSpawnId = spawnId;
        }

        void Awake() => interactable = GetComponent<Interactable>();
        void OnEnable() => interactable.Interacted += Enter;
        void OnDisable() => interactable.Interacted -= Enter;

        void Enter(Interactable _) => SceneTransition.Go(targetScene, targetSpawnId);
    }
}
