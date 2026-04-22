using UnityEngine;
using System;

/// <summary>
/// Represents a temporary buff or debuff on a combatant.
/// Duration is measured in the AFFECTED unit's turns, not global ticks.
///
/// Examples:
///   - Anchor:          Stat debuff (AGI -25%), 2 turns on target
///   - Silence:         Skill lock, 1 turn on target
///   - Wraith's Eye:    Guaranteed hit, 2 turns on self
///   - Stat buffs from items/skills follow the same pattern
/// </summary>
[System.Serializable]
public class StatusEffect
{
    public enum EffectType
    {
        // --- Stat Modifiers ---
        StatBuff,           // Increase a stat by a percentage
        StatDebuff,         // Decrease a stat by a percentage

        // --- Control ---
        Silence,            // Prevents skill usage (basic attack only)
        GuaranteedHit,      // All attacks bypass accuracy/dodge checks

        // --- Over Time ---
        HealOverTime,       // Heal X HP at start of each turn
        DamageOverTime,     // Take X damage at start of each turn
    }

    public string EffectName;
    public EffectType Type;

    /// <summary>Remaining turns on the AFFECTED unit. Decremented when they act.</summary>
    public int TurnsRemaining;

    /// <summary>
    /// Meaning depends on Type:
    ///   StatBuff/Debuff -> percentage modifier (e.g. 0.25 = 25%)
    ///   HealOverTime/DamageOverTime -> flat amount per turn
    ///   Silence/GuaranteedHit -> unused (set to 0)
    /// </summary>
    public float Value;

    /// <summary>For StatBuff/StatDebuff: which stat is affected.</summary>
    public StatType AffectedStat;

    /// <summary>Who applied this effect (for tracking / UI purposes).</summary>
    public ICombatant Source;

    /// <summary>Whether this effect stacks with itself or refreshes duration.</summary>
    public bool Stackable;

    public StatusEffect(string name, EffectType type, int turns, float value = 0f,
                        StatType affectedStat = StatType.Strength,
                        ICombatant source = null, bool stackable = true)
    {
        EffectName = name;
        Type = type;
        TurnsRemaining = turns;
        Value = value;
        AffectedStat = affectedStat;
        Source = source;
        Stackable = stackable;
    }

    public StatusEffect Clone()
    {
        return new StatusEffect(EffectName, Type, TurnsRemaining, Value, AffectedStat, Source, Stackable);
    }

    public override string ToString()
    {
        string detail = Type switch
        {
            EffectType.StatBuff   => $"+{Value:P0} {AffectedStat}",
            EffectType.StatDebuff => $"-{Value:P0} {AffectedStat}",
            EffectType.Silence    => "Silenced",
            EffectType.GuaranteedHit => "Guaranteed Hit",
            EffectType.HealOverTime  => $"+{Value} HP/turn",
            EffectType.DamageOverTime => $"-{Value} HP/turn",
            _ => ""
        };
        return $"{EffectName} ({detail}, {TurnsRemaining}t)";
    }
}
