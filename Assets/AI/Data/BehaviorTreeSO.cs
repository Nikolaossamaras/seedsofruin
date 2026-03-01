using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.AI
{
    [Serializable]
    public class BTNodeData
    {
        public string NodeName;
        public string NodeType;
        public int ParentIndex = -1;
        public List<string> Parameters = new();
    }

    [CreateAssetMenu(menuName = "SoR/AI/BehaviorTree")]
    public class BehaviorTreeSO : ScriptableObject
    {
        public string TreeName;
        public List<BTNodeData> Nodes = new();
    }
}
