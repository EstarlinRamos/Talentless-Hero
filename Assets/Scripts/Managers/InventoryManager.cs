using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    public const int MAX_SLOTS = 99;
    public const int SLOTS_PER_PAGE = 9;

    // ─────────────────────────────────────────────
    //  STATE
    // ─────────────────────────────────────────────

    private List<InventorySlot> slots = new List<InventorySlot>();

    // ─────────────────────────────────────────────
    //  EVENTS
    // ─────────────────────────────────────────────

    public event Action OnInventoryChanged;
    public event Action<string> OnItemUsed;
    public event Action<string, EquipmentSlot> OnItemEquipped;
    public event Action<string> OnItemDiscarded;

    // ─────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────
    //  ADD ITEM
    // ─────────────────────────────────────────────

    /// <summary>
    /// Adds an item to the inventory. Materials stack, everything else
    /// takes a new slot. Returns the quantity actually added.
    /// </summary>
    public int AddItem(string itemID, int quantity = 1)
    {
        ItemDefinition def = ItemDatabase.Instance?.GetItem(itemID);
        if (def == null)
        {
            Debug.LogWarning($"[Inventory] Cannot add unknown item: {itemID}");
            return 0;
        }

        int added = 0;

        // Materials stack - try existing stacks first
        if (def.CanStack)
        {
            // Fill existing stacks
            for (int i = 0; i < slots.Count && quantity > 0; i++)
            {
                if (slots[i].itemID == itemID && slots[i].quantity < def.maxStackSize)
                {
                    int space = def.maxStackSize - slots[i].quantity;
                    int toAdd = Mathf.Min(space, quantity);
                    slots[i].quantity += toAdd;
                    quantity -= toAdd;
                    added += toAdd;
                }
            }

            // Create new stacks if needed
            while (quantity > 0 && slots.Count < MAX_SLOTS)
            {
                int toAdd = Mathf.Min(def.maxStackSize, quantity);
                slots.Add(new InventorySlot(itemID, toAdd));
                quantity -= toAdd;
                added += toAdd;
            }
        }
        else
        {
            // Non-stackable: one item per slot
            while (quantity > 0 && slots.Count < MAX_SLOTS)
            {
                slots.Add(new InventorySlot(itemID, 1));
                quantity--;
                added++;
            }
        }

        if (added > 0)
        {
            Debug.Log($"[Inventory] Added {added}x {def.itemName}");
            OnInventoryChanged?.Invoke();
        }

        if (quantity > 0)
            Debug.LogWarning($"[Inventory] Inventory full! Could not add {quantity}x {def.itemName}");

        return added;
    }

    // ─────────────────────────────────────────────
    //  REMOVE ITEM
    // ─────────────────────────────────────────────

    /// <summary>
    /// Removes quantity of an item. Removes from last stack first.
    /// Returns the quantity actually removed.
    /// </summary>
    public int RemoveItem(string itemID, int quantity = 1)
    {
        int removed = 0;

        for (int i = slots.Count - 1; i >= 0 && quantity > 0; i--)
        {
            if (slots[i].itemID != itemID) continue;

            int toRemove = Mathf.Min(slots[i].quantity, quantity);
            slots[i].quantity -= toRemove;
            quantity -= toRemove;
            removed += toRemove;

            if (slots[i].quantity <= 0)
                slots.RemoveAt(i);
        }

        if (removed > 0)
            OnInventoryChanged?.Invoke();

        return removed;
    }

    // ─────────────────────────────────────────────
    //  SLOT ACTIONS
    // ─────────────────────────────────────────────

    /// <summary>
    /// Uses the item at the given slot index. Only works for Consumables.
    /// </summary>
    public bool UseItem(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        InventorySlot slot = slots[slotIndex];
        ItemDefinition def = slot.GetDefinition();

        if (def == null || !def.CanUse)
        {
            Debug.Log("[Inventory] This item cannot be used.");
            return false;
        }

        // TODO: Apply consumable effects here
        // Example: if (def.itemID == "potion_hp") PlayerHealth += 50;

        Debug.Log($"[Inventory] Used: {def.itemName}");
        OnItemUsed?.Invoke(def.itemID);

        slot.quantity--;
        if (slot.quantity <= 0)
            slots.RemoveAt(slotIndex);

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Equips the item at the given slot index. Only works for Equipment.
    /// </summary>
    public bool EquipItem(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        InventorySlot slot = slots[slotIndex];
        ItemDefinition def = slot.GetDefinition();

        if (def == null || !def.CanEquip)
        {
            Debug.Log("[Inventory] This item cannot be equipped.");
            return false;
        }

        // TODO: Wire to actual equipment system
        // Example: EquipmentManager.Instance.Equip(def);

        Debug.Log($"[Inventory] Equipped: {def.itemName} to {def.equipSlot}");
        OnItemEquipped?.Invoke(def.itemID, def.equipSlot);
        return true;
    }

    /// <summary>
    /// Discards the item at the given slot index. Quest items cannot be discarded.
    /// </summary>
    public bool DiscardItem(int slotIndex, int quantity = 1)
    {
        if (!IsValidSlot(slotIndex)) return false;

        InventorySlot slot = slots[slotIndex];
        ItemDefinition def = slot.GetDefinition();

        if (def == null || !def.CanDiscard)
        {
            Debug.Log("[Inventory] This item cannot be discarded.");
            return false;
        }

        int toDiscard = Mathf.Min(quantity, slot.quantity);
        slot.quantity -= toDiscard;

        Debug.Log($"[Inventory] Discarded {toDiscard}x {def.itemName}");
        OnItemDiscarded?.Invoke(def.itemID);

        if (slot.quantity <= 0)
            slots.RemoveAt(slotIndex);

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Submits a quest item. Only works for QuestItem category.
    /// </summary>
    public bool SubmitQuestItem(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;

        InventorySlot slot = slots[slotIndex];
        ItemDefinition def = slot.GetDefinition();

        if (def == null || !def.CanSubmit)
        {
            Debug.Log("[Inventory] This item cannot be submitted.");
            return false;
        }

        Debug.Log($"[Inventory] Submitted quest item: {def.itemName}");

        slot.quantity--;
        if (slot.quantity <= 0)
            slots.RemoveAt(slotIndex);

        OnInventoryChanged?.Invoke();
        return true;
    }

    // ─────────────────────────────────────────────
    //  QUERIES
    // ─────────────────────────────────────────────

    public int SlotCount => slots.Count;
    public int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)slots.Count / SLOTS_PER_PAGE));

    public bool HasItem(string itemID)
    {
        return slots.Exists(s => s.itemID == itemID);
    }

    public int GetItemCount(string itemID)
    {
        int total = 0;
        foreach (var slot in slots)
        {
            if (slot.itemID == itemID)
                total += slot.quantity;
        }
        return total;
    }

    /// <summary>
    /// Returns the slots for a given page (0-indexed).
    /// Always returns exactly SLOTS_PER_PAGE entries, padding with empty slots.
    /// </summary>
    public InventorySlot[] GetSlotsForPage(int page)
    {
        InventorySlot[] pageSlots = new InventorySlot[SLOTS_PER_PAGE];
        int startIndex = page * SLOTS_PER_PAGE;

        for (int i = 0; i < SLOTS_PER_PAGE; i++)
        {
            int slotIndex = startIndex + i;
            if (slotIndex < slots.Count)
                pageSlots[i] = slots[slotIndex];
            else
                pageSlots[i] = InventorySlot.Empty();
        }

        return pageSlots;
    }

    /// <summary>
    /// Returns true if the given page has at least one item.
    /// Used to determine if forward arrow should be active.
    /// </summary>
    public bool PageHasItems(int page)
    {
        int startIndex = page * SLOTS_PER_PAGE;
        return startIndex < slots.Count;
    }

    /// <summary>
    /// Returns the available actions for an item at the given slot index.
    /// </summary>
    public List<string> GetAvailableActions(int slotIndex)
    {
        List<string> actions = new List<string>();

        if (!IsValidSlot(slotIndex)) return actions;

        ItemDefinition def = slots[slotIndex].GetDefinition();
        if (def == null) return actions;

        switch (def.category)
        {
            case ItemCategory.Equipment:
                actions.Add("Equip");
                actions.Add("Discard");
                break;
            case ItemCategory.Consumable:
                actions.Add("Use");
                actions.Add("Discard");
                break;
            case ItemCategory.Material:
                actions.Add("Discard");
                break;
            case ItemCategory.QuestItem:
                // No direct actions - submit is handled through quest system
                break;
        }

        return actions;
    }

    public bool IsFull => slots.Count >= MAX_SLOTS;

    // ─────────────────────────────────────────────
    //  SAVE / LOAD
    // ─────────────────────────────────────────────

    public InventoryData CaptureForSave()
    {
        InventoryData data = new InventoryData();

        for (int i = 0; i < slots.Count; i++)
        {
            ItemDefinition def = slots[i].GetDefinition();
            data.items.Add(new InventoryItemData
            {
                itemID = slots[i].itemID,
                itemName = def != null ? def.itemName : "",
                quantity = slots[i].quantity,
                slotIndex = i
            });
        }

        return data;
    }

    public void LoadFromSave(InventoryData data)
    {
        slots.Clear();

        if (data?.items == null) return;

        foreach (var item in data.items)
        {
            if (!string.IsNullOrEmpty(item.itemID))
                slots.Add(new InventorySlot(item.itemID, item.quantity));
        }

        Debug.Log($"[Inventory] Loaded {slots.Count} items from save.");
        OnInventoryChanged?.Invoke();
    }

    public void ResetForNewGame()
    {
        slots.Clear();
        OnInventoryChanged?.Invoke();
        Debug.Log("[Inventory] Reset for new game.");
    }

    // ─────────────────────────────────────────────
    //  INTERNAL
    // ─────────────────────────────────────────────

    private bool IsValidSlot(int index) => index >= 0 && index < slots.Count;
}
