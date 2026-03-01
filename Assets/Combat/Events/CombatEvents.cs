using SoR.Core;
using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public readonly struct DamageDealtEvent : IGameEvent
    {
        public readonly GameObject Source;
        public readonly GameObject Target;
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly Element Element;
        public readonly bool IsCrit;
        public readonly Vector3 HitPoint;

        public DamageDealtEvent(
            GameObject source,
            GameObject target,
            float amount,
            DamageType type,
            Element element,
            bool isCrit,
            Vector3 hitPoint)
        {
            Source = source;
            Target = target;
            Amount = amount;
            Type = type;
            Element = element;
            IsCrit = isCrit;
            HitPoint = hitPoint;
        }
    }

    public readonly struct EnemyKilledEvent : IGameEvent
    {
        public readonly GameObject Enemy;
        public readonly Vector3 Position;
        public readonly string EnemyDefinitionId;

        public EnemyKilledEvent(GameObject enemy, Vector3 position, string enemyDefinitionId)
        {
            Enemy = enemy;
            Position = position;
            EnemyDefinitionId = enemyDefinitionId;
        }
    }

    public readonly struct StaggerBrokenEvent : IGameEvent
    {
        public readonly GameObject Target;
        public readonly float VulnerabilityDuration;

        public StaggerBrokenEvent(GameObject target, float vulnerabilityDuration)
        {
            Target = target;
            VulnerabilityDuration = vulnerabilityDuration;
        }
    }

    public readonly struct SynergyTriggeredEvent : IGameEvent
    {
        public readonly ElementalSynergySO Synergy;
        public readonly Vector3 Position;

        public SynergyTriggeredEvent(ElementalSynergySO synergy, Vector3 position)
        {
            Synergy = synergy;
            Position = position;
        }
    }
}
