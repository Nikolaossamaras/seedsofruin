using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    [CreateAssetMenu(fileName = "New Weapon", menuName = "SoR/Combat/WeaponDefinition")]
    public class WeaponDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string WeaponName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Classification")]
        public WeaponType WeaponType;
        public Rarity Rarity;
        public float Range = 2f;

        [Header("Stats")]
        public float BaseDamage;
        public float BaseStagger;
        public float AttackSpeed;
        public float ChargedMultiplier = 1.5f;

        [Header("Type")]
        public DamageType DamageType;
        public Element Element;

        [Header("Combo")]
        public int MaxComboHits = 4;

        [Header("Special")]
        [TextArea(1, 3)]
        public string UniquePassive;

        [Header("UI")]
        public Sprite Icon;
    }
}
