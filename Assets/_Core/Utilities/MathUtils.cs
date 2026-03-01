using UnityEngine;

namespace SoR.Core
{
    public static class MathUtils
    {
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = (value - fromMin) / (fromMax - fromMin);
            return Mathf.Lerp(toMin, toMax, t);
        }

        public static float DamageReduction(float defense)
        {
            return defense / (1f + defense);
        }

        public static bool RollChance(float chance)
        {
            return Random.value < chance;
        }
    }
}
