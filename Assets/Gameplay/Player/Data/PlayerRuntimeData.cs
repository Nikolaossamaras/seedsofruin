using System;
using System.Collections.Generic;
using SoR.Shared;

namespace SoR.Gameplay
{
    [Serializable]
    public class PlayerRuntimeData
    {
        // Progression
        public int Level = 1;
        public int XP;
        public int StatPoints;

        // Health
        public float CurrentHealth;
        public float MaxHealth;

        // Verdance (mana/energy)
        public float CurrentVerdance;
        public float MaxVerdance;

        // Stat allocation
        public StatBlock AllocatedStats;
        public StatBlock BonusStats;

        // Reference to the base stats from the ScriptableObject.
        // Must be set externally when the runtime data is created.
        private StatBlock _baseStats;

        public StatBlock TotalStats => _baseStats + AllocatedStats + BonusStats;

        // World state
        public string CurrentRegion;

        // Companions
        public List<string> UnlockedCompanionIds = new();

        // Currencies
        public int Gold;
        public int GuildTokens;
        public int AccordShards;
        public int StarbloomPetals;
        public int AccordEssence;
        public int Stardust;

        public void SetBaseStats(StatBlock baseStats)
        {
            _baseStats = baseStats;
        }
    }
}
