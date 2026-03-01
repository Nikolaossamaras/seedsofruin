using SoR.Shared;
using UnityEngine;

namespace SoR.Gameplay
{
    [CreateAssetMenu(fileName = "New CompanionClass", menuName = "SoR/Gameplay/CompanionClass")]
    public class CompanionClassSO : ScriptableObject
    {
        [Header("Class Identity")]
        public CompanionClass ClassType;
        public string ClassName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Active Swap Bonus")]
        public string ActiveSwapBonusName;
        [TextArea(2, 4)]
        public string ActiveSwapBonusDescription;
        public float SwapBonusDuration;
        public float SwapBonusMagnitude;
    }
}
