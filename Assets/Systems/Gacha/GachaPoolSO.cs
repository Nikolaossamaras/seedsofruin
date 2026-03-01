using System.Collections.Generic;
using SoR.Shared;
using UnityEngine;

namespace SoR.Systems.Gacha
{
    [CreateAssetMenu(menuName = "SoR/Systems/Gacha/Pool")]
    public class GachaPoolSO : ScriptableObject
    {
        public string PoolName;
        public List<string> CompanionIds = new();
        public Rarity PoolRarity;
    }
}
