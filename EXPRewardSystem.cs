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

/// <summary>
/// Singleton authority for all EXP and leveling. Uses compound scaling:
/// EXP required = base * (multiplier ^ (level - 1)).
/// All EXP must flow through AwardEXP().
/// </summary>
public class EXPRewardSystem : MonoBehaviour
{
    public static EXPRewardSystem Instance { get; private set; }

    [Header("Level Curve")]
    [SerializeField] private int baseEXPRequired = 100;
    [SerializeField] private float scalingMultiplier = 1.25f;
    [SerializeField] private int maxLevel = 99;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private int currentLevel = 1;
    private int currentEXP = 0;
    private EXPLedger ledger = new EXPLedger();

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired every time EXP is awarded (entry, current toward next, required).</summary>
    public event Action<EXPRewardEntry, int, int> OnEXPAwarded;

    /// <summary>Fired when the player levels up (new level).</summary>
    public event Action<int> OnLevelUp;

    // ─────────────────────────────────────────────
    //  Lifecycle
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
    //  Public API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Central method for all EXP awards. Handles ledger tracking and level-up processing.
    /// </summary>
    public void AwardEXP(int amount, EXPSource source, string description = "")
    {
        if (currentLevel >= maxLevel)
        {
            Debug.Log("[EXP] Already at max level.");
            return;
        }

        if (amount <= 0) return;

        EXPRewardEntry entry = new EXPRewardEntry(source, description, amount);
        ledger.history.Add(entry);
        TallyBySource(source, amount);

        currentEXP += amount;

        int expForNext = GetEXPRequiredForLevel(currentLevel);

        OnEXPAwarded?.Invoke(entry, currentEXP, expForNext);

        while (currentEXP >= expForNext && currentLevel < maxLevel)
        {
            currentEXP -= expForNext;
            currentLevel++;
            expForNext = GetEXPRequiredForLevel(currentLevel);

            Debug.Log($"[EXP] LEVEL UP! Now level {currentLevel}");
            OnLevelUp?.Invoke(currentLevel);
        }

        if (currentLevel >= maxLevel)
        {
            currentEXP = 0;
        }
    }

    public EXPRewardEntry AwardCombatEXP(int amount, string enemyName)
    {
        string desc = $"Defeated {enemyName}";
        AwardEXP(amount, EXPSource.Combat, desc);
        return ledger.history[ledger.history.Count - 1];
    }

    public void AwardQuestEXP(int amount, string questName)
    {
        AwardEXP(amount, EXPSource.Quest, $"Completed: {questName}");
    }

    public void AwardMiscEXP(int amount, string reason)
    {
        AwardEXP(amount, EXPSource.Misc, reason);
    }

    // ─────────────────────────────────────────────
    //  Queries
    // ─────────────────────────────────────────────

    public int GetCurrentLevel() => currentLevel;
    public int GetCurrentEXP() => currentEXP;
    public int GetEXPToNextLevel() => GetEXPRequiredForLevel(currentLevel);
    public int GetEXPRemaining() => GetEXPToNextLevel() - currentEXP;
    public bool IsMaxLevel() => currentLevel >= maxLevel;

    /// <summary>Returns 0.0–1.0 representing progress toward next level.</summary>
    public float GetLevelProgressNormalized()
    {
        if (currentLevel >= maxLevel) return 1f;

        int required = GetEXPRequiredForLevel(currentLevel);
        if (required <= 0) return 1f;

        return Mathf.Clamp01((float)currentEXP / required);
    }

    public int GetLevelProgressPercent()
    {
        return Mathf.RoundToInt(GetLevelProgressNormalized() * 100f);
    }

    public string GetLevelProgressString()
    {
        if (currentLevel >= maxLevel) return "MAX";
        return $"{GetLevelProgressPercent()}%";
    }

    // ─────────────────────────────────────────────
    //  Ledger Queries
    // ─────────────────────────────────────────────

    public EXPLedger GetLedger() => ledger;
    public int GetTotalCombatEXP() => ledger.totalCombatEXP;
    public int GetTotalQuestEXP() => ledger.totalQuestEXP;
    public int GetTotalMiscEXP() => ledger.totalMiscEXP;
    public int GetTotalEXPEarned() => ledger.TotalAllSources;
    public List<EXPRewardEntry> GetEXPHistory() => ledger.history;

    // ─────────────────────────────────────────────
    //  Level Curve
    // ─────────────────────────────────────────────

    /// <summary>
    /// EXP required to go from the given level to level + 1.
    /// Formula: base * (multiplier ^ (level - 1))
    /// </summary>
    public int GetEXPRequiredForLevel(int level)
    {
        return Mathf.RoundToInt(baseEXPRequired * Mathf.Pow(scalingMultiplier, level - 1));
    }

    /// <summary>
    /// Total cumulative EXP needed to reach a given level from level 1.
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
    //  Save / Load
    // ─────────────────────────────────────────────

    public EXPSaveData CaptureForSave()
    {
        return new EXPSaveData
        {
            currentLevel = this.currentLevel,
            currentEXP = this.currentEXP,
            ledger = this.ledger
        };
    }

    public void LoadFromSave(EXPSaveData saveData)
    {
        currentLevel = saveData.currentLevel;
        currentEXP = saveData.currentEXP;
        ledger = saveData.ledger ?? new EXPLedger();

        Debug.Log($"[EXP] Loaded: Level {currentLevel}, {currentEXP}/{GetEXPToNextLevel()} EXP ({GetLevelProgressString()})");
    }

    public void ResetForNewGame()
    {
        currentLevel = 1;
        currentEXP = 0;
        ledger = new EXPLedger();
        Debug.Log("[EXP] Reset for new game.");
    }

    // ─────────────────────────────────────────────
    //  Internal
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
