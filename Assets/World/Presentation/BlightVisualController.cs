using UnityEngine;

namespace SoR.World
{
    public class BlightVisualController : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _groundRenderer;
        [SerializeField] private Material _blightedMaterial;
        [SerializeField] private Material _healthyMaterial;

        public MeshRenderer GroundRenderer => _groundRenderer;
        public Material BlightedMaterial => _blightedMaterial;
        public Material HealthyMaterial => _healthyMaterial;

        /// <summary>
        /// Lerps between the healthy and blighted materials based on the blight level.
        /// </summary>
        /// <param name="blightLevel">Blight level from 0 (healthy) to 1 (fully blighted).</param>
        public void UpdateBlightVisuals(float blightLevel)
        {
            if (_groundRenderer == null || _healthyMaterial == null || _blightedMaterial == null)
                return;

            blightLevel = Mathf.Clamp01(blightLevel);

            _groundRenderer.material.Lerp(_healthyMaterial, _blightedMaterial, blightLevel);
        }
    }
}
