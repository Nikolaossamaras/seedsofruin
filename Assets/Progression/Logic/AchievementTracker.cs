using System.Collections.Generic;
using UnityEngine;
using SoR.Core;
using SoR.Progression.Events;

namespace SoR.Progression
{
    public class AchievementTracker
    {
        private readonly Dictionary<string, bool> _achievements = new();

        public IReadOnlyDictionary<string, bool> Achievements => _achievements;

        /// <summary>
        /// Unlocks an achievement by ID. Raises AchievementUnlockedEvent if newly unlocked.
        /// </summary>
        public void UnlockAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId))
                return;

            if (_achievements.TryGetValue(achievementId, out bool unlocked) && unlocked)
            {
                Debug.Log($"[Achievement] Achievement '{achievementId}' is already unlocked.");
                return;
            }

            _achievements[achievementId] = true;
            EventBus.Raise(new AchievementUnlockedEvent(achievementId));
            Debug.Log($"[Achievement] Unlocked: {achievementId}");
        }

        /// <summary>
        /// Checks whether an achievement has been unlocked.
        /// </summary>
        public bool IsUnlocked(string id)
        {
            return _achievements.TryGetValue(id, out bool unlocked) && unlocked;
        }
    }
}
