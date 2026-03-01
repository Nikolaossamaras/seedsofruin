using System.Collections.Generic;
using SoR.Core;
using UnityEngine;

namespace SoR.Gameplay
{
    public class CompanionManager : MonoBehaviour, IService
    {
        [SerializeField] private float _swapCooldown = 8f;

        private List<CompanionRuntimeData> _ownedCompanions = new();
        private CompanionRuntimeData _activeCompanion;
        private CompanionRuntimeData _supportCompanion;
        private float _swapCooldownRemaining;

        public IReadOnlyList<CompanionRuntimeData> OwnedCompanions => _ownedCompanions;
        public CompanionRuntimeData ActiveCompanion => _activeCompanion;
        public CompanionRuntimeData SupportCompanion => _supportCompanion;
        public float SwapCooldownRemaining => _swapCooldownRemaining;

        public void Initialize()
        {
            ServiceLocator.Register(this);
        }

        public void Dispose()
        {
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (_swapCooldownRemaining > 0f)
            {
                _swapCooldownRemaining -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Swaps the active and support companions if the cooldown has elapsed.
        /// </summary>
        public void SwapCompanion()
        {
            if (_swapCooldownRemaining > 0f)
            {
                Debug.Log($"[CompanionManager] Swap on cooldown: {_swapCooldownRemaining:F1}s remaining.");
                return;
            }

            if (_activeCompanion == null || _supportCompanion == null)
            {
                Debug.LogWarning("[CompanionManager] Cannot swap: active or support companion is null.");
                return;
            }

            // Swap roles.
            _activeCompanion.IsActive = false;
            _activeCompanion.IsSupport = true;

            _supportCompanion.IsSupport = false;
            _supportCompanion.IsActive = true;

            (_activeCompanion, _supportCompanion) = (_supportCompanion, _activeCompanion);
            _swapCooldownRemaining = _swapCooldown;

            Debug.Log($"[CompanionManager] Swapped. Active: {_activeCompanion.CompanionId}, Support: {_supportCompanion.CompanionId}");
        }

        /// <summary>
        /// Sets the active and support companions by their IDs.
        /// </summary>
        public void SetParty(string activeId, string supportId)
        {
            _activeCompanion = FindOwned(activeId);
            _supportCompanion = FindOwned(supportId);

            if (_activeCompanion != null)
            {
                _activeCompanion.IsActive = true;
                _activeCompanion.IsSupport = false;
            }

            if (_supportCompanion != null)
            {
                _supportCompanion.IsActive = false;
                _supportCompanion.IsSupport = true;
            }
        }

        /// <summary>
        /// Unlocks a new companion and adds it to the owned list.
        /// </summary>
        public void UnlockCompanion(string companionId)
        {
            if (FindOwned(companionId) != null)
            {
                Debug.LogWarning($"[CompanionManager] Companion {companionId} is already owned.");
                return;
            }

            var data = new CompanionRuntimeData
            {
                CompanionId = companionId,
                ConstellationLevel = 0,
                BondLevel = 1,
                BondXP = 0
            };

            _ownedCompanions.Add(data);
            Debug.Log($"[CompanionManager] Unlocked companion: {companionId}");
        }

        private CompanionRuntimeData FindOwned(string companionId)
        {
            for (int i = 0; i < _ownedCompanions.Count; i++)
            {
                if (_ownedCompanions[i].CompanionId == companionId)
                    return _ownedCompanions[i];
            }

            return null;
        }
    }
}
