using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Paginated inventory UI with bracket selection and contextual action buttons.
/// </summary>
public class InventoryPageUI : MonoBehaviour
{
    [Header("Slot References (assign 9)")]
    [SerializeField] private InventorySlotUI[] slotUIs = new InventorySlotUI[9];

    [Header("Navigation")]
    [SerializeField] private Button backArrow;
    [SerializeField] private Button forwardArrow;
    [SerializeField] private TextMeshProUGUI pageNumberText;

    [Header("Item Details Panel")]
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private TextMeshProUGUI itemCategoryText;
    [SerializeField] private TextMeshProUGUI itemQuantityText;

    [Header("Action Buttons")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private Button useButton;
    [SerializeField] private TextMeshProUGUI useButtonText;
    [SerializeField] private Button discardButton;

    private int currentPage = 0;
    private int selectedSlotIndex = -1;

    // ─────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        currentPage = 0;
        selectedSlotIndex = -1;

        backArrow.onClick.AddListener(PreviousPage);
        forwardArrow.onClick.AddListener(NextPage);

        if (useButton != null)
            useButton.onClick.AddListener(OnActionButtonPressed);
        if (discardButton != null)
            discardButton.onClick.AddListener(OnDiscardPressed);

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += RefreshCurrentPage;

        for (int i = 0; i < slotUIs.Length; i++)
        {
            int index = i;
            if (slotUIs[i] != null)
                slotUIs[i].OnSlotClicked += () => SelectSlot(index);
        }

        RefreshCurrentPage();
        HideDetails();
    }

    private void OnDisable()
    {
        backArrow.onClick.RemoveListener(PreviousPage);
        forwardArrow.onClick.RemoveListener(NextPage);

        if (useButton != null)
            useButton.onClick.RemoveListener(OnActionButtonPressed);
        if (discardButton != null)
            discardButton.onClick.RemoveListener(OnDiscardPressed);

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshCurrentPage;

        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] != null)
                slotUIs[i].ClearClickCallback();
        }
    }

    // ─────────────────────────────────────────────
    //  Page Navigation
    // ─────────────────────────────────────────────

    private void NextPage()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        int nextPage = currentPage + 1;
        if (inv.PageHasItems(nextPage))
        {
            currentPage = nextPage;
            selectedSlotIndex = -1;
            RefreshCurrentPage();
            HideDetails();
        }
    }

    private void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            selectedSlotIndex = -1;
            RefreshCurrentPage();
            HideDetails();
        }
    }

    // ─────────────────────────────────────────────
    //  Slot Selection
    // ─────────────────────────────────────────────

    private void SelectSlot(int localSlotIndex)
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        int globalIndex = currentPage * InventoryManager.SLOTS_PER_PAGE + localSlotIndex;

        InventorySlot[] pageSlots = inv.GetSlotsForPage(currentPage);
        if (localSlotIndex >= pageSlots.Length || pageSlots[localSlotIndex].IsEmpty)
        {
            Deselect();
            return;
        }

        if (selectedSlotIndex == localSlotIndex)
        {
            Deselect();
            return;
        }

        selectedSlotIndex = localSlotIndex;
        RefreshSlotHighlights();
        ShowDetails(pageSlots[localSlotIndex], globalIndex);
    }

    private void Deselect()
    {
        selectedSlotIndex = -1;
        RefreshSlotHighlights();
        HideDetails();
    }

    // ─────────────────────────────────────────────
    //  Display Refresh
    // ─────────────────────────────────────────────

    private void RefreshCurrentPage()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        InventorySlot[] pageSlots = inv.GetSlotsForPage(currentPage);

        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] == null) continue;

            if (i < pageSlots.Length && !pageSlots[i].IsEmpty)
                slotUIs[i].SetSlot(pageSlots[i]);
            else
                slotUIs[i].SetEmpty();
        }

        RefreshSlotHighlights();
        RefreshNavArrows();
        RefreshPageNumber();

        if (selectedSlotIndex >= 0 && selectedSlotIndex < pageSlots.Length
            && !pageSlots[selectedSlotIndex].IsEmpty)
        {
            int globalIndex = currentPage * InventoryManager.SLOTS_PER_PAGE + selectedSlotIndex;
            ShowDetails(pageSlots[selectedSlotIndex], globalIndex);
        }
        else
        {
            Deselect();
        }
    }

    private void RefreshSlotHighlights()
    {
        for (int i = 0; i < slotUIs.Length; i++)
        {
            if (slotUIs[i] != null)
                slotUIs[i].SetSelected(i == selectedSlotIndex);
        }
    }

    private void RefreshNavArrows()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        backArrow.interactable = currentPage > 0;
        forwardArrow.interactable = inv.PageHasItems(currentPage + 1);
    }

    private void RefreshPageNumber()
    {
        if (pageNumberText == null) return;

        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        pageNumberText.text = $"{currentPage + 1} / {inv.TotalPages}";
    }

    // ─────────────────────────────────────────────
    //  Details & Action Panel
    // ─────────────────────────────────────────────

    private void ShowDetails(InventorySlot slot, int globalIndex)
    {
        ItemDefinition def = slot.GetDefinition();
        if (def == null)
        {
            HideDetails();
            return;
        }

        if (detailsPanel != null) detailsPanel.SetActive(true);
        if (actionPanel != null)  actionPanel.SetActive(true);

        if (itemNameText != null)        itemNameText.text = def.itemName;
        if (itemDescriptionText != null)  itemDescriptionText.text = def.description;
        if (itemCategoryText != null)     itemCategoryText.text = def.category.ToString();
        if (itemQuantityText != null)
        {
            itemQuantityText.text = def.CanStack ? $"x{slot.quantity}" : "";
        }

        ConfigureActionButtons(def, globalIndex);
    }

    private void ConfigureActionButtons(ItemDefinition def, int globalIndex)
    {
        if (useButton != null)
        {
            bool showPrimary = def.CanEquip || def.CanUse;
            useButton.gameObject.SetActive(showPrimary);

            if (showPrimary && useButtonText != null)
            {
                if (def.CanEquip)
                    useButtonText.text = "Equip";
                else if (def.CanUse)
                    useButtonText.text = "Use";
            }
        }

        if (discardButton != null)
            discardButton.gameObject.SetActive(def.CanDiscard);
    }

    private void HideDetails()
    {
        if (detailsPanel != null) detailsPanel.SetActive(false);
        if (actionPanel != null)  actionPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  Action Handlers
    // ─────────────────────────────────────────────

    private void OnActionButtonPressed()
    {
        if (selectedSlotIndex < 0) return;

        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        int globalIndex = currentPage * InventoryManager.SLOTS_PER_PAGE + selectedSlotIndex;
        InventorySlot[] pageSlots = inv.GetSlotsForPage(currentPage);

        if (selectedSlotIndex >= pageSlots.Length || pageSlots[selectedSlotIndex].IsEmpty)
            return;

        ItemDefinition def = pageSlots[selectedSlotIndex].GetDefinition();
        if (def == null) return;

        switch (def.category)
        {
            case ItemCategory.Equipment:
                inv.EquipItem(globalIndex);
                break;
            case ItemCategory.Consumable:
                inv.UseItem(globalIndex);
                break;
        }
    }

    private void OnDiscardPressed()
    {
        if (selectedSlotIndex < 0) return;

        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        int globalIndex = currentPage * InventoryManager.SLOTS_PER_PAGE + selectedSlotIndex;
        inv.DiscardItem(globalIndex);
    }
}
