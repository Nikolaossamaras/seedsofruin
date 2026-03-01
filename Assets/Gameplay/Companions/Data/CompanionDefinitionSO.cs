using SoR.Combat;
using SoR.Shared;
using UnityEngine;

namespace SoR.Gameplay
{
    [CreateAssetMenu(fileName = "New CompanionDefinition", menuName = "SoR/Gameplay/CompanionDefinition")]
    public class CompanionDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string CompanionName;
        public string CompanionId;
        [TextArea(2, 4)]
        public string Description;

        [Header("Classification")]
        public CompanionClass Class;
        public Element Element;
        public Rarity Rarity;

        [Header("Stats")]
        public StatBlock BaseStats;
        public float BaseHealth;

        [Header("Skills")]
        public SkillDefinitionSO ActiveSkill;
        public SkillDefinitionSO UltimateSkill;

        [Header("Swap Bonus")]
        [TextArea(2, 4)]
        public string SwapBonusDescription;

        [Header("UI")]
        public Sprite Portrait;
        public Sprite Icon;

        [Header("Prefab")]
        public GameObject Prefab;

        [Header("Constellations")]
        [Tooltip("Descriptions for constellation levels C1 through C6.")]
        public string[] ConstellationDescriptions = new string[6];
    }
}
