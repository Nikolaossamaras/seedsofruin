using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.Systems.Shop
{
    [Serializable]
    public class ShopItem
    {
        public string ItemId;
        public int Price;
        [Tooltip("Set to -1 for unlimited stock.")]
        public int Stock = -1;
    }

    [CreateAssetMenu(menuName = "SoR/Systems/Shop")]
    public class ShopInventorySO : ScriptableObject
    {
        [Header("Identity")]
        public string ShopName;
        public string ShopId;

        [Header("Inventory")]
        public List<ShopItem> Items = new();
    }
}
