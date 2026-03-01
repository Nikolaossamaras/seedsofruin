using System.Collections.Generic;
using SoR.Combat;
using SoR.Core;
using SoR.Systems.Inventory;
using UnityEngine;

namespace SoR.Systems.Quests
{
    /// <summary>
    /// MonoBehaviour that subscribes to relevant game events (enemy kills, item collections, etc.)
    /// and forwards progress updates to the QuestManager.
    /// </summary>
    public class QuestObjectiveTracker : MonoBehaviour
    {
        private QuestManager _questManager;

        private void Awake()
        {
            _questManager = ServiceLocator.Resolve<QuestManager>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Subscribe<ItemCollectedEvent>(OnItemCollected);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventBus.Unsubscribe<ItemCollectedEvent>(OnItemCollected);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            UpdateObjectivesOfType(ObjectiveType.Kill, evt.EnemyDefinitionId);
        }

        private void OnItemCollected(ItemCollectedEvent evt)
        {
            UpdateObjectivesOfType(ObjectiveType.Collect, evt.ItemId, evt.Quantity);
        }

        /// <summary>
        /// Scans all active quests for objectives matching the given type and target,
        /// then increments their progress.
        /// </summary>
        private void UpdateObjectivesOfType(ObjectiveType type, string targetId, int amount = 1)
        {
            if (_questManager == null)
                return;

            Dictionary<string, QuestState> activeQuests = _questManager.GetActiveQuests();

            foreach (var kvp in activeQuests)
            {
                QuestState state = kvp.Value;
                if (state.IsComplete)
                    continue;

                for (int i = 0; i < state.Definition.Objectives.Count; i++)
                {
                    QuestObjectiveSO objective = state.Definition.Objectives[i];

                    if (objective.Type == type && objective.TargetId == targetId)
                    {
                        int newProgress = state.ObjectiveProgress[i] + amount;
                        _questManager.UpdateObjective(kvp.Key, i, newProgress);
                    }
                }
            }
        }

        /// <summary>
        /// Public method for external systems to report Talk/Explore objectives.
        /// </summary>
        public void ReportObjective(ObjectiveType type, string targetId, int amount = 1)
        {
            UpdateObjectivesOfType(type, targetId, amount);
        }
    }
}
