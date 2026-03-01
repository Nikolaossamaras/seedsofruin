using System.Collections.Generic;
using SoR.Core;
using SoR.Shared;
using UnityEngine;

namespace SoR.Systems.Gacha
{
    public struct GachaPullResult
    {
        public string CompanionId;
        public Rarity Rarity;
        public bool IsDuplicate;

        public GachaPullResult(string companionId, Rarity rarity, bool isDuplicate)
        {
            CompanionId = companionId;
            Rarity = rarity;
            IsDuplicate = isDuplicate;
        }
    }

    public class GachaSystem : IService
    {
        private readonly Dictionary<string, GachaPityTracker> _trackers = new();
        private readonly HashSet<string> _ownedCompanions = new();

        public void Initialize()
        {
            Debug.Log("[GachaSystem] Initialized.");
        }

        public void Dispose()
        {
            _trackers.Clear();
            _ownedCompanions.Clear();
        }

        /// <summary>
        /// Register a companion as already owned (for duplicate detection).
        /// </summary>
        public void RegisterOwnedCompanion(string companionId)
        {
            _ownedCompanions.Add(companionId);
        }

        /// <summary>
        /// Perform a single gacha pull on the given banner.
        /// </summary>
        public GachaPullResult Pull(BannerDefinitionSO banner)
        {
            var pity = GetOrCreateTracker(banner.BannerId);
            pity.IncrementPull();

            Rarity rarity = ResolveRarity(banner, pity);
            string companionId = PickFromPool(banner, rarity);
            bool isDuplicate = _ownedCompanions.Contains(companionId);

            if (!isDuplicate)
                _ownedCompanions.Add(companionId);

            // Reset pity counters as appropriate
            if (rarity == Rarity.Legendary)
                pity.ResetLegendaryPity();
            if (rarity == Rarity.Mythic)
            {
                pity.ResetMythicPity();
                pity.ResetLegendaryPity();
            }

            var result = new GachaPullResult(companionId, rarity, isDuplicate);
            EventBus.Raise(new GachaPullCompletedEvent(result));

            // Grant Accord Essence for duplicates
            if (isDuplicate)
            {
                int essenceAmount = rarity switch
                {
                    Rarity.Common => 5,
                    Rarity.Rare => 15,
                    Rarity.Legendary => 50,
                    Rarity.Mythic => 100,
                    _ => 5
                };
                EventBus.Raise(new AccordEssenceGainedEvent(essenceAmount));
            }

            return result;
        }

        /// <summary>
        /// Perform a 10-pull with a floor guarantee of at least one Rare or higher.
        /// </summary>
        public List<GachaPullResult> Pull10(BannerDefinitionSO banner)
        {
            var results = new List<GachaPullResult>(10);
            bool hasRareOrHigher = false;

            for (int i = 0; i < 10; i++)
            {
                var result = Pull(banner);
                results.Add(result);

                if (result.Rarity >= Rarity.Rare)
                    hasRareOrHigher = true;
            }

            // Floor guarantee: if no Rare or higher in 10 pulls, replace the last with a Rare
            if (!hasRareOrHigher)
            {
                var pity = GetOrCreateTracker(banner.BannerId);
                string companionId = PickFromPool(banner, Rarity.Rare);
                bool isDuplicate = _ownedCompanions.Contains(companionId);

                if (!isDuplicate)
                    _ownedCompanions.Add(companionId);

                results[9] = new GachaPullResult(companionId, Rarity.Rare, isDuplicate);
            }

            EventBus.Raise(new GachaMultiPullCompletedEvent(results));
            return results;
        }

        /// <summary>
        /// Resolves the rarity for a pull, considering hard pity, soft pity, and base rates.
        /// </summary>
        public Rarity ResolveRarity(BannerDefinitionSO banner, GachaPityTracker pity)
        {
            PityConfigSO pityConfig = banner.PityConfig;

            // Hard pity: guaranteed Mythic
            if (pityConfig != null && pity.PullsSinceMythic >= pityConfig.MythicHardPity)
            {
                return Rarity.Mythic;
            }

            // Hard pity: guaranteed Legendary
            if (pityConfig != null && pity.PullsSinceLegendary >= pityConfig.LegendaryHardPity)
            {
                return Rarity.Legendary;
            }

            // Calculate effective rates with soft pity
            float mythicRate = banner.MythicRate;
            float legendaryRate = banner.LegendaryRate;

            if (pityConfig != null && pity.PullsSinceLegendary >= pityConfig.LegendarySoftPity)
            {
                int pullsOverSoft = pity.PullsSinceLegendary - pityConfig.LegendarySoftPity;
                legendaryRate += pullsOverSoft * pityConfig.SoftPityRateBoost;
            }

            // Roll
            float roll = Random.value;
            float cumulative = 0f;

            cumulative += mythicRate;
            if (roll < cumulative)
                return Rarity.Mythic;

            cumulative += legendaryRate;
            if (roll < cumulative)
                return Rarity.Legendary;

            cumulative += banner.RareRate;
            if (roll < cumulative)
                return Rarity.Rare;

            return Rarity.Common;
        }

        private string PickFromPool(BannerDefinitionSO banner, Rarity rarity)
        {
            List<string> pool = banner.GetPoolForRarity(rarity);

            if (pool == null || pool.Count == 0)
            {
                Debug.LogWarning($"[GachaSystem] Empty pool for rarity {rarity} on banner {banner.BannerName}.");
                return "unknown_companion";
            }

            // Rate-up check: 50/50 for featured companion
            if (banner.HasRateUp(rarity))
            {
                var pity = GetOrCreateTracker(banner.BannerId);

                // If lost last 50/50, guarantee the featured companion
                if (pity.LostLastFiftyFifty)
                {
                    pity.LostLastFiftyFifty = false;
                    return banner.FeaturedCompanionId;
                }

                // 50/50 roll
                if (Random.value < 0.5f)
                {
                    return banner.FeaturedCompanionId;
                }
                else
                {
                    pity.LostLastFiftyFifty = true;
                    // Pick a non-featured companion from the pool
                    var nonFeatured = pool.FindAll(id => id != banner.FeaturedCompanionId);
                    if (nonFeatured.Count > 0)
                        return nonFeatured[Random.Range(0, nonFeatured.Count)];
                }
            }

            return pool[Random.Range(0, pool.Count)];
        }

        private GachaPityTracker GetOrCreateTracker(string bannerId)
        {
            if (!_trackers.TryGetValue(bannerId, out var tracker))
            {
                tracker = new GachaPityTracker();
                _trackers[bannerId] = tracker;
            }

            return tracker;
        }

        /// <summary>
        /// Returns the pity tracker for a banner (for save/load purposes).
        /// </summary>
        public GachaPityTracker GetPityTracker(string bannerId)
        {
            return GetOrCreateTracker(bannerId);
        }
    }
}
