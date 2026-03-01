using System.Collections.Generic;
using UnityEngine;
using SoR.Core;
using SoR.Combat;

namespace SoR.AI
{
    public class SpawnManager : MonoBehaviour
    {
        [SerializeField] private SpawnProfileSO _profile;
        [SerializeField] private Transform[] _spawnPoints;

        private readonly List<GameObject> _activeEnemies = new();
        private float _spawnTimer;

        public SpawnProfileSO Profile => _profile;
        public IReadOnlyList<GameObject> ActiveEnemies => _activeEnemies;

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyKilledEvent>(HandleEnemyKilled);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyKilledEvent>(HandleEnemyKilled);
        }

        private void Update()
        {
            if (_profile == null) return;

            _spawnTimer -= Time.deltaTime;

            if (_spawnTimer <= 0f && _activeEnemies.Count < _profile.MaxConcurrentEnemies)
            {
                SpawnWave();
                _spawnTimer = _profile.SpawnInterval;
            }

            // Clean up null references from destroyed enemies
            _activeEnemies.RemoveAll(e => e == null);
        }

        public void SpawnWave()
        {
            if (_profile == null || _profile.Entries.Count == 0) return;
            if (_spawnPoints == null || _spawnPoints.Length == 0) return;

            // Calculate total weight for weighted random selection
            float totalWeight = 0f;
            foreach (var entry in _profile.Entries)
                totalWeight += entry.Weight;

            foreach (var entry in _profile.Entries)
            {
                int count = Random.Range(entry.MinCount, entry.MaxCount + 1);

                for (int i = 0; i < count; i++)
                {
                    if (_activeEnemies.Count >= _profile.MaxConcurrentEnemies)
                        return;

                    // Weighted random check: skip this entry based on weight probability
                    float roll = Random.Range(0f, totalWeight);
                    if (roll > entry.Weight) continue;

                    if (entry.Enemy == null || entry.Enemy.Prefab == null) continue;

                    Transform spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
                    GameObject enemy = Instantiate(
                        entry.Enemy.Prefab,
                        spawnPoint.position,
                        spawnPoint.rotation
                    );

                    var controller = enemy.GetComponent<EnemyAIController>();
                    if (controller == null)
                        controller = enemy.AddComponent<EnemyAIController>();

                    _activeEnemies.Add(enemy);
                }
            }
        }

        public void OnEnemyKilled(GameObject enemy)
        {
            _activeEnemies.Remove(enemy);
        }

        private void HandleEnemyKilled(EnemyKilledEvent evt)
        {
            OnEnemyKilled(evt.Enemy);
        }
    }
}
