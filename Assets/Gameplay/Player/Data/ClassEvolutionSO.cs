using SoR.Shared;
using UnityEngine;

namespace SoR.Gameplay
{
    [CreateAssetMenu(fileName = "New ClassEvolution", menuName = "SoR/Gameplay/ClassEvolution")]
    public class ClassEvolutionSO : ScriptableObject
    {
        [Header("Identity")]
        public string EvolutionName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Requirements")]
        [Tooltip("Required level to unlock this evolution (10, 20, or 35).")]
        public int RequiredLevel = 10;
        public StatType PrimaryStat;
        public float PrimaryStatMinimum;

        [Header("Passive Ability")]
        public string PassiveAbilityName;
        [TextArea(2, 4)]
        public string PassiveDescription;

        [Header("Bonuses")]
        public StatBlock StatBonuses;

        [Header("UI")]
        public Sprite EvolutionIcon;
    }
}
