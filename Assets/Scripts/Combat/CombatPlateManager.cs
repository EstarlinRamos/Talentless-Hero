using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages combat plates for the plate-based combat system.
///
/// DEMO LAYOUT (current):
///   1 plate per side — hero on the left, enemy on the right.
///   Simple and clean. No spotlight swapping needed.
///
///   [Hero]                    [Enemy]
///
/// FUTURE LAYOUT (FGO-style):
///   Up to 3 active plates per side, arranged horizontally.
///   When an active enemy dies, the next reserve auto-swaps in.
///   Change MAX_ACTIVE_PER_SIDE to 3 to enable this.
///
///   [Ally3] [Ally2] [Ally1]   [Enemy1] [Enemy2] [Enemy3]
///                              ↑ dies → reserve swaps in
///
/// Attach to a parent GameObject that contains the plate UI elements.
/// </summary>
public class CombatPlateManager : MonoBehaviour
{
    // Demo: 1 plate per side. Future: set to 3 for FGO-style.
    [Header("Layout")]
    [Tooltip("Max plates visible per side. Demo = 1, Future = 3.")]
    [SerializeField] private int maxActivePlatesPerSide = 1;

    // ─────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────

    [Header("Plate Prefab")]
    [Tooltip("Prefab for a single combat plate. Will be instantiated as needed.")]
    [SerializeField] private CombatPlate platePrefab;

    [Header("Plate Containers")]
    [Tooltip("Parent transform for ally plates.")]
    [SerializeField] private RectTransform allyContainer;

    [Tooltip("Parent transform for enemy plates.")]
    [SerializeField] private RectTransform enemyContainer;

    [Header("Positioning")]
    [Tooltip("Horizontal spacing between plates in pixels (used when multiple plates per side).")]
    [SerializeField] private float plateSpacing = 160f;

    [Tooltip("Y position for all plates.")]
    [SerializeField] private float plateY = 0f;

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    /// <summary>Fired when a plate is clicked during targeting mode.</summary>
    public event Action<ICombatant> OnTargetSelected;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private List<CombatPlate> _allyPlates = new List<CombatPlate>();
    private List<CombatPlate> _enemyPlates = new List<CombatPlate>();

    // Reserve queues for future FGO-style swapping
    private List<ICombatant> _allyReserves = new List<ICombatant>();
    private List<ICombatant> _enemyReserves = new List<ICombatant>();

    private CombatPlate _currentSpotlightPlate = null;
    private CombatPlate _displacedFrontPlate = null;
    private bool _targetingActive = false;

    // ─────────────────────────────────────────────
    //  Public Properties
    // ─────────────────────────────────────────────

    public bool IsTargetingActive => _targetingActive;
    public IReadOnlyList<CombatPlate> AllyPlates => _allyPlates.AsReadOnly();
    public IReadOnlyList<CombatPlate> EnemyPlates => _enemyPlates.AsReadOnly();

    /// <summary>All occupied, alive plates across both sides.</summary>
    public IEnumerable<CombatPlate> AllActivePlates =>
        _allyPlates.Concat(_enemyPlates).Where(p => p.IsOccupied && p.IsAlive);

    // ═════════════════════════════════════════════
    //  Combat Setup
    // ═════════════════════════════════════════════

    /// <summary>
    /// Initialize plates for a new combat encounter.
    /// Active combatants get plates, extras go into reserves.
    /// </summary>
    public void InitializePlates(List<ICombatant> allies, List<ICombatant> enemies)
    {
        ClearAllPlates();

        // Sort allies: player always first
        var sortedAllies = allies
            .OrderByDescending(a => a is PlayerStats)
            .ToList();

        // Sort enemies: highest MaxHP first
        var sortedEnemies = enemies
            .OrderByDescending(e =>
            {
                if (e is EnemyStats es) return es.MaxHP;
                return 0;
            })
            .ToList();

        // Create active ally plates (up to max)
        int activeAllyCount = Mathf.Min(sortedAllies.Count, maxActivePlatesPerSide);
        for (int i = 0; i < activeAllyCount; i++)
        {
            Vector2 pos = GetAllyPlatePosition(i, activeAllyCount);
            CombatPlate plate = CreatePlate(allyContainer);
            plate.AssignCombatant(sortedAllies[i], true, i, pos);
            plate.OnPlateClicked += HandlePlateClicked;
            _allyPlates.Add(plate);
        }

        // Remaining allies go to reserves
        _allyReserves.Clear();
        for (int i = activeAllyCount; i < sortedAllies.Count; i++)
            _allyReserves.Add(sortedAllies[i]);

        // Create active enemy plates (up to max)
        int activeEnemyCount = Mathf.Min(sortedEnemies.Count, maxActivePlatesPerSide);
        for (int i = 0; i < activeEnemyCount; i++)
        {
            Vector2 pos = GetEnemyPlatePosition(i, activeEnemyCount);
            CombatPlate plate = CreatePlate(enemyContainer);
            plate.AssignCombatant(sortedEnemies[i], false, i, pos);
            plate.OnPlateClicked += HandlePlateClicked;
            _enemyPlates.Add(plate);
        }

        // Remaining enemies go to reserves
        _enemyReserves.Clear();
        for (int i = activeEnemyCount; i < sortedEnemies.Count; i++)
            _enemyReserves.Add(sortedEnemies[i]);

        Debug.Log($"[Plates] Active: {_allyPlates.Count} allies, {_enemyPlates.Count} enemies. " +
                  $"Reserves: {_allyReserves.Count} allies, {_enemyReserves.Count} enemies.");
    }

    /// <summary>
    /// Clean up all plates (combat ended).
    /// </summary>
    public void ClearAllPlates()
    {
        foreach (var plate in _allyPlates)
        {
            plate.ClearClickCallback();
            Destroy(plate.gameObject);
        }
        foreach (var plate in _enemyPlates)
        {
            plate.ClearClickCallback();
            Destroy(plate.gameObject);
        }

        _allyPlates.Clear();
        _enemyPlates.Clear();
        _allyReserves.Clear();
        _enemyReserves.Clear();
        _currentSpotlightPlate = null;
        _displacedFrontPlate = null;
        _targetingActive = false;
    }

    // ═════════════════════════════════════════════
    //  Plate Positions
    // ═════════════════════════════════════════════

    /// <summary>
    /// Ally plates positioned on the left side.
    /// Single plate: centered in container.
    /// Multiple plates: spread horizontally, rightmost is front.
    /// </summary>
    private Vector2 GetAllyPlatePosition(int index, int totalActive)
    {
        if (totalActive <= 1)
            return new Vector2(0f, plateY);

        // Center the group, rightmost plate is index 0 (front)
        float totalWidth = (totalActive - 1) * plateSpacing;
        float startX = totalWidth * 0.5f;
        float x = startX - (index * plateSpacing);
        return new Vector2(x, plateY);
    }

    /// <summary>
    /// Enemy plates positioned on the right side.
    /// Single plate: centered in container.
    /// Multiple plates: spread horizontally, leftmost is front.
    /// </summary>
    private Vector2 GetEnemyPlatePosition(int index, int totalActive)
    {
        if (totalActive <= 1)
            return new Vector2(0f, plateY);

        // Center the group, leftmost plate is index 0 (front)
        float totalWidth = (totalActive - 1) * plateSpacing;
        float startX = -totalWidth * 0.5f;
        float x = startX + (index * plateSpacing);
        return new Vector2(x, plateY);
    }

    // ═════════════════════════════════════════════
    //  Targeting Mode
    // ═════════════════════════════════════════════

    /// <summary>Enter targeting mode — all alive plates become clickable.</summary>
    public void EnterTargetingMode()
    {
        _targetingActive = true;

        foreach (var plate in AllActivePlates)
        {
            plate.SetInteractable(true);
            plate.SetTargetHighlight(false);
        }

        Debug.Log("[Plates] Targeting mode active.");
    }

    /// <summary>Exit targeting mode — disable all plate interaction.</summary>
    public void ExitTargetingMode()
    {
        _targetingActive = false;

        foreach (var plate in _allyPlates.Concat(_enemyPlates))
        {
            plate.SetInteractable(false);
            plate.SetTargetHighlight(false);
        }

        RestoreSpotlight();
    }

    // ═════════════════════════════════════════════
    //  Spotlight Swap
    //  Only relevant when multiple plates per side.
    //  In 1v1 demo mode, this does nothing.
    // ═════════════════════════════════════════════

    public void SpotlightTarget(CombatPlate targetPlate)
    {
        RestoreSpotlight();

        if (targetPlate == null || targetPlate.PlateIndex == 0) return;
        if (maxActivePlatesPerSide <= 1) return;

        List<CombatPlate> sidePlates = targetPlate.IsAllySide ? _allyPlates : _enemyPlates;
        CombatPlate frontPlate = sidePlates.FirstOrDefault(p => p.PlateIndex == 0);

        if (frontPlate == null || frontPlate == targetPlate) return;

        _currentSpotlightPlate = targetPlate;
        _displacedFrontPlate = frontPlate;

        targetPlate.MoveToFrontPosition(frontPlate.LockedPosition);
        frontPlate.SetPosition(targetPlate.LockedPosition);
    }

    public void RestoreSpotlight()
    {
        if (_currentSpotlightPlate != null)
        {
            _currentSpotlightPlate.ReturnToLockedPosition();
            _currentSpotlightPlate = null;
        }

        if (_displacedFrontPlate != null)
        {
            _displacedFrontPlate.ReturnToLockedPosition();
            _displacedFrontPlate = null;
        }
    }

    // ═════════════════════════════════════════════
    //  Display Refresh
    // ═════════════════════════════════════════════

    public void RefreshAllPlates()
    {
        foreach (var plate in _allyPlates.Concat(_enemyPlates))
        {
            if (plate.IsOccupied)
                plate.RefreshDisplay();
        }
    }

    public void RefreshPlate(ICombatant combatant)
    {
        CombatPlate plate = FindPlate(combatant);
        if (plate != null)
            plate.RefreshDisplay();
    }

    /// <summary>
    /// Remove a plate for a defeated combatant.
    /// If there are reserves on that side, the next one swaps in.
    /// </summary>
    public void RemovePlate(ICombatant combatant)
    {
        CombatPlate plate = FindPlate(combatant);
        if (plate == null) return;

        bool isAlly = plate.IsAllySide;
        int removedIndex = plate.PlateIndex;
        Vector2 removedPosition = plate.LockedPosition;

        plate.ClearClickCallback();
        plate.Clear();

        // Remove from active list
        if (isAlly)
            _allyPlates.Remove(plate);
        else
            _enemyPlates.Remove(plate);

        Destroy(plate.gameObject);

        // Try to swap in a reserve
        List<ICombatant> reserves = isAlly ? _allyReserves : _enemyReserves;

        if (reserves.Count > 0)
        {
            ICombatant replacement = reserves[0];
            reserves.RemoveAt(0);

            RectTransform container = isAlly ? allyContainer : enemyContainer;
            CombatPlate newPlate = CreatePlate(container);
            newPlate.AssignCombatant(replacement, isAlly, removedIndex, removedPosition);
            newPlate.OnPlateClicked += HandlePlateClicked;

            if (isAlly)
                _allyPlates.Add(newPlate);
            else
                _enemyPlates.Add(newPlate);

            Debug.Log($"[Plates] {replacement.CombatName} swapped in from reserves!");
        }

        Debug.Log($"[Plates] Removed plate for {combatant.CombatName}. " +
                  $"Reserves remaining: {reserves.Count}");
    }

    // ═════════════════════════════════════════════
    //  Queries
    // ═════════════════════════════════════════════

    public CombatPlate FindPlate(ICombatant combatant)
    {
        return _allyPlates.Concat(_enemyPlates)
            .FirstOrDefault(p => p.Combatant == combatant);
    }

    public bool HasAliveBoss()
    {
        // Check active plates
        bool onPlate = _enemyPlates.Any(p =>
            p.IsOccupied && p.IsAlive &&
            p.Combatant is EnemyStats enemy && enemy.IsBoss);

        // Also check reserves
        bool inReserve = _enemyReserves.Any(r =>
            r is EnemyStats es && es.IsBoss && es.IsAlive);

        return onPlate || inReserve;
    }

    public List<ICombatant> GetAliveCombatants(bool allySide)
    {
        var plates = allySide ? _allyPlates : _enemyPlates;
        var alive = plates
            .Where(p => p.IsOccupied && p.IsAlive)
            .Select(p => p.Combatant)
            .ToList();

        // Include reserves
        var reserves = allySide ? _allyReserves : _enemyReserves;
        alive.AddRange(reserves.Where(r => r.IsAlive));

        return alive;
    }

    // ═════════════════════════════════════════════
    //  Internal
    // ═════════════════════════════════════════════

    private CombatPlate CreatePlate(RectTransform parent)
    {
        if (platePrefab == null)
        {
            Debug.LogError("[Plates] No plate prefab assigned!");
            return null;
        }

        CombatPlate plate = Instantiate(platePrefab, parent);
        return plate;
    }

    private void HandlePlateClicked(CombatPlate plate)
    {
        if (!_targetingActive) return;
        if (plate == null || !plate.IsOccupied || !plate.IsAlive) return;

        foreach (var p in AllActivePlates)
            p.SetTargetHighlight(false);
        plate.SetTargetHighlight(true);

        SpotlightTarget(plate);
        OnTargetSelected?.Invoke(plate.Combatant);
    }
}
