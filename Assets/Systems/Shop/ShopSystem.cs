using System.Collections.Generic;
using SoR.Core;
using SoR.Systems.Inventory;
using UnityEngine;

namespace SoR.Systems.Shop
{
    public readonly struct ItemPurchasedEvent : IGameEvent
    {
        public readonly string ShopId;
        public readonly string ItemId;
        public readonly int Price;

        public ItemPurchasedEvent(string shopId, string itemId, int price)
        {
            ShopId = shopId;
            ItemId = itemId;
            Price = price;
        }
    }

    public readonly struct ItemSoldEvent : IGameEvent
    {
        public readonly string ItemId;
        public readonly int Quantity;
        public readonly int GoldGained;

        public ItemSoldEvent(string itemId, int quantity, int goldGained)
        {
            ItemId = itemId;
            Quantity = quantity;
            GoldGained = goldGained;
        }
    }

    public class ShopSystem : IService
    {
        // Key format: "{shopId}_{itemId}" -> remaining stock
        private readonly Dictionary<string, int> _currentStock = new();
        private readonly Dictionary<string, ShopInventorySO> _shopDefinitions = new();

        private InventorySystem _inventory;

        public void Initialize()
        {
            _inventory = ServiceLocator.Resolve<InventorySystem>();
            Debug.Log("[ShopSystem] Initialized.");
        }

        public void Dispose()
        {
            _currentStock.Clear();
            _shopDefinitions.Clear();
        }

        /// <summary>
        /// Register a shop definition so the system can look up prices and stock.
        /// </summary>
        public void RegisterShop(ShopInventorySO shop)
        {
            if (shop == null)
                return;

            _shopDefinitions[shop.ShopId] = shop;

            foreach (var item in shop.Items)
            {
                string key = GetStockKey(shop.ShopId, item.ItemId);
                _currentStock[key] = item.Stock;
            }
        }

        public bool CanBuy(string shopId, string itemId, int playerGold)
        {
            if (!_shopDefinitions.TryGetValue(shopId, out var shop))
                return false;

            ShopItem shopItem = FindShopItem(shop, itemId);
            if (shopItem == null)
                return false;

            if (playerGold < shopItem.Price)
                return false;

            string key = GetStockKey(shopId, itemId);
            if (_currentStock.TryGetValue(key, out int stock))
            {
                // -1 means unlimited
                if (stock == 0)
                    return false;
            }

            return true;
        }

        public bool Buy(string shopId, string itemId, ref int playerGold)
        {
            if (!CanBuy(shopId, itemId, playerGold))
                return false;

            var shop = _shopDefinitions[shopId];
            ShopItem shopItem = FindShopItem(shop, itemId);

            playerGold -= shopItem.Price;

            // Decrease stock if limited
            string key = GetStockKey(shopId, itemId);
            if (_currentStock.TryGetValue(key, out int stock) && stock > 0)
            {
                _currentStock[key] = stock - 1;
            }

            _inventory.AddItem(itemId, 1);

            EventBus.Raise(new ItemPurchasedEvent(shopId, itemId, shopItem.Price));
            Debug.Log($"[ShopSystem] Purchased '{itemId}' from '{shopId}' for {shopItem.Price} gold.");

            return true;
        }

        public int Sell(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return 0;

            if (!_inventory.HasItem(itemId, quantity))
                return 0;

            // Look up sell price from all loaded ItemDefinitionSO assets
            var itemDef = LoadItemDefinition(itemId);
            int sellPrice = itemDef != null ? itemDef.SellPrice : 0;

            if (sellPrice <= 0)
            {
                Debug.LogWarning($"[ShopSystem] Item '{itemId}' has no sell value.");
                return 0;
            }

            _inventory.RemoveItem(itemId, quantity);
            int goldGained = sellPrice * quantity;

            EventBus.Raise(new ItemSoldEvent(itemId, quantity, goldGained));
            Debug.Log($"[ShopSystem] Sold {quantity}x '{itemId}' for {goldGained} gold.");

            return goldGained;
        }

        private ShopItem FindShopItem(ShopInventorySO shop, string itemId)
        {
            foreach (var item in shop.Items)
            {
                if (item.ItemId == itemId)
                    return item;
            }

            return null;
        }

        private string GetStockKey(string shopId, string itemId)
        {
            return $"{shopId}_{itemId}";
        }

        private Inventory.ItemDefinitionSO LoadItemDefinition(string itemId)
        {
            var allItems = Resources.LoadAll<Inventory.ItemDefinitionSO>("");
            foreach (var item in allItems)
            {
                if (item.ItemId == itemId)
                    return item;
            }

            return null;
        }
    }
}
