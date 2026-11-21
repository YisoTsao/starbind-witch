using System;
using System.Collections.Generic;
using Starbind.CameraRig;
using Starbind.Core;
using Starbind.Player;
using Starbind.World;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Monster pacing for the Forest (GDD §10.2): target alive count grows with run time,
    /// enemy mix depends on the player's zone, events (altar waves, elite) on top. Uses enemy ids only;
    /// all randomness through the spawner's seeded GameRng. Bosses suppress ambient spawns.</summary>
    public sealed class SpawnDirector : MonoBehaviour
    {
        [Serializable]
        public struct Pace { public float fromMinute; public int targetMin; public int targetMax; public float interval; }

        // GDD §10.2 table (scaled to the 10-12 minute demo)
        public static readonly Pace[] Timeline =
        {
            new Pace { fromMinute = 0, targetMin = 5, targetMax = 12, interval = 1.6f },
            new Pace { fromMinute = 2, targetMin = 12, targetMax = 25, interval = 1.1f },
            new Pace { fromMinute = 4, targetMin = 20, targetMax = 35, interval = 0.8f },
            new Pace { fromMinute = 6, targetMin = 35, targetMax = 50, interval = 0.55f },
            new Pace { fromMinute = 8, targetMin = 40, targetMax = 60, interval = 0.45f },
            new Pace { fromMinute = 10, targetMin = 6, targetMax = 10, interval = 2.5f },
        };

        static readonly Dictionary<string, (string id, float w)[]> zoneMix = new Dictionary<string, (string, float)[]>
        {
            { "Z01", new[] { ("slime", 10f), ("wisp", 1f) } },
            { "Z02", new[] { ("slime", 6f), ("spore_shroom", 5f), ("wisp", 2f) } },
            { "Z03", new[] { ("slime", 5f), ("spore_shroom", 4f), ("wisp", 4f), ("root_guard", 1.5f) } },
            { "Z04", new[] { ("slime", 4f), ("spore_shroom", 3f), ("wisp", 3f), ("root_guard", 3f) } },
        };

        public bool Running { get; set; }
        public float Elapsed { get; set; }
        public bool BossActive { get; set; }
        public bool EliteSpawned { get; private set; }
        public float TimeScaleForPacing { get; set; } = 1f;
        public string CurrentZone { get; private set; } = "";

        public bool WaveActive => waveRemaining > 0 || waveEnemies.Count > 0;
        public int WaveRemainingToSpawn => waveRemaining;
        public int WaveAlive => waveEnemies.Count;
        public event Action WaveCleared;
        public event Action<Enemy> EliteAppeared;

        readonly HashSet<Enemy> waveEnemies = new HashSet<Enemy>();
        readonly List<float> weights = new List<float>();
        int waveRemaining;
        Vector2 waveCenter;
        float waveRadius;
        float nextSpawn;
        float nextWaveSpawn;
        EnemySpawner spawner;

        void Awake()
        {
            spawner = EnemySpawner.Instance;
            spawner.Removed += OnRemoved;
        }

        void OnDestroy()
        {
            if (spawner != null)
                spawner.Removed -= OnRemoved;
        }

        public static Pace PaceAt(float minutes)
        {
            var p = Timeline[0];
            foreach (var t in Timeline)
                if (minutes >= t.fromMinute)
                    p = t;
            return p;
        }

        /// <summary>Target alive count at a run time (interpolated inside the band).</summary>
        public static int TargetAlive(float seconds)
        {
            float m = seconds / 60f;
            var p = PaceAt(m);
            float into = Mathf.Clamp01(m - p.fromMinute) / 2f;
            return Mathf.RoundToInt(Mathf.Lerp(p.targetMin, p.targetMax, into));
        }

        public void StartWave(Vector2 center, float radius, int count)
        {
            waveCenter = center;
            waveRadius = radius;
            waveRemaining = count;
            nextWaveSpawn = Time.time;
        }

        void OnRemoved(Enemy e)
        {
            if (waveEnemies.Remove(e) && waveRemaining == 0 && waveEnemies.Count == 0)
                WaveCleared?.Invoke();
        }

        string PickType()
        {
            if (!zoneMix.TryGetValue(CurrentZone, out var mix))
                mix = zoneMix["Z01"];
            weights.Clear();
            foreach (var (_, w) in mix)
                weights.Add(w);
            int i = spawner.Rng.Weighted(weights);
            return mix[Mathf.Max(0, i)].id;
        }

        void Update()
        {
            var player = PlayerController.Current;
            if (player == null)
                return;
            Vector2 pp = player.transform.position;
            var zm = ZoneMap.Current;
            if (zm != null && zm.TryGet(pp, out var zone))
                CurrentZone = zone.id;
            if (GamePause.IsPaused)
                return;
            float now = Time.time;

            // altar wave: bursts around the altar ring, ignoring the off-screen rule (it is an event)
            if (waveRemaining > 0 && now >= nextWaveSpawn)
            {
                nextWaveSpawn = now + 0.35f;
                int burst = Mathf.Min(3, waveRemaining);
                for (int i = 0; i < burst; i++)
                {
                    var p = spawner.RandomAround(waveCenter, waveRadius);
                    if ((p - pp).sqrMagnitude < 4f)
                        p = waveCenter + (waveCenter - pp).normalized * waveRadius;
                    var e = spawner.SpawnAt(PickType(), p);
                    if (e == null)
                        break;
                    waveEnemies.Add(e);
                    waveRemaining--;
                }
            }

            // ambient pacing only while the run is active (events above always run)
            if (!Running)
                return;
            Elapsed += Time.deltaTime * TimeScaleForPacing;
            if (!EliteSpawned && Elapsed >= 360f && !BossActive)
                SpawnElite(pp);

            if (BossActive || now < nextSpawn)
                return;
            var pace = PaceAt(Elapsed / 60f);
            nextSpawn = now + pace.interval;
            bool safe = zm != null && zm.TryGet(pp, out var z2) && z2.noSpawn;
            if (safe || spawner.Alive >= TargetAlive(Elapsed))
                return;
            var cam = CameraFollow2D.Main;
            var view = cam != null ? cam.ViewRect() : new Rect(pp.x - 8, pp.y - 4.5f, 16, 9);
            int batch = 1 + spawner.Rng.Range(0, Elapsed > 240f ? 3 : 2);
            for (int i = 0; i < batch; i++)
                if (spawner.TryFindSpawnPoint(pp, view, 15f, out var sp))
                    spawner.SpawnAt(PickType(), sp);
        }

        public Enemy SpawnElite(Vector2 near)
        {
            EliteSpawned = true;
            var cam = CameraFollow2D.Main;
            var view = cam != null ? cam.ViewRect() : new Rect(near.x - 8, near.y - 4.5f, 16, 9);
            var p = spawner.TryFindSpawnPoint(near, view, 13f, out var sp) ? sp : spawner.RandomAround(near, 6f);
            var e = spawner.SpawnAt("spore_elite", p);
            if (e != null)
                EliteAppeared?.Invoke(e);
            return e;
        }
    }
}
