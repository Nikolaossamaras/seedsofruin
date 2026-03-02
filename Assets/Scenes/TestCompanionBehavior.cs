using System;
using System.Collections.Generic;
using UnityEngine;
using SoR.AI;

namespace SoR.Testing
{
    /// <summary>
    /// Lightweight companion behaviour — mirrors TestEnemyBehavior but reversed:
    /// follows the player, attacks nearby enemies.
    /// </summary>
    public class TestCompanionBehavior : MonoBehaviour
    {
        [Header("Follow")]
        public float FollowDistance = 2.5f;
        public float TeleportDistance = 20f;
        public Vector3 FollowOffset = new Vector3(-1.5f, 0f, -1.5f);
        public float MoveSpeed = 5f;
        public float RotationSpeed = 8f;

        [Header("Combat")]
        public float DetectRange = 10f;
        public float AttackRange = 2.5f;
        public float AttackCooldown = 2f;
        public float AttackDamage = 25f;

        /// <summary>
        /// Fired when the companion lands a hit on an enemy.
        /// Passes the target EnemyAIController so TestSceneSetup can route through CombatSystem.
        /// </summary>
        public Action<EnemyAIController> OnAttackEnemy;

        private Transform _followTarget;
        private List<EnemyAIController> _enemies;
        private CompanionProceduralAnimator _animator;
        private float _attackTimer;
        private string _currentAnim = "Idle";

        public void Initialize(Transform followTarget, List<EnemyAIController> enemies, CompanionProceduralAnimator animator)
        {
            _followTarget = followTarget;
            _enemies = enemies;
            _animator = animator;
        }

        private void Update()
        {
            if (_followTarget == null) return;

            _attackTimer -= Time.deltaTime;

            // Find nearest alive enemy within detect range
            EnemyAIController nearestEnemy = null;
            float nearestDist = float.MaxValue;

            if (_enemies != null)
            {
                foreach (var enemy in _enemies)
                {
                    if (enemy == null || !enemy.IsAlive) continue;
                    float dist = Vector3.Distance(transform.position, enemy.transform.position);
                    if (dist <= DetectRange && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestEnemy = enemy;
                    }
                }
            }

            if (nearestEnemy != null)
            {
                if (nearestDist <= AttackRange)
                {
                    // Face enemy and attack
                    FaceTarget(nearestEnemy.transform.position);
                    SetAnim("Attack");

                    if (_attackTimer <= 0f)
                    {
                        _attackTimer = AttackCooldown;
                        OnAttackEnemy?.Invoke(nearestEnemy);
                    }
                }
                else
                {
                    // Move toward enemy
                    Vector3 dir = (nearestEnemy.transform.position - transform.position);
                    dir.y = 0f;
                    dir.Normalize();
                    transform.position += dir * MoveSpeed * Time.deltaTime;
                    FaceDirection(dir);
                    SetAnim("Walk");
                }
            }
            else
            {
                // Follow player
                Vector3 targetPos = _followTarget.position + _followTarget.TransformDirection(FollowOffset);
                float distToTarget = Vector3.Distance(transform.position, targetPos);

                // Teleport snap if too far from player
                float distToPlayer = Vector3.Distance(transform.position, _followTarget.position);
                if (distToPlayer > TeleportDistance)
                {
                    transform.position = targetPos;
                    SetAnim("Idle");
                    return;
                }

                if (distToTarget > 0.5f)
                {
                    Vector3 dir = (targetPos - transform.position);
                    dir.y = 0f;
                    dir.Normalize();
                    transform.position += dir * MoveSpeed * Time.deltaTime;
                    FaceDirection(dir);
                    SetAnim("Walk");
                }
                else
                {
                    SetAnim("Idle");
                }
            }
        }

        private void FaceTarget(Vector3 targetPosition)
        {
            Vector3 dir = (targetPosition - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * RotationSpeed);
            }
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    Time.deltaTime * RotationSpeed);
            }
        }

        private void SetAnim(string animName)
        {
            if (_currentAnim == animName) return;
            _currentAnim = animName;
            if (_animator != null)
                _animator.SetAnimation(animName);
        }
    }
}
