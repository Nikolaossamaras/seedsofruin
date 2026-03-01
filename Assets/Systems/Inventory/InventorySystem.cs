using System.Collections.Generic;
using SoR.Core;
using UnityEngine;

namespace SoR.Systems.Inventory
{
    public class InventorySystem : IService
    {
        private readonly Dictionary<string, int> _items = new();

        public void Initialize()
        {
            Debug.Log("[InventorySystem] Initialized.");
        }

        public void Dispose()
        {
            _items.Clear();
        }

        public void AddItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return;

            if (_items.ContainsKey(itemId))
                _items[itemId] += quantity;
            else
                _items[itemId] = quantity;

            int newQuantity = _items[itemId];
            EventBus.Raise(new InventoryChangedEvent(itemId, newQuantity, quantity));
            EventBus.Raise(new ItemCollectedEvent(itemId, quantity));
        }

        public bool RemoveItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return false;

            if (!_items.TryGetValue(itemId, out int current) || current < quantity)
                return false;

            _items[itemId] = current - quantity;
            int newQuantity = _items[itemId];

            if (newQuantity <= 0)
                _items.Remove(itemId);

            EventBus.Raise(new InventoryChangedEvent(itemId, newQuantity, -quantity));
            return true;
        }

        public bool HasItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return false;

            return _items.TryGetValue(itemId, out int current) && current >= quantity;
        }

        public int GetItemCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return 0;

            return _items.TryGetValue(itemId, out int count) ? count : 0;
        }

        public Dictionary<string, int> GetAllItems()
        {
            return new Dictionary<string, int>(_items);
        }
    }
}
