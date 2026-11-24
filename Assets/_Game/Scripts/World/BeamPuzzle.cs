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
    /// <summary>Pure light-beam tracing on the tile grid (GDD §2.2 光束折射): the beam leaves the emitter
    /// cell in a direction, '/' and '\' mirrors turn it, solid cells stop it. Used by BeamPuzzle and tests.</summary>
    public static class BeamTracer
    {
        public const int MaxSteps = 96;

        /// <summary>Mirror kind 0 = '/', 1 = '\'.</summary>
        public static Vector2Int Reflect(Vector2Int dir, int kind) =>
            kind == 0 ? new Vector2Int(dir.y, dir.x) : new Vector2Int(-dir.y, -dir.x);

        /// <summary>Cells the beam passes (emitter excluded). Stops at <paramref name="target"/> (returns
        /// true), at a solid cell, or after MaxSteps.</summary>
        public static bool Trace(Vector2Int start, Vector2Int dir, Func<Vector2Int, int> mirrorAt, Func<Vector2Int, bool> solid,
            Vector2Int target, List<Vector2Int> path)
        {
            path?.Clear();
            var p = start;
            for (int i = 0; i < MaxSteps; i++)
            {
                p += dir;
                path?.Add(p);
                if (p == target)
                    return true;
                if (solid(p))
                    return false;
                int m = mirrorAt(p);
                if (m >= 0)
                    dir = Reflect(dir, m);
            }
            return false;
        }
    }

    /// <summary>Observatory refraction puzzle: an emitter, rotatable mirrors (F flips '/' and '\') and a
    /// receiver; when the traced beam reaches the receiver its gate opens. Draws the beam path.</summary>
    public sealed class BeamPuzzle : MonoBehaviour
    {
        [SerializeField] Vector2Int emitter;
        [SerializeField] Vector2Int direction = Vector2Int.up;
        [SerializeField] Vector2Int receiver;
        [SerializeField] string gate;
        [SerializeField] BeamMirror[] mirrors;
        LineRenderer line;
        readonly List<Vector2Int> path = new List<Vector2Int>(64);
        SpawnGrid grid;

        public bool Solved { get; private set; }
        public IReadOnlyList<Vector2Int> Path => path;

        public void Configure(Vector2Int from, Vector2Int dir, Vector2Int to, string gateId, BeamMirror[] ms)
        {
            emitter = from;
            direction = dir;
            receiver = to;
            gate = gateId;
            mirrors = ms;
        }

        void Start()
        {
            grid = FindAnyObjectByType<SpawnGrid>();
            line = gameObject.AddComponent<LineRenderer>();
            var db = GameDatabase.Instance;
            if (db != null && db.unlitMaterial != null)
                line.sharedMaterial = db.unlitMaterial;
            line.sortingLayerName = StarbindLayers.SortVfx;
            line.widthMultiplier = 0.12f;
            line.startColor = line.endColor = new Color(1f, 0.92f, 0.6f, 0.9f);
            foreach (var m in mirrors)
                if (m != null)
                    m.Turned += Retrace;
            Retrace();
        }

        int MirrorAt(Vector2Int c)
        {
            foreach (var m in mirrors)
                if (m != null && m.Cell == c)
                    return m.Kind;
            return -1;
        }

        bool Solid(Vector2Int c) => grid != null && !grid.IsWalkable(new Vector2(c.x + 0.5f, c.y + 0.5f)) && MirrorAt(c) < 0;

        public void Retrace()
        {
            bool hit = BeamTracer.Trace(emitter, direction, MirrorAt, Solid, receiver, path);
            line.positionCount = path.Count + 1;
            line.SetPosition(0, new Vector3(emitter.x + 0.5f, emitter.y + 0.5f, 0));
            for (int i = 0; i < path.Count; i++)
                line.SetPosition(i + 1, new Vector3(path[i].x + 0.5f, path[i].y + 0.5f, 0));
            if (hit && !Solved)
            {
                Solved = true;
                line.startColor = line.endColor = new Color(1f, 1f, 0.8f, 1f);
                VfxService.Instance.Play("VFX_flash", new Vector2(receiver.x + 0.5f, receiver.y + 0.5f), 0, 1.6f, new Color(1f, 0.95f, 0.6f));
                PowerGate.Open(gate);
            }
        }
    }
}
