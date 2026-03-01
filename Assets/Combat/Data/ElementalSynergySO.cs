using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    [CreateAssetMenu(fileName = "New ElementalSynergy", menuName = "SoR/Combat/ElementalSynergy")]
    public class ElementalSynergySO : ScriptableObject
    {
        [Header("Identity")]
        public string SynergyName;
        [TextArea(2, 4)]
        public string Description;

        [Header("Elements")]
        public Element ElementA;
        public Element ElementB;

        [Header("Effect")]
        public float DamageMultiplier = 1.5f;
        public float Duration;
        public StatusEffectSO AppliedEffect;

        [Header("Visuals")]
        public GameObject VFXPrefab;
    }
}
