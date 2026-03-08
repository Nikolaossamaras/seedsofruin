using System.Collections.Generic;
using SoR.Core;
using SoR.Systems.Inventory;
using UnityEngine;

namespace SoR.Systems.Crafting
{
    public class CraftingSystem : IService
    {
        private readonly Dictionary<CraftingDiscipline, int> _disciplineLevels = new();
        private readonly Dictionary<CraftingDiscipline, int> _disciplineXP = new();

        private InventorySystem _inventory;

        // GDD discipline unlock levels (player level required)
        private static readonly Dictionary<CraftingDiscipline, int> DisciplineUnlockLevels = new()
        {
            { CraftingDiscipline.Herbalism, 1 },
            { CraftingDiscipline.Forging, 8 },
            { CraftingDiscipline.Seedcraft, 14 },
            { CraftingDiscipline.Runebinding, 22 }
        };

        public void Initialize()
        {
            _inventory = ServiceLocator.Resolve<InventorySystem>();

            // Initialize all disciplines to level 1 / 0 XP
            foreach (CraftingDiscipline discipline in System.Enum.GetValues(typeof(CraftingDiscipline)))
            {
                _disciplineLevels[discipline] = 1;
                _disciplineXP[discipline] = 0;
            }

            Debug.Log("[CraftingSystem] Initialized.");
        }

        public void Dispose()
        {
            _disciplineLevels.Clear();
            _disciplineXP.Clear();
        }

        /// <summary>
        /// Returns the player level required to unlock a discipline.
        /// </summary>
        public static int GetDisciplineUnlockLevel(CraftingDiscipline discipline)
        {
            return DisciplineUnlockLevels.TryGetValue(discipline, out int level) ? level : 1;
        }

        /// <summary>
        /// Returns true if the discipline is unlocked at the given player level.
        /// </summary>
        public static bool IsDisciplineUnlocked(CraftingDiscipline discipline, int playerLevel)
        {
            return playerLevel >= GetDisciplineUnlockLevel(discipline);
        }

        /// <summary>
        /// Original CanCraft — no player-level lock check (backward compat).
        /// </summary>
        public bool CanCraft(RecipeDefinitionSO recipe)
        {
            return CanCraft(recipe, int.MaxValue);
        }

        /// <summary>
        /// CanCraft with player-level discipline unlock check.
        /// </summary>
        public bool CanCraft(RecipeDefinitionSO recipe, int playerLevel)
        {
            if (recipe == null)
                return false;

            // Check discipline unlock
            if (!IsDisciplineUnlocked(recipe.Discipline, playerLevel))
                return false;

            // Check skill level requirement
            if (GetDisciplineLevel(recipe.Discipline) < recipe.RequiredSkillLevel)
                return false;

            // Check ingredient availability
            foreach (var ingredient in recipe.Ingredients)
            {
                if (!_inventory.HasItem(ingredient.ItemId, ingredient.Quantity))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Original Craft — backward compat, no timing minigame.
        /// </summary>
        public bool Craft(RecipeDefinitionSO recipe)
        {
            int quality = Craft(recipe, int.MaxValue, 0f, 0.5f);
            return quality >= 0;
        }

        /// <summary>
        /// Craft with player level, harvest stat, and timing bonus.
        /// Returns quality (1-5) on success, -1 on failure.
        /// </summary>
        public int Craft(RecipeDefinitionSO recipe, int playerLevel, float harvestStat, float timingBonus)
        {
            if (!CanCraft(recipe, playerLevel))
                return -1;

            // Remove ingredients
            foreach (var ingredient in recipe.Ingredients)
            {
                _inventory.RemoveItem(ingredient.ItemId, ingredient.Quantity);
            }

            // Calculate quality
            int disciplineLevel = GetDisciplineLevel(recipe.Discipline);
            int quality = QualityCalculator.CalculateQuality(harvestStat, disciplineLevel, timingBonus);

            // Produce output
            _inventory.AddItem(recipe.OutputItemId, recipe.OutputQuantity);

            // Grant crafting XP
            AddDisciplineXP(recipe.Discipline, recipe.RequiredSkillLevel * 10);

            Debug.Log($"[CraftingSystem] Crafted {recipe.RecipeName} — {QualityCalculator.QualityName(quality)} {QualityCalculator.QualityStars(quality)}");
            return quality;
        }

        public int GetDisciplineLevel(CraftingDiscipline discipline)
        {
            return _disciplineLevels.TryGetValue(discipline, out int level) ? level : 1;
        }

        /// <summary>
        /// Directly sets a discipline level (for cheat menu).
        /// </summary>
        public void SetDisciplineLevel(CraftingDiscipline discipline, int level)
        {
            _disciplineLevels[discipline] = Mathf.Clamp(level, 1, 100);
        }

        public void AddDisciplineXP(CraftingDiscipline discipline, int xp)
        {
            if (xp <= 0)
                return;

            if (!_disciplineXP.ContainsKey(discipline))
                _disciplineXP[discipline] = 0;

            _disciplineXP[discipline] += xp;

            // Simple level-up check: every 100 XP per level
            int currentLevel = GetDisciplineLevel(discipline);
            int xpRequired = currentLevel * 100;

            while (_disciplineXP[discipline] >= xpRequired && currentLevel < 100)
            {
                _disciplineXP[discipline] -= xpRequired;
                currentLevel++;
                _disciplineLevels[discipline] = currentLevel;
                xpRequired = currentLevel * 100;

                Debug.Log($"[CraftingSystem] {discipline} leveled up to {currentLevel}!");
            }
        }
    }
}
