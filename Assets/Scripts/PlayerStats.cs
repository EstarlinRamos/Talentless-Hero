using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Core stats and leveling system for Talentless Hero.
/// Attach to the Player GameObject.
///
/// EXP and leveling are managed by EXPRewardSystem (single authority).
/// This component subscribes to OnLevelUp for stat points and HP/MP scaling.
///
/// IMPORTANT — HP/MP Scaling:
///   Only ALLOCATED stat points contribute to HP/MP bonus, not base stats.
///   This prevents base stats from inflating HP/MP at level 1.
///   Base stats only affect combat rolls (damage, defense, dodge, etc.).
///
/// Stats:
///   HP  — 100 base + per-level + (allocated STR × hpPerStr)
///   MP  — 100 base + per-level + (allocated INT × mpPerInt)
///   STR — physical damage/defense, HP bonus (allocated only)
///   AGI — turn order in CR system
///   INT — magic damage/defense, MP bonus (allocated only)
///   LCK — dodge chance, loot multiplier
///   HIT — accuracy, crit chance
///   Crit Damage — bonus from items/quests only, never from leveling
/// </summary>
[RequireComponent(typeof(StatusEffectHandler))]
public class PlayerStats : MonoBehaviour, ICombatant
{
    // ─────────────────────────────────────────────
    //  ICombatant
    // ─────────────────────────────────────────────
    public string CombatName    => gameObject.name;
    public int    CombatAgility => EffectiveAgility;
    public bool   IsPlayerSide  => true;
    public bool   IsAlive       => _currentHP > 0;

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────
    public event Action<int> OnLevelUp;
    public event Action<StatType, int> OnStatAllocated;
    public event Action OnHPChanged;
    public event Action OnMPChanged;
    public event Action OnRested;
    public event Action OnCritDamageChanged;

    // ─────────────────────────────────────────────
    //  Inspector — Level
    // ─────────────────────────────────────────────
    [Header("Level Settings")]
    [SerializeField] private int statPointsPerLevel = 5;

    // ─────────────────────────────────────────────
    //  Inspector — Base Stats (Level 1)
    // ─────────────────────────────────────────────
    [Header("Base Stats (Level 1, before allocation)")]
    [SerializeField] private int baseStrength     = 5;
    [SerializeField] private int baseAgility      = 5;
    [SerializeField] private int baseIntelligence = 5;
    [SerializeField] private int baseLuck         = 5;
    [SerializeField] private int baseHit          = 5;

    // ─────────────────────────────────────────────
    //  Inspector — HP / MP Scaling
    // ─────────────────────────────────────────────
    [Header("HP Scaling")]
    [SerializeField] private int baseMaxHP = 100;
    [SerializeField] private int hpPerLevel = 50;

    [Tooltip("Additional max HP per ALLOCATED point of Strength (base STR excluded).")]
    [SerializeField] private int hpPerStrength = 45;

    [Header("MP Scaling")]
    [SerializeField] private int baseMaxMP = 100;
    [SerializeField] private int mpPerLevel = 5;

    [Tooltip("Additional max MP per ALLOCATED point of Intelligence (base INT excluded).")]
    [SerializeField] private int mpPerIntelligence = 4;

    // ─────────────────────────────────────────────
    //  Inspector — Combat Scalars
    // ─────────────────────────────────────────────
    [Header("Physical Combat (Strength)")]
    [SerializeField] private float physDamagePerStr = 1.5f;
    [SerializeField] private float physDefensePerStr = 0.8f;

    [Header("Magic Combat (Intelligence)")]
    [SerializeField] private float magDamagePerInt = 1.5f;
    [SerializeField] private float magDefensePerInt = 0.8f;

    [Header("Dodge (Luck)")]
    [SerializeField] private float baseDodgeChance = 2f;
    [SerializeField] private float dodgePerLuck = 0.5f;
    [SerializeField] private float maxDodgeChance = 95f;

    [Header("Loot (Luck)")]
    [SerializeField] private float baseLootMultiplier = 1.0f;
    [SerializeField] private float lootMultPerLuck = 0.02f;

    [Header("Accuracy & Crit (Hit)")]
    [SerializeField] private float baseAccuracy = 80f;
    [SerializeField] private float accuracyPerHit = 0.5f;
    [SerializeField] private float maxAccuracy = 99f;
    [SerializeField] private float baseCritChance = 2f;
    [SerializeField] private float critChancePerHit = 0.3f;
    [SerializeField] private float maxCritChance = 75f;

    [Header("Crit Damage")]
    [SerializeField] private float baseCritDamageMultiplier = 1.5f;

    // ─────────────────────────────────────────────
    //  Runtime State
    // ─────────────────────────────────────────────
    private int _level = 1;
    private int _unspentStatPoints = 0;

    private Dictionary<StatType, int> _allocatedPoints = new Dictionary<StatType, int>()
    {
        { StatType.Strength,     0 },
        { StatType.Agility,      0 },
        { StatType.Intelligence, 0 },
        { StatType.Luck,         0 },
        { StatType.Hit,          0 }
    };

    private int _currentHP;
    private int _currentMP;
    private float _bonusCritDamage = 0f;
    private StatusEffectHandler _effects;

    // ─────────────────────────────────────────────
    //  Public Properties
    // ─────────────────────────────────────────────
    public StatusEffectHandler Effects => _effects;

    public int Level          => _level;
    public int UnspentPoints  => _unspentStatPoints;

    // Total stats (base + allocated)
    public int Strength     => baseStrength     + _allocatedPoints[StatType.Strength];
    public int Agility      => baseAgility      + _allocatedPoints[StatType.Agility];
    public int Intelligence => baseIntelligence + _allocatedPoints[StatType.Intelligence];
    public int Luck         => baseLuck         + _allocatedPoints[StatType.Luck];
    public int Hit          => baseHit          + _allocatedPoints[StatType.Hit];

    // Effective stats (after buffs/debuffs)
    public int EffectiveStrength     => Mathf.RoundToInt(Strength * _effects.GetStatMultiplier(StatType.Strength));
    public int EffectiveAgility      => Mathf.RoundToInt(Agility * _effects.GetStatMultiplier(StatType.Agility));
    public int EffectiveIntelligence => Mathf.RoundToInt(Intelligence * _effects.GetStatMultiplier(StatType.Intelligence));
    public int EffectiveLuck         => Mathf.RoundToInt(Luck * _effects.GetStatMultiplier(StatType.Luck));
    public int EffectiveHit          => Mathf.RoundToInt(Hit * _effects.GetStatMultiplier(StatType.Hit));

    // HP/MP — ONLY allocated points contribute to bonus, not base stats
    public int MaxHP     => baseMaxHP + ((_level - 1) * hpPerLevel) + (_allocatedPoints[StatType.Strength] * hpPerStrength);
    public int CurrentHP => _currentHP;
    public int MaxMP     => baseMaxMP + ((_level - 1) * mpPerLevel) + (_allocatedPoints[StatType.Intelligence] * mpPerIntelligence);
    public int CurrentMP => _currentMP;

    // Derived combat stats (use effective values in combat)
    public float PhysicalDamage  => EffectiveStrength * physDamagePerStr;
    public float PhysicalDefense => EffectiveStrength * physDefensePerStr;
    public float MagicDamage     => EffectiveIntelligence * magDamagePerInt;
    public float MagicDefense    => EffectiveIntelligence * magDefensePerInt;

    public float DodgeChance => Mathf.Clamp(baseDodgeChance + (EffectiveLuck * dodgePerLuck), 0f, maxDodgeChance);
    public float LootMultiplier => baseLootMultiplier + (EffectiveLuck * lootMultPerLuck);
    public float Accuracy => Mathf.Clamp(baseAccuracy + (EffectiveHit * accuracyPerHit), 0f, maxAccuracy);
    public float CritChance => Mathf.Clamp(baseCritChance + (EffectiveHit * critChancePerHit), 0f, maxCritChance);
    public float CritDamageMultiplier => baseCritDamageMultiplier + _bonusCritDamage;
    public int TurnPriority => EffectiveAgility;

    // ═════════════════════════════════════════════
    //  Lifecycle
    // ═════════════════════════════════════════════

    private void Awake()
    {
        _effects = GetComponent<StatusEffectHandler>();
        _currentHP = MaxHP;
        _currentMP = MaxMP;
    }

    private void Start()
    {
        StartCoroutine(SubscribeToEXPSystem());
    }

    /// <summary>
    /// Wait until EXPRewardSystem is available, then subscribe.
    /// Handles any initialization order between singletons.
    /// </summary>
    private System.Collections.IEnumerator SubscribeToEXPSystem()
    {
        // Wait until the singleton is ready
        while (EXPRewardSystem.Instance == null)
            yield return null;

        EXPRewardSystem.Instance.OnLevelUp += HandleEXPSystemLevelUp;
        Debug.Log("[PlayerStats] Subscribed to EXPRewardSystem.OnLevelUp.");
    }

    private void OnDestroy()
    {
        if (EXPRewardSystem.Instance != null)
            EXPRewardSystem.Instance.OnLevelUp -= HandleEXPSystemLevelUp;
    }

    // ═════════════════════════════════════════════
    //  Leveling
    // ═════════════════════════════════════════════

    private void HandleEXPSystemLevelUp(int newLevel)
    {
        int oldMaxHP = MaxHP;
        int oldMaxMP = MaxMP;

        _level = newLevel;
        _unspentStatPoints += statPointsPerLevel;

        // Grant new HP/MP from the level increase
        _currentHP += MaxHP - oldMaxHP;
        _currentMP += MaxMP - oldMaxMP;

        Debug.Log($"[PlayerStats] Level Up! Lv.{_level} | " +
                  $"HP {_currentHP}/{MaxHP} | MP {_currentMP}/{MaxMP} | " +
                  $"Unspent: {_unspentStatPoints}");

        OnHPChanged?.Invoke();
        OnMPChanged?.Invoke();
        OnLevelUp?.Invoke(_level);
    }

    // ═════════════════════════════════════════════
    //  Stat Allocation
    // ═════════════════════════════════════════════

    public bool AllocatePoint(StatType stat)
    {
        return AllocatePoints(stat, 1);
    }

    public bool AllocatePoints(StatType stat, int amount)
    {
        if (amount <= 0 || amount > _unspentStatPoints) return false;

        int oldMaxHP = MaxHP;
        int oldMaxMP = MaxMP;

        _allocatedPoints[stat] += amount;
        _unspentStatPoints -= amount;

        // Grant new max HP/MP as current when STR/INT increases
        if (stat == StatType.Strength)
        {
            _currentHP += MaxHP - oldMaxHP;
            OnHPChanged?.Invoke();
        }
        else if (stat == StatType.Intelligence)
        {
            _currentMP += MaxMP - oldMaxMP;
            OnMPChanged?.Invoke();
        }

        Debug.Log($"[PlayerStats] +{amount} {stat}. Total: {GetTotalStat(stat)}. Remaining: {_unspentStatPoints}");
        OnStatAllocated?.Invoke(stat, GetTotalStat(stat));
        return true;
    }

    public int GetTotalStat(StatType stat) => stat switch
    {
        StatType.Strength     => Strength,
        StatType.Agility      => Agility,
        StatType.Intelligence => Intelligence,
        StatType.Luck         => Luck,
        StatType.Hit          => Hit,
        _ => 0
    };

    public int GetAllocatedPoints(StatType stat) => _allocatedPoints[stat];

    public int GetBaseStat(StatType stat) => stat switch
    {
        StatType.Strength     => baseStrength,
        StatType.Agility      => baseAgility,
        StatType.Intelligence => baseIntelligence,
        StatType.Luck         => baseLuck,
        StatType.Hit          => baseHit,
        _ => 0
    };

    // ═════════════════════════════════════════════
    //  Crit Damage (Items / Quests only)
    // ═════════════════════════════════════════════

    public void AddBonusCritDamage(float amount)
    {
        _bonusCritDamage += amount;
        OnCritDamageChanged?.Invoke();
    }

    public void RemoveBonusCritDamage(float amount)
    {
        _bonusCritDamage = Mathf.Max(0f, _bonusCritDamage - amount);
        OnCritDamageChanged?.Invoke();
    }

    // ═════════════════════════════════════════════
    //  HP / MP — Damage, Healing, Spending
    // ═════════════════════════════════════════════

    public int TakeDamage(int rawDamage)
    {
        int actual = Mathf.Clamp(rawDamage, 0, _currentHP);
        _currentHP -= actual;
        OnHPChanged?.Invoke();

        if (_currentHP <= 0)
            Debug.Log("[PlayerStats] Player has been defeated!");

        return actual;
    }

    public int HealHP(int amount)
    {
        int before = _currentHP;
        _currentHP = Mathf.Clamp(_currentHP + amount, 0, MaxHP);
        int healed = _currentHP - before;
        if (healed > 0) OnHPChanged?.Invoke();
        return healed;
    }

    public bool SpendMP(int amount)
    {
        if (amount > _currentMP) return false;
        _currentMP -= amount;
        OnMPChanged?.Invoke();
        return true;
    }

    public int RestoreMP(int amount)
    {
        int before = _currentMP;
        _currentMP = Mathf.Clamp(_currentMP + amount, 0, MaxMP);
        int restored = _currentMP - before;
        if (restored > 0) OnMPChanged?.Invoke();
        return restored;
    }

    // ═════════════════════════════════════════════
    //  Resting (Innkeeper)
    // ═════════════════════════════════════════════

    public void Rest()
    {
        _currentHP = MaxHP;
        _currentMP = MaxMP;
        _effects.ClearAll();

        Debug.Log("[PlayerStats] Rested. HP/MP/effects restored.");

        OnHPChanged?.Invoke();
        OnMPChanged?.Invoke();
        OnRested?.Invoke();
    }

    // ═════════════════════════════════════════════
    //  Combat Rolls
    // ═════════════════════════════════════════════

    public bool RollDodge()
    {
        return UnityEngine.Random.Range(0f, 100f) < DodgeChance;
    }

    public bool RollHit(float targetDodgeChance)
    {
        float effectiveHit = Mathf.Clamp(Accuracy - targetDodgeChance, 5f, 99f);
        return UnityEngine.Random.Range(0f, 100f) < effectiveHit;
    }

    public bool RollCrit()
    {
        return UnityEngine.Random.Range(0f, 100f) < CritChance;
    }

    // ═════════════════════════════════════════════
    //  Save / Load
    // ═════════════════════════════════════════════

    public PlayerStatsData ToSaveData()
    {
        return new PlayerStatsData
        {
            level             = _level,
            unspentStatPoints = _unspentStatPoints,
            currentHP         = _currentHP,
            currentMP         = _currentMP,
            allocStr          = _allocatedPoints[StatType.Strength],
            allocAgi          = _allocatedPoints[StatType.Agility],
            allocInt          = _allocatedPoints[StatType.Intelligence],
            allocLck          = _allocatedPoints[StatType.Luck],
            allocHit          = _allocatedPoints[StatType.Hit],
            bonusCritDamage   = _bonusCritDamage
        };
    }

    public void LoadSaveData(PlayerStatsData data)
    {
        if (data == null) return;

        _level             = data.level;
        _unspentStatPoints = data.unspentStatPoints;
        _bonusCritDamage   = data.bonusCritDamage;

        _allocatedPoints[StatType.Strength]     = data.allocStr;
        _allocatedPoints[StatType.Agility]      = data.allocAgi;
        _allocatedPoints[StatType.Intelligence] = data.allocInt;
        _allocatedPoints[StatType.Luck]         = data.allocLck;
        _allocatedPoints[StatType.Hit]          = data.allocHit;

        // Clamp to new maximums (which now factor in the loaded allocations)
        _currentHP = Mathf.Clamp(data.currentHP, 1, MaxHP);
        _currentMP = Mathf.Clamp(data.currentMP, 0, MaxMP);

        OnHPChanged?.Invoke();
        OnMPChanged?.Invoke();

        Debug.Log($"[PlayerStats] Loaded — {this}");
    }

    public void ResetForNewGame()
    {
        _level = 1;
        _unspentStatPoints = 0;
        _bonusCritDamage = 0f;

        _allocatedPoints[StatType.Strength]     = 0;
        _allocatedPoints[StatType.Agility]      = 0;
        _allocatedPoints[StatType.Intelligence] = 0;
        _allocatedPoints[StatType.Luck]         = 0;
        _allocatedPoints[StatType.Hit]          = 0;

        _currentHP = MaxHP;
        _currentMP = MaxMP;
        _effects.ClearAll();

        OnHPChanged?.Invoke();
        OnMPChanged?.Invoke();

        Debug.Log("[PlayerStats] Reset for new game.");
    }

    // ═════════════════════════════════════════════
    //  Debug
    // ═════════════════════════════════════════════

    public override string ToString()
    {
        return $"Lv.{_level} | HP {_currentHP}/{MaxHP} | MP {_currentMP}/{MaxMP} | " +
               $"STR {Strength} AGI {Agility} INT {Intelligence} LCK {Luck} HIT {Hit} | " +
               $"Acc {Accuracy:F1}% Crit {CritChance:F1}% (x{CritDamageMultiplier:F2}) Dodge {DodgeChance:F1}% | " +
               $"Unspent: {_unspentStatPoints}";
    }
}
