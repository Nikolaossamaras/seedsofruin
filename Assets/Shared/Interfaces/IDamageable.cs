using UnityEngine;

namespace SoR.Shared
{
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        void TakeDamage(DamagePayload payload, Vector3 hitPoint);
        void Heal(float amount);
    }
}
