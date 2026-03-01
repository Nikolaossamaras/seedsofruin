using System.Collections.Generic;
using SoR.Shared;
using UnityEngine;

namespace SoR.Combat
{
    public class HitboxController : MonoBehaviour
    {
        [SerializeField] private float radius = 1f;
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private LayerMask targetLayers;

        public float Radius => radius;
        public float Duration => duration;
        public LayerMask TargetLayers => targetLayers;

        private readonly HashSet<GameObject> _alreadyHit = new();
        private float _elapsedTime;
        private bool _isActive;

        /// <summary>
        /// Activates the hitbox. Call this when an attack swing begins.
        /// </summary>
        public void Activate()
        {
            _alreadyHit.Clear();
            _elapsedTime = 0f;
            _isActive = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Deactivates the hitbox immediately.
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isActive) return;

            _elapsedTime += Time.deltaTime;
            if (_elapsedTime >= duration)
            {
                Deactivate();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive) return;

            // Check layer mask.
            if ((targetLayers & (1 << other.gameObject.layer)) == 0) return;

            // Prevent double-hitting the same target.
            if (!_alreadyHit.Add(other.gameObject)) return;

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = other.GetComponentInParent<IDamageable>();
            }

            if (damageable != null)
            {
                // The actual damage processing is handled externally via CombatSystem.
                // This controller simply detects valid targets.
                OnTargetHit(other.gameObject);
            }
        }

        /// <summary>
        /// Called when a valid IDamageable target is detected. Override or subscribe
        /// to handle the hit in a higher-level system.
        /// </summary>
        public System.Action<GameObject> OnTargetDetected;

        private void OnTargetHit(GameObject target)
        {
            OnTargetDetected?.Invoke(target);
        }

        private void OnDisable()
        {
            _isActive = false;
            _alreadyHit.Clear();
        }
    }
}
