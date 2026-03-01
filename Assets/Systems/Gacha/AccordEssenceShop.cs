using System.Collections.Generic;
using SoR.Core;
using UnityEngine;

namespace SoR.Systems.Gacha
{
    public readonly struct AccordEssenceShopPurchaseEvent : IGameEvent
    {
        public readonly string CompanionId;
        public readonly int EssenceCost;

        public AccordEssenceShopPurchaseEvent(string companionId, int essenceCost)
        {
            CompanionId = companionId;
            EssenceCost = essenceCost;
        }
    }

    public class AccordEssenceShop : IService
    {
        private readonly Dictionary<string, int> _shopItems = new();

        public void Initialize()
        {
            Debug.Log("[AccordEssenceShop] Initialized.");
        }

        public void Dispose()
        {
            _shopItems.Clear();
        }

        /// <summary>
        /// Register an item available for purchase in the Accord Essence shop.
        /// </summary>
        public void RegisterShopItem(string companionId, int essenceCost)
        {
            _shopItems[companionId] = essenceCost;
        }

        public bool CanPurchase(string itemId, int currentEssence)
        {
            if (string.IsNullOrEmpty(itemId))
                return false;

            if (!_shopItems.TryGetValue(itemId, out int cost))
                return false;

            return currentEssence >= cost;
        }

        public bool Purchase(string itemId, ref int currentEssence)
        {
            if (!CanPurchase(itemId, currentEssence))
                return false;

            int cost = _shopItems[itemId];
            currentEssence -= cost;

            EventBus.Raise(new AccordEssenceShopPurchaseEvent(itemId, cost));
            Debug.Log($"[AccordEssenceShop] Purchased companion '{itemId}' for {cost} Accord Essence.");

            return true;
        }

        public Dictionary<string, int> GetAllShopItems()
        {
            return new Dictionary<string, int>(_shopItems);
        }
    }
}
