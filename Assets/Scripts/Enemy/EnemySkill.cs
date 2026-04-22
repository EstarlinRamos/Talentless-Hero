using UnityEngine;

/// <summary>
/// Base class for all enemy skills. Create subclasses for each unique skill.
/// Enemies do NOT use MP — skills are gated by cooldowns and AI priority.
///
/// Each skill defines:
///   - A cooldown (turns until it can be used again after use)
///   - A priority category (Setup, Offensive, Utility) for AI decision-making
///   - An Execute method containing the skill's actual logic
/// </summary>
public abstract class EnemySkill : ScriptableObject
{
    public enum SkillCategory
    {
        Setup,      // Buffs self or debuffs player — AI tries these first
        Offensive,  // Deals damage (possibly with side effects)
        Utility     // Situational (heals, cleanses, etc.)
    }

    [Header("Skill Info")]
    public string skillName = "Unnamed Skill";
    [TextArea] public string description = "";

    [Header("Cooldown")]
    [Tooltip("Turns after use before this skill can be used again. 0 = no cooldown.")]
    public int cooldownTurns = 0;

    [Header("AI Behavior")]
    public SkillCategory category = SkillCategory.Offensive;

    [Tooltip("Higher = AI prefers this skill over others in the same category.")]
    [Range(0, 100)]
    public int aiPriority = 50;

    /// <summary>Remaining cooldown. 0 = ready to use.</summary>
    [System.NonSerialized] public int currentCooldown = 0;

    public bool IsReady => currentCooldown <= 0;

    /// <summary>
    /// Tick the cooldown down by 1. Call at the start of this enemy's turn.
    /// </summary>
    public void TickCooldown()
    {
        if (currentCooldown > 0) currentCooldown--;
    }

    /// <summary>
    /// Put the skill on cooldown after use.
    /// </summary>
    public void StartCooldown()
    {
        currentCooldown = cooldownTurns;
    }

    /// <summary>
    /// Reset cooldown (e.g. combat start, respawn).
    /// </summary>
    public void ResetCooldown()
    {
        currentCooldown = 0;
    }

    /// <summary>
    /// Can this skill be used right now given the combat state?
    /// Override for conditional logic (e.g. "only use heal below 50% HP").
    /// </summary>
    public virtual bool CanUse(EnemyStats self, PlayerStats player,
                               StatusEffectHandler selfEffects,
                               StatusEffectHandler playerEffects)
    {
        return IsReady;
    }

    /// <summary>
    /// Execute the skill. Returns a result describing what happened (for combat log).
    /// </summary>
    public abstract SkillResult Execute(EnemyStats self, PlayerStats player,
                                        StatusEffectHandler selfEffects,
                                        StatusEffectHandler playerEffects,
                                        CombatTurnManager turnManager);

    /// <summary>
    /// Data returned after a skill executes — feed this to your combat UI/log.
    /// </summary>
    public struct SkillResult
    {
        public string SkillName;
        public string LogMessage;
        public int DamageDealt;
        public int HealingDone;
        public bool AppliedEffect;
    }
}
