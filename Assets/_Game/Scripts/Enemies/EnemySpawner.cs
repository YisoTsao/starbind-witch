using System;
using System.Collections.Generic;
using Starbind.Core;
using Starbind.World;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Pooled enemy instances per definition id, the 60-alive cap (GDD §10.2), seeded
    /// placement helpers and the walkable spawn grid (spawn only on Spawnable cells, off-screen,
    /// at least MinSpawnDistance from the player).</summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        public const int MaxAlive = 60;
        public const float MinSpawnDistance = 9f;

        static EnemySpawner instance;

        readonly Dictionary<string, ComponentPool<Enemy>> pools = new Dictionary<string, ComponentPool<Enemy>>();
        readonly List<Enemy> alive = new List<Enemy>(MaxAlive + 8);
        GameRng rng = new GameRng(1);
        [SerializeField] SpawnGrid grid;

        public event Action<Enemy> Spawned;
        public event Action<Enemy> Removed;

        public static bool Exists => instance != null;
        public int Alive => alive.Count;
        public IReadOnlyList<Enemy> AliveList => alive;
        public GameRng Rng => rng;
        public int PeakAlive { get; private set; }
        public int TotalSpawned { get; private set; }
        public int RejectedByCap { get; private set; }
        public SpawnGrid Grid { get => grid; set => grid = value; }
        /// <summary>Chapter difficulty: health multiplier for every non-boss spawn (RunManager sets it).</summary>
        public float HpScale { get; set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static EnemySpawner Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<EnemySpawner>();
                    if (instance == null)
                        instance = new GameObject("[EnemySpawner]").AddComponent<EnemySpawner>();
                }
                return instance;
            }
        }

        void Awake()
        {
            if (instance == null)
                instance = this;
            if (grid == null)
                grid = FindAnyObjectByType<SpawnGrid>();
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public void SetSeed(int seed) => rng = new GameRng(seed);

        public Enemy SpawnAt(string id, Vector2 pos, bool summoned = false, float hpMultiplier = 1f)
        {
            var def = GameDatabase.Instance.Enemy(id);
            if (def == null)
            {
                Debug.LogWarning("[EnemySpawner] unknown enemy " + id);
                return null;
            }
            if (!def.isBoss && alive.Count >= MaxAlive)
            {
                RejectedByCap++;
                return null;
            }
            if (!pools.TryGetValue(id, out var pool))
            {
                pool = new ComponentPool<Enemy>(() =>
                {
                    var en = Enemy.Create(transform);
                    en.Released += OnReleased;
                    return en;
                });
                pools[id] = pool;
            }
            var e = pool.Get();
            e.name = id;
            e.Spawn(def, pos, def.isBoss ? hpMultiplier : hpMultiplier * HpScale);
            alive.Add(e);
            TotalSpawned++;
            PeakAlive = Mathf.Max(PeakAlive, alive.Count);
            Spawned?.Invoke(e);
            return e;
        }

        void OnReleased(Enemy e)
        {
            if (!alive.Remove(e))
                return;
            Removed?.Invoke(e);
            if (pools.TryGetValue(e.name, out var pool))
                pool.Release(e);
            else
                e.gameObject.SetActive(false);
        }

        public void DespawnAll()
        {
            foreach (var e in new List<Enemy>(alive))
                e.Release();
        }

        /// <summary>Random point within <paramref name="radius"/> of <paramref name="center"/> on a walkable
        /// cell when a grid exists (seeded).</summary>
        public Vector2 RandomAround(Vector2 center, float radius)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                float r = radius * Mathf.Sqrt(rng.Value());
                var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (grid == null || grid.IsWalkable(p))
                    return p;
            }
            return center;
        }

        /// <summary>Seeded spawn point: Spawnable cell, outside <paramref name="view"/> (+1 margin), at
        /// least MinSpawnDistance from the player and within <paramref name="maxDistance"/>.</summary>
        public bool TryFindSpawnPoint(Vector2 player, Rect view, float maxDistance, out Vector2 point)
        {
            point = default;
            if (grid == null)
                return false;
            var outer = new Rect(view.xMin - 1f, view.yMin - 1f, view.width + 2f, view.height + 2f);
            for (int i = 0; i < 24; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                float d = rng.Range(MinSpawnDistance, Mathf.Max(MinSpawnDistance + 0.5f, maxDistance));
                var p = player + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (outer.Contains(p) || !grid.IsSpawnable(p))
                    continue;
                point = p;
                return true;
            }
            return false;
        }
    }
}
