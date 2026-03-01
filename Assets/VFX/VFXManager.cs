using UnityEngine;
using SoR.Core;

namespace SoR.VFX
{
    public class VFXManager : MonoBehaviour, IService
    {
        [SerializeField] private VFXLibrarySO _vfxLibrary;

        public void Initialize()
        {
            Debug.Log("[VFXManager] Initialized.");
        }

        public void Dispose() { }

        /// <summary>
        /// Spawns a VFX prefab at the given position and rotation.
        /// The VFX must be manually destroyed or managed externally.
        /// </summary>
        public void SpawnVFX(string vfxId, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = GetPrefab(vfxId);
            if (prefab == null) return;

            Instantiate(prefab, position, rotation);
            Debug.Log($"[VFX] Spawned '{vfxId}' at {position}");
        }

        /// <summary>
        /// Spawns a VFX prefab at the given position and rotation, auto-destroying after the specified duration.
        /// </summary>
        public void SpawnVFX(string vfxId, Vector3 position, Quaternion rotation, float duration)
        {
            GameObject prefab = GetPrefab(vfxId);
            if (prefab == null) return;

            GameObject instance = Instantiate(prefab, position, rotation);
            Destroy(instance, duration);
            Debug.Log($"[VFX] Spawned '{vfxId}' at {position} (auto-destroy in {duration}s)");
        }

        private GameObject GetPrefab(string vfxId)
        {
            if (_vfxLibrary == null)
            {
                Debug.LogWarning("[VFXManager] VFX Library is not assigned.");
                return null;
            }

            GameObject prefab = _vfxLibrary.GetPrefab(vfxId);
            if (prefab == null)
            {
                Debug.LogWarning($"[VFXManager] VFX prefab not found for ID: {vfxId}");
            }

            return prefab;
        }
    }
}
