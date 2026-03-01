using SoR.Core;

namespace SoR.Systems.Inventory
{
    public readonly struct InventoryChangedEvent : IGameEvent
    {
        public readonly string ItemId;
        public readonly int NewQuantity;
        public readonly int Delta;

        public InventoryChangedEvent(string itemId, int newQuantity, int delta)
        {
            ItemId = itemId;
            NewQuantity = newQuantity;
            Delta = delta;
        }
    }

    public readonly struct EquipmentChangedEvent : IGameEvent
    {
        public readonly EquipmentSlot Slot;
        public readonly string ItemId;

        public EquipmentChangedEvent(EquipmentSlot slot, string itemId)
        {
            Slot = slot;
            ItemId = itemId;
        }
    }

    public readonly struct ItemCollectedEvent : IGameEvent
    {
        public readonly string ItemId;
        public readonly int Quantity;

        public ItemCollectedEvent(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }
}
