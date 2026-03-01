using UnityEngine;

namespace SoR.World
{
    [RequireComponent(typeof(Collider))]
    public class InteractableHighlight : MonoBehaviour
    {
        [SerializeField] private float _highlightRadius = 3f;
        [SerializeField] private GameObject _highlightEffect;

        public float HighlightRadius => _highlightRadius;

        private void Start()
        {
            // Ensure the collider is set as a trigger with the correct radius
            var sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;
                sphereCollider.radius = _highlightRadius;
            }

            if (_highlightEffect != null)
                _highlightEffect.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                ShowHighlight();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                HideHighlight();
            }
        }

        private void ShowHighlight()
        {
            if (_highlightEffect != null)
                _highlightEffect.SetActive(true);

            Debug.Log($"[Highlight] Showing highlight on {gameObject.name}");
        }

        private void HideHighlight()
        {
            if (_highlightEffect != null)
                _highlightEffect.SetActive(false);

            Debug.Log($"[Highlight] Hiding highlight on {gameObject.name}");
        }
    }
}
