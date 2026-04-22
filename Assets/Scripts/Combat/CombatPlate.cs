using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// A single combat plate — the "foothold" for one unit on the battlefield.
///
/// Layout (vertical, bottom to top):
///   [HP/MP Text]     ← stats below the plate
///   [Plate Image]    ← the foothold / ground marker
///   [Unit Sprite]    ← side-view combat sprite standing on the plate
///   [Name Text]      ← unit name above everything
///
/// The unit sprite is pulled from CombatSpriteHolder (dedicated side-view art),
/// NOT from the overworld SpriteRenderer. Sprites face RIGHT by default.
/// Enemy sprites are auto-flipped horizontally to face left.
///
/// Visual: allies on the left facing right → ← enemies on the right facing left
///
/// PREFAB SETUP:
///   Create a UI panel with this hierarchy:
///     CombatPlate (root)          ← Button component for targeting clicks
///       ├── PlateImage            ← Image, the foothold graphic
///       ├── UnitSprite            ← Image, positioned ABOVE PlateImage
///       ├── NameText              ← TMP, above UnitSprite
///       ├── HPText                ← TMP, below PlateImage
///       ├── MPText                ← TMP, below HPText (hidden for enemies)
///       └── TargetHighlight       ← Image/border, DISABLED by default
///
///   Anchor UnitSprite above PlateImage so the character stands on it.
///   Set UnitSprite's Image Type to "Simple" with Preserve Aspect checked.
/// </summary>
public class CombatPlate : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector References
    // ─────────────────────────────────────────────

    [Header("Unit Display")]
    [Tooltip("Image for the side-view combat sprite. Positioned above the plate.")]
    [SerializeField] private Image unitSprite;

    [Tooltip("Text for the unit name (above the sprite).")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("Text for HP display (below the plate).")]
    [SerializeField] private TextMeshProUGUI hpText;

    [Tooltip("Text for MP display (below HP, allies only).")]
    [SerializeField] private TextMeshProUGUI mpText;

    [Header("Plate Visual")]
    [Tooltip("The plate / foothold image the unit stands on.")]
    [SerializeField] private Image plateImage;

    [SerializeField] private Color allyPlateColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    [SerializeField] private Color enemyPlateColor = new Color(0.8f, 0.3f, 0.3f, 1f);

    [Header("Targeting")]
    [Tooltip("Visual highlight shown when this plate is targeted.")]
    [SerializeField] private GameObject targetHighlight;

    [Tooltip("Button component for click detection during targeting.")]
    [SerializeField] private Button plateButton;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private ICombatant _combatant;
    private bool _isAllySide;
    private int _plateIndex;
    private Vector2 _lockedPosition;

    /// <summary>Fired when this plate is clicked during targeting mode.</summary>
    public event Action<CombatPlate> OnPlateClicked;

    // ─────────────────────────────────────────────
    //  Public Properties
    // ─────────────────────────────────────────────

    public ICombatant Combatant => _combatant;
    public bool IsAllySide => _isAllySide;
    public int PlateIndex => _plateIndex;
    public Vector2 LockedPosition => _lockedPosition;
    public bool IsOccupied => _combatant != null;
    public bool IsAlive => _combatant != null && _combatant.IsAlive;

    // ─────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (plateButton == null)
            plateButton = GetComponent<Button>();

        if (plateButton != null)
            plateButton.onClick.AddListener(() => OnPlateClicked?.Invoke(this));

        SetTargetHighlight(false);
        SetInteractable(false);
    }

    // ─────────────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────────────

    /// <summary>
    /// Assign a combatant to this plate at combat start.
    /// Pulls the side-view sprite from CombatSpriteHolder.
    /// Enemies are auto-flipped to face left.
    /// </summary>
    public void AssignCombatant(ICombatant combatant, bool isAlly, int index, Vector2 position)
    {
        _combatant = combatant;
        _isAllySide = isAlly;
        _plateIndex = index;
        _lockedPosition = position;

        // Set plate color
        if (plateImage != null)
            plateImage.color = isAlly ? allyPlateColor : enemyPlateColor;

        // Show MP only for allies
        if (mpText != null)
            mpText.gameObject.SetActive(isAlly);

        // Set name
        if (nameText != null)
            nameText.text = combatant.CombatName;

        // Set combat sprite from CombatSpriteHolder
        UpdateCombatSprite();

        // Position the plate
        SetPosition(position);

        gameObject.SetActive(true);
        RefreshDisplay();
    }

    /// <summary>
    /// Clear this plate (unit died or combat ended).
    /// </summary>
    public void Clear()
    {
        _combatant = null;
        SetTargetHighlight(false);
        SetInteractable(false);
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  Display Updates
    // ─────────────────────────────────────────────

    /// <summary>
    /// Refresh HP and MP text from the combatant's current stats.
    /// </summary>
    public void RefreshDisplay()
    {
        if (_combatant == null) return;

        if (hpText != null)
        {
            if (_combatant is PlayerStats player)
                hpText.text = $"HP {player.CurrentHP}/{player.MaxHP}";
            else if (_combatant is EnemyStats enemy)
                hpText.text = $"HP {enemy.CurrentHP}/{enemy.MaxHP}";
        }

        if (mpText != null && _isAllySide && _combatant is PlayerStats playerMP)
            mpText.text = $"MP {playerMP.CurrentMP}/{playerMP.MaxMP}";

        // Do NOT call Clear() here on death.
        // CheckForDeaths() in CombatUIManager handles removal
        // so the turn manager gets notified properly.
    }

    // ─────────────────────────────────────────────
    //  Combat Sprite
    // ─────────────────────────────────────────────

    /// <summary>
    /// Pull the side-view sprite from CombatSpriteHolder.
    /// Sprites are assigned already facing the correct direction.
    /// </summary>
    private void UpdateCombatSprite()
    {
        if (unitSprite == null || _combatant == null) return;

        MonoBehaviour combatantMB = _combatant as MonoBehaviour;
        if (combatantMB == null) return;

        // Look for dedicated combat sprite
        CombatSpriteHolder holder = combatantMB.GetComponent<CombatSpriteHolder>();
        if (holder != null && holder.CombatSprite != null)
        {
            unitSprite.sprite = holder.CombatSprite;
            unitSprite.color = Color.white;
            unitSprite.preserveAspect = true;
            return;
        }

        // Fallback: colored placeholder
        unitSprite.sprite = null;
        unitSprite.color = _isAllySide ? allyPlateColor : enemyPlateColor;
    }

    // ─────────────────────────────────────────────
    //  Positioning
    // ─────────────────────────────────────────────

    public void SetPosition(Vector2 position)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = position;
    }

    public void MoveToFrontPosition(Vector2 frontPosition)
    {
        SetPosition(frontPosition);
    }

    public void ReturnToLockedPosition()
    {
        SetPosition(_lockedPosition);
    }

    // ─────────────────────────────────────────────
    //  Targeting
    // ─────────────────────────────────────────────

    public void SetTargetHighlight(bool active)
    {
        if (targetHighlight != null)
            targetHighlight.SetActive(active);
    }

    public void SetInteractable(bool interactable)
    {
        if (plateButton != null)
            plateButton.interactable = interactable;
    }

    public void ClearClickCallback()
    {
        OnPlateClicked = null;
    }
}
