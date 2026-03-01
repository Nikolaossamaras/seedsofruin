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

        public bool CanCraft(RecipeDefinitionSO recipe)
        {
            if (recipe == null)
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

        public bool Craft(RecipeDefinitionSO recipe)
        {
            if (!CanCraft(recipe))
                return false;

            // Remove ingredients
            foreach (var ingredient in recipe.Ingredients)
            {
                _inventory.RemoveItem(ingredient.ItemId, ingredient.Quantity);
            }

            // Calculate quality based on average ingredient quality and discipline level
            int disciplineLevel = GetDisciplineLevel(recipe.Discipline);
            int quality = QualityCalculator.CalculateQuality(0f, disciplineLevel, 3);

            // Produce output
            _inventory.AddItem(recipe.OutputItemId, recipe.OutputQuantity);

            // Grant crafting XP
            AddDisciplineXP(recipe.Discipline, recipe.RequiredSkillLevel * 10);

            Debug.Log($"[CraftingSystem] Crafted {recipe.RecipeName} (Quality: {quality} stars).");
            return true;
        }

        public int GetDisciplineLevel(CraftingDiscipline discipline)
        {
            return _disciplineLevels.TryGetValue(discipline, out int level) ? level : 1;
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
