using System.Collections.Generic;
using Starbind.Save;
using UnityEngine;

namespace Starbind.Progression
{
    public enum QuestKind { Main, Side }

    public enum QuestStatus { Locked, Active, Done }

    /// <summary>One quest (STORY_WORLD_QUESTS §4, Q-3). Progress is not stored here: a quest is done when
    /// its <see cref="flag"/> is in ProfileData.completedQuests (the flag ids predate this system, so old
    /// saves keep their progress) and active once <see cref="requires"/> is done.</summary>
    [CreateAssetMenu(menuName = "Starbind/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        public string id;
        public string chapter;
        public QuestKind kind;
        public string title;
        [TextArea] public string description;
        public string giver;
        public string flag;
        public string requires;
        public int order;
    }

    /// <summary>Quest status derived from profile flags. Pure logic over a quest list.</summary>
    public static class QuestLog
    {
        public static QuestStatus Status(QuestDefinition q, ProfileData p)
        {
            if (p.HasQuest(q.flag))
                return QuestStatus.Done;
            return string.IsNullOrEmpty(q.requires) || p.HasQuest(q.requires) ? QuestStatus.Active : QuestStatus.Locked;
        }

        public static List<QuestDefinition> WithStatus(IEnumerable<QuestDefinition> all, ProfileData p, QuestStatus status)
        {
            var list = new List<QuestDefinition>();
            foreach (var q in all)
                if (q != null && Status(q, p) == status)
                    list.Add(q);
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        /// <summary>The first active main quest (the "what now" line), or null when the story is done.</summary>
        public static QuestDefinition CurrentMain(IEnumerable<QuestDefinition> all, ProfileData p)
        {
            foreach (var q in WithStatus(all, p, QuestStatus.Active))
                if (q.kind == QuestKind.Main)
                    return q;
            return null;
        }
    }
}
