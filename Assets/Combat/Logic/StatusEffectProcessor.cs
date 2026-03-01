using System.Collections.Generic;
using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public class StatusEffectProcessor : MonoBehaviour
    {
        private class ActiveStatusEffect
        {
            public StatusEffectSO Definition;
            public float RemainingDuration;
            public float TickTimer;
            public int CurrentStacks;

            public ActiveStatusEffect(StatusEffectSO definition)
            {
                Definition = definition;
                RemainingDuration = definition.Duration;
                TickTimer = definition.TickInterval;
                CurrentStacks = 1;
            }
        }

        private readonly Dictionary<GameObject, List<ActiveStatusEffect>> _activeEffects = new();

        /// <summary>
        /// Applies a status effect to the target. Handles stacking if the effect is stackable.
        /// </summary>
        public void ApplyEffect(GameObject target, StatusEffectSO effect)
        {
            if (target == null || effect == null) return;

            if (!_activeEffects.ContainsKey(target))
            {
                _activeEffects[target] = new List<ActiveStatusEffect>();
            }

            List<ActiveStatusEffect> effects = _activeEffects[target];

            // Check if this effect type is already applied.
            ActiveStatusEffect existing = effects.Find(e => e.Definition == effect);
            if (existing != null)
            {
                if (effect.IsStackable && existing.CurrentStacks < effect.MaxStacks)
                {
                    existing.CurrentStacks++;
                }
                // Refresh duration regardless of stacking.
                existing.RemainingDuration = effect.Duration;
                return;
            }

            effects.Add(new ActiveStatusEffect(effect));
        }

        /// <summary>
        /// Ticks all active status effects, processing DoT and removing expired effects.
        /// </summary>
        public void TickEffects(float deltaTime)
        {
            List<GameObject> toRemove = null;

            foreach (var kvp in _activeEffects)
            {
                GameObject target = kvp.Key;
                List<ActiveStatusEffect> effects = kvp.Value;

                if (target == null)
                {
                    toRemove ??= new List<GameObject>();
                    toRemove.Add(target);
                    continue;
                }

                for (int i = effects.Count - 1; i >= 0; i--)
                {
                    ActiveStatusEffect active = effects[i];
                    active.RemainingDuration -= deltaTime;

                    if (active.RemainingDuration <= 0f)
                    {
                        effects.RemoveAt(i);
                        continue;
                    }

                    // Process DoT ticks.
                    if (active.Definition.TickDamage > 0f && active.Definition.TickInterval > 0f)
                    {
                        active.TickTimer -= deltaTime;
                        if (active.TickTimer <= 0f)
                        {
                            active.TickTimer += active.Definition.TickInterval;

                            float tickDamage = active.Definition.TickDamage * active.CurrentStacks;
                            IDamageable damageable = target.GetComponent<IDamageable>();
                            if (damageable != null)
                            {
                                DamagePayload payload = new DamagePayload(
                                    amount: tickDamage,
                                    type: DamageType.Magical,
                                    element: active.Definition.AssociatedElement,
                                    isCrit: false,
                                    staggerDamage: 0f);
                                damageable.TakeDamage(payload, target.transform.position);
                            }
                        }
                    }
                }

                if (effects.Count == 0)
                {
                    toRemove ??= new List<GameObject>();
                    toRemove.Add(target);
                }
            }

            if (toRemove != null)
            {
                foreach (GameObject key in toRemove)
                {
                    _activeEffects.Remove(key);
                }
            }
        }

        /// <summary>
        /// Returns all active effects for a given target, or null if none exist.
        /// </summary>
        public List<StatusEffectSO> GetActiveEffects(GameObject target)
        {
            if (target == null || !_activeEffects.TryGetValue(target, out var effects))
                return null;

            var result = new List<StatusEffectSO>(effects.Count);
            foreach (var active in effects)
            {
                result.Add(active.Definition);
            }
            return result;
        }

        /// <summary>
        /// Removes all status effects from a target.
        /// </summary>
        public void ClearEffects(GameObject target)
        {
            if (target != null)
            {
                _activeEffects.Remove(target);
            }
        }

        private void Update()
        {
            TickEffects(Time.deltaTime);
        }
    }
}
