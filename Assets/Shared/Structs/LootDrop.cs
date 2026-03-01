using System;

namespace SoR.Shared
{
    [Serializable]
    public struct LootDrop
    {
        public string ItemId;
        public int Quantity;
        public float DropChance;

        public LootDrop(string itemId, int quantity, float dropChance)
        {
            ItemId = itemId;
            Quantity = quantity;
            DropChance = dropChance;
        }
    }
}
