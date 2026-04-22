using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central item registry. Loads all ItemDefinitions from a Resources folder.
/// Place your ItemDefinition assets in: Resources/Items/
/// </summary>
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    private Dictionary<string, ItemDefinition> items = new Dictionary<string, ItemDefinition>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAllItems();
    }

    private void LoadAllItems()
    {
        ItemDefinition[] allItems = Resources.LoadAll<ItemDefinition>("Items");
        items.Clear();

        foreach (var item in allItems)
        {
            if (!items.ContainsKey(item.itemID))
                items[item.itemID] = item;
            else
                Debug.LogWarning($"[ItemDatabase] Duplicate item ID: {item.itemID}");
        }

        Debug.Log($"[ItemDatabase] Loaded {items.Count} item definitions.");
    }

    public ItemDefinition GetItem(string itemID)
    {
        if (items.TryGetValue(itemID, out ItemDefinition def))
            return def;

        Debug.LogWarning($"[ItemDatabase] Item not found: {itemID}");
        return null;
    }

    public bool HasItem(string itemID) => items.ContainsKey(itemID);
}
