using UnityEngine;

namespace SoR.Systems.Quests
{
    public enum ObjectiveType
    {
        Kill,
        Collect,
        Talk,
        Explore,
        Craft
    }

    [CreateAssetMenu(menuName = "SoR/Systems/QuestObjective")]
    public class QuestObjectiveSO : ScriptableObject
    {
        public string ObjectiveDescription;
        public ObjectiveType Type;
        public string TargetId;
        public int RequiredCount = 1;
    }
}
