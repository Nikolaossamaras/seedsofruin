using UnityEngine;

namespace SoR.Systems.Crafting
{
    [CreateAssetMenu(menuName = "SoR/Systems/CraftingDiscipline")]
    public class CraftingDisciplineSO : ScriptableObject
    {
        [Header("Identity")]
        public CraftingDiscipline DisciplineType;
        public string DisciplineName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Progression")]
        public int MaxLevel = 100;
        public AnimationCurve XPCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}
