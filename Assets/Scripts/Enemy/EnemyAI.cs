using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enemy combat AI for Talentless Hero.
///
/// Decision priority:
///   1. Setup skills (self-buffs, player debuffs) — if any are ready and conditions met
///   2. Offensive skills — if ready
///   3. Utility skills (heals etc.) — if conditions met
///   4. Basic Attack — always available as fallback
///
/// Within each category, skills are sorted by aiPriority (highest first).
/// The AI is intentionally simple and deterministic — the Wraith will
/// ALWAYS try to set up before going on offense. This makes the fight
/// learnable and strategic rather than random.
///
/// Attach alongside EnemyStats and StatusEffectHandler.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Skills")]
    [Tooltip("All skills this enemy has. Configure in Inspector or via ScriptableObjects.")]
    [SerializeField] private List<EnemySkill> skills = new List<EnemySkill>();

    [Header("Basic Attack Settings")]
    [Tooltip("Damage type for basic attack.")]
    [SerializeField] private bool usesMagicBasicAttack = false;

    [Tooltip("Basic attack damage multiplier (relative to phys or magic damage).")]
    [SerializeField] private float basicAttackMultiplier = 1.0f;

    // Cached references
    private EnemyStats _stats;
    private StatusEffectHandler _selfEffects;

    private void Awake()
    {
        _stats = GetComponent<EnemyStats>();
        _selfEffects = GetComponent<StatusEffectHandler>();
    }

    /// <summary>
    /// Reset all skill cooldowns (e.g. at combat start or respawn).
    /// </summary>
    public void ResetCooldowns()
    {
        foreach (var skill in skills)
            skill.ResetCooldown();
    }

    /// <summary>
    /// Called by the combat manager when it's this enemy's turn.
    /// Decides and executes an action, then returns the result.
    /// </summary>
    public EnemySkill.SkillResult DecideAndAct(PlayerStats player,
                                                StatusEffectHandler playerEffects,
                                                CombatTurnManager turnManager)
    {
        // Tick all cooldowns at the start of our turn
        foreach (var skill in skills)
            skill.TickCooldown();

        // Process status effects on self (DoT, duration ticks)
        int dotDamage = _selfEffects.OnTurnStart();
        if (dotDamage > 0)
        {
            _stats.TakeDamage(dotDamage);
            Debug.Log($"[AI] {_stats.EnemyName} took {dotDamage} DoT damage.");
        }

        // --- Decision Logic ---
        // Try each category in priority order: Setup → Offensive → Utility
        EnemySkill chosen = TryPickSkill(EnemySkill.SkillCategory.Setup, player, playerEffects);

        if (chosen == null)
            chosen = TryPickSkill(EnemySkill.SkillCategory.Offensive, player, playerEffects);

        if (chosen == null)
            chosen = TryPickSkill(EnemySkill.SkillCategory.Utility, player, playerEffects);

        if (chosen != null)
        {
            Debug.Log($"[AI] {_stats.EnemyName} chose: {chosen.skillName}");
            return chosen.Execute(_stats, player, _selfEffects, playerEffects, turnManager);
        }

        // Fallback: Basic Attack
        return ExecuteBasicAttack(player, playerEffects);
    }

    /// <summary>
    /// Try to pick the highest-priority ready skill from a category.
    /// Returns null if nothing is available.
    /// </summary>
    private EnemySkill TryPickSkill(EnemySkill.SkillCategory category,
                                     PlayerStats player,
                                     StatusEffectHandler playerEffects)
    {
        return skills
            .Where(s => s.category == category &&
                        s.CanUse(_stats, player, _selfEffects, playerEffects))
            .OrderByDescending(s => s.aiPriority)
            .FirstOrDefault();
    }

    /// <summary>
    /// Basic attack — always available, uses physical or magic damage.
    /// </summary>
    private EnemySkill.SkillResult ExecuteBasicAttack(PlayerStats player,
                                                       StatusEffectHandler playerEffects)
    {
        // Get effective stats (with status effect modifiers applied)
        float effectiveStr = _stats.Strength * _selfEffects.GetStatMultiplier(StatType.Strength);
        float effectiveInt = _stats.Intelligence * _selfEffects.GetStatMultiplier(StatType.Intelligence);

        float rawDamage;
        float defense;
        string damageType;

        if (usesMagicBasicAttack)
        {
            rawDamage = effectiveInt * 1.5f * basicAttackMultiplier;
            defense = player.MagicDefense;
            damageType = "magic";
        }
        else
        {
            rawDamage = effectiveStr * 1.5f * basicAttackMultiplier;
            defense = player.PhysicalDefense;
            damageType = "physical";
        }

        float reducedDamage = Mathf.Max(rawDamage - defense, 1f);
        int finalDamage = Mathf.RoundToInt(reducedDamage);

        // Hit check
        bool hits = _selfEffects.HasGuaranteedHit || _stats.RollHit(player.DodgeChance);

        if (!hits)
        {
            string missLog = $"{_stats.EnemyName} attacked but missed!";
            Debug.Log($"[AI] {missLog}");
            return new EnemySkill.SkillResult
            {
                SkillName = "Basic Attack",
                LogMessage = missLog,
                DamageDealt = 0,
                HealingDone = 0,
                AppliedEffect = false
            };
        }

        // Crit check
        bool crit = _stats.RollCrit();
        if (crit)
        {
            finalDamage = Mathf.RoundToInt(finalDamage * _stats.CritDamageMultiplier);
        }

        int actualDamage = player.TakeDamage(finalDamage);

        string log = crit
            ? $"{_stats.EnemyName} CRIT! Basic attack dealt {actualDamage} {damageType} damage!"
            : $"{_stats.EnemyName} used basic attack. Dealt {actualDamage} {damageType} damage.";
        Debug.Log($"[AI] {log}");

        return new EnemySkill.SkillResult
        {
            SkillName = "Basic Attack",
            LogMessage = log,
            DamageDealt = actualDamage,
            HealingDone = 0,
            AppliedEffect = false
        };
    }
}
