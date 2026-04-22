using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// A single combat plate representing one unit on the battlefield.
/// Displays HP (and MP for allies) text beneath the plate and handles
/// click detection for the targeting system.
/// </summary>
public class CombatPlate : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Image unitSprite;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI mpText;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("Targeting")]
    [SerializeField] private GameObject targetHighlight;
    [SerializeField] private Button plateButton;

    [Header("Plate Visual")]
    [SerializeField] private Image plateImage;
    [SerializeField] private Color allyPlateColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    [SerializeField] private Color enemyPlateColor = new Color(0.8f, 0.3f, 0.3f, 1f);

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
    /// </summary>
    public void AssignCombatant(ICombatant combatant, bool isAlly, int index, Vector2 position)
    {
        _combatant = combatant;
        _isAllySide = isAlly;
        _plateIndex = index;
        _lockedPosition = position;

        if (plateImage != null)
            plateImage.color = isAlly ? allyPlateColor : enemyPlateColor;

        if (mpText != null)
            mpText.gameObject.SetActive(isAlly);

        if (nameText != null)
            nameText.text = combatant.CombatName;

        UpdateUnitSprite();
        SetPosition(position);

        gameObject.SetActive(true);
        RefreshDisplay();
    }

    /// <summary>
    /// Clear this plate when a unit dies or combat ends.
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
                hpText.text = $"{player.CurrentHP}/{player.MaxHP}";
            else if (_combatant is EnemyStats enemy)
                hpText.text = $"{enemy.CurrentHP}/{enemy.MaxHP}";
        }

        if (mpText != null && _isAllySide && _combatant is PlayerStats playerMP)
        {
            mpText.text = $"{playerMP.CurrentMP}/{playerMP.MaxMP}";
        }

        if (!_combatant.IsAlive)
        {
            Clear();
        }
    }

    // ─────────────────────────────────────────────
    //  Positioning
    // ─────────────────────────────────────────────

    /// <summary>
    /// Move this plate to a specific UI position (instant snap).
    /// </summary>
    public void SetPosition(Vector2 position)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = position;
    }

    /// <summary>
    /// Snap to the front plate position for targeting spotlight.
    /// </summary>
    public void MoveToFrontPosition(Vector2 frontPosition)
    {
        SetPosition(frontPosition);
    }

    /// <summary>
    /// Return to the locked position assigned at combat start.
    /// </summary>
    public void ReturnToLockedPosition()
    {
        SetPosition(_lockedPosition);
    }

    // ─────────────────────────────────────────────
    //  Targeting
    // ─────────────────────────────────────────────

    /// <summary>
    /// Show or hide the targeting highlight bracket around this plate.
    /// </summary>
    public void SetTargetHighlight(bool active)
    {
        if (targetHighlight != null)
            targetHighlight.SetActive(active);
    }

    /// <summary>
    /// Enable or disable click interaction (only active during targeting mode).
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (plateButton != null)
            plateButton.interactable = interactable;
    }

    // ─────────────────────────────────────────────
    //  Internal
    // ─────────────────────────────────────────────

    private void UpdateUnitSprite()
    {
        if (unitSprite == null || _combatant == null) return;

        MonoBehaviour combatantMB = _combatant as MonoBehaviour;
        if (combatantMB != null)
        {
            SpriteRenderer sr = combatantMB.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                unitSprite.sprite = sr.sprite;
                unitSprite.color = Color.white;
                return;
            }
        }

        unitSprite.color = _isAllySide ? allyPlateColor : enemyPlateColor;
    }

    public void ClearClickCallback()
    {
        OnPlateClicked = null;
    }
}
