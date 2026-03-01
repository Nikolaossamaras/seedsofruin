using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.VFX
{
    [Serializable]
    public class VFXEntry
    {
        public string Id;
        public GameObject Prefab;
        public float DefaultDuration = 2f;
    }

    [CreateAssetMenu(menuName = "SoR/VFX/VFXLibrary")]
    public class VFXLibrarySO : ScriptableObject
    {
        [SerializeField] private List<VFXEntry> _entries = new();

        public List<VFXEntry> Entries => _entries;

        /// <summary>
        /// Returns the VFX prefab associated with the given ID, or null if not found.
        /// </summary>
        public GameObject GetPrefab(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var entry in _entries)
            {
                if (entry.Id == id)
                    return entry.Prefab;
            }

            Debug.LogWarning($"[VFXLibrary] Prefab not found for ID: {id}");
            return null;
        }
    }
}
