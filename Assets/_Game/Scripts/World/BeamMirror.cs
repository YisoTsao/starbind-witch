using System;
using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>A rotatable mirror of a BeamPuzzle (F flips it).</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class BeamMirror : MonoBehaviour
    {
        [SerializeField] Vector2Int cell;
        [SerializeField] int kind;
        [SerializeField] SpriteRenderer sprite;
        public Vector2Int Cell => cell;
        public int Kind => kind;
        public event Action Turned;

        public void Configure(Vector2Int c, int k, SpriteRenderer sr)
        {
            cell = c;
            kind = k;
            sprite = sr;
        }

        void Start() => Apply();
        void OnEnable() => GetComponent<Interactable>().Interacted += Turn;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Turn;

        public void Turn(Interactable _)
        {
            kind = 1 - kind;
            Apply();
            Sfx.Play("sfx_portal", 0.4f);
            Turned?.Invoke();
        }

        void Apply()
        {
            if (sprite != null)
                sprite.flipX = kind == 1;
        }
    }
}
