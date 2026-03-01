using System;
using UnityEngine;

namespace SoR.Core
{
    public class PooledObject : MonoBehaviour
    {
        [SerializeField] private float _autoReleaseDuration = 0f;

        private float _elapsed;
        private bool _autoRelease;
        private ObjectPool _pool;

        public Action OnReturnToPool;

        public void SetPool(ObjectPool pool)
        {
            _pool = pool;
        }

        public void SetAutoRelease(float duration)
        {
            _autoReleaseDuration = duration;
            _autoRelease = duration > 0f;
            _elapsed = 0f;
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _autoRelease = _autoReleaseDuration > 0f;
        }

        private void Update()
        {
            if (!_autoRelease) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _autoReleaseDuration)
            {
                ReturnToPool();
            }
        }

        public void ReturnToPool()
        {
            OnReturnToPool?.Invoke();

            if (_pool != null)
                _pool.Release(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
