using UnityEngine;
using System;

/// <summary>
/// Represents a temporary buff or debuff on a combatant.
/// Duration is measured in the AFFECTED unit's turns, not global ticks.
///
/// Effect Types:
///   StatBuff/Debuff  — percentage modifier to a stat
///   Silence          — prevents skill/spell usage
///   GuaranteedHit    — all attacks bypass dodge
///   HealOverTime     — heal X HP at start of each turn
///   DamageOverTime   — take X damage at start of each turn (burn, poison, etc.)
///   Freeze           — skip this unit's next turn(s)
///   DamageReduction  — reduce incoming damage by a percentage (Guard)
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
        Freeze,             // Skip this unit's turn entirely

        // --- Over Time ---
        HealOverTime,       // Heal X HP at start of each turn
        DamageOverTime,     // Take X damage at start of each turn

        // --- Defensive ---
        DamageReduction,    // Reduce incoming damage by Value% (e.g., 0.5 = 50%)
    }

    public string EffectName;
    public EffectType Type;

    /// <summary>Remaining turns on the AFFECTED unit. Decremented when they act.</summary>
    public int TurnsRemaining;

    /// <summary>
    /// Meaning depends on Type:
    ///   StatBuff/Debuff    → percentage modifier (e.g., 0.25 = 25%)
    ///   HealOverTime/DoT   → flat amount per turn
    ///   DamageReduction    → percentage (e.g., 0.5 = 50% reduction)
    ///   Silence/Freeze/Hit → unused (set to 0)
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
            EffectType.StatBuff        => $"+{Value:P0} {AffectedStat}",
            EffectType.StatDebuff      => $"-{Value:P0} {AffectedStat}",
            EffectType.Silence         => "Silenced",
            EffectType.GuaranteedHit   => "Guaranteed Hit",
            EffectType.Freeze          => "Frozen",
            EffectType.HealOverTime    => $"+{Value} HP/turn",
            EffectType.DamageOverTime  => $"-{Value} HP/turn",
            EffectType.DamageReduction => $"-{Value:P0} damage taken",
            _ => ""
        };
        return $"{EffectName} ({detail}, {TurnsRemaining}t)";
    }
}
