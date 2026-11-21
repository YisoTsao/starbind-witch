using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Moves the player to another scene when they walk into a rectangle (Village south exit,
    /// Forest exit). Rect check, no physics.</summary>
    public sealed class AreaExit : MonoBehaviour
    {
        [SerializeField] Rect area;
        [SerializeField] string targetScene;
        [SerializeField] string targetSpawn;
        [SerializeField] string label;
        public Rect Area => area;
        public string TargetScene => targetScene;
        float armedAt;

        public void Configure(Rect r, string scene, string spawn, string text)
        {
            area = r;
            targetScene = scene;
            targetSpawn = spawn;
            label = text;
        }

        void Start() => armedAt = Time.time + 0.5f;

        void Update()
        {
            var p = Player.PlayerController.Current;
            if (p == null || Time.time < armedAt || SceneTransition.IsBusy)
                return;
            if (area.Contains(p.transform.position))
            {
                armedAt = Time.time + 2f;
                if (!string.IsNullOrEmpty(label))
                    GameSession.Instance.UI.Notice(label);
                SceneTransition.Go(targetScene, targetSpawn);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}
