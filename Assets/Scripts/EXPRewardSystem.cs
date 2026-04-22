using System;
using System.Collections.Generic;
using UnityEngine;

public enum EXPSource
{
    Combat,
    Quest,
    Misc
}

[Serializable]
public class EXPRewardEntry
{
    public EXPSource source;
    public string description;
    public int amount;
    public string dateTime;

    public EXPRewardEntry(EXPSource source, string description, int amount)
    {
        this.source = source;
        this.description = description;
        this.amount = amount;
        this.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

[Serializable]
public class EXPLedger
{
    public int totalCombatEXP;
    public int totalQuestEXP;
    public int totalMiscEXP;
    public List<EXPRewardEntry> history = new List<EXPRewardEntry>();

    public int TotalAllSources => totalCombatEXP + totalQuestEXP + totalMiscEXP;
}

public class EXPRewardSystem : MonoBehaviour
{
    public static EXPRewardSystem Instance { get; private set; }

    // ─────────────────────────────────────────────
    //  LEVEL CURVE SETTINGS
    //  Uses compound scaling: EXP = base * (multiplier ^ (level - 1))
    // ─────────────────────────────────────────────

    [Header("Level Curve")]
    [SerializeField] private int baseEXPRequired = 100;
    [SerializeField] private float scalingMultiplier = 1.25f;
    [SerializeField] private int maxLevel = 99;

    // ─────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────

    private int currentLevel = 1;
    private int currentEXP = 0; // EXP accumulated toward CURRENT level
    private EXPLedger ledger = new EXPLedger();

    // ─────────────────────────────────────────────
    //  EVENTS (Subscribe from UI, combat log, etc.)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Fired every time EXP is awarded. Includes source, amount, 
    /// current EXP toward next level, and total required for next level.
    /// </summary>
    public event Action<EXPRewardEntry, int, int> OnEXPAwarded;

    /// <summary>
    /// Fired when the player levels up. Includes new level.
    /// </summary>
    public event Action<int> OnLevelUp;

    // ─────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// ALL exp must flow through this method.
    /// Call this from combat, quest completion, NPC rewards, etc.
    /// </summary>
    public void AwardEXP(int amount, EXPSource source, string description = "")
    {
        if (currentLevel >= maxLevel)
        {
            Debug.Log("[EXP] Already at max level.");
            return;
        }

        if (amount <= 0) return;

        // Log to ledger
        EXPRewardEntry entry = new EXPRewardEntry(source, description, amount);
        ledger.history.Add(entry);
        TallyBySource(source, amount);

        // Add EXP and check for level ups
        currentEXP += amount;

        int expForNext = GetEXPRequiredForLevel(currentLevel);

        // Fire event BEFORE level-up so combat/UI can show "earned X / needed Y"
        OnEXPAwarded?.Invoke(entry, currentEXP, expForNext);

        // Process level ups (could be multiple from a big reward)
        while (currentEXP >= expForNext && currentLevel < maxLevel)
        {
            currentEXP -= expForNext;
            currentLevel++;
            expForNext = GetEXPRequiredForLevel(currentLevel);

            Debug.Log($"[EXP] LEVEL UP! Now level {currentLevel}");
            OnLevelUp?.Invoke(currentLevel);
        }

        // Clamp at max level
        if (currentLevel >= maxLevel)
        {
            currentEXP = 0;
        }
    }

    /// <summary>
    /// Shorthand for combat EXP. Returns the entry for combat log display.
    /// </summary>
    public EXPRewardEntry AwardCombatEXP(int amount, string enemyName)
    {
        string desc = $"Defeated {enemyName}";
        AwardEXP(amount, EXPSource.Combat, desc);
        return ledger.history[ledger.history.Count - 1];
    }

    /// <summary>
    /// Shorthand for quest EXP.
    /// </summary>
    public void AwardQuestEXP(int amount, string questName)
    {
        AwardEXP(amount, EXPSource.Quest, $"Completed: {questName}");
    }

    /// <summary>
    /// Shorthand for misc EXP (exploration, items, NPC gifts, etc.)
    /// </summary>
    public void AwardMiscEXP(int amount, string reason)
    {
        AwardEXP(amount, EXPSource.Misc, reason);
    }

    // ─────────────────────────────────────────────
    //  QUERIES (Used by Player Page UI)
    // ─────────────────────────────────────────────

    public int GetCurrentLevel() => currentLevel;
    public int GetCurrentEXP() => currentEXP;
    public int GetEXPToNextLevel() => GetEXPRequiredForLevel(currentLevel);
    public int GetEXPRemaining() => GetEXPToNextLevel() - currentEXP;
    public bool IsMaxLevel() => currentLevel >= maxLevel;

    /// <summary>
    /// Returns 0.0 - 1.0 representing progress toward next level.
    /// Player page displays this as a percentage.
    /// </summary>
    public float GetLevelProgressNormalized()
    {
        if (currentLevel >= maxLevel) return 1f;

        int required = GetEXPRequiredForLevel(currentLevel);
        if (required <= 0) return 1f;

        return Mathf.Clamp01((float)currentEXP / required);
    }

    /// <summary>
    /// Returns 0 - 100 as a clean integer percentage.
    /// </summary>
    public int GetLevelProgressPercent()
    {
        return Mathf.RoundToInt(GetLevelProgressNormalized() * 100f);
    }

    /// <summary>
    /// Returns a formatted string like "73%" for direct UI binding.
    /// </summary>
    public string GetLevelProgressString()
    {
        if (currentLevel >= maxLevel) return "MAX";
        return $"{GetLevelProgressPercent()}%";
    }

    // ─────────────────────────────────────────────
    //  LEDGER QUERIES
    // ─────────────────────────────────────────────

    public EXPLedger GetLedger() => ledger;
    public int GetTotalCombatEXP() => ledger.totalCombatEXP;
    public int GetTotalQuestEXP() => ledger.totalQuestEXP;
    public int GetTotalMiscEXP() => ledger.totalMiscEXP;
    public int GetTotalEXPEarned() => ledger.TotalAllSources;
    public List<EXPRewardEntry> GetEXPHistory() => ledger.history;

    // ─────────────────────────────────────────────
    //  LEVEL CURVE MATH
    // ─────────────────────────────────────────────

    /// <summary>
    /// EXP required to go from the given level to level + 1.
    /// Formula: base * (multiplier ^ (level - 1))
    /// Level 1: 100, Level 2: 125, Level 3: 156, Level 4: 195, etc.
    /// </summary>
    public int GetEXPRequiredForLevel(int level)
    {
        return Mathf.RoundToInt(baseEXPRequired * Mathf.Pow(scalingMultiplier, level - 1));
    }

    /// <summary>
    /// Total cumulative EXP needed to reach a given level from level 1.
    /// Useful for displaying overall progression milestones.
    /// </summary>
    public int GetCumulativeEXPForLevel(int level)
    {
        int total = 0;
        for (int i = 1; i < level; i++)
        {
            total += GetEXPRequiredForLevel(i);
        }
        return total;
    }

    // ─────────────────────────────────────────────
    //  SAVE / LOAD SUPPORT
    // ─────────────────────────────────────────────

    /// <summary>
    /// Call from SaveManager.CaptureCurrentState() to include EXP data.
    /// </summary>
    public EXPSaveData CaptureForSave()
    {
        return new EXPSaveData
        {
            currentLevel = this.currentLevel,
            currentEXP = this.currentEXP,
            ledger = this.ledger
        };
    }

    /// <summary>
    /// Call from SaveManager.ApplyLoadedState() to restore EXP data.
    /// </summary>
    public void LoadFromSave(EXPSaveData saveData)
    {
        currentLevel = saveData.currentLevel;
        currentEXP = saveData.currentEXP;
        ledger = saveData.ledger ?? new EXPLedger();

        Debug.Log($"[EXP] Loaded: Level {currentLevel}, {currentEXP}/{GetEXPToNextLevel()} EXP ({GetLevelProgressString()})");
    }

    /// <summary>
    /// Resets all EXP state for a new game.
    /// </summary>
    public void ResetForNewGame()
    {
        currentLevel = 1;
        currentEXP = 0;
        ledger = new EXPLedger();
        Debug.Log("[EXP] Reset for new game.");
    }

    // ─────────────────────────────────────────────
    //  INTERNAL
    // ─────────────────────────────────────────────

    private void TallyBySource(EXPSource source, int amount)
    {
        switch (source)
        {
            case EXPSource.Combat: ledger.totalCombatEXP += amount; break;
            case EXPSource.Quest:  ledger.totalQuestEXP += amount;  break;
            case EXPSource.Misc:   ledger.totalMiscEXP += amount;   break;
        }
    }
}

// EXPSaveData lives in SaveData.cs
