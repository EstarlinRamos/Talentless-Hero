using UnityEngine;

/// <summary>
/// Base class for all enemy skills. Enemies do not use MP — skills are
/// gated by cooldowns and AI priority. Create subclasses for each unique skill.
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

    [System.NonSerialized] public int currentCooldown = 0;

    public bool IsReady => currentCooldown <= 0;

    public void TickCooldown()
    {
        if (currentCooldown > 0) currentCooldown--;
    }

    public void StartCooldown()
    {
        currentCooldown = cooldownTurns;
    }

    public void ResetCooldown()
    {
        currentCooldown = 0;
    }

    /// <summary>
    /// Whether this skill can be used given the current combat state.
    /// Override for conditional logic (e.g. "only heal below 50% HP").
    /// </summary>
    public virtual bool CanUse(EnemyStats self, PlayerStats player,
                               StatusEffectHandler selfEffects,
                               StatusEffectHandler playerEffects)
    {
        return IsReady;
    }

    /// <summary>
    /// Execute the skill and return a result for the combat log.
    /// </summary>
    public abstract SkillResult Execute(EnemyStats self, PlayerStats player,
                                        StatusEffectHandler selfEffects,
                                        StatusEffectHandler playerEffects,
                                        CombatTurnManager turnManager);

    public struct SkillResult
    {
        public string SkillName;
        public string LogMessage;
        public int DamageDealt;
        public int HealingDone;
        public bool AppliedEffect;
    }
}
