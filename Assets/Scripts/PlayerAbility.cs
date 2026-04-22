using UnityEngine;

/// <summary>
/// Defines a player combat ability (skill or spell).
/// Create via: Right-click → Create → Talentless Hero → Player Ability
///
/// Abilities are categorized as Skills (physical/utility) or Spells (magic).
/// The CombatUIManager reads the list of equipped abilities and populates
/// the sub-panels dynamically at combat start.
///
/// Demo abilities:
///   Skills:
///     - Combat Healing: HoT 20 HP/turn for 3 turns (self, no target needed)
///     - Power Strike: 30 base physical damage (target enemy)
///     - Guard: 50% damage reduction for 1 turn (self, no target needed)
///
///   Spells:
///     - Firebolt: 20 magic damage, 25% burn chance (5% max HP/turn, 3 turns)
///     - Ice Shard: 20 magic damage, 10% freeze chance (skip 1 turn)
/// </summary>
[CreateAssetMenu(fileName = "NewAbility", menuName = "Talentless Hero/Player Ability")]
public class PlayerAbility : ScriptableObject
{
    public enum AbilityCategory
    {
        Skill,
        Spell
    }

    public enum TargetType
    {
        Enemy,      // Must select an enemy target
        Self,       // Applies to the player automatically
        Ally        // Must select an ally (future use)
    }

    public enum DamageType
    {
        None,       // No damage (buff/heal only)
        Physical,
        Magic
    }

    [Header("Identity")]
    public string abilityName = "New Ability";

    [TextArea(1, 3)]
    public string description = "Ability description.";

    public AbilityCategory category = AbilityCategory.Skill;

    [Header("Targeting")]
    public TargetType targetType = TargetType.Enemy;

    [Header("Cost")]
    [Tooltip("MP cost to use. 0 = free.")]
    public int mpCost = 0;

    [Header("Damage")]
    public DamageType damageType = DamageType.None;

    [Tooltip("Base damage before defense/modifiers.")]
    public int baseDamage = 0;

    [Tooltip("If true, also factors in player's STR (physical) or INT (magic) scaling.")]
    public bool scalesWithStats = false;

    [Header("Status Effect on Target")]
    [Tooltip("Apply a status effect to the target on hit.")]
    public bool appliesEffect = false;

    [Tooltip("Chance to apply the effect (0-1). 1.0 = guaranteed.")]
    [Range(0f, 1f)]
    public float effectChance = 1f;

    [Tooltip("Name of the status effect (for display and stacking rules).")]
    public string effectName = "";

    [Tooltip("Type of status effect to apply.")]
    public StatusEffect.EffectType effectType = StatusEffect.EffectType.DamageOverTime;

    [Tooltip("Duration in turns.")]
    public int effectDuration = 1;

    [Tooltip("Value for the effect (damage/heal per turn, percentage, etc.).")]
    public float effectValue = 0f;

    [Tooltip("Which stat is affected (for StatBuff/StatDebuff).")]
    public StatType effectStat = StatType.Strength;

    [Tooltip("Does this effect stack or refresh?")]
    public bool effectStackable = false;

    [Header("Self Buff (Skills like Guard/Healing)")]
    [Tooltip("Apply a status effect to the PLAYER when used.")]
    public bool appliesSelfEffect = false;

    public string selfEffectName = "";
    public StatusEffect.EffectType selfEffectType = StatusEffect.EffectType.HealOverTime;
    public int selfEffectDuration = 1;
    public float selfEffectValue = 0f;
    public bool selfEffectStackable = false;

    [Header("Audio")]
    public AudioClip castSFX;

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    /// <summary>
    /// Create the target status effect for this ability.
    /// </summary>
    public StatusEffect CreateTargetEffect(ICombatant source)
    {
        return new StatusEffect(
            effectName, effectType, effectDuration, effectValue,
            effectStat, source, effectStackable
        );
    }

    /// <summary>
    /// Create the self-buff status effect for this ability.
    /// </summary>
    public StatusEffect CreateSelfEffect(ICombatant source)
    {
        return new StatusEffect(
            selfEffectName, selfEffectType, selfEffectDuration, selfEffectValue,
            StatType.Strength, source, selfEffectStackable
        );
    }

    /// <summary>
    /// Roll whether the status effect procs.
    /// </summary>
    public bool RollEffectChance()
    {
        return Random.Range(0f, 1f) < effectChance;
    }
}
