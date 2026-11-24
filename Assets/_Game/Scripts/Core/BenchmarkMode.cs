using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starbind.Enemies;
using Starbind.Player;
using Starbind.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starbind.Core
{
    /// <summary>Player-build performance check (GDD §13.7, §19): launched with
    /// <c>-starbindBenchmark [-benchmarkOut file.json] [-benchmarkSeconds 10]</c> it loads the Forest,
    /// fights 60 enemies at the altar with auto spells and overdrive, records frame times and a
    /// screenshot, writes JSON and quits. Inert in normal play.</summary>
    public sealed class BenchmarkMode : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-starbindBenchmark"))
                return;
            Application.runInBackground = true; // benchmark only: the game itself pauses when unfocused
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var go = new GameObject("[Benchmark]");
            DontDestroyOnLoad(go);
            go.AddComponent<BenchmarkMode>();
        }

        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        IEnumerator Start()
        {
            string outPath = Arg("-benchmarkOut", Path.Combine(Application.persistentDataPath, "benchmark.json"));
            float seconds = float.Parse(Arg("-benchmarkSeconds", "10"), System.Globalization.CultureInfo.InvariantCulture);
            GameSession.Instance.RedirectSaves(Path.Combine(Application.temporaryCachePath, "bench_saves"));
            SceneManager.LoadScene("Forest");
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(1f);
            var run = RunManager.Current;
            run.Director.Running = false;
            var player = PlayerController.Current;
            player.InputEnabled = false;
            player.Vitals.Health.InvulnerableUntil = float.MaxValue;
            player.Teleport(new Vector2(32f, 39f));
            CameraRig.CameraFollow2D.Main.SnapToTarget();
            player.Build.AddAuto("frost_orbit");
            player.Combat.Resonance.ForceFill();
            player.Combat.TryOverdrive();
            var sp = EnemySpawner.Instance;
            string[] mix = { "slime", "spore_shroom", "wisp", "slime", "root_guard" };
            int i = 0;
            // Warm-up: the first frame that spawns the full wave and casts pays one-time shader
            // compilation (Metal cache cold: ~150 ms on M2, warm: ~18 ms). Report it separately so the
            // steady-state numbers below measure combat, not first use.
            float warmupMax = 0;
            for (int w = 0; w < 30; w++)
            {
                while (sp.Alive < EnemySpawner.MaxAlive)
                    if (sp.SpawnAt(mix[i++ % mix.Length], sp.RandomAround(new Vector2(32f, 39f), 9f)) == null)
                        break;
                if (w == 0)
                {
                    var target = sp.AliveList.Count > 0 ? sp.AliveList[0].Center : new Vector2(32f, 42f);
                    player.Combat.CastSlot(0, target);
                    player.Combat.CastSlot(1, target);
                }
                yield return null;
                warmupMax = Mathf.Max(warmupMax, Time.unscaledDeltaTime * 1000f);
            }
            var frames = new List<float>(4096);
            var spikes = new List<string>();
            int minAlive = int.MaxValue;
            float start = Time.realtimeSinceStartup;
            float end = start + seconds;
            float nextCast = 0;
            int gcCount = GC.CollectionCount(0);
            while (Time.realtimeSinceStartup < end)
            {
                // What the previous frame did, so a long frame can be tied to its cause.
                int spawned = 0;
                bool upgrade = false, cast = false;
                while (sp.Alive < EnemySpawner.MaxAlive)
                {
                    if (sp.SpawnAt(mix[i++ % mix.Length], sp.RandomAround(new Vector2(32f, 39f), 9f)) == null)
                        break;
                    spawned++;
                }
                if (GameSession.Instance.UI.OpenPanel == "upgrade")
                {
                    GameSession.Instance.UI.ChooseCard(0);
                    upgrade = true;
                }
                if (Time.time > nextCast)
                {
                    nextCast = Time.time + 0.8f;
                    var target = sp.AliveList.Count > 0 ? sp.AliveList[0].Center : new Vector2(32f, 42f);
                    player.Combat.CastSlot(0, target);
                    player.Combat.CastSlot(1, target);
                    cast = true;
                }
                yield return null;
                float ms = Time.unscaledDeltaTime * 1000f;
                frames.Add(ms);
                minAlive = Mathf.Min(minAlive, sp.Alive);
                int gcNow = GC.CollectionCount(0);
                if (ms > 16.7f && spikes.Count < 64)
                    spikes.Add($"{{\"frame\":{frames.Count},\"t\":{Time.realtimeSinceStartup - start:0.000},\"ms\":{ms:0.00}," +
                               $"\"alive\":{sp.Alive},\"spawned\":{spawned},\"upgrade\":{(upgrade ? "true" : "false")}," +
                               $"\"cast\":{(cast ? "true" : "false")},\"gc\":{gcNow - gcCount}}}");
                gcCount = gcNow;
            }
            // Capture after sampling so PNG readback and encoding do not affect frame times.
            ScreenCapture.CaptureScreenshot(Path.ChangeExtension(outPath, ".png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            frames.Sort();
            float avg = frames.Average();
            string json = $"{{\"build\":\"{Application.platform}\",\"unity\":\"{Application.unityVersion}\",\"screen\":\"{Screen.width}x{Screen.height}\"," +
                          $"\"seconds\":{seconds},\"frames\":{frames.Count},\"avg_ms\":{avg:0.00},\"fps_avg\":{1000f / avg:0.0}," +
                          $"\"p95_ms\":{frames[(int)(frames.Count * 0.95f)]:0.00},\"p99_ms\":{frames[(int)(frames.Count * 0.99f)]:0.00},\"max_ms\":{frames[frames.Count - 1]:0.00}," +
                          $"\"enemies_target\":{EnemySpawner.MaxAlive},\"enemies_min_alive\":{minAlive},\"gc_total_mb\":{GC.GetTotalMemory(false) / 1048576f:0.0}," +
                          $"\"vsync\":{QualitySettings.vSyncCount},\"device\":\"{SystemInfo.deviceModel} / {SystemInfo.graphicsDeviceName}\"," +
                          $"\"warmup_max_ms\":{warmupMax:0.00},\"spikes\":[{string.Join(",", spikes)}]}}";
            File.WriteAllText(outPath, json);
            Debug.Log("[Benchmark] " + json);
            // real Player frame of the settings panel (pixel-font glyphs at the actual window resolution)
            sp.DespawnAll();
            var ui = GameSession.Instance.UI;
            ui.CloseModal();
            ui.TogglePause();
            ui.ShowSettings();
            for (int f = 0; f < 5; f++)
                yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(outPath), Path.GetFileNameWithoutExtension(outPath) + "_settings.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            ui.CloseModal();
            Application.Quit();
        }
    }
}
