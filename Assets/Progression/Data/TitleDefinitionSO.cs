using UnityEngine;
using SoR.Shared;

namespace SoR.Progression
{
    [CreateAssetMenu(menuName = "SoR/Progression/Title")]
    public class TitleDefinitionSO : ScriptableObject
    {
        public string TitleName;
        public string TitleId;

        [TextArea(2, 4)]
        public string Description;

        [Tooltip("Condition string describing how to unlock this title.")]
        public string UnlockCondition;

        [Tooltip("Stat bonuses applied while this title is equipped.")]
        public StatBlock StatBonuses;

        public Sprite Icon;
    }
}
