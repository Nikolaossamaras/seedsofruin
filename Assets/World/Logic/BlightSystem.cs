using System.Collections.Generic;
using UnityEngine;

namespace SoR.World
{
    public class BlightSystem : MonoBehaviour
    {
        [SerializeField] private float _tickInterval = 60f;

        private Dictionary<string, float> _regionBlightLevels = new();
        private float _tickTimer;

        public IReadOnlyDictionary<string, float> RegionBlightLevels => _regionBlightLevels;

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= _tickInterval)
            {
                _tickTimer = 0f;
                Tick();
            }
        }

        public void IncreaseBlightForRegion(string regionId, float amount)
        {
            if (!_regionBlightLevels.ContainsKey(regionId))
                _regionBlightLevels[regionId] = 0f;

            _regionBlightLevels[regionId] = Mathf.Clamp01(_regionBlightLevels[regionId] + amount);
            Debug.Log($"[Blight] Region '{regionId}' blight increased to {_regionBlightLevels[regionId]:F2}");
        }

        public void DecreaseBlightForRegion(string regionId, float amount)
        {
            if (!_regionBlightLevels.ContainsKey(regionId))
                _regionBlightLevels[regionId] = 0f;

            _regionBlightLevels[regionId] = Mathf.Clamp01(_regionBlightLevels[regionId] - amount);
            Debug.Log($"[Blight] Region '{regionId}' blight decreased to {_regionBlightLevels[regionId]:F2}");
        }

        public float GetBlightLevel(string regionId)
        {
            return _regionBlightLevels.TryGetValue(regionId, out float level) ? level : 0f;
        }

        /// <summary>
        /// Called on each tick interval. Blight affects enemy spawns, weather, and available resources.
        /// Higher blight naturally spreads to connected regions over time.
        /// </summary>
        private void Tick()
        {
            var snapshot = new Dictionary<string, float>(_regionBlightLevels);

            foreach (var kvp in snapshot)
            {
                // Blight slowly increases on its own if above a threshold
                if (kvp.Value > 0.5f)
                {
                    float naturalSpread = 0.001f * kvp.Value;
                    _regionBlightLevels[kvp.Key] = Mathf.Clamp01(kvp.Value + naturalSpread);
                }
            }
        }
    }
}
