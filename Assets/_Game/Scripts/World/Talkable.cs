using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Lines used once the profile has <see cref="flag"/> (story stages; the last matching wins).</summary>
    [Serializable]
    public sealed class StageLines
    {
        public string flag;
        /// <summary>Quest flag completed when this stage's lines are shown ("" = none).</summary>
        public string quest;
        [TextArea] public string[] lines;
    }

    /// <summary>NPC or sign with lines shown in the dialogue box (GDD §2.3, §3.4). Lines may depend on
    /// profile progress (first visit / after a victory / later story stages). Talking may complete a quest
    /// flag; a quest giver with a reward hands it over once, the first time the flag completes.</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class Talkable : MonoBehaviour
    {
        [SerializeField] string speaker;
        [SerializeField, TextArea] string[] lines;
        [SerializeField, TextArea] string[] linesAfterVictory;
        [SerializeField] string questId;
        [SerializeField] bool healsPlayer;
        [SerializeField] StageLines[] stages;
        [SerializeField] string reward;
        [SerializeField] string requiresFlag;
        [SerializeField, TextArea] string[] linesLocked;

        public string Speaker => speaker;
        public string[] Lines => lines;

        public void Configure(string who, string[] l, string[] after = null, string quest = null, bool heals = false)
        {
            speaker = who;
            lines = l;
            linesAfterVictory = after;
            questId = quest;
            healsPlayer = heals;
        }

        public void ConfigureStages(StageLines[] s, string rewardId, string requires = null, string[] locked = null)
        {
            stages = s;
            reward = rewardId;
            requiresFlag = requires;
            linesLocked = locked;
        }

        /// <summary>The lines this talk would show for <paramref name="p"/>.</summary>
        public string[] LinesFor(Save.ProfileData p) => LinesFor(p, out _);

        string[] LinesFor(Save.ProfileData p, out StageLines stage)
        {
            stage = null;
            if (!string.IsNullOrEmpty(requiresFlag) && !p.HasQuest(requiresFlag) && linesLocked != null && linesLocked.Length > 0)
                return linesLocked;
            string[] use = p.victories > 0 && linesAfterVictory != null && linesAfterVictory.Length > 0 ? linesAfterVictory : lines;
            if (stages != null)
                foreach (var st in stages)
                    if (st != null && st.lines != null && st.lines.Length > 0 && p.HasQuest(st.flag))
                    {
                        use = st.lines;
                        stage = st;
                    }
            return use;
        }

        void OnEnable() => GetComponent<Interactable>().Interacted += Talk;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Talk;

        /// <summary>"page" drops a grimoire page; "sand:N" / "dew:N" / "shard:N" / "spore:N" add materials.</summary>
        void GiveReward(GameSession s)
        {
            var p = s.Profile;
            if (reward == "page")
            {
                Pickup.Drop(PickupKind.Page, (Vector2)transform.position + new Vector2(0, -0.8f));
                return;
            }
            var parts = reward.Split(':');
            int n = parts.Length > 1 && int.TryParse(parts[1], out var v) ? v : 1;
            switch (parts[0])
            {
                case "sand": p.starSand += n; break;
                case "dew": p.moonDew += n; break;
                case "shard": p.coreShards += n; break;
                case "spore": p.spores += n; break;
            }
            s.UI.Notice($"獲得報酬：{RewardName(parts[0])} x{n}");
            s.Save();
        }

        static string RewardName(string id) => id switch
        {
            "sand" => "星砂",
            "dew" => "月露",
            "shard" => "星核碎片",
            "spore" => "孢子",
            _ => id,
        };

        void Talk(Interactable _)
        {
            var s = GameSession.Instance;
            var use = LinesFor(s.Profile, out var stage);
            s.UI.ShowDialogue(speaker, use);
            if (stage != null && !string.IsNullOrEmpty(stage.quest))
                s.Profile.CompleteQuest(stage.quest);
            var anim = transform.parent != null ? transform.parent.GetComponentInChildren<Player.CharacterAnimator>() : null;
            if (anim != null)
                anim.PlayCast(0.9f, true); // NPC greets (villager waves, the prince raises his sword)
            bool locked = !string.IsNullOrEmpty(requiresFlag) && !s.Profile.HasQuest(requiresFlag);
            if (!string.IsNullOrEmpty(questId) && !locked)
            {
                bool first = !s.Profile.HasQuest(questId);
                s.Profile.CompleteQuest(questId);
                if (first && !string.IsNullOrEmpty(reward))
                    GiveReward(s);
            }
            if (healsPlayer && Player.PlayerController.Current != null)
            {
                Player.PlayerController.Current.Vitals.FullRestore();
                VfxService.Instance.Play("VFX_levelup", Player.PlayerController.Current.transform.position + new Vector3(0, 0.4f), 0, 1.5f, new Color(1f, 1f, 0.7f));
                Sfx.Play("sfx_potion", 0.8f);
            }
        }
    }
}
