using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Workshop stations: grimoire lectern (loadout), research desk, bed (save + rest).</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class WorkshopStation : MonoBehaviour
    {
        public enum Kind { Lectern, Research, Bed }
        [SerializeField] Kind kind;
        public Kind StationKind => kind;
        public void Configure(Kind k) => kind = k;
        void OnEnable() => GetComponent<Interactable>().Interacted += Use;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Use;

        void Use(Interactable _)
        {
            var s = GameSession.Instance;
            switch (kind)
            {
                case Kind.Lectern: s.UI.ShowLoadout(); break;
                case Kind.Research: s.UI.ShowResearch(); break;
                case Kind.Bed:
                    s.RestAndSave();
                    s.UI.ShowDialogue("床", new[] { "你小睡片刻。HP/MP 已回復，藥水已補滿，進度已存檔。" });
                    break;
            }
        }
    }
}
