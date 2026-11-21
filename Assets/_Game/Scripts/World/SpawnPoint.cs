using UnityEngine;

namespace Starbind.World
{
    /// <summary>Named arrival point; transitions resolve spawns by id, never by coordinates.</summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] string spawnId = "spawn_default";

        public string SpawnId => spawnId;

        public void Configure(string id) => spawnId = id;

        public static SpawnPoint Find(string id)
        {
            foreach (var s in FindObjectsByType<SpawnPoint>())
                if (s.spawnId == id)
                    return s;
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
