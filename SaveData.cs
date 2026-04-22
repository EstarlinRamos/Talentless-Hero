using System;
using System.Collections.Generic;

/// <summary>
/// Master save container. Each system is its own serializable block.
/// Missing or null blocks are safely ignored on load, so old saves
/// remain compatible as new systems are added.
/// </summary>
[Serializable]
public class GameSaveData
{
    public string saveName;
    public string dateTime;
    public int saveNumber;

    public PlayerPositionData position;
    public PlayerStatsData stats;
    public EXPSaveData exp;
    public InventoryData inventory;
    public QuestSaveData quests;
    public NPCSaveData npcs;
    public WorldFlagData worldFlags;
}

// ─────────────────────────────────────────────
//  Position
// ─────────────────────────────────────────────

[Serializable]
public class PlayerPositionData
{
    public string sceneName;
    public float x;
    public float y;
    public float z;
}

// ─────────────────────────────────────────────
//  Player Stats
// ─────────────────────────────────────────────

[Serializable]
public class PlayerStatsData
{
    public int level;
    public int unspentStatPoints;
    public int currentHP;
    public int currentMP;
    public int allocStr;
    public int allocAgi;
    public int allocInt;
    public int allocLck;
    public int allocHit;
    public float bonusCritDamage;
}

// ─────────────────────────────────────────────
//  EXP
// ─────────────────────────────────────────────

[Serializable]
public class EXPSaveData
{
    public int currentLevel;
    public int currentEXP;
    public EXPLedger ledger;
}

// ─────────────────────────────────────────────
//  Inventory
// ─────────────────────────────────────────────

[Serializable]
public class InventoryData
{
    public List<InventoryItemData> items = new List<InventoryItemData>();
    public List<InventoryItemData> equipped = new List<InventoryItemData>();
    public int gold;
}

[Serializable]
public class InventoryItemData
{
    public string itemID;
    public string itemName;
    public int quantity;
    public int slotIndex;
}

// ─────────────────────────────────────────────
//  Quests
// ─────────────────────────────────────────────

[Serializable]
public class QuestSaveData
{
    public List<QuestEntryData> active = new List<QuestEntryData>();
    public List<QuestEntryData> completed = new List<QuestEntryData>();
}

[Serializable]
public class QuestEntryData
{
    public string questID;
    public string questName;
    public string currentObjective;
    public int progress;
    public int requiredProgress;
    public bool isComplete;
}

// ─────────────────────────────────────────────
//  NPCs
// ─────────────────────────────────────────────

[Serializable]
public class NPCSaveData
{
    public List<NPCStateData> states = new List<NPCStateData>();
}

[Serializable]
public class NPCStateData
{
    public string npcID;
    public string npcName;
    public float posX;
    public float posY;
    public float posZ;
    public bool isAlive;
    public int dialogueIndex;
    public int relationshipLevel;
    public List<string> flags = new List<string>();
}

// ─────────────────────────────────────────────
//  World Flags
// ─────────────────────────────────────────────

[Serializable]
public class WorldFlagData
{
    public List<string> flags = new List<string>();
}
