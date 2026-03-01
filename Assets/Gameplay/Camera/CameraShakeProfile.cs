using UnityEngine;

namespace SoR.Gameplay
{
    [CreateAssetMenu(fileName = "New CameraShakeProfile", menuName = "SoR/Gameplay/CameraShakeProfile")]
    public class CameraShakeProfile : ScriptableObject
    {
        [Header("Shake Parameters")]
        public float Intensity = 0.3f;
        public float Duration = 0.2f;
        public float Frequency = 25f;

        [Header("Falloff")]
        public AnimationCurve FalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }
}
