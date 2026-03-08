using UnityEngine;

namespace SoR.Systems.Crafting
{
    public static class QualityCalculator
    {
        /// <summary>
        /// Returns the maximum quality tier reachable based on Harvest stat.
        /// &lt;10→1, 10+→2, 20+→3, 30+→4, 40+→5
        /// </summary>
        public static int HarvestCeiling(float harvestStat)
        {
            if (harvestStat >= 40f) return 5;
            if (harvestStat >= 30f) return 4;
            if (harvestStat >= 20f) return 3;
            if (harvestStat >= 10f) return 2;
            return 1;
        }

        /// <summary>
        /// Calculates crafting quality as a 1-5 star rating.
        /// Score = disciplineLevel factor (60%) + timingBonus (40%), mapped to tier within Harvest ceiling.
        /// </summary>
        /// <param name="harvestStat">The player's Harvest stat value.</param>
        /// <param name="disciplineLevel">The player's level in the relevant crafting discipline.</param>
        /// <param name="timingBonus">0.0-1.0 from timing minigame (1.0 = Perfect).</param>
        /// <returns>An integer quality rating from 1 to 5.</returns>
        public static int CalculateQuality(float harvestStat, int disciplineLevel, float timingBonus)
        {
            int ceiling = HarvestCeiling(harvestStat);

            // Discipline factor: level 1→0.0, level 10+→1.0
            float disciplineFactor = Mathf.Clamp01((disciplineLevel - 1f) / 9f);

            // Weighted score: 60% discipline, 40% timing
            float score = disciplineFactor * 0.6f + Mathf.Clamp01(timingBonus) * 0.4f;

            // Map score 0.0-1.0 to quality 1-ceiling
            int quality = Mathf.RoundToInt(Mathf.Lerp(1f, ceiling, score));
            return Mathf.Clamp(quality, 1, ceiling);
        }

        /// <summary>
        /// Backward-compatible overload — assumes mid-tier timing (0.5).
        /// </summary>
        public static int CalculateQuality(float harvestStat, int disciplineLevel, int ingredientQuality)
        {
            return CalculateQuality(harvestStat, disciplineLevel, 0.5f);
        }

        /// <summary>
        /// Returns the display name for a quality tier.
        /// </summary>
        public static string QualityName(int quality)
        {
            return quality switch
            {
                1 => "Crude",
                2 => "Fine",
                3 => "Superior",
                4 => "Exquisite",
                5 => "Masterwork",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Returns a star string for the quality tier (e.g. "★★☆☆☆").
        /// </summary>
        public static string QualityStars(int quality)
        {
            quality = Mathf.Clamp(quality, 1, 5);
            string stars = "";
            for (int i = 0; i < 5; i++)
                stars += i < quality ? "\u2605" : "\u2606";
            return stars;
        }

        /// <summary>
        /// Returns a stat multiplier for crafted items based on quality.
        /// 1★=1.0, 2★=1.1, 3★=1.25, 4★=1.45, 5★=1.7
        /// </summary>
        public static float QualityStatMultiplier(int quality)
        {
            return quality switch
            {
                1 => 1.0f,
                2 => 1.1f,
                3 => 1.25f,
                4 => 1.45f,
                5 => 1.7f,
                _ => 1.0f
            };
        }
    }
}
