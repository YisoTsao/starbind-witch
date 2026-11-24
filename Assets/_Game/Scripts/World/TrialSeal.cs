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
    /// <summary>Citadel tower trial (STORY_WORLD_QUESTS M5-02): F summons one school's monsters around
    /// the seal; clearing them breaks the seal. All seals of a group open the group's gate.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class TrialSeal : MonoBehaviour
    {
        [SerializeField] string group;
        [SerializeField] string school;
        [SerializeField] string[] enemies;
        [SerializeField] int waveSize = 18;
        static readonly Dictionary<string, List<TrialSeal>> groups = new Dictionary<string, List<TrialSeal>>();
        SpawnDirector director;

        public bool Started { get; private set; }
        public bool Cleared { get; private set; }
        public string School => school;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => groups.Clear();

        public void Configure(string g, string schoolName, string[] mix, int size)
        {
            group = g;
            school = schoolName;
            enemies = mix;
            waveSize = size;
        }

        void OnEnable()
        {
            GetComponent<Interactable>().Interacted += Begin;
            if (!groups.TryGetValue(group, out var l))
                groups[group] = l = new List<TrialSeal>();
            l.Add(this);
        }

        void OnDisable()
        {
            GetComponent<Interactable>().Interacted -= Begin;
            if (groups.TryGetValue(group, out var l))
                l.Remove(this);
            if (director != null)
                director.WaveCleared -= OnCleared;
        }

        public void Begin(Interactable _)
        {
            if (Started)
                return;
            director = FindAnyObjectByType<SpawnDirector>();
            if (director == null || director.WaveActive)
            {
                GameSession.Instance.UI.Notice("先平息眼前的怪潮");
                return;
            }
            Started = true;
            director.WaveCleared += OnCleared;
            director.StartWave(transform.position, 4.5f, waveSize, enemies);
            GameSession.Instance.UI.Notice($"{school}試煉開始！");
            VfxService.Instance.Play("VFX_overdrive_aura", transform.position, 0, 2f, new Color(0.8f, 0.6f, 1f));
            Sfx.Play("sfx_boss_roar", 0.6f);
        }

        void OnCleared()
        {
            director.WaveCleared -= OnCleared;
            Cleared = true;
            RunManager.Current?.GrantBonusLevelUp();
            VfxService.Instance.Play("VFX_levelup", transform.position, 0, 2f, new Color(1f, 0.9f, 0.6f));
            int done = 0, total = 0;
            foreach (var s in groups[group])
            {
                total++;
                if (s.Cleared) done++;
            }
            GameSession.Instance.UI.Notice($"{school}試煉完成（{done}/{total}）");
            if (done == total)
                PowerGate.Open(group);
        }
    }
}
