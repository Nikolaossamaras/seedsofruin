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
        public readonly string QuestName;

        public QuestCompletedEvent(string questId, string questName)
        {
            QuestId = questId;
            QuestName = questName;
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
        private readonly Dictionary<string, QuestState> _completedUncollected = new();
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
            _completedUncollected.Clear();
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
                    if (!IsQuestComplete(prereqId))
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

            state.IsComplete = true;
            _activeQuests.Remove(questId);
            _completedUncollected[questId] = state;

            EventBus.Raise(new QuestCompletedEvent(questId, state.Definition.QuestName));
            Debug.Log($"[QuestManager] Completed quest: {state.Definition.QuestName} (awaiting collection)");
        }

        public bool CollectQuestRewards(string questId)
        {
            if (!_completedUncollected.TryGetValue(questId, out var state))
                return false;

            if (state.Definition.Type == QuestType.GuildContract)
                return false;

            GrantRewards(state);
            FinalizeQuest(questId, state);
            return true;
        }

        public bool CollectGuildQuestRewards(string questId)
        {
            if (!_completedUncollected.TryGetValue(questId, out var state))
                return false;

            if (state.Definition.Type != QuestType.GuildContract)
                return false;

            GrantRewards(state);
            FinalizeQuest(questId, state);
            return true;
        }

        private void GrantRewards(QuestState state)
        {
            foreach (var reward in state.Definition.Rewards)
            {
                if (!string.IsNullOrEmpty(reward.ItemId) && reward.Quantity > 0)
                {
                    _inventory.AddItem(reward.ItemId, reward.Quantity);
                }
            }
        }

        private void FinalizeQuest(string questId, QuestState state)
        {
            _completedUncollected.Remove(questId);
            _completedQuestIds.Add(questId);
            Debug.Log($"[QuestManager] Collected rewards for quest: {state.Definition.QuestName}");
        }

        public bool IsQuestComplete(string questId)
        {
            if (_completedQuestIds.Contains(questId) || _completedUncollected.ContainsKey(questId))
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

        public Dictionary<string, QuestState> GetCompletedUncollectedQuests()
        {
            return new Dictionary<string, QuestState>(_completedUncollected);
        }

        private void CheckQuestCompletion(QuestState state)
        {
            for (int i = 0; i < state.Definition.Objectives.Count; i++)
            {
                if (state.ObjectiveProgress[i] < state.Definition.Objectives[i].RequiredCount)
                    return;
            }

            CompleteQuest(state.Definition.QuestId);
        }
    }
}
