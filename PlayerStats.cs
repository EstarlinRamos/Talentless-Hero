using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Core player stats and leveling for Talentless Hero.
///
/// EXP and leveling are managed by EXPRewardSystem (the single authority).
/// This component subscribes to EXPRewardSystem.OnLevelUp to update the
/// local level cache, grant stat points, and adjust HP/MP for new maximums.
///
/// HP/MP use only ALLOCATED stat points (not base stats) for bonus scaling.
/// </summary>
[RequireComponent(typeof(StatusEffectHandler))]
public class PlayerStats : MonoBehaviour, ICombatant
{
    // ─── ICombatant (uses effective AGI so Anchor debuff slows CR) ───
    public string CombatName   => gameObject.name;
    public int    CombatAgility => EffectiveAgility;
    public bool   IsPlayerSide => true;
    public bool   IsAlive      => _currentHP > 0;

    // ─── Events ─────────────────────────────────
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
    [Tooltip("Stat points awarded per level up.")]
    [SerializeField] private int statPointsPerLevel = 5;

    // ─────────────────────────────────────────────
    //  Inspector — Base Stats (Level 1)
    // ─────────────────────────────────────────────
    [Header("Base Stat Values (Level 1, before allocation)")]
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
    [SerializeField] private int hpPerStrength = 45;

    [Header("MP Scaling")]
    [SerializeField] private int baseMaxMP = 100;
    [SerializeField] private int mpPerLevel = 5;
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
    [Tooltip("Base crit damage multiplier (e.g. 1.5 = 150% damage on crit).")]
    [SerializeField] private float baseCritDamageMultiplier = 1.5f;

    // ─────────────────────────────────────────────
    //  Runtime State
    // ─────────────────────────────────────────────
    private int _level = 1;
    private int _unspentStatPoints = 0;

    // Allocated (bonus) points from leveling — separate from base for UI display.
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

    // Crit damage bonus from external sources (quests, items only — not leveling).
    private float _bonusCritDamage = 0f;

    private StatusEffectHandler _effects;

    // ─────────────────────────────────────────────
    //  Public Properties
    // ─────────────────────────────────────────────

    public StatusEffectHandler Effects => _effects;

    public int Level          => _level;
    public int UnspentPoints  => _unspentStatPoints;

    // Final stat totals (base + allocated)
    public int Strength     => baseStrength     + _allocatedPoints[StatType.Strength];
    public int Agility      => baseAgility      + _allocatedPoints[StatType.Agility];
    public int Intelligence => baseIntelligence + _allocatedPoints[StatType.Intelligence];
    public int Luck         => baseLuck         + _allocatedPoints[StatType.Luck];
    public int Hit          => baseHit          + _allocatedPoints[StatType.Hit];

    // Effective stats (after buffs/debuffs — used by combat system)
    public int EffectiveStrength     => Mathf.RoundToInt(Strength * _effects.GetStatMultiplier(StatType.Strength));
    public int EffectiveAgility      => Mathf.RoundToInt(Agility * _effects.GetStatMultiplier(StatType.Agility));
    public int EffectiveIntelligence => Mathf.RoundToInt(Intelligence * _effects.GetStatMultiplier(StatType.Intelligence));
    public int EffectiveLuck         => Mathf.RoundToInt(Luck * _effects.GetStatMultiplier(StatType.Luck));
    public int EffectiveHit          => Mathf.RoundToInt(Hit * _effects.GetStatMultiplier(StatType.Hit));

    // HP / MP
    public int MaxHP     => baseMaxHP + ((_level - 1) * hpPerLevel) + (Strength * hpPerStrength);
    public int CurrentHP => _currentHP;
    public int MaxMP     => baseMaxMP + ((_level - 1) * mpPerLevel) + (Intelligence * mpPerIntelligence);
    public int CurrentMP => _currentMP;

    // Derived combat stats
    public float PhysicalDamage  => EffectiveStrength * physDamagePerStr;
    public float PhysicalDefense => EffectiveStrength * physDefensePerStr;
    public float MagicDamage     => EffectiveIntelligence * magDamagePerInt;
    public float MagicDefense    => EffectiveIntelligence * magDefensePerInt;

    public float DodgeChance => Mathf.Clamp(baseDodgeChance + (EffectiveLuck * dodgePerLuck), 0f, maxDodgeChance);
    public float LootMultiplier => baseLootMultiplier + (EffectiveLuck * lootMultPerLuck);
    public float Accuracy => Mathf.Clamp(baseAccuracy + (EffectiveHit * accuracyPerHit), 0f, maxAccuracy);
    public float CritChance          => Mathf.Clamp(baseCritChance + (EffectiveHit * critChancePerHit), 0f, maxCritChance);
    public float CritDamageMultiplier => baseCritDamageMultiplier + _bonusCritDamage;
    public int TurnPriority => EffectiveAgility;

    // ═════════════════════════════════════════════
    //  Initialization
    // ═════════════════════════════════════════════

    private void Awake()
    {
        _effects = GetComponent<StatusEffectHandler>();
        _currentHP = MaxHP;
        _currentMP = MaxMP;
    }

    private void Start()
    {
        if (EXPRewardSystem.Instance != null)
        {
            EXPRewardSystem.Instance.OnLevelUp += HandleEXPSystemLevelUp;
        }
    }

    private void OnDestroy()
    {
        if (EXPRewardSystem.Instance != null)
        {
            EXPRewardSystem.Instance.OnLevelUp -= HandleEXPSystemLevelUp;
        }
    }

    // ═════════════════════════════════════════════
    //  Leveling (driven by EXPRewardSystem)
    // ═════════════════════════════════════════════

    private void HandleEXPSystemLevelUp(int newLevel)
    {
        int oldMaxHP = MaxHP;
        int oldMaxMP = MaxMP;

        _level = newLevel;
        _unspentStatPoints += statPointsPerLevel;

        _currentHP += MaxHP - oldMaxHP;
        _currentMP += MaxMP - oldMaxMP;

        Debug.Log($"[PlayerStats] Level Up! Now level {_level}. " +
                  $"HP {_currentHP}/{MaxHP} | MP {_currentMP}/{MaxMP} | " +
                  $"Unspent points: {_unspentStatPoints}");

        OnHPChanged?.Invoke();
        OnMPChanged?.Invoke();
        OnLevelUp?.Invoke(_level);
    }

    // ═════════════════════════════════════════════
    //  Stat Allocation
    // ═════════════════════════════════════════════

    /// <summary>Spend one stat point on the chosen stat.</summary>
    public bool AllocatePoint(StatType stat)
    {
        return AllocatePoints(stat, 1);
    }

    /// <summary>Spend multiple stat points on one stat at once.</summary>
    public bool AllocatePoints(StatType stat, int amount)
    {
        if (amount <= 0 || amount > _unspentStatPoints) return false;

        int oldMaxHP = MaxHP;
        int oldMaxMP = MaxMP;

        _allocatedPoints[stat] += amount;
        _unspentStatPoints -= amount;

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

        Debug.Log($"[PlayerStats] Allocated {amount} point(s) to {stat}. " +
                  $"New total: {GetTotalStat(stat)}. Remaining points: {_unspentStatPoints}");

        OnStatAllocated?.Invoke(stat, GetTotalStat(stat));
        return true;
    }

    public int GetTotalStat(StatType stat)
    {
        return stat switch
        {
            StatType.Strength     => Strength,
            StatType.Agility      => Agility,
            StatType.Intelligence => Intelligence,
            StatType.Luck         => Luck,
            StatType.Hit          => Hit,
            _ => 0
        };
    }

    /// <summary>Returns only the allocated (bonus) portion of a stat.</summary>
    public int GetAllocatedPoints(StatType stat)
    {
        return _allocatedPoints[stat];
    }

    /// <summary>Returns the base (unmodified) value of a stat.</summary>
    public int GetBaseStat(StatType stat)
    {
        return stat switch
        {
            StatType.Strength     => baseStrength,
            StatType.Agility      => baseAgility,
            StatType.Intelligence => baseIntelligence,
            StatType.Luck         => baseLuck,
            StatType.Hit          => baseHit,
            _ => 0
        };
    }

    // ═════════════════════════════════════════════
    //  Crit Damage (Items / Quests only)
    // ═════════════════════════════════════════════

    public void AddBonusCritDamage(float amount)
    {
        _bonusCritDamage += amount;
        Debug.Log($"[PlayerStats] Crit damage bonus increased by {amount:P0}. " +
                  $"Total multiplier: {CritDamageMultiplier:F2}x");
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

    /// <summary>Deal damage to the player. Returns actual damage taken.</summary>
    public int TakeDamage(int rawDamage)
    {
        int actual = Mathf.Clamp(rawDamage, 0, _currentHP);
        _currentHP -= actual;
        OnHPChanged?.Invoke();

        if (_currentHP <= 0)
        {
            Debug.Log("[PlayerStats] Player has been defeated!");
        }

        return actual;
    }

    /// <summary>Heal the player. Returns actual amount healed.</summary>
    public int HealHP(int amount)
    {
        int before = _currentHP;
        _currentHP = Mathf.Clamp(_currentHP + amount, 0, MaxHP);
        int healed = _currentHP - before;
        if (healed > 0) OnHPChanged?.Invoke();
        return healed;
    }

    /// <summary>Spend MP for casting. Returns true if enough MP was available.</summary>
    public bool SpendMP(int amount)
    {
        if (amount > _currentMP) return false;
        _currentMP -= amount;
        OnMPChanged?.Invoke();
        return true;
    }

    /// <summary>Restore MP. Returns actual amount restored.</summary>
    public int RestoreMP(int amount)
    {
        int before = _currentMP;
        _currentMP = Mathf.Clamp(_currentMP + amount, 0, MaxMP);
        int restored = _currentMP - before;
        if (restored > 0) OnMPChanged?.Invoke();
        return restored;
    }

    // ═════════════════════════════════════════════
    //  Resting
    // ═════════════════════════════════════════════

    /// <summary>
    /// Fully restore HP, MP, and clear status effects.
    /// EnemyManager listens to OnRested to respawn mobs.
    /// </summary>
    public void Rest()
    {
        _currentHP = MaxHP;
        _currentMP = MaxMP;
        _effects.ClearAll();

        Debug.Log("[PlayerStats] Rested at the inn. HP, MP, and status effects fully restored.");

        OnHPChanged?.Invoke();
        OnMPChanged?.Invoke();
        OnRested?.Invoke();
    }

    // ═════════════════════════════════════════════
    //  Combat Rolls
    // ═════════════════════════════════════════════

    public bool RollDodge()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        bool dodged = roll < DodgeChance;
        if (dodged)
            Debug.Log($"[PlayerStats] Dodged! (Roll: {roll:F1} < {DodgeChance:F1}%)");
        return dodged;
    }

    /// <summary>
    /// Roll accuracy against a target's dodge. Effective hit chance is
    /// clamped to [5, 99] so there's always a small miss/hit chance.
    /// </summary>
    public bool RollHit(float targetDodgeChance)
    {
        float effectiveHitChance = Mathf.Clamp(Accuracy - targetDodgeChance, 5f, 99f);
        float roll = UnityEngine.Random.Range(0f, 100f);
        bool hit = roll < effectiveHitChance;

        if (!hit)
            Debug.Log($"[PlayerStats] Missed! (Roll: {roll:F1} >= {effectiveHitChance:F1}% effective accuracy)");

        return hit;
    }

    public bool RollCrit()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        bool crit = roll < CritChance;
        if (crit)
            Debug.Log($"[PlayerStats] CRITICAL HIT! (Roll: {roll:F1} < {CritChance:F1}%) " +
                      $"Damage x{CritDamageMultiplier:F2}");
        return crit;
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

        _currentHP = Mathf.Clamp(data.currentHP, 0, MaxHP);
        _currentMP = Mathf.Clamp(data.currentMP, 0, MaxMP);

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

    public override string ToString()
    {
        return $"Lv.{_level} | HP {_currentHP}/{MaxHP} | MP {_currentMP}/{MaxMP} | " +
               $"STR {Strength} AGI {Agility} INT {Intelligence} LCK {Luck} HIT {Hit} | " +
               $"Acc {Accuracy:F1}% Crit {CritChance:F1}% (x{CritDamageMultiplier:F2}) Dodge {DodgeChance:F1}% | " +
               $"Unspent: {_unspentStatPoints}";
    }
}
