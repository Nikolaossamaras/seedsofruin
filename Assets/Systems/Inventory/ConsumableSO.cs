using UnityEngine;

namespace SoR.Systems.Inventory
{
    public enum ConsumableType
    {
        HealthRestore,
        VerdanceRestore,
        StatBuff,
        CraftingMaterial
    }

    [CreateAssetMenu(menuName = "SoR/Systems/Consumable")]
    public class ConsumableSO : ItemDefinitionSO
    {
        [Header("Consumable")]
        public ConsumableType Type;
        public float EffectValue;
        public float Duration;
    }
}
