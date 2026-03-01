using System.Collections.Generic;
using UnityEngine;

namespace SoR.AI
{
    public class AggroSystem : MonoBehaviour
    {
        private readonly Dictionary<GameObject, float> _threatTable = new();

        public IReadOnlyDictionary<GameObject, float> ThreatTable => _threatTable;

        public void AddThreat(GameObject source, float amount)
        {
            if (source == null) return;

            if (_threatTable.ContainsKey(source))
                _threatTable[source] += amount;
            else
                _threatTable[source] = amount;
        }

        public GameObject GetHighestThreat()
        {
            GameObject highestTarget = null;
            float highestThreat = float.MinValue;

            foreach (var kvp in _threatTable)
            {
                // Skip destroyed or null targets
                if (kvp.Key == null) continue;

                if (kvp.Value > highestThreat)
                {
                    highestThreat = kvp.Value;
                    highestTarget = kvp.Key;
                }
            }

            return highestTarget;
        }

        public void RemoveTarget(GameObject target)
        {
            if (target == null) return;
            _threatTable.Remove(target);
        }

        public void Reset()
        {
            _threatTable.Clear();
        }
    }
}
