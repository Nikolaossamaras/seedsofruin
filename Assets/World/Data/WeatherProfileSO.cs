using UnityEngine;

namespace SoR.World
{
    [CreateAssetMenu(menuName = "SoR/World/WeatherProfile")]
    public class WeatherProfileSO : ScriptableObject
    {
        public string WeatherName;
        public float WindStrength;
        public float RainIntensity;
        public float FogDensity;
        public Color AmbientColorTint = Color.white;

        [Tooltip("Duration in seconds. 0 means permanent.")]
        public float Duration;
    }
}
