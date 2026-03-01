using System.Collections.Generic;
using UnityEngine;
using SoR.Shared;

namespace SoR.World
{
    [CreateAssetMenu(menuName = "SoR/World/Region")]
    public class RegionDefinitionSO : ScriptableObject
    {
        public string RegionName;
        public string RegionId;

        [TextArea(2, 5)]
        public string Description;

        public string ScenePath;
        public int RecommendedLevel;
        public Element DominantElement;
        public List<string> ConnectedRegionIds = new();
        public Sprite MapIcon;

        [Range(0f, 1f)]
        public float BlightLevel;
    }
}
