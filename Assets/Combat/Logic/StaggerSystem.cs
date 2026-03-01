using System.Collections.Generic;
using SoR.Core;
using UnityEngine;

namespace SoR.Combat
{
    public class StaggerSystem : MonoBehaviour
    {
        private const float VulnerabilityDuration = 3f;

        private readonly Dictionary<GameObject, float> _staggerValues = new();

        /// <summary>
        /// Applies stagger damage to a target. When the accumulated stagger meets or
        /// exceeds maxStagger, a StaggerBrokenEvent is raised and stagger is reset.
        /// </summary>
        public void ApplyStagger(GameObject target, float staggerDamage, float maxStagger)
        {
            if (target == null) return;

            if (!_staggerValues.ContainsKey(target))
            {
                _staggerValues[target] = 0f;
            }

            _staggerValues[target] += staggerDamage;

            if (_staggerValues[target] >= maxStagger)
            {
                EventBus.Raise(new StaggerBrokenEvent(target, VulnerabilityDuration));
                ResetStagger(target);
            }
        }

        /// <summary>
        /// Resets the stagger accumulation for a target back to zero.
        /// </summary>
        public void ResetStagger(GameObject target)
        {
            if (target != null && _staggerValues.ContainsKey(target))
            {
                _staggerValues[target] = 0f;
            }
        }

        /// <summary>
        /// Gets the current stagger value for a target.
        /// </summary>
        public float GetStagger(GameObject target)
        {
            if (target != null && _staggerValues.TryGetValue(target, out float value))
            {
                return value;
            }
            return 0f;
        }

        /// <summary>
        /// Removes tracking for a target entirely (e.g., when it is destroyed).
        /// </summary>
        public void RemoveTarget(GameObject target)
        {
            if (target != null)
            {
                _staggerValues.Remove(target);
            }
        }
    }
}
