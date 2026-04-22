using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Single inventory slot UI element showing item icon, quantity, and bracket highlight.
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
            if (itemIcon != null)
            {
                itemIcon.sprite = def.icon != null ? def.icon : emptySlotSprite;
                itemIcon.color = Color.white;
            }

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

    public void SetEmpty()
    {
        isEmpty = true;

        if (itemIcon != null)
        {
            itemIcon.sprite = emptySlotSprite;
            itemIcon.color = new Color(1f, 1f, 1f, 0.2f);
        }

        if (quantityText != null)
            quantityText.gameObject.SetActive(false);

        SetSelected(false);
    }

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
