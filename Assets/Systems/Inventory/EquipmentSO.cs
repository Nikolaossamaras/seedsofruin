using SoR.Shared;
using UnityEngine;

namespace SoR.Systems.Inventory
{
    public enum EquipmentSlot
    {
        Weapon,
        Head,
        Chest,
        Legs,
        Accessory
    }

    [CreateAssetMenu(menuName = "SoR/Systems/Equipment")]
    public class EquipmentSO : ItemDefinitionSO
    {
        [Header("Equipment")]
        public EquipmentSlot Slot;
        public StatBlock StatBonuses;
        public int RequiredLevel;
    }
}
