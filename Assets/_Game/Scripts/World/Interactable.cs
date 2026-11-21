using System;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Something the player can use with F (doors, signs, NPCs). Needs a trigger collider
    /// on the Interactable physics layer so PlayerInteractor can find it.</summary>
    public sealed class Interactable : MonoBehaviour
    {
        [SerializeField] string interactionId;
        [SerializeField] Vector2 promptOffset = new Vector2(0f, 1.4f);

        public event Action<Interactable> Interacted;

        public string InteractionId => interactionId;
        public int InteractCount { get; private set; }
        public Vector3 PromptPosition => transform.position + (Vector3)promptOffset;

        public void Configure(string id, Vector2 offset)
        {
            interactionId = id;
            promptOffset = offset;
        }

        public void Interact()
        {
            InteractCount++;
            Interacted?.Invoke(this);
        }
    }
}
