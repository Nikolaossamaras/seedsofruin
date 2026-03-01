using UnityEngine;
using SoR.Shared;

namespace SoR.World
{
    [CreateAssetMenu(menuName = "SoR/World/HarvestPoint")]
    public class HarvestPointSO : ScriptableObject
    {
        public string PointName;
        public string PointId;
        public string[] PossibleItemIds;
        public float RespawnTime;
        public int RequiredHarvestLevel;
        public Element Element;
    }
}
