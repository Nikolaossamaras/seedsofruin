using SoR.Shared;
using UnityEngine;

namespace SoR.Gameplay
{
    public class PlayerStatManager : MonoBehaviour
    {
        [SerializeField] private PlayerStatsSO _baseData;

        private PlayerRuntimeData _runtimeData;

        public PlayerRuntimeData RuntimeData => _runtimeData;

        public void Initialize(PlayerStatsSO baseData)
        {
            _baseData = baseData;

            _runtimeData = new PlayerRuntimeData();
            _runtimeData.SetBaseStats(_baseData.BaseStats);
            _runtimeData.MaxHealth = _baseData.BaseHealth;
            _runtimeData.CurrentHealth = _runtimeData.MaxHealth;
            _runtimeData.MaxVerdance = _baseData.BaseVerdance;
            _runtimeData.CurrentVerdance = _runtimeData.MaxVerdance;
        }

        public void AllocateStat(StatType stat)
        {
            if (_runtimeData.StatPoints <= 0)
            {
                Debug.LogWarning("[PlayerStatManager] No stat points available to allocate.");
                return;
            }

            float currentValue = _runtimeData.AllocatedStats.GetStat(stat);
            _runtimeData.AllocatedStats.SetStat(stat, currentValue + 1f);
            _runtimeData.StatPoints--;
        }

        public StatBlock GetTotalStats()
        {
            return _runtimeData.TotalStats;
        }

        public float GetCurrentHealth()
        {
            return _runtimeData.CurrentHealth;
        }

        public float GetCurrentVerdance()
        {
            return _runtimeData.CurrentVerdance;
        }

        public void ApplyDamage(float amount)
        {
            _runtimeData.CurrentHealth = Mathf.Max(0f, _runtimeData.CurrentHealth - amount);
        }

        public void RestoreHealth(float amount)
        {
            _runtimeData.CurrentHealth = Mathf.Min(
                _runtimeData.MaxHealth,
                _runtimeData.CurrentHealth + amount);
        }

        public void SpendVerdance(float amount)
        {
            _runtimeData.CurrentVerdance = Mathf.Max(0f, _runtimeData.CurrentVerdance - amount);
        }

        public void RestoreVerdance(float amount)
        {
            _runtimeData.CurrentVerdance = Mathf.Min(
                _runtimeData.MaxVerdance,
                _runtimeData.CurrentVerdance + amount);
        }
    }
}
