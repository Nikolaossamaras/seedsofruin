using UnityEngine;

namespace SoR.Progression
{
    [CreateAssetMenu(menuName = "SoR/Progression/XPCurve")]
    public class XPCurveSO : ScriptableObject
    {
        [SerializeField] private AnimationCurve _xpCurve = AnimationCurve.EaseInOut(1f, 100f, 40f, 100000f);
        [SerializeField] private int _maxLevel = 40;

        public AnimationCurve XPCurve => _xpCurve;
        public int MaxLevel => _maxLevel;

        /// <summary>
        /// Evaluates the XP curve to determine total XP needed to reach a given level.
        /// </summary>
        /// <param name="level">The target level.</param>
        /// <returns>Total XP required to reach the specified level.</returns>
        public int GetXPForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, _maxLevel);
            return Mathf.RoundToInt(_xpCurve.Evaluate(level));
        }
    }
}
