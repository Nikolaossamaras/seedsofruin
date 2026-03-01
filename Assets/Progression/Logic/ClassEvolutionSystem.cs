using System.Collections.Generic;
using UnityEngine;
using SoR.Core;
using SoR.Shared;
using SoR.Progression.Events;

namespace SoR.Progression
{
    public class ClassEvolutionSystem
    {
        private readonly List<string> _unlockedEvolutions = new();
        private string _currentEvolution;

        public IReadOnlyList<string> UnlockedEvolutions => _unlockedEvolutions;
        public string CurrentEvolution => _currentEvolution;

        /// <summary>
        /// Returns a list of evolution node IDs the player qualifies for based on level and stats.
        /// </summary>
        /// <param name="level">The player's current level.</param>
        /// <param name="stats">The player's current stat block.</param>
        /// <param name="tree">The class evolution tree to evaluate.</param>
        /// <returns>List of qualifying evolution node IDs.</returns>
        public List<string> GetAvailableEvolutions(int level, StatBlock stats, ClassEvolutionTreeSO tree)
        {
            var available = new List<string>();

            if (tree == null || tree.Nodes == null)
                return available;

            foreach (var node in tree.Nodes)
            {
                if (level < node.RequiredLevel)
                    continue;

                if (stats.GetStat(node.PrimaryStat) < node.StatThreshold)
                    continue;

                if (_unlockedEvolutions.Contains(node.NodeId))
                    continue;

                available.Add(node.NodeId);
            }

            return available;
        }

        /// <summary>
        /// Selects and applies an evolution, unlocking it and setting it as the current evolution.
        /// </summary>
        /// <param name="evolutionId">The evolution node ID to select.</param>
        public void SelectEvolution(string evolutionId)
        {
            if (string.IsNullOrEmpty(evolutionId))
                return;

            if (!_unlockedEvolutions.Contains(evolutionId))
            {
                _unlockedEvolutions.Add(evolutionId);
            }

            _currentEvolution = evolutionId;
            EventBus.Raise(new ClassEvolutionUnlockedEvent(evolutionId));
            Debug.Log($"[Evolution] Selected evolution: {evolutionId}");
        }
    }
}
