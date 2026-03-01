using SoR.Core;
using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public class CombatSystem : MonoBehaviour, IService
    {
        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        public void Initialize()
        {
            // Initialization logic if needed.
        }

        public void Dispose()
        {
            // Cleanup logic if needed.
        }

        public void ProcessAttack(
            GameObject attacker,
            GameObject target,
            WeaponDefinitionSO weapon,
            StatBlock attackerStats,
            StatBlock defenderStats,
            float comboMultiplier,
            bool isCharged)
        {
            DamagePayload payload = DamageCalculator.Calculate(
                attackerStats,
                defenderStats,
                weapon,
                comboMultiplier,
                isCharged);

            Vector3 hitPoint = target.transform.position;

            EventBus.Raise(new DamageDealtEvent(
                source: attacker,
                target: target,
                amount: payload.Amount,
                type: payload.Type,
                element: payload.Element,
                isCrit: payload.IsCrit,
                hitPoint: hitPoint));

            IDamageable damageable = target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(payload, hitPoint);
            }
        }
    }
}
