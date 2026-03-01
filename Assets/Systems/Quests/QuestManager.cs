using System;
using System.Collections.Generic;
using SoR.Core;
using SoR.Systems.Inventory;
using UnityEngine;

namespace SoR.Systems.Quests
{
    [Serializable]
    public class QuestState
    {
        public QuestDefinitionSO Definition;
        public int[] ObjectiveProgress;
        public bool IsComplete;

        public QuestState(QuestDefinitionSO definition)
        {
            Definition = definition;
            ObjectiveProgress = new int[definition.Objectives.Count];
            IsComplete = false;
        }
    }

    public readonly struct QuestCompletedEvent : IGameEvent
    {
        public readonly string QuestId;

        public QuestCompletedEvent(string questId)
        {
            QuestId = questId;
        }
    }

    public readonly struct QuestAcceptedEvent : IGameEvent
    {
        public readonly string QuestId;

        public QuestAcceptedEvent(string questId)
        {
            QuestId = questId;
        }
    }

    public readonly struct QuestAbandonedEvent : IGameEvent
    {
        public readonly string QuestId;

        public QuestAbandonedEvent(string questId)
        {
            QuestId = questId;
        }
    }

    public readonly struct QuestObjectiveUpdatedEvent : IGameEvent
    {
        public readonly string QuestId;
        public readonly int ObjectiveIndex;
        public readonly int Progress;

        public QuestObjectiveUpdatedEvent(string questId, int objectiveIndex, int progress)
        {
            QuestId = questId;
            ObjectiveIndex = objectiveIndex;
            Progress = progress;
        }
    }

    public class QuestManager : IService
    {
        private readonly Dictionary<string, QuestState> _activeQuests = new();
        private readonly HashSet<string> _completedQuestIds = new();
        private InventorySystem _inventory;

        public void Initialize()
        {
            _inventory = ServiceLocator.Resolve<InventorySystem>();
            Debug.Log("[QuestManager] Initialized.");
        }

        public void Dispose()
        {
            _activeQuests.Clear();
            _completedQuestIds.Clear();
        }

        public void AcceptQuest(QuestDefinitionSO quest)
        {
            if (quest == null || _activeQuests.ContainsKey(quest.QuestId))
                return;

            if (_completedQuestIds.Contains(quest.QuestId))
            {
                Debug.LogWarning($"[QuestManager] Quest '{quest.QuestName}' has already been completed.");
                return;
            }

            // Check prerequisites
            if (quest.PrerequisiteQuestIds != null)
            {
                foreach (string prereqId in quest.PrerequisiteQuestIds)
                {
                    if (!_completedQuestIds.Contains(prereqId))
                    {
                        Debug.LogWarning($"[QuestManager] Prerequisite quest '{prereqId}' not completed.");
                        return;
                    }
                }
            }

            var state = new QuestState(quest);
            _activeQuests[quest.QuestId] = state;

            EventBus.Raise(new QuestAcceptedEvent(quest.QuestId));
            Debug.Log($"[QuestManager] Accepted quest: {quest.QuestName}");
        }

        public void AbandonQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId) || !_activeQuests.ContainsKey(questId))
                return;

            _activeQuests.Remove(questId);
            EventBus.Raise(new QuestAbandonedEvent(questId));
            Debug.Log($"[QuestManager] Abandoned quest: {questId}");
        }

        public void UpdateObjective(string questId, int objectiveIndex, int progress)
        {
            if (!_activeQuests.TryGetValue(questId, out var state))
                return;

            if (state.IsComplete)
                return;

            if (objectiveIndex < 0 || objectiveIndex >= state.ObjectiveProgress.Length)
                return;

            state.ObjectiveProgress[objectiveIndex] = progress;
            EventBus.Raise(new QuestObjectiveUpdatedEvent(questId, objectiveIndex, progress));

            // Check if all objectives are met
            CheckQuestCompletion(state);
        }

        public void CompleteQuest(string questId)
        {
            if (!_activeQuests.TryGetValue(questId, out var state))
                return;

            if (!IsQuestComplete(questId))
            {
                Debug.LogWarning($"[QuestManager] Quest '{questId}' objectives not yet fulfilled.");
                return;
            }

            // Grant rewards
            foreach (var reward in state.Definition.Rewards)
            {
                if (!string.IsNullOrEmpty(reward.ItemId) && reward.Quantity > 0)
                {
                    _inventory.AddItem(reward.ItemId, reward.Quantity);
                }
            }

            state.IsComplete = true;
            _completedQuestIds.Add(questId);
            _activeQuests.Remove(questId);

            EventBus.Raise(new QuestCompletedEvent(questId));
            Debug.Log($"[QuestManager] Completed quest: {state.Definition.QuestName}");
        }

        public bool IsQuestComplete(string questId)
        {
            if (_completedQuestIds.Contains(questId))
                return true;

            if (!_activeQuests.TryGetValue(questId, out var state))
                return false;

            for (int i = 0; i < state.Definition.Objectives.Count; i++)
            {
                if (state.ObjectiveProgress[i] < state.Definition.Objectives[i].RequiredCount)
                    return false;
            }

            return true;
        }

        public QuestState GetQuestState(string questId)
        {
            return _activeQuests.TryGetValue(questId, out var state) ? state : null;
        }

        public Dictionary<string, QuestState> GetActiveQuests()
        {
            return new Dictionary<string, QuestState>(_activeQuests);
        }

        public HashSet<string> GetCompletedQuestIds()
        {
            return new HashSet<string>(_completedQuestIds);
        }

        private void CheckQuestCompletion(QuestState state)
        {
            for (int i = 0; i < state.Definition.Objectives.Count; i++)
            {
                if (state.ObjectiveProgress[i] < state.Definition.Objectives[i].RequiredCount)
                    return;
            }

            Debug.Log($"[QuestManager] All objectives met for quest: {state.Definition.QuestName}");
        }
    }
}
