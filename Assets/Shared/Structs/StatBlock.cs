using System;

namespace SoR.Shared
{
    [Serializable]
    public struct StatBlock
    {
        public float Vigor;
        public float Strength;
        public float Harvest;
        public float Verdance;
        public float Agility;
        public float Resilience;

        public float GetStat(StatType type) => type switch
        {
            StatType.Vigor => Vigor,
            StatType.Strength => Strength,
            StatType.Harvest => Harvest,
            StatType.Verdance => Verdance,
            StatType.Agility => Agility,
            StatType.Resilience => Resilience,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        public void SetStat(StatType type, float value)
        {
            switch (type)
            {
                case StatType.Vigor:      Vigor = value;      break;
                case StatType.Strength:   Strength = value;   break;
                case StatType.Harvest:    Harvest = value;    break;
                case StatType.Verdance:   Verdance = value;   break;
                case StatType.Agility:    Agility = value;    break;
                case StatType.Resilience: Resilience = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static StatBlock operator +(StatBlock a, StatBlock b)
        {
            return new StatBlock
            {
                Vigor = a.Vigor + b.Vigor,
                Strength = a.Strength + b.Strength,
                Harvest = a.Harvest + b.Harvest,
                Verdance = a.Verdance + b.Verdance,
                Agility = a.Agility + b.Agility,
                Resilience = a.Resilience + b.Resilience
            };
        }
    }
}
