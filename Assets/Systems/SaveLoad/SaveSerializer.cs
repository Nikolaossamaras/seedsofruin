using System.IO;
using UnityEngine;

namespace SoR.Systems.SaveLoad
{
    public static class SaveSerializer
    {
        /// <summary>
        /// Serializes a SaveData object to a JSON string.
        /// </summary>
        public static string Serialize(SaveData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// Deserializes a JSON string into a SaveData object.
        /// </summary>
        public static SaveData Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[SaveSerializer] Attempted to deserialize null/empty JSON.");
                return null;
            }

            return JsonUtility.FromJson<SaveData>(json);
        }

        /// <summary>
        /// Serializes SaveData and writes it to the specified file path.
        /// </summary>
        public static void SaveToFile(SaveData data, string filePath)
        {
            string json = Serialize(data);
            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(filePath, json);
            Debug.Log($"[SaveSerializer] Saved to: {filePath}");
        }

        /// <summary>
        /// Reads a save file and deserializes it into a SaveData object.
        /// </summary>
        public static SaveData LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[SaveSerializer] Save file not found: {filePath}");
                return null;
            }

            string json = File.ReadAllText(filePath);
            return Deserialize(json);
        }
    }
}
