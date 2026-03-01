using System;
using System.Collections.Generic;
using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    /// <summary>
    /// Event raised when a companion's bond level increases.
    /// </summary>
    public readonly struct CompanionBondLevelUpEvent : IGameEvent
    {
        public readonly string CompanionId;
        public readonly int NewBondLevel;

        public CompanionBondLevelUpEvent(string companionId, int newBondLevel)
        {
            CompanionId = companionId;
            NewBondLevel = newBondLevel;
        }
    }

    /// <summary>
    /// Manages companion bond levels. Bond XP is earned through gameplay
    /// interactions and higher bond levels unlock companion quests.
    /// </summary>
    public class CompanionBondSystem : MonoBehaviour
    {
        [SerializeField] private int[] _xpPerLevel = { 0, 100, 300, 600, 1000, 1500, 2200, 3000, 4000, 5500 };

        private CompanionManager _companionManager;

        private void Start()
        {
            if (ServiceLocator.TryResolve(out CompanionManager manager))
            {
                _companionManager = manager;
            }
        }

        /// <summary>
        /// Adds bond XP to the specified companion and checks for level-ups.
        /// </summary>
        public void AddBondXP(string companionId, int xp)
        {
            CompanionRuntimeData data = FindCompanionData(companionId);
            if (data == null)
            {
                Debug.LogWarning($"[CompanionBondSystem] Companion {companionId} not found.");
                return;
            }

            data.BondXP += xp;

            // Check for level-ups.
            while (data.BondLevel < _xpPerLevel.Length - 1
                   && data.BondXP >= _xpPerLevel[data.BondLevel + 1])
            {
                data.BondXP -= _xpPerLevel[data.BondLevel + 1];
                data.BondLevel++;

                Debug.Log($"[CompanionBondSystem] {companionId} bond level up to {data.BondLevel}!");
                EventBus.Raise(new CompanionBondLevelUpEvent(companionId, data.BondLevel));
            }
        }

        /// <summary>
        /// Returns the current bond level for the specified companion.
        /// </summary>
        public int GetBondLevel(string companionId)
        {
            CompanionRuntimeData data = FindCompanionData(companionId);
            return data?.BondLevel ?? 0;
        }

        private CompanionRuntimeData FindCompanionData(string companionId)
        {
            if (_companionManager == null)
                return null;

            IReadOnlyList<CompanionRuntimeData> owned = _companionManager.OwnedCompanions;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].CompanionId == companionId)
                    return owned[i];
            }

            return null;
        }
    }
}
