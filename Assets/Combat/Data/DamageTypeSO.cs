using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    [CreateAssetMenu(fileName = "New DamageType", menuName = "SoR/Combat/DamageType")]
    public class DamageTypeSO : ScriptableObject
    {
        public DamageType Type;
        public float ArmorPenetration;
        [TextArea(2, 4)]
        public string Description;
        public Color DisplayColor = Color.white;
    }
}
