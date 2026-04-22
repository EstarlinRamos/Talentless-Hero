using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Epic Seven-style Combat Readiness (CR) turn system.
///
/// Each tick, all combatants gain CR proportional to their Agility. When CR
/// reaches 100+, that combatant acts (ties broken by highest CR, then AGI).
/// After acting, CR resets to 0. A unit with 2x the AGI gets ~2 turns per 1
/// of the slower unit, enabling turn lapping.
///
/// Skills and buffs can push/pull CR directly for additional turn control.
/// </summary>
public class CombatTurnManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when a combatant's CR reaches 100+ and it's their turn.</summary>
    public event Action<ICombatant> OnTurnReady;

    /// <summary>Fired every tick with all combatants and their CR values.</summary>
    public event Action<IReadOnlyList<CombatantState>> OnTickUpdate;

    /// <summary>Fired when combat ends.</summary>
    public event Action OnCombatEnded;

    // ─────────────────────────────────────────────
    //  Settings
    // ─────────────────────────────────────────────

    [Header("Tick Settings")]
    [Tooltip("Base CR gained per tick is Agility / this divisor.")]
    [SerializeField] private float agilityDivisor = 10f;

    [Tooltip("Maximum number of ticks to simulate per frame to prevent infinite loops.")]
    [SerializeField] private int maxTicksPerAdvance = 1000;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private List<CombatantState> _combatants = new List<CombatantState>();
    private bool _inCombat = false;
    private bool _waitingForAction = false;

    // ─────────────────────────────────────────────
    //  Combat Lifecycle
    // ─────────────────────────────────────────────

    /// <summary>
    /// Initialize combat with a set of participants.
    /// </summary>
    public void StartCombat(IEnumerable<ICombatant> participants)
    {
        _combatants.Clear();

        foreach (var p in participants)
        {
            _combatants.Add(new CombatantState(p));
        }

        _inCombat = true;
        _waitingForAction = false;

        Debug.Log($"[TurnManager] Combat started with {_combatants.Count} combatants.");
        LogTurnOrder();

        AdvanceToNextTurn();
    }

    /// <summary>
    /// Call after the current combatant finishes their action.
    /// Resets their CR and resumes ticking toward the next turn.
    /// </summary>
    public void EndTurn()
    {
        if (!_waitingForAction) return;
        _waitingForAction = false;

        AdvanceToNextTurn();
    }

    /// <summary>
    /// Remove a combatant from combat (e.g. defeated).
    /// </summary>
    public void RemoveCombatant(ICombatant combatant)
    {
        _combatants.RemoveAll(c => c.Combatant == combatant);

        Debug.Log($"[TurnManager] {combatant.CombatName} removed from combat. " +
                  $"{_combatants.Count} remain.");

        bool anyPlayers = _combatants.Any(c => c.Combatant.IsPlayerSide);
        bool anyEnemies = _combatants.Any(c => !c.Combatant.IsPlayerSide);

        if (!anyPlayers || !anyEnemies)
        {
            EndCombat();
        }
    }

    /// <summary>
    /// End combat manually.
    /// </summary>
    public void EndCombat()
    {
        _inCombat = false;
        _waitingForAction = false;
        Debug.Log("[TurnManager] Combat ended.");
        OnCombatEnded?.Invoke();
    }

    // ─────────────────────────────────────────────
    //  CR Manipulation
    // ─────────────────────────────────────────────

    /// <summary>
    /// Increase a combatant's CR by a percentage (0-100).
    /// </summary>
    public void PushCR(ICombatant target, float amount)
    {
        var state = GetState(target);
        if (state == null) return;

        state.CombatReadiness = Mathf.Min(state.CombatReadiness + amount, 100f);
        Debug.Log($"[TurnManager] {target.CombatName} CR pushed +{amount:F1}% " +
                  $"(now {state.CombatReadiness:F1}%)");
    }

    /// <summary>
    /// Decrease a combatant's CR by a percentage.
    /// </summary>
    public void PullCR(ICombatant target, float amount)
    {
        var state = GetState(target);
        if (state == null) return;

        state.CombatReadiness = Mathf.Max(state.CombatReadiness - amount, 0f);
        Debug.Log($"[TurnManager] {target.CombatName} CR pulled -{amount:F1}% " +
                  $"(now {state.CombatReadiness:F1}%)");
    }

    // ─────────────────────────────────────────────
    //  Queries
    // ─────────────────────────────────────────────

    /// <summary>
    /// Snapshot of all combatants and their CR, sorted by CR descending.
    /// </summary>
    public IReadOnlyList<CombatantState> GetTurnOrder()
    {
        return _combatants
            .OrderByDescending(c => c.CombatReadiness)
            .ThenByDescending(c => c.Combatant.CombatAgility)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Simulate upcoming turns without modifying real state.
    /// </summary>
    public List<ICombatant> PreviewTurnOrder(int turnsToPreview)
    {
        var simulated = _combatants.Select(c => new CombatantState(c)).ToList();
        var result = new List<ICombatant>();

        for (int safety = 0; safety < turnsToPreview * maxTicksPerAdvance; safety++)
        {
            foreach (var s in simulated)
                s.CombatReadiness += s.Combatant.CombatAgility / agilityDivisor;

            var ready = simulated
                .Where(s => s.CombatReadiness >= 100f)
                .OrderByDescending(s => s.CombatReadiness)
                .ThenByDescending(s => s.Combatant.CombatAgility)
                .ToList();

            foreach (var r in ready)
            {
                result.Add(r.Combatant);
                r.CombatReadiness = 0f;
                if (result.Count >= turnsToPreview) return result;
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────
    //  Tick Simulation
    // ─────────────────────────────────────────────

    private void AdvanceToNextTurn()
    {
        if (!_inCombat || _combatants.Count == 0) return;

        for (int tick = 0; tick < maxTicksPerAdvance; tick++)
        {
            foreach (var state in _combatants)
            {
                float crGain = state.Combatant.CombatAgility / agilityDivisor;
                state.CombatReadiness += crGain;
            }

            OnTickUpdate?.Invoke(_combatants.AsReadOnly());

            var ready = _combatants
                .Where(s => s.CombatReadiness >= 100f)
                .OrderByDescending(s => s.CombatReadiness)
                .ThenByDescending(s => s.Combatant.CombatAgility)
                .FirstOrDefault();

            if (ready != null)
            {
                ready.CombatReadiness = 0f;
                _waitingForAction = true;

                Debug.Log($"[TurnManager] >> {ready.Combatant.CombatName}'s turn! " +
                          $"(AGI: {ready.Combatant.CombatAgility})");

                OnTurnReady?.Invoke(ready.Combatant);
                return;
            }
        }

        Debug.LogWarning("[TurnManager] Hit max ticks without a turn. Check agility values.");
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private CombatantState GetState(ICombatant combatant)
    {
        return _combatants.FirstOrDefault(c => c.Combatant == combatant);
    }

    private void LogTurnOrder()
    {
        var preview = PreviewTurnOrder(10);
        string order = string.Join(" → ", preview.Select(p => p.CombatName));
        Debug.Log($"[TurnManager] Projected first 10 turns: {order}");
    }

    // ─────────────────────────────────────────────
    //  Inner Types
    // ─────────────────────────────────────────────

    /// <summary>
    /// Tracks a combatant's CR during battle.
    /// </summary>
    public class CombatantState
    {
        public ICombatant Combatant { get; private set; }
        public float CombatReadiness { get; set; }

        public CombatantState(ICombatant combatant)
        {
            Combatant = combatant;
            CombatReadiness = 0f;
        }

        /// <summary>Copy constructor for simulation previews.</summary>
        public CombatantState(CombatantState other)
        {
            Combatant = other.Combatant;
            CombatReadiness = other.CombatReadiness;
        }
    }
}

/// <summary>
/// Any entity that can participate in the CR-based turn system.
/// Implemented by PlayerStats and EnemyStats.
/// </summary>
public interface ICombatant
{
    string CombatName { get; }
    int CombatAgility { get; }
    bool IsPlayerSide { get; }
    bool IsAlive { get; }
}
