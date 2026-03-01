using UnityEngine;

namespace SoR.Gameplay
{
    public class FootstepHandler : MonoBehaviour
    {
        [SerializeField] private float _raycastDistance = 1.5f;
        [SerializeField] private LayerMask _groundLayerMask = ~0;

        /// <summary>
        /// Raycasts downward to determine the ground material and
        /// plays the appropriate footstep SFX.
        /// </summary>
        public void PlayFootstep()
        {
            string material = DetectGroundMaterial();
            // Placeholder: log the footstep until the audio system is integrated.
            Debug.Log($"[FootstepHandler] Footstep on material: {material}");
        }

        private string DetectGroundMaterial()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _raycastDistance, _groundLayerMask))
            {
                Renderer renderer = hit.collider.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial.name;
                }

                return hit.collider.tag;
            }

            return "Default";
        }
    }
}
