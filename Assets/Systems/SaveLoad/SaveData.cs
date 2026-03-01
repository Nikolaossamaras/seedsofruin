using System;
using System.Collections.Generic;

namespace SoR.Systems.SaveLoad
{
    [Serializable]
    public class SerializedStatBlock
    {
        public float Vigor;
        public float Strength;
        public float Harvest;
        public float Verdance;
        public float Agility;
        public float Resilience;
    }

    [Serializable]
    public class SerializedCompanionData
    {
        public string CompanionId;
        public int Level;
        public int XP;
        public int AscensionRank;
        public int BondLevel;
    }

    [Serializable]
    public class SerializedInventoryEntry
    {
        public string ItemId;
        public int Quantity;
    }

    [Serializable]
    public class SerializedEquipmentEntry
    {
        public string Slot;
        public string ItemId;
    }

    [Serializable]
    public class SerializedQuestProgress
    {
        public string QuestId;
        public string ProgressJson;
    }

    [Serializable]
    public class SerializedPityEntry
    {
        public string BannerId;
        public int PullCount;
    }

    [Serializable]
    public class SaveData
    {
        // Player Identity
        public string PlayerName;

        // Progression
        public int Level;
        public int XP;

        // Vitals
        public float CurrentHealth;
        public float CurrentVerdance;

        // Stats
        public SerializedStatBlock AllocatedStats;

        // Inventory (serialized as lists for JsonUtility compatibility)
        public List<SerializedInventoryEntry> Inventory = new();

        // Equipment
        public List<SerializedEquipmentEntry> EquippedItems = new();

        // Companions
        public List<SerializedCompanionData> Companions = new();

        // Quests
        public List<string> CompletedQuestIds = new();
        public List<SerializedQuestProgress> ActiveQuestProgress = new();

        // Gacha
        public List<SerializedPityEntry> GachaPityData = new();

        // Currency
        public int Gold;
        public int GuildTokens;
        public int AccordShards;
        public int StarbloomPetals;
        public int AccordEssence;
        public int Stardust;

        // World
        public string CurrentRegion;

        // Meta
        public string SaveTimestamp;
    }
}
