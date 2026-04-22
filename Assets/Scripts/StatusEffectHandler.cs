using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages active status effects on a combatant (player or enemy).
/// Attach alongside PlayerStats or EnemyStats.
///
/// Call OnTurnStart() at the beginning of this unit's turn to:
///   1. Check for Freeze (skip turn if frozen)
///   2. Apply over-time effects (HoT, DoT/Burn)
///   3. Decrement durations
///   4. Remove expired effects
///
/// The stat and combat systems query this handler for active modifiers.
/// </summary>
public class StatusEffectHandler : MonoBehaviour
{
    public event Action<StatusEffect> OnEffectApplied;
    public event Action<StatusEffect> OnEffectExpired;
    public event Action<StatusEffect> OnEffectTick;

    private List<StatusEffect> _activeEffects = new List<StatusEffect>();

    /// <summary>All currently active effects (read-only access).</summary>
    public IReadOnlyList<StatusEffect> ActiveEffects => _activeEffects.AsReadOnly();

    // ═════════════════════════════════════════════
    //  Applying & Removing Effects
    // ═════════════════════════════════════════════

    /// <summary>
    /// Apply a new status effect. If not stackable and an effect with the
    /// same name already exists, refreshes the duration instead.
    /// </summary>
    public void ApplyEffect(StatusEffect effect)
    {
        if (!effect.Stackable)
        {
            var existing = _activeEffects.FirstOrDefault(e => e.EffectName == effect.EffectName);
            if (existing != null)
            {
                existing.TurnsRemaining = Mathf.Max(existing.TurnsRemaining, effect.TurnsRemaining);
                existing.Value = effect.Value;
                Debug.Log($"[Status] {gameObject.name}: {effect.EffectName} refreshed " +
                          $"({existing.TurnsRemaining} turns)");
                return;
            }
        }

        _activeEffects.Add(effect.Clone());
        Debug.Log($"[Status] {gameObject.name}: {effect} applied");
        OnEffectApplied?.Invoke(effect);
    }

    /// <summary>Remove all instances of a named effect.</summary>
    public void RemoveEffect(string effectName)
    {
        _activeEffects.RemoveAll(e =>
        {
            if (e.EffectName == effectName)
            {
                Debug.Log($"[Status] {gameObject.name}: {e.EffectName} removed");
                OnEffectExpired?.Invoke(e);
                return true;
            }
            return false;
        });
    }

    /// <summary>Remove all debuffs.</summary>
    public void ClearDebuffs()
    {
        _activeEffects.RemoveAll(e =>
        {
            bool isDebuff = e.Type == StatusEffect.EffectType.StatDebuff ||
                            e.Type == StatusEffect.EffectType.Silence ||
                            e.Type == StatusEffect.EffectType.DamageOverTime ||
                            e.Type == StatusEffect.EffectType.Freeze;
            if (isDebuff) OnEffectExpired?.Invoke(e);
            return isDebuff;
        });
    }

    /// <summary>Remove all buffs.</summary>
    public void ClearBuffs()
    {
        _activeEffects.RemoveAll(e =>
        {
            bool isBuff = e.Type == StatusEffect.EffectType.StatBuff ||
                          e.Type == StatusEffect.EffectType.GuaranteedHit ||
                          e.Type == StatusEffect.EffectType.HealOverTime ||
                          e.Type == StatusEffect.EffectType.DamageReduction;
            if (isBuff) OnEffectExpired?.Invoke(e);
            return isBuff;
        });
    }

    /// <summary>Clear everything (combat ended, resting at inn).</summary>
    public void ClearAll()
    {
        _activeEffects.Clear();
    }

    // ═════════════════════════════════════════════
    //  Turn Processing
    // ═════════════════════════════════════════════

    /// <summary>
    /// Call at the START of this combatant's turn.
    /// Processes over-time effects, then decrements durations.
    /// Returns total DoT damage taken (for combat log / UI).
    ///
    /// IMPORTANT: Check IsFrozen BEFORE calling this. If frozen,
    /// the combat system should skip the turn entirely but still
    /// call OnTurnStart to decrement the freeze duration.
    /// </summary>
    public int OnTurnStart()
    {
        int totalDot = 0;
        int totalHot = 0;

        // Process over-time effects
        foreach (var effect in _activeEffects)
        {
            if (effect.Type == StatusEffect.EffectType.DamageOverTime)
            {
                totalDot += Mathf.RoundToInt(effect.Value);
                OnEffectTick?.Invoke(effect);
            }
            else if (effect.Type == StatusEffect.EffectType.HealOverTime)
            {
                totalHot += Mathf.RoundToInt(effect.Value);
                OnEffectTick?.Invoke(effect);
            }
        }

        // Apply HoT healing directly if this is a player
        if (totalHot > 0)
        {
            PlayerStats ps = GetComponent<PlayerStats>();
            if (ps != null)
            {
                ps.HealHP(totalHot);
                Debug.Log($"[Status] {gameObject.name} healed {totalHot} HP from HoT effects.");
            }
        }

        // Decrement durations and remove expired
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            _activeEffects[i].TurnsRemaining--;
            if (_activeEffects[i].TurnsRemaining <= 0)
            {
                Debug.Log($"[Status] {gameObject.name}: {_activeEffects[i].EffectName} expired");
                OnEffectExpired?.Invoke(_activeEffects[i]);
                _activeEffects.RemoveAt(i);
            }
        }

        return totalDot;
    }

    // ═════════════════════════════════════════════
    //  Queries
    // ═════════════════════════════════════════════

    /// <summary>Get the total percentage modifier for a stat.</summary>
    public float GetStatMultiplier(StatType stat)
    {
        float multiplier = 1f;

        foreach (var effect in _activeEffects)
        {
            if (effect.AffectedStat != stat) continue;

            if (effect.Type == StatusEffect.EffectType.StatBuff)
                multiplier *= (1f + effect.Value);
            else if (effect.Type == StatusEffect.EffectType.StatDebuff)
                multiplier *= (1f - effect.Value);
        }

        return multiplier;
    }

    /// <summary>
    /// Get the damage reduction multiplier from DamageReduction effects.
    /// Returns 0.5 if Guard is active (50% reduction).
    /// Multiple reductions stack multiplicatively.
    /// </summary>
    public float GetDamageMultiplier()
    {
        float multiplier = 1f;

        foreach (var effect in _activeEffects)
        {
            if (effect.Type == StatusEffect.EffectType.DamageReduction)
                multiplier *= (1f - effect.Value);
        }

        return multiplier;
    }

    /// <summary>Is this combatant currently silenced?</summary>
    public bool IsSilenced => _activeEffects.Any(e => e.Type == StatusEffect.EffectType.Silence);

    /// <summary>Does this combatant currently have guaranteed hit?</summary>
    public bool HasGuaranteedHit => _activeEffects.Any(e => e.Type == StatusEffect.EffectType.GuaranteedHit);

    /// <summary>Is this combatant currently frozen (skip turn)?</summary>
    public bool IsFrozen => _activeEffects.Any(e => e.Type == StatusEffect.EffectType.Freeze);

    /// <summary>Is this combatant currently guarding (damage reduction)?</summary>
    public bool IsGuarding => _activeEffects.Any(e => e.Type == StatusEffect.EffectType.DamageReduction);

    /// <summary>Check if a specific named effect is active.</summary>
    public bool HasEffect(string effectName) =>
        _activeEffects.Any(e => e.EffectName == effectName);

    /// <summary>Get total HoT value per turn.</summary>
    public int GetHealPerTurn() =>
        Mathf.RoundToInt(_activeEffects
            .Where(e => e.Type == StatusEffect.EffectType.HealOverTime)
            .Sum(e => e.Value));

    /// <summary>Summary string for debug / UI.</summary>
    public override string ToString()
    {
        if (_activeEffects.Count == 0) return "No active effects";
        return string.Join(", ", _activeEffects.Select(e => e.ToString()));
    }
}
