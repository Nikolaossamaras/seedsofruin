using System;
using System.Collections.Generic;
using UnityEngine;
using SoR.Core;
using SoR.Shared;
using SoR.Combat;

namespace SoR.AI
{
    public struct EnemyDamagedEvent : IGameEvent
    {
        public GameObject Enemy;
        public DamagePayload Payload;
        public Vector3 HitPoint;
        public float CurrentHealth;
        public float MaxHealth;
    }

    public class EnemyAIController : MonoBehaviour, IDamageable
    {
        [SerializeField] private EnemyDefinitionSO _definition;

        private float _currentHealth;
        private float _currentStagger;
        private BTNode _behaviorTreeRoot;
        private bool _isDead;

        public EnemyDefinitionSO Definition => _definition;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _definition != null ? _definition.MaxHealth : 0f;
        public float CurrentStagger => _currentStagger;
        public float MaxStagger => _definition != null ? _definition.MaxStagger : 0f;
        public bool IsAlive => !_isDead && _currentHealth > 0f;
        public BTNode BehaviorTreeRoot => _behaviorTreeRoot;

        public Transform Target { get; set; }

        public float DistanceToTarget
        {
            get
            {
                if (Target == null) return float.MaxValue;
                return Vector3.Distance(transform.position, Target.position);
            }
        }

        private void Awake()
        {
            if (_definition != null)
            {
                _currentHealth = _definition.MaxHealth;
                _currentStagger = 0f;
                InitializeBehaviorTree();
            }
        }

        public void InitializeBehaviorTree()
        {
            if (_definition == null || _definition.BehaviorTree == null) return;

            var treeData = _definition.BehaviorTree;
            if (treeData.Nodes == null || treeData.Nodes.Count == 0) return;

            var builtNodes = new List<BTNode>();

            foreach (var nodeData in treeData.Nodes)
            {
                BTNode node = nodeData.NodeType switch
                {
                    "Selector" => new BTSelector { Name = nodeData.NodeName },
                    "Sequence" => new BTSequence { Name = nodeData.NodeName },
                    "Condition" => new BTCondition { Name = nodeData.NodeName },
                    "Action" => new BTAction { Name = nodeData.NodeName },
                    _ => null
                };

                if (node == null)
                {
                    Debug.LogWarning($"Unknown BT node type: {nodeData.NodeType}");
                    builtNodes.Add(null);
                    continue;
                }

                node.Initialize(this);
                builtNodes.Add(node);
            }

            // Wire up parent-child relationships
            for (int i = 0; i < treeData.Nodes.Count; i++)
            {
                var nodeData = treeData.Nodes[i];
                if (nodeData.ParentIndex < 0 || builtNodes[i] == null) continue;

                var parent = builtNodes[nodeData.ParentIndex];
                var child = builtNodes[i];

                if (parent is BTSelector selector)
                    selector.Children.Add(child);
                else if (parent is BTSequence sequence)
                    sequence.Children.Add(child);
            }

            // Root is the first node
            _behaviorTreeRoot = builtNodes.Count > 0 ? builtNodes[0] : null;
        }

        private void Update()
        {
            if (_isDead || _behaviorTreeRoot == null) return;

            _behaviorTreeRoot.Evaluate();
        }

        public void TakeDamage(DamagePayload payload, Vector3 hitPoint)
        {
            if (_isDead) return;

            _currentHealth -= payload.Amount;
            _currentStagger += payload.StaggerDamage;

            EventBus.Raise(new EnemyDamagedEvent
            {
                Enemy = gameObject,
                Payload = payload,
                HitPoint = hitPoint,
                CurrentHealth = _currentHealth,
                MaxHealth = MaxHealth
            });

            if (_currentHealth <= 0f)
            {
                Die(null);
            }
            else if (_currentStagger >= MaxStagger)
            {
                _currentStagger = 0f;
                // Stagger break could trigger special behavior
            }
        }

        public void Heal(float amount)
        {
            if (_isDead) return;
            _currentHealth = Mathf.Min(_currentHealth + amount, MaxHealth);
        }

        private void Die(GameObject killer)
        {
            if (_isDead) return;
            _isDead = true;

            EventBus.Raise(new EnemyKilledEvent(
                gameObject,
                transform.position,
                _definition != null ? _definition.EnemyId : string.Empty
            ));
        }

        /// <summary>
        /// Allows runtime-created enemies to wire their definition from code.
        /// </summary>
        public void SetDefinition(EnemyDefinitionSO definition)
        {
            _definition = definition;
            _currentHealth = definition != null ? definition.MaxHealth : 0f;
            _currentStagger = 0f;
            _isDead = false;
        }

        /// <summary>
        /// Resets health to max for respawning.
        /// </summary>
        public void ResetHealth()
        {
            _currentHealth = MaxHealth;
            _currentStagger = 0f;
            _isDead = false;
        }
    }
}
