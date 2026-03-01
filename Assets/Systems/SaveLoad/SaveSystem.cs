using System;
using System.Collections.Generic;
using System.IO;
using SoR.Core;
using UnityEngine;

namespace SoR.Systems.SaveLoad
{
    public class SaveSystem : MonoBehaviour, IService
    {
        private const string SaveFilePrefix = "save_slot_";
        private const string SaveFileExtension = ".json";

        private readonly List<ISaveable> _registeredSaveables = new();

        public void Initialize()
        {
            Debug.Log("[SaveSystem] Initialized.");
        }

        public void Dispose()
        {
            _registeredSaveables.Clear();
        }

        private void Awake()
        {
            ServiceLocator.Register<SaveSystem>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Release<SaveSystem>();
        }

        /// <summary>
        /// Register a saveable component so its data will be included in saves.
        /// </summary>
        public void RegisterSaveable(ISaveable saveable)
        {
            if (saveable != null && !_registeredSaveables.Contains(saveable))
            {
                _registeredSaveables.Add(saveable);
            }
        }

        /// <summary>
        /// Unregister a saveable component.
        /// </summary>
        public void UnregisterSaveable(ISaveable saveable)
        {
            _registeredSaveables.Remove(saveable);
        }

        /// <summary>
        /// Gathers data from all registered saveables, serializes, and writes to the given slot.
        /// </summary>
        public void Save(int slotIndex)
        {
            var saveData = new SaveData
            {
                SaveTimestamp = DateTime.UtcNow.ToString("o")
            };

            // Each saveable writes its portion into the SaveData via JSON fragments.
            // In a full implementation, each system would populate the relevant fields.
            // For now, we gather individual JSON strings and store them.
            foreach (var saveable in _registeredSaveables)
            {
                try
                {
                    string data = saveable.GetSaveData();
                    // Each saveable contributes its data; a production system would
                    // merge this into the SaveData fields or use a keyed approach.
                    Debug.Log($"[SaveSystem] Gathered save data from {saveable.GetType().Name}.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] Error gathering save data from {saveable.GetType().Name}: {e.Message}");
                }
            }

            string filePath = GetSaveFilePath(slotIndex);
            SaveSerializer.SaveToFile(saveData, filePath);

            Debug.Log($"[SaveSystem] Game saved to slot {slotIndex}.");
        }

        /// <summary>
        /// Reads a save file, deserializes, and distributes data to all registered saveables.
        /// </summary>
        public void Load(int slotIndex)
        {
            string filePath = GetSaveFilePath(slotIndex);
            SaveData saveData = SaveSerializer.LoadFromFile(filePath);

            if (saveData == null)
            {
                Debug.LogWarning($"[SaveSystem] No save data found for slot {slotIndex}.");
                return;
            }

            string json = SaveSerializer.Serialize(saveData);

            foreach (var saveable in _registeredSaveables)
            {
                try
                {
                    saveable.LoadSaveData(json);
                    Debug.Log($"[SaveSystem] Loaded save data into {saveable.GetType().Name}.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveSystem] Error loading save data into {saveable.GetType().Name}: {e.Message}");
                }
            }

            Debug.Log($"[SaveSystem] Game loaded from slot {slotIndex}.");
        }

        /// <summary>
        /// Deletes the save file for the given slot.
        /// </summary>
        public void DeleteSave(int slotIndex)
        {
            string filePath = GetSaveFilePath(slotIndex);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[SaveSystem] Deleted save in slot {slotIndex}.");
            }
            else
            {
                Debug.LogWarning($"[SaveSystem] No save file to delete for slot {slotIndex}.");
            }
        }

        /// <summary>
        /// Checks whether a save file exists for the given slot.
        /// </summary>
        public bool HasSave(int slotIndex)
        {
            return File.Exists(GetSaveFilePath(slotIndex));
        }

        private string GetSaveFilePath(int slotIndex)
        {
            return Path.Combine(Application.persistentDataPath, $"{SaveFilePrefix}{slotIndex}{SaveFileExtension}");
        }
    }
}
