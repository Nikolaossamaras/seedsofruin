using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    [CreateAssetMenu(fileName = "New StatusEffect", menuName = "SoR/Combat/StatusEffect")]
    public class StatusEffectSO : ScriptableObject
    {
        [Header("Identity")]
        public string EffectName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Timing")]
        public float Duration;
        public float TickInterval;
        public float TickDamage;

        [Header("Stacking")]
        public bool IsStackable;
        public int MaxStacks = 1;

        [Header("Element")]
        public Element AssociatedElement;

        [Header("UI")]
        public Sprite Icon;
    }
}
