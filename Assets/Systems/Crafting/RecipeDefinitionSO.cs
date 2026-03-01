using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.Systems.Crafting
{
    public enum CraftingDiscipline
    {
        Herbalism,
        Forging,
        Seedcraft,
        Runebinding
    }

    [Serializable]
    public class RecipeIngredient
    {
        public string ItemId;
        public int Quantity;
    }

    [CreateAssetMenu(menuName = "SoR/Systems/Recipe")]
    public class RecipeDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string RecipeName;
        public string RecipeId;

        [Header("Discipline")]
        public CraftingDiscipline Discipline;

        [Header("Ingredients")]
        public List<RecipeIngredient> Ingredients = new();

        [Header("Output")]
        public string OutputItemId;
        public int OutputQuantity = 1;

        [Header("Requirements")]
        public int RequiredSkillLevel;
        public float BaseCraftTime;
    }
}
