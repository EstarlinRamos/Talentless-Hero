using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton inventory system with paginated slots, gold tracking, and save/load support.
/// Materials stack, equipment and consumables occupy individual slots.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    public const int MAX_SLOTS = 99;
    public const int SLOTS_PER_PAGE = 9;

    private List<InventorySlot> slots = new List<InventorySlot>();
    private int _gold = 0;

    // ─────────────────────────────────────────────
    //  Events
    // ─────────────────────────────────────────────

    public event Action OnInventoryChanged;
    public event Action<string> OnItemUsed;
    public event Action<string, EquipmentSlot> OnItemEquipped;
    public event Action<string> OnItemDiscarded;
    public event Action<int> OnGoldChanged;

    // ─────────────────────────────────────────────
    //  Gold
    // ─────────────────────────────────────────────

    public int Gold => _gold;

    /// <summary>Add gold. Returns new total.</summary>
    public int AddGold(int amount)
    {
        if (amount <= 0) return _gold;

        _gold += amount;
        Debug.Log($"[Inventory] +{amount} gold (total: {_gold})");
        OnGoldChanged?.Invoke(_gold);
        return _gold;
    }

    /// <summary>Spend gold. Returns true if the player had enough.</summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (amount > _gold)
        {
            Debug.Log($"[Inventory] Not enough gold! Need {amount}, have {_gold}.");
            return false;
        }

        _gold -= amount;
        Debug.Log($"[Inventory] -{amount} gold (total: {_gold})");
        OnGoldChanged?.Invoke(_gold);
        return true;
    }

    // ─────────────────────────────────────────────
    //  Lifecycle
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
    //  Add Item
    // ─────────────────────────────────────────────

    /// <summary>
    /// Add an item to the inventory. Materials stack; everything else takes a new slot.
    /// Returns the quantity actually added.
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

        if (def.CanStack)
        {
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
    //  Remove Item
    // ─────────────────────────────────────────────

    /// <summary>
    /// Remove quantity of an item, starting from the last stack.
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
    //  Slot Actions
    // ─────────────────────────────────────────────

    /// <summary>
    /// Use the consumable at the given slot index.
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

        Debug.Log($"[Inventory] Used: {def.itemName}");
        OnItemUsed?.Invoke(def.itemID);

        slot.quantity--;
        if (slot.quantity <= 0)
            slots.RemoveAt(slotIndex);

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Equip the equipment at the given slot index.
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

        Debug.Log($"[Inventory] Equipped: {def.itemName} to {def.equipSlot}");
        OnItemEquipped?.Invoke(def.itemID, def.equipSlot);
        return true;
    }

    /// <summary>
    /// Discard an item. Quest items cannot be discarded.
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
    /// Submit a quest item through the quest system.
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
    //  Queries
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
    /// Returns exactly SLOTS_PER_PAGE entries for the given page, padding with empties.
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
                break;
        }

        return actions;
    }

    public bool IsFull => slots.Count >= MAX_SLOTS;

    // ─────────────────────────────────────────────
    //  Save / Load
    // ─────────────────────────────────────────────

    public InventoryData CaptureForSave()
    {
        InventoryData data = new InventoryData();
        data.gold = _gold;

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
        _gold = 0;

        if (data == null) return;

        _gold = data.gold;

        if (data.items != null)
        {
            foreach (var item in data.items)
            {
                if (!string.IsNullOrEmpty(item.itemID))
                    slots.Add(new InventorySlot(item.itemID, item.quantity));
            }
        }

        Debug.Log($"[Inventory] Loaded {slots.Count} items, {_gold} gold from save.");
        OnInventoryChanged?.Invoke();
        OnGoldChanged?.Invoke(_gold);
    }

    public void ResetForNewGame()
    {
        slots.Clear();
        _gold = 0;
        OnInventoryChanged?.Invoke();
        OnGoldChanged?.Invoke(_gold);
        Debug.Log("[Inventory] Reset for new game.");
    }

    private bool IsValidSlot(int index) => index >= 0 && index < slots.Count;
}
