using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoR.AI
{
    [Serializable]
    public class SpawnEntry
    {
        public EnemyDefinitionSO Enemy;
        public float Weight = 1f;
        public int MinCount = 1;
        public int MaxCount = 1;
    }

    [CreateAssetMenu(menuName = "SoR/AI/SpawnProfile")]
    public class SpawnProfileSO : ScriptableObject
    {
        public string ProfileName;
        public List<SpawnEntry> Entries = new();

        [Header("Spawn Settings")]
        public float SpawnInterval = 5f;
        public int MaxConcurrentEnemies = 10;

        [Header("Scaling")]
        [Tooltip("Multiplier applied per player level")]
        public float DifficultyScaling = 1.05f;
    }
}
