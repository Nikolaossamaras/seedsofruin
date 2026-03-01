using System.Collections.Generic;
using UnityEngine;
using SoR.Core;
using SoR.Progression.Events;

namespace SoR.Progression
{
    public class TitleSystem
    {
        private string _equippedTitleId;
        private readonly List<string> _unlockedTitleIds = new();

        public string EquippedTitleId => _equippedTitleId;
        public IReadOnlyList<string> UnlockedTitleIds => _unlockedTitleIds;

        /// <summary>
        /// Unlocks a title, making it available for equipping.
        /// </summary>
        public void UnlockTitle(string titleId)
        {
            if (string.IsNullOrEmpty(titleId))
                return;

            if (_unlockedTitleIds.Contains(titleId))
            {
                Debug.Log($"[Title] Title '{titleId}' is already unlocked.");
                return;
            }

            _unlockedTitleIds.Add(titleId);
            EventBus.Raise(new TitleUnlockedEvent(titleId));
            Debug.Log($"[Title] Unlocked title: {titleId}");
        }

        /// <summary>
        /// Equips a previously unlocked title.
        /// </summary>
        public void EquipTitle(string titleId)
        {
            if (!_unlockedTitleIds.Contains(titleId))
            {
                Debug.LogWarning($"[Title] Cannot equip title '{titleId}' - not unlocked.");
                return;
            }

            _equippedTitleId = titleId;
            Debug.Log($"[Title] Equipped title: {titleId}");
        }

        /// <summary>
        /// Unequips the currently equipped title.
        /// </summary>
        public void UnequipTitle()
        {
            Debug.Log($"[Title] Unequipped title: {_equippedTitleId}");
            _equippedTitleId = null;
        }
    }
}
