using System;
using System.Collections.Generic;

/// <summary>
/// Master save container. Each system is its own serializable block.
/// Missing or null blocks are safely ignored on load, so the game
/// works whether or not inventory, quests, etc. are implemented.
/// </summary>
[Serializable]
public class GameSaveData
{
    // --- Meta ---
    public string saveName;
    public string dateTime;
    public int saveNumber;
    public bool isAutoSave;

    // --- Compartmentalized Modules ---
    public PlayerPositionData position;
    public PlayerStatsData stats;
    public EXPSaveData exp;
    public InventoryData inventory;
    public QuestSaveData quests;
    public NPCSaveData npcs;
    public WorldFlagData worldFlags;
}

// ─────────────────────────────────────────────
//  POSITION
// ─────────────────────────────────────────────

[Serializable]
public class PlayerPositionData
{
    public string sceneName;
    public string zoneName;
    public float x;
    public float y;
    public float z;
}

// ─────────────────────────────────────────────
//  PLAYER STATS
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
//  INVENTORY
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
//  QUESTS (Placeholder)
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
//  NPCs (Placeholder)
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
//  WORLD FLAGS (Placeholder)
// ─────────────────────────────────────────────

[Serializable]
public class WorldFlagData
{
    public List<string> flags = new List<string>();
}
