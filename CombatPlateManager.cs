using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages all combat plates for both ally and enemy sides.
///
/// Layout: Up to 5 plates per side in a ]• •[ arrangement.
///   - Ally plates: front (index 0) is rightmost, back is leftmost.
///   - Enemy plates: front (index 0) is leftmost, back is rightmost.
///   - Positions are locked at combat start.
///
/// Targeting spotlight swaps the targeted unit to the front plate position,
/// then restores positions after the action resolves.
///
/// Sorting: Player is always the front ally plate. Highest MaxHP enemy is front.
/// </summary>
public class CombatPlateManager : MonoBehaviour
{
    public const int MAX_PLATES_PER_SIDE = 5;

    [Header("Plate Prefab")]
    [Tooltip("Prefab for a single combat plate. Will be instantiated as needed.")]
    [SerializeField] private CombatPlate platePrefab;

    [Header("Plate Containers")]
    [Tooltip("Parent transform for ally plates.")]
    [SerializeField] private RectTransform allyContainer;

    [Tooltip("Parent transform for enemy plates.")]
    [SerializeField] private RectTransform enemyContainer;

    [Header("Layout Settings")]
    [Tooltip("Horizontal spacing between plates in pixels.")]
    [SerializeField] private float plateSpacing = 120f;

    [Tooltip("X offset for the front plate from the center of the container.")]
    [SerializeField] private float frontPlateOffset = 60f;

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
    private CombatPlate _currentSpotlightPlate = null;
    private CombatPlate _displacedFrontPlate = null;
    private bool _targetingActive = false;

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
    /// </summary>
    public void InitializePlates(List<ICombatant> allies, List<ICombatant> enemies)
    {
        ClearAllPlates();

        var sortedAllies = allies
            .OrderByDescending(a => a is PlayerStats)
            .ToList();

        var sortedEnemies = enemies
            .OrderByDescending(e =>
            {
                if (e is EnemyStats es) return es.MaxHP;
                return 0;
            })
            .ToList();

        for (int i = 0; i < Mathf.Min(sortedAllies.Count, MAX_PLATES_PER_SIDE); i++)
        {
            Vector2 pos = GetAllyPlatePosition(i);
            CombatPlate plate = CreatePlate(allyContainer);
            plate.AssignCombatant(sortedAllies[i], true, i, pos);
            plate.OnPlateClicked += HandlePlateClicked;
            _allyPlates.Add(plate);
        }

        for (int i = 0; i < Mathf.Min(sortedEnemies.Count, MAX_PLATES_PER_SIDE); i++)
        {
            Vector2 pos = GetEnemyPlatePosition(i);
            CombatPlate plate = CreatePlate(enemyContainer);
            plate.AssignCombatant(sortedEnemies[i], false, i, pos);
            plate.OnPlateClicked += HandlePlateClicked;
            _enemyPlates.Add(plate);
        }

        Debug.Log($"[Plates] Initialized {_allyPlates.Count} ally plates, " +
                  $"{_enemyPlates.Count} enemy plates.");
    }

    /// <summary>
    /// Clean up all plates at combat end.
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
        _currentSpotlightPlate = null;
        _displacedFrontPlate = null;
        _targetingActive = false;
    }

    // ═════════════════════════════════════════════
    //  Plate Positions
    // ═════════════════════════════════════════════

    /// <summary>Ally layout: [4] [3] [2] [1] [0] → center</summary>
    private Vector2 GetAllyPlatePosition(int index)
    {
        float x = frontPlateOffset - (index * plateSpacing);
        return new Vector2(x, plateY);
    }

    /// <summary>Enemy layout: center ← [0] [1] [2] [3] [4]</summary>
    private Vector2 GetEnemyPlatePosition(int index)
    {
        float x = -frontPlateOffset + (index * plateSpacing);
        return new Vector2(x, plateY);
    }

    // ═════════════════════════════════════════════
    //  Targeting Mode
    // ═════════════════════════════════════════════

    /// <summary>
    /// Enter targeting mode — all alive plates become clickable.
    /// </summary>
    public void EnterTargetingMode()
    {
        _targetingActive = true;

        foreach (var plate in AllActivePlates)
        {
            plate.SetInteractable(true);
            plate.SetTargetHighlight(false);
        }

        Debug.Log("[Plates] Targeting mode active. Click a plate to select target.");
    }

    /// <summary>
    /// Exit targeting mode — disable all plate interaction.
    /// </summary>
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
    // ═════════════════════════════════════════════

    /// <summary>
    /// Swap the targeted unit to the front plate position on their side.
    /// The front plate unit temporarily takes the targeted unit's position.
    /// </summary>
    public void SpotlightTarget(CombatPlate targetPlate)
    {
        RestoreSpotlight();

        if (targetPlate == null || targetPlate.PlateIndex == 0) return;

        List<CombatPlate> sidePlates = targetPlate.IsAllySide ? _allyPlates : _enemyPlates;
        CombatPlate frontPlate = sidePlates.FirstOrDefault(p => p.PlateIndex == 0);

        if (frontPlate == null || frontPlate == targetPlate) return;

        _currentSpotlightPlate = targetPlate;
        _displacedFrontPlate = frontPlate;

        targetPlate.MoveToFrontPosition(frontPlate.LockedPosition);
        frontPlate.SetPosition(targetPlate.LockedPosition);

        Debug.Log($"[Plates] Spotlight: {targetPlate.Combatant.CombatName} → front plate");
    }

    /// <summary>
    /// Restore the spotlight swap — both plates return to locked positions.
    /// </summary>
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

    /// <summary>
    /// Refresh all plate HP/MP displays.
    /// </summary>
    public void RefreshAllPlates()
    {
        foreach (var plate in _allyPlates.Concat(_enemyPlates))
        {
            if (plate.IsOccupied)
                plate.RefreshDisplay();
        }
    }

    /// <summary>
    /// Refresh a specific combatant's plate.
    /// </summary>
    public void RefreshPlate(ICombatant combatant)
    {
        CombatPlate plate = FindPlate(combatant);
        if (plate != null)
            plate.RefreshDisplay();
    }

    /// <summary>
    /// Remove a plate for a defeated combatant.
    /// </summary>
    public void RemovePlate(ICombatant combatant)
    {
        CombatPlate plate = FindPlate(combatant);
        if (plate == null) return;

        plate.ClearClickCallback();
        plate.Clear();

        Debug.Log($"[Plates] Removed plate for {combatant.CombatName}");
    }

    // ═════════════════════════════════════════════
    //  Queries
    // ═════════════════════════════════════════════

    public CombatPlate FindPlate(ICombatant combatant)
    {
        return _allyPlates.Concat(_enemyPlates)
            .FirstOrDefault(p => p.Combatant == combatant);
    }

    /// <summary>
    /// Returns true if any alive enemy on a plate is a boss.
    /// Used to determine if fleeing is allowed.
    /// </summary>
    public bool HasAliveBoss()
    {
        return _enemyPlates.Any(p =>
            p.IsOccupied && p.IsAlive &&
            p.Combatant is EnemyStats enemy && enemy.IsBoss);
    }

    /// <summary>
    /// Get all alive combatants on a given side.
    /// </summary>
    public List<ICombatant> GetAliveCombatants(bool allySide)
    {
        var plates = allySide ? _allyPlates : _enemyPlates;
        return plates
            .Where(p => p.IsOccupied && p.IsAlive)
            .Select(p => p.Combatant)
            .ToList();
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

        Debug.Log($"[Plates] Target selected: {plate.Combatant.CombatName}");
    }
}
