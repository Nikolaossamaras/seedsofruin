using UnityEngine;

namespace SoR.Combat
{
    public class WeaponTrailRenderer : MonoBehaviour
    {
        [SerializeField] private TrailRenderer trailRenderer;

        private void Awake()
        {
            if (trailRenderer == null)
            {
                trailRenderer = GetComponent<TrailRenderer>();
            }

            if (trailRenderer != null)
            {
                trailRenderer.emitting = false;
            }
        }

        /// <summary>
        /// Enables the weapon trail. Call this from an animation event
        /// at the start of an attack swing.
        /// </summary>
        public void EnableTrail()
        {
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
                trailRenderer.emitting = true;
            }
        }

        /// <summary>
        /// Disables the weapon trail. Call this from an animation event
        /// at the end of an attack swing.
        /// </summary>
        public void DisableTrail()
        {
            if (trailRenderer != null)
            {
                trailRenderer.emitting = false;
            }
        }
    }
}
