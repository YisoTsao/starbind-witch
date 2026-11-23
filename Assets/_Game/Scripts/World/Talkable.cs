using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>NPC or sign with lines shown in the dialogue box (GDD §2.3, §3.4). Lines may depend on
    /// profile progress (first visit / after a victory).</summary>
    [RequireComponent(typeof(Interactable))]
    public sealed class Talkable : MonoBehaviour
    {
        [SerializeField] string speaker;
        [SerializeField, TextArea] string[] lines;
        [SerializeField, TextArea] string[] linesAfterVictory;
        [SerializeField] string questId;
        [SerializeField] bool healsPlayer;

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

        void OnEnable() => GetComponent<Interactable>().Interacted += Talk;
        void OnDisable() => GetComponent<Interactable>().Interacted -= Talk;

        void Talk(Interactable _)
        {
            var s = GameSession.Instance;
            var use = s.Profile.victories > 0 && linesAfterVictory != null && linesAfterVictory.Length > 0 ? linesAfterVictory : lines;
            s.UI.ShowDialogue(speaker, use);
            var anim = transform.parent != null ? transform.parent.GetComponentInChildren<Player.CharacterAnimator>() : null;
            if (anim != null)
                anim.PlayCast(0.9f, true); // NPC greets (villager waves, the prince raises his sword)
            if (!string.IsNullOrEmpty(questId))
                s.Profile.CompleteQuest(questId);
            if (healsPlayer && Player.PlayerController.Current != null)
            {
                Player.PlayerController.Current.Vitals.FullRestore();
                VfxService.Instance.Play("VFX_levelup", Player.PlayerController.Current.transform.position + new Vector3(0, 0.4f), 0, 1.5f, new Color(1f, 1f, 0.7f));
                Sfx.Play("sfx_potion", 0.8f);
            }
        }
    }
}
