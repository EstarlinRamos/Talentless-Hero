using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single inventory slot UI element.
/// Shows the item icon, quantity (for materials), and bracket highlight when selected.
/// 
/// SETUP:
/// Create a UI panel with:
///   - An Image child for the item icon
///   - A TextMeshPro child for the quantity label
///   - A GameObject with bracket images (4 corner brackets) for selection
///   - A Button component on the root for click detection
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Sprite emptySlotSprite;

    [Header("Selection Bracket")]
    [SerializeField] private GameObject bracketHighlight;

    [Header("Button")]
    [SerializeField] private Button slotButton;

    // Callback for when this slot is clicked
    public event Action OnSlotClicked;

    private bool isEmpty = true;

    private void Awake()
    {
        if (slotButton == null)
            slotButton = GetComponent<Button>();

        if (slotButton != null)
            slotButton.onClick.AddListener(() => OnSlotClicked?.Invoke());

        SetEmpty();
    }

    /// <summary>
    /// Populate this slot with item data.
    /// </summary>
    public void SetSlot(InventorySlot slot)
    {
        if (slot == null || slot.IsEmpty)
        {
            SetEmpty();
            return;
        }

        isEmpty = false;
        ItemDefinition def = slot.GetDefinition();

        if (def != null)
        {
            // Icon
            if (itemIcon != null)
            {
                itemIcon.sprite = def.icon != null ? def.icon : emptySlotSprite;
                itemIcon.color = Color.white;
            }

            // Quantity - only show for stackable materials
            if (quantityText != null)
            {
                if (def.CanStack && slot.quantity > 1)
                {
                    quantityText.gameObject.SetActive(true);
                    quantityText.text = $"x{slot.quantity}";
                }
                else
                {
                    quantityText.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            SetEmpty();
        }
    }

    /// <summary>
    /// Clear this slot to show as empty.
    /// </summary>
    public void SetEmpty()
    {
        isEmpty = true;

        if (itemIcon != null)
        {
            itemIcon.sprite = emptySlotSprite;
            itemIcon.color = new Color(1f, 1f, 1f, 0.2f); // Dim empty slots
        }

        if (quantityText != null)
            quantityText.gameObject.SetActive(false);

        SetSelected(false);
    }

    /// <summary>
    /// Show or hide the bracket selection highlight.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (bracketHighlight != null)
            bracketHighlight.SetActive(selected && !isEmpty);
    }

    public void ClearClickCallback()
    {
        OnSlotClicked = null;
    }
}
