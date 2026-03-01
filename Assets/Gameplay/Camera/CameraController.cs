using UnityEngine;

namespace SoR.Gameplay
{
    public class CameraController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform _followTarget;
        [SerializeField] private Vector3 _offset = new(0f, 10f, -8f);
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private float _lookAheadDistance = 2f;

        [Header("Shake")]
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;

        public void SetFollowTarget(Transform target)
        {
            _followTarget = target;
        }

        public void Shake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTimer = 0f;
        }

        private void LateUpdate()
        {
            if (_followTarget == null)
                return;

            // Calculate look-ahead based on target's forward direction.
            Vector3 lookAhead = _followTarget.forward * _lookAheadDistance;
            Vector3 desiredPosition = _followTarget.position + _offset + lookAhead;

            // Smooth follow.
            Vector3 smoothedPosition = Vector3.Lerp(
                transform.position,
                desiredPosition,
                _smoothSpeed * Time.deltaTime);

            // Apply screen shake.
            if (_shakeTimer < _shakeDuration)
            {
                _shakeTimer += Time.deltaTime;
                float shakeProgress = _shakeTimer / _shakeDuration;
                float currentIntensity = _shakeIntensity * (1f - shakeProgress);
                Vector3 shakeOffset = Random.insideUnitSphere * currentIntensity;
                shakeOffset.z = 0f;
                smoothedPosition += shakeOffset;
            }

            transform.position = smoothedPosition;
            transform.LookAt(_followTarget.position);
        }
    }
}
