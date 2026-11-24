using System.Collections.Generic;
using Starbind.Core;
using Starbind.Progression;
using Starbind.Save;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Village star plate as a region selector (Q-4): F lists the cottage door and every chapter
    /// whose unlock flag is done; picking one travels there. Replaces the cottage-only LocalTeleport.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class RegionSelector : MonoBehaviour
    {
        [SerializeField] string homeSpawn = "spawn_cottage_door";

        public string HomeSpawn => homeSpawn;

        public void Configure(string home) => homeSpawn = home;

        void OnEnable() => GetComponent<Interactable>().Interacted += Open;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Open;

        void Open(Interactable _) => GameSession.Instance.UI.ShowRegions(this);

        /// <summary>Chapters the profile can travel to, in story order.</summary>
        public static List<ChapterDefinition> Available(ProfileData p)
        {
            var list = new List<ChapterDefinition>();
            foreach (var c in GameDatabase.Instance.chapters)
                if (c != null && (string.IsNullOrEmpty(c.requiresFlag) || p.HasQuest(c.requiresFlag)))
                    list.Add(c);
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        public void TravelHome()
        {
            var sp = SpawnPoint.Find(homeSpawn);
            var p = Player.PlayerController.Current;
            if (sp == null || p == null)
                return;
            VFX.VfxService.Instance.Play("VFX_portal", p.transform.position + new Vector3(0, 0.4f), 0, 1.2f);
            p.Teleport(sp.transform.position);
            CameraRig.CameraFollow2D.Main?.SnapToTarget();
            VFX.VfxService.Instance.Play("VFX_portal", sp.transform.position + new Vector3(0, 0.4f), 0, 1.2f);
            VFX.Sfx.Play("sfx_portal", 0.8f);
        }

        public static void Travel(ChapterDefinition c)
        {
            VFX.Sfx.Play("sfx_portal", 0.8f);
            SceneTransition.Go(c.scene, c.entrySpawn);
        }
    }

    /// <summary>The finale's two endings (Q-2: the player chooses). Flags are kept in completedQuests.</summary>
    public static class EndingChoice
    {
        public const string SealFlag = "ending_seal";
        public const string PactFlag = "ending_pact";

        public static readonly string[] SealEpilogue =
        {
            "露米把扭曲的書頁一頁一頁闔上，以艾拉傳下的月影封印重新鎖住魔典。",
            "改寫法術的力量隨封印沉睡。從今以後，她只是一名普通的魔女。",
            "但艾斯特拉的星空恢復了原本的秩序，礦脈、湖面與觀星台都安靜下來。",
            "——結局 A：封印。星露村的燈火依舊溫暖。",
        };

        public static readonly string[] PactEpilogue =
        {
            "露米沒有闔上魔典。她把手放在書脊上，與失控的意志締結契約。",
            "「你想改寫世界的規則？那就照我的規則來。」魔典第一次安靜下來。",
            "她成為新的星痕魔女，失控的書頁從此只聽她一人的話。",
            "——結局 B：共存。星露村的人們抬頭，看見夜空多了一顆新的星。",
        };

        public static void Choose(ProfileData p, bool seal)
        {
            p.CompleteQuest(seal ? SealFlag : PactFlag);
            p.CompleteQuest("story_complete");
        }
    }
}
