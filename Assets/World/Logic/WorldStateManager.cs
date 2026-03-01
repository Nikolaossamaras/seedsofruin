using UnityEngine;
using SoR.Core;

namespace SoR.World
{
    public class WorldStateManager : MonoBehaviour, IService
    {
        [SerializeField] private RegionDefinitionSO _currentRegion;
        [SerializeField] private WeatherProfileSO _currentWeather;

        public RegionDefinitionSO CurrentRegion => _currentRegion;
        public WeatherProfileSO CurrentWeather => _currentWeather;
        public float WorldTime { get; private set; }

        public void Initialize()
        {
            WorldTime = 0f;
        }

        public void Dispose() { }

        private void Update()
        {
            WorldTime += Time.deltaTime;
        }

        public void SetActiveRegion(RegionDefinitionSO region)
        {
            _currentRegion = region;
            Debug.Log($"[WorldState] Active region set to: {region.RegionName}");
        }

        public void SetWeather(WeatherProfileSO weather)
        {
            _currentWeather = weather;
            Debug.Log($"[WorldState] Weather changed to: {weather.WeatherName}");
        }

        public float GetBlightLevel()
        {
            return _currentRegion != null ? _currentRegion.BlightLevel : 0f;
        }
    }
}
