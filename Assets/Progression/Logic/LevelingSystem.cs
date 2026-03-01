using UnityEngine;
using SoR.Core;
using SoR.Progression.Events;

namespace SoR.Progression
{
    public class LevelingSystem : IService
    {
        private XPCurveSO _xpCurve;

        public XPCurveSO XPCurve
        {
            get => _xpCurve;
            set => _xpCurve = value;
        }

        public void Initialize() { }
        public void Dispose() { }

        /// <summary>
        /// Adds XP and handles level-up logic. Modifies currentXP and currentLevel by reference.
        /// </summary>
        /// <param name="amount">Amount of XP to add.</param>
        /// <param name="currentXP">The player's current XP (modified in place).</param>
        /// <param name="currentLevel">The player's current level (modified in place).</param>
        public void AddXP(int amount, ref int currentXP, ref int currentLevel)
        {
            if (_xpCurve == null)
            {
                Debug.LogWarning("[Leveling] XPCurve is not set.");
                return;
            }

            currentXP += amount;

            while (currentLevel < _xpCurve.MaxLevel)
            {
                int xpForNext = _xpCurve.GetXPForLevel(currentLevel + 1);
                if (currentXP >= xpForNext)
                {
                    currentLevel++;
                    int statPointsGained = 3; // Base stat points per level
                    EventBus.Raise(new LevelUpEvent(currentLevel, statPointsGained));
                    Debug.Log($"[Leveling] Level up! New level: {currentLevel}");
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Returns the total XP required to reach the next level from the current level.
        /// </summary>
        public int GetXPForNextLevel(int currentLevel)
        {
            if (_xpCurve == null) return 0;
            if (currentLevel >= _xpCurve.MaxLevel) return 0;
            return _xpCurve.GetXPForLevel(currentLevel + 1);
        }

        /// <summary>
        /// Returns a 0-1 float representing progress toward the next level.
        /// </summary>
        public float GetLevelProgress(int currentXP, int currentLevel)
        {
            if (_xpCurve == null) return 0f;
            if (currentLevel >= _xpCurve.MaxLevel) return 1f;

            int currentLevelXP = _xpCurve.GetXPForLevel(currentLevel);
            int nextLevelXP = _xpCurve.GetXPForLevel(currentLevel + 1);
            int xpRange = nextLevelXP - currentLevelXP;

            if (xpRange <= 0) return 1f;

            return Mathf.Clamp01((float)(currentXP - currentLevelXP) / xpRange);
        }
    }
}
