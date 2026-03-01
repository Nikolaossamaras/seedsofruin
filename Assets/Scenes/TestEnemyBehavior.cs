using System;
using UnityEngine;
using SoR.AI;

namespace SoR.Testing
{
    /// <summary>
    /// Lightweight enemy behaviour that replaces the full BehaviorTree system.
    /// Chases the player when in range, attacks when close, respawns after death.
    /// </summary>
    public class TestEnemyBehavior : MonoBehaviour
    {
        [Header("Behavior")]
        public float DetectRange = 12f;
        public float AttackRange = 2.5f;
        public float MoveSpeed = 3f;
        public float AttackCooldown = 1.5f;
        public float RespawnDelay = 3f;

        [Header("Attack")]
        public float AttackDamage = 15f;

        /// <summary>Fired when the enemy lands a hit. Passes damage amount.</summary>
        public Action<float> OnAttackHit;

        private EnemyAIController _ai;
        private Transform _target;
        private float _attackTimer;
        private Vector3 _spawnPosition;
        private bool _waitingRespawn;
        private float _respawnTimer;

        private void Awake()
        {
            _ai = GetComponent<EnemyAIController>();
            _spawnPosition = transform.position;
        }

        public void SetTarget(Transform target) => _target = target;

        private void Update()
        {
            if (_ai == null) return;

            // Handle respawn
            if (!_ai.IsAlive)
            {
                if (!_waitingRespawn)
                {
                    _waitingRespawn = true;
                    _respawnTimer = RespawnDelay;
                    SetVisible(false);
                }

                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f)
                    Respawn();

                return;
            }

            if (_target == null) return;

            float dist = Vector3.Distance(transform.position, _target.position);
            _attackTimer -= Time.deltaTime;

            // Chase when within detection range but outside attack range
            if (dist <= DetectRange && dist > AttackRange)
            {
                Vector3 dir = (_target.position - transform.position);
                dir.y = 0f;
                dir.Normalize();
                transform.position += dir * MoveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(dir);
            }

            // Attack when in range and cooldown elapsed
            if (dist <= AttackRange && _attackTimer <= 0f)
            {
                _attackTimer = AttackCooldown;
                OnAttackHit?.Invoke(AttackDamage);
            }
        }

        private void Respawn()
        {
            _waitingRespawn = false;
            transform.position = _spawnPosition;
            _ai.ResetHealth();
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                r.enabled = visible;

            var canvases = GetComponentsInChildren<Canvas>();
            foreach (var c in canvases)
                c.enabled = visible;
        }
    }
}
