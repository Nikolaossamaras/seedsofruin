using System.Collections.Generic;
using UnityEngine;
using SoR.Shared;

namespace SoR.AI
{
    [CreateAssetMenu(menuName = "SoR/AI/EnemyDefinition")]
    public class EnemyDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string EnemyName;
        public string EnemyId;
        [TextArea(2, 4)]
        public string Description;

        [Header("Stats")]
        public StatBlock BaseStats;
        public float MaxHealth = 100f;
        public float MaxStagger = 50f;
        public Element Element;

        [Header("Difficulty & Rewards")]
        [Tooltip("Common=normal, Rare=elite, Legendary=miniboss, Mythic=boss")]
        public Rarity Tier;
        public int XPReward;
        public int GoldReward;
        public List<LootDrop> LootTable = new();

        [Header("Behavior")]
        public BehaviorTreeSO BehaviorTree;

        [Header("Visuals")]
        public Sprite Icon;
        public GameObject Prefab;
    }
}
