using System.Collections.Generic;
using SoR.Core;
using SoR.Shared;
using UnityEngine;

namespace SoR.Systems.Inventory
{
    public class EquipmentSystem : IService
    {
        private readonly Dictionary<EquipmentSlot, string> _equippedItems = new();
        private readonly Dictionary<string, EquipmentSO> _equipmentDatabase = new();

        public void Initialize()
        {
            Debug.Log("[EquipmentSystem] Initialized.");

            // Preload all equipment definitions so we can look up stat bonuses
            var allEquipment = Resources.LoadAll<EquipmentSO>("");
            foreach (var eq in allEquipment)
            {
                _equipmentDatabase[eq.ItemId] = eq;
            }
        }

        public void Dispose()
        {
            _equippedItems.Clear();
            _equipmentDatabase.Clear();
        }

        public void Equip(string itemId, EquipmentSlot slot)
        {
            if (string.IsNullOrEmpty(itemId))
                return;

            // Unequip existing item in that slot first
            if (_equippedItems.ContainsKey(slot))
            {
                Unequip(slot);
            }

            _equippedItems[slot] = itemId;
            EventBus.Raise(new EquipmentChangedEvent(slot, itemId));
        }

        public void Unequip(EquipmentSlot slot)
        {
            if (_equippedItems.TryGetValue(slot, out string itemId))
            {
                _equippedItems.Remove(slot);
                EventBus.Raise(new EquipmentChangedEvent(slot, null));
            }
        }

        public string GetEquipped(EquipmentSlot slot)
        {
            return _equippedItems.TryGetValue(slot, out string itemId) ? itemId : null;
        }

        public StatBlock GetTotalStatBonuses()
        {
            StatBlock total = default;

            foreach (var kvp in _equippedItems)
            {
                if (_equipmentDatabase.TryGetValue(kvp.Value, out var equipment))
                {
                    total = total + equipment.StatBonuses;
                }
            }

            return total;
        }
    }
}
