using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public static class DamageCalculator
    {
        public static DamagePayload Calculate(
            StatBlock attackerStats,
            StatBlock defenderStats,
            WeaponDefinitionSO weapon,
            float comboMultiplier = 1f,
            bool isChargedAttack = false)
        {
            float baseDmg = weapon.BaseDamage
                * (1f + attackerStats.Strength * 0.02f)
                * comboMultiplier;

            if (isChargedAttack)
                baseDmg *= weapon.ChargedMultiplier;

            float critChance = 0.05f + attackerStats.Harvest * 0.008f;
            bool isCrit = Random.value < critChance;
            if (isCrit) baseDmg *= 1.5f + attackerStats.Harvest * 0.005f;

            float defReduction = defenderStats.Vigor * 0.005f;
            float mitigation = defReduction / (1f + defReduction);
            float finalDmg = baseDmg * (1f - mitigation);

            float stagger = weapon.BaseStagger + attackerStats.Strength * 1f;

            return new DamagePayload(
                amount: Mathf.Max(1f, finalDmg),
                type: weapon.DamageType,
                element: weapon.Element,
                isCrit: isCrit,
                staggerDamage: stagger
            );
        }
    }
}
