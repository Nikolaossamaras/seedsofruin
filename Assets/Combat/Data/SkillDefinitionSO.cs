using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    [CreateAssetMenu(fileName = "New Skill", menuName = "SoR/Combat/SkillDefinition")]
    public class SkillDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string SkillName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Stats")]
        public float BaseDamage;
        public float CooldownTime;
        public float VerdanceCost;

        [Header("Type")]
        public DamageType DamageType;
        public Element Element;

        [Header("Area & Casting")]
        public float AreaOfEffect;
        public float CastTime;

        [Header("UI")]
        public Sprite Icon;

        [Header("Behavior")]
        public bool IsPassive;
    }
}
