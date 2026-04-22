using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages active status effects on a combatant (player or enemy).
/// Attach alongside PlayerStats or EnemyStats.
///
/// Call OnTurnStart() at the beginning of this unit's turn to:
///   1. Apply over-time effects (HoT, DoT)
///   2. Decrement durations
///   3. Remove expired effects
///
/// The stat system queries this handler for active modifiers.
/// </summary>
public class StatusEffectHandler : MonoBehaviour
{
    public event Action<StatusEffect> OnEffectApplied;
    public event Action<StatusEffect> OnEffectExpired;
    public event Action<StatusEffect> OnEffectTick;   // For DoT/HoT per-turn triggers

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
                // Refresh duration to the higher value
                existing.TurnsRemaining = Mathf.Max(existing.TurnsRemaining, effect.TurnsRemaining);
                // Update value in case it changed (e.g. stronger version of same debuff)
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

    /// <summary>
    /// Remove all instances of a named effect (e.g. for a cleanse skill).
    /// </summary>
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

    /// <summary>
    /// Remove all debuffs (for a full cleanse).
    /// </summary>
    public void ClearDebuffs()
    {
        _activeEffects.RemoveAll(e =>
        {
            bool isDebuff = e.Type == StatusEffect.EffectType.StatDebuff ||
                            e.Type == StatusEffect.EffectType.Silence ||
                            e.Type == StatusEffect.EffectType.DamageOverTime;
            if (isDebuff) OnEffectExpired?.Invoke(e);
            return isDebuff;
        });
    }

    /// <summary>
    /// Remove all buffs (for a dispel skill).
    /// </summary>
    public void ClearBuffs()
    {
        _activeEffects.RemoveAll(e =>
        {
            bool isBuff = e.Type == StatusEffect.EffectType.StatBuff ||
                          e.Type == StatusEffect.EffectType.GuaranteedHit ||
                          e.Type == StatusEffect.EffectType.HealOverTime;
            if (isBuff) OnEffectExpired?.Invoke(e);
            return isBuff;
        });
    }

    /// <summary>
    /// Clear everything (e.g. combat ended, resting at inn).
    /// </summary>
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
    /// </summary>
    public int OnTurnStart()
    {
        int totalDot = 0;

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
                OnEffectTick?.Invoke(effect);
                // Actual healing should be applied by the combat manager
                // using the Value from this effect
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
    //  Queries — Used by combat system & stat getters
    // ═════════════════════════════════════════════

    /// <summary>
    /// Get the total percentage modifier for a stat from all active effects.
    /// Returns a multiplier (e.g. 0.75 if debuffed by 25%, 1.25 if buffed by 25%).
    /// Stacking: multiple debuffs multiply together.
    ///   Two 25% AGI debuffs = 0.75 * 0.75 = 0.5625 (43.75% total reduction).
    /// </summary>
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

    /// <summary>Is this combatant currently silenced?</summary>
    public bool IsSilenced => _activeEffects.Any(e => e.Type == StatusEffect.EffectType.Silence);

    /// <summary>Does this combatant currently have guaranteed hit?</summary>
    public bool HasGuaranteedHit => _activeEffects.Any(e => e.Type == StatusEffect.EffectType.GuaranteedHit);

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
