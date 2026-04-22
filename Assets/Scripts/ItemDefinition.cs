using System;
using UnityEngine;

// ─────────────────────────────────────────────
//  ITEM CATEGORIES
// ─────────────────────────────────────────────

public enum ItemCategory
{
    Equipment,      // Always equippable
    Consumable,     // Potions, food, throwable weapons - Use action
    Material,       // Crafting/upgrading - only category that stacks
    QuestItem       // Submit only, cannot discard or use
}

public enum EquipmentSlot
{
    None,
    Weapon,
    Helmet,
    Armor,
    Boots,
    Accessory
}

// ─────────────────────────────────────────────
//  ITEM DEFINITION (ScriptableObject)
//  Create these as assets: Right-click > Create > Inventory > Item
// ─────────────────────────────────────────────

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemID;
    public string itemName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Classification")]
    public ItemCategory category;
    public EquipmentSlot equipSlot = EquipmentSlot.None;

    [Header("Stacking (Materials Only)")]
    public int maxStackSize = 99;

    [Header("Value")]
    public int buyPrice;
    public int sellPrice;

    // ─────────────────────────────────────────
    //  RULES (derived from category)
    // ─────────────────────────────────────────

    public bool CanEquip    => category == ItemCategory.Equipment;
    public bool CanUse      => category == ItemCategory.Consumable;
    public bool CanDiscard  => category != ItemCategory.QuestItem;
    public bool CanStack    => category == ItemCategory.Material;
    public bool CanSubmit   => category == ItemCategory.QuestItem;
}

// ─────────────────────────────────────────────
//  INVENTORY SLOT (Runtime instance in the inventory)
// ─────────────────────────────────────────────

[Serializable]
public class InventorySlot
{
    public string itemID;
    public int quantity;

    [NonSerialized] private ItemDefinition _cachedDef;

    public InventorySlot(string itemID, int quantity = 1)
    {
        this.itemID = itemID;
        this.quantity = quantity;
    }

    /// <summary>
    /// Resolves the ItemDefinition from the ItemDatabase.
    /// Cached after first lookup.
    /// </summary>
    public ItemDefinition GetDefinition()
    {
        if (_cachedDef == null || _cachedDef.itemID != itemID)
            _cachedDef = ItemDatabase.Instance?.GetItem(itemID);
        return _cachedDef;
    }

    public bool IsEmpty => string.IsNullOrEmpty(itemID);

    /// <summary>
    /// Creates an empty slot.
    /// </summary>
    public static InventorySlot Empty() => new InventorySlot("", 0);
}
