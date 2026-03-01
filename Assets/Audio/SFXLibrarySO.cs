using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.Audio
{
    [Serializable]
    public class SFXEntry
    {
        public string Id;
        public AudioClip Clip;

        [Range(0f, 1f)]
        public float Volume = 1f;

        [Range(0f, 0.5f)]
        public float PitchVariation;
    }

    [CreateAssetMenu(menuName = "SoR/Audio/SFXLibrary")]
    public class SFXLibrarySO : ScriptableObject
    {
        [SerializeField] private List<SFXEntry> _entries = new();

        public List<SFXEntry> Entries => _entries;

        /// <summary>
        /// Returns the AudioClip associated with the given ID, or null if not found.
        /// </summary>
        public AudioClip GetClip(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var entry in _entries)
            {
                if (entry.Id == id)
                    return entry.Clip;
            }

            Debug.LogWarning($"[SFXLibrary] Clip not found for ID: {id}");
            return null;
        }
    }
}
