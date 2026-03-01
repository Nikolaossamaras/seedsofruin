using System.Collections.Generic;
using SoR.Core;
using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public class ElementalSynergyResolver : MonoBehaviour
    {
        [SerializeField] private List<ElementalSynergySO> synergyDefinitions = new();

        /// <summary>
        /// Checks if the combination of an applied element and an existing element triggers
        /// a synergy. If a match is found, raises a SynergyTriggeredEvent and spawns VFX.
        /// Returns the matched synergy, or null if none was found.
        /// </summary>
        public ElementalSynergySO TryResolveSynergy(
            Element appliedElement,
            Element existingElement,
            Vector3 position)
        {
            foreach (ElementalSynergySO synergy in synergyDefinitions)
            {
                bool matchForward = synergy.ElementA == appliedElement && synergy.ElementB == existingElement;
                bool matchReverse = synergy.ElementA == existingElement && synergy.ElementB == appliedElement;

                if (matchForward || matchReverse)
                {
                    EventBus.Raise(new SynergyTriggeredEvent(synergy, position));

                    if (synergy.VFXPrefab != null)
                    {
                        Object.Instantiate(synergy.VFXPrefab, position, Quaternion.identity);
                    }

                    return synergy;
                }
            }

            return null;
        }
    }
}
