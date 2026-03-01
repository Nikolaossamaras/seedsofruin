using System.Collections.Generic;
using SoR.Shared;
using UnityEngine;

namespace SoR.Systems.Gacha
{
    [CreateAssetMenu(menuName = "SoR/Systems/Gacha/Banner")]
    public class BannerDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string BannerName;
        public string BannerId;
        [TextArea(2, 4)]
        public string Description;

        [Header("Rates (must sum to 1.0)")]
        [Range(0f, 1f)] public float CommonRate = 0.80f;
        [Range(0f, 1f)] public float RareRate = 0.15f;
        [Range(0f, 1f)] public float LegendaryRate = 0.04f;
        [Range(0f, 1f)] public float MythicRate = 0.01f;

        [Header("Pity")]
        public PityConfigSO PityConfig;

        [Header("Pools (Companion IDs)")]
        public List<string> CommonPool = new();
        public List<string> RarePool = new();
        public List<string> LegendaryPool = new();
        public List<string> MythicPool = new();

        [Header("Featured")]
        public string FeaturedCompanionId;
        public Rarity FeaturedRarity;

        [Header("Visuals")]
        public Sprite BannerArt;

        public bool HasRateUp(Rarity rarity) =>
            !string.IsNullOrEmpty(FeaturedCompanionId) && rarity == FeaturedRarity;

        public List<string> GetPoolForRarity(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common => CommonPool,
                Rarity.Rare => RarePool,
                Rarity.Legendary => LegendaryPool,
                Rarity.Mythic => MythicPool,
                _ => CommonPool
            };
        }
    }
}
