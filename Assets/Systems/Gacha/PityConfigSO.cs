using UnityEngine;

namespace SoR.Systems.Gacha
{
    [CreateAssetMenu(menuName = "SoR/Systems/Gacha/PityConfig")]
    public class PityConfigSO : ScriptableObject
    {
        [Header("Legendary Pity")]
        public int LegendarySoftPity = 70;
        public int LegendaryHardPity = 90;

        [Header("Mythic Pity")]
        public int MythicHardPity = 180;

        [Header("Soft Pity")]
        [Tooltip("Rate increase per pull over the soft pity threshold.")]
        public float SoftPityRateBoost = 0.05f;
    }
}
