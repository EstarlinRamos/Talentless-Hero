using UnityEngine;
using System;

/// <summary>
/// Base enemy stats for Talentless Hero.
/// Attach to each enemy prefab alongside StatusEffectHandler and EnemyAI.
///
/// Boss Persistence:
///   Boss enemies (isBoss = true) cannot respawn after being killed.
///   Once defeated, they stay permanently disabled for the rest of the session.
///   Regular enemies respawn normally via EnemyManager or EnemySpawner.
///
/// Enemies do NOT have MP — skills are gated by cooldowns and AI logic.
/// </summary>
[RequireComponent(typeof(StatusEffectHandler))]
public class EnemyStats : MonoBehaviour, ICombatant
{
    // ─────────────────────────────────────────────
    //  ICombatant Implementation
    // ─────────────────────────────────────────────
    public string CombatName    => enemyName;
    public int    CombatAgility => EffectiveAgility;
    public bool   IsPlayerSide  => false;
    public bool   IsAlive       => _currentHP > 0;

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────
    public event Action OnDefeated;
    public event Action OnHPChanged;

    [Header("Identity")]
    [SerializeField] private string enemyName = "Slime";
    [SerializeField] private int enemyLevel = 1;

    [Tooltip("Boss enemies prevent the player from escaping combat " +
             "and cannot respawn after being killed.")]
    [SerializeField] private bool isBoss = false;

    [Header("Core Stats")]
    [SerializeField] private int maxHP         = 30;
    [SerializeField] private int strength      = 4;
    [SerializeField] private int agility       = 3;
    [SerializeField] private int intelligence  = 2;
    [SerializeField] private int luck          = 1;
    [SerializeField] private int hit           = 3;

    [Header("Rewards")]
    [SerializeField] private int expReward     = 25;
    [SerializeField] private int goldReward    = 10;

    private int _currentHP;
    private StatusEffectHandler _effects;
    private bool _permanentlyDefeated = false;

    // ─── Public Properties ──────────────────────
    public string EnemyName    => enemyName;
    public int EnemyLevel      => enemyLevel;
    public bool IsBoss         => isBoss;
    public int MaxHP           => maxHP;
    public int CurrentHP       => _currentHP;
    public bool IsPermanentlyDefeated => _permanentlyDefeated;

    // Base stats
    public int Strength        => strength;
    public int Agility         => agility;
    public int Intelligence    => intelligence;
    public int Luck            => luck;
    public int Hit             => hit;

    // Effective stats (after buffs/debuffs)
    public int EffectiveStrength     => Mathf.RoundToInt(strength * _effects.GetStatMultiplier(StatType.Strength));
    public int EffectiveAgility      => Mathf.RoundToInt(agility * _effects.GetStatMultiplier(StatType.Agility));
    public int EffectiveIntelligence => Mathf.RoundToInt(intelligence * _effects.GetStatMultiplier(StatType.Intelligence));
    public int EffectiveLuck         => Mathf.RoundToInt(luck * _effects.GetStatMultiplier(StatType.Luck));
    public int EffectiveHit          => Mathf.RoundToInt(hit * _effects.GetStatMultiplier(StatType.Hit));

    public int ExpReward       => expReward;
    public int GoldReward      => goldReward;

    // ─── Derived Combat Stats ───────────────────
    public float PhysicalDamage  => EffectiveStrength * 1.5f;
    public float PhysicalDefense => EffectiveStrength * 0.8f;
    public float MagicDamage     => EffectiveIntelligence * 1.5f;
    public float MagicDefense    => EffectiveIntelligence * 0.8f;
    public float DodgeChance     => Mathf.Clamp(2f + (EffectiveLuck * 0.5f), 0f, 50f);
    public float Accuracy        => Mathf.Clamp(80f + (EffectiveHit * 0.5f), 0f, 99f);
    public float CritChance      => Mathf.Clamp(2f + (EffectiveHit * 0.3f), 0f, 50f);
    public float CritDamageMultiplier => 1.5f;
    public int   TurnPriority    => EffectiveAgility;

    // ─── Lifecycle ──────────────────────────────
    private void Awake()
    {
        _effects = GetComponent<StatusEffectHandler>();
        _currentHP = maxHP;

        // Restore permanent defeat from saved flags
        if (isBoss && WorldFlagManager.Instance != null)
        {
            string flag = $"boss_defeated_{enemyName.Replace(" ", "_").ToLower()}";
            if (WorldFlagManager.Instance.HasFlag(flag))
            {
                _permanentlyDefeated = true;
                gameObject.SetActive(false);
                Debug.Log($"[EnemyStats] {enemyName} was previously defeated (restored from flags).");
            }
        }
    }

    /// <summary>
    /// Reset this enemy to full health and clear all status effects.
    /// Bosses that have been permanently defeated will NOT respawn.
    /// </summary>
    public void Respawn()
    {
        // Bosses stay dead once killed
        if (_permanentlyDefeated)
        {
            Debug.Log($"[EnemyStats] {enemyName} is permanently defeated. Cannot respawn.");
            return;
        }

        _currentHP = maxHP;

        // Re-cache effects in case Awake hasn't run (spawner-created enemies)
        if (_effects == null)
            _effects = GetComponent<StatusEffectHandler>();

        if (_effects != null)
            _effects.ClearAll();

        gameObject.SetActive(true);
        OnHPChanged?.Invoke();
    }

    // ─── Damage & Healing ───────────────────────

    /// <summary>
    /// Apply damage. Returns actual damage dealt.
    /// If this kills a boss, they are marked as permanently defeated.
    /// </summary>
    public int TakeDamage(int rawDamage)
    {
        int actual = Mathf.Clamp(rawDamage, 0, _currentHP);
        _currentHP -= actual;
        OnHPChanged?.Invoke();

        if (_currentHP <= 0)
        {
            Debug.Log($"[EnemyStats] {enemyName} defeated!");

            if (isBoss)
            {
                _permanentlyDefeated = true;

                // Persist to WorldFlagManager so the defeat survives save/load
                if (WorldFlagManager.Instance != null)
                {
                    string flag = $"boss_defeated_{enemyName.Replace(" ", "_").ToLower()}";
                    WorldFlagManager.Instance.SetFlag(flag);
                }

                Debug.Log($"[EnemyStats] BOSS {enemyName} permanently defeated! Will not respawn.");
            }

            OnDefeated?.Invoke();
        }

        return actual;
    }

    /// <summary>
    /// Heal this enemy. Returns actual amount healed.
    /// </summary>
    public int Heal(int amount)
    {
        int before = _currentHP;
        _currentHP = Mathf.Clamp(_currentHP + amount, 0, maxHP);
        int healed = _currentHP - before;
        if (healed > 0)
        {
            OnHPChanged?.Invoke();
            Debug.Log($"[EnemyStats] {enemyName} healed for {healed} HP ({_currentHP}/{maxHP}).");
        }
        return healed;
    }

    // ─── Combat Rolls ───────────────────────────

    public bool RollDodge()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < DodgeChance;
    }

    public bool RollHit(float targetDodgeChance)
    {
        if (_effects.HasGuaranteedHit) return true;
        float effectiveHitChance = Mathf.Clamp(Accuracy - targetDodgeChance, 5f, 99f);
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < effectiveHitChance;
    }

    public bool RollCrit()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        return roll < CritChance;
    }

    public override string ToString()
    {
        return $"{enemyName} Lv.{enemyLevel}{(isBoss ? " [BOSS]" : "")} | HP {_currentHP}/{maxHP} | " +
               $"STR {strength}({EffectiveStrength}) AGI {agility}({EffectiveAgility}) " +
               $"INT {intelligence}({EffectiveIntelligence}) LCK {luck} HIT {hit} | " +
               $"Effects: {_effects}" +
               $"{(_permanentlyDefeated ? " [PERMANENTLY DEFEATED]" : "")}";
    }
}
