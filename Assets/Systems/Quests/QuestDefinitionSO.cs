using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.Systems.Quests
{
    public enum QuestType
    {
        MainStory,
        SideQuest,
        GuildContract,
        CompanionQuest
    }

    [Serializable]
    public class QuestReward
    {
        public string ItemId;
        public int Quantity;
        public int XP;
        public int Gold;
    }

    [CreateAssetMenu(menuName = "SoR/Systems/Quest")]
    public class QuestDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string QuestName;
        public string QuestId;
        [TextArea(3, 6)]
        public string Description;

        [Header("Classification")]
        public QuestType Type;
        public int RequiredLevel;

        [Header("Objectives")]
        public List<QuestObjectiveSO> Objectives = new();

        [Header("Rewards")]
        public List<QuestReward> Rewards = new();

        [Header("Prerequisites")]
        public string[] PrerequisiteQuestIds;
    }
}
