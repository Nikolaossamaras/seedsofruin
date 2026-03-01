using UnityEngine;

namespace SoR.Systems.Crafting
{
    public static class QualityCalculator
    {
        /// <summary>
        /// Calculates crafting quality as a 1-5 star rating.
        /// Formula: base = (harvestStat * 0.3 + disciplineLevel * 0.5 + ingredientQuality * 0.2) / 20, clamped 1-5.
        /// </summary>
        /// <param name="harvestStat">The player's Harvest stat value.</param>
        /// <param name="disciplineLevel">The player's level in the relevant crafting discipline.</param>
        /// <param name="ingredientQuality">The average quality of input ingredients.</param>
        /// <returns>An integer quality rating from 1 to 5.</returns>
        public static int CalculateQuality(float harvestStat, int disciplineLevel, int ingredientQuality)
        {
            float baseValue = (harvestStat * 0.3f + disciplineLevel * 0.5f + ingredientQuality * 0.2f) / 20f;
            int quality = Mathf.RoundToInt(baseValue);
            return Mathf.Clamp(quality, 1, 5);
        }
    }
}
