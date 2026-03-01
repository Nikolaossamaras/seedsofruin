using System;
using System.Collections.Generic;
using UnityEngine;
using SoR.Shared;

namespace SoR.Progression
{
    [Serializable]
    public class EvolutionNode
    {
        public string NodeId;
        public string Name;
        public int RequiredLevel;
        public StatType PrimaryStat;
        public float StatThreshold;
        public string[] ChildNodeIds;
    }

    [CreateAssetMenu(menuName = "SoR/Progression/ClassEvolutionTree")]
    public class ClassEvolutionTreeSO : ScriptableObject
    {
        public string TreeName;
        public List<EvolutionNode> Nodes = new();
    }
}
