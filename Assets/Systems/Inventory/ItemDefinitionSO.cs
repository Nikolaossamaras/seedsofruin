using SoR.Shared;
using UnityEngine;

namespace SoR.Systems.Inventory
{
    [CreateAssetMenu(menuName = "SoR/Systems/ItemDefinition")]
    public class ItemDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string ItemName;
        public string ItemId;
        [TextArea(2, 4)]
        public string Description;

        [Header("Classification")]
        public Rarity Rarity;

        [Header("Stacking & Economy")]
        public int MaxStackSize = 99;
        public int BuyPrice;
        public int SellPrice;

        [Header("Visuals")]
        public Sprite Icon;

        [Header("Flags")]
        public bool IsQuestItem;
    }
}
