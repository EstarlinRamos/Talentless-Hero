using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple string-based flag system for tracking persistent game state.
/// Flags survive save/load cycles. Used for:
///   - Boss permanent defeats ("boss_wraith_defeated")
///   - NPC first meetings ("npc_met_innkeeper", "npc_met_garrett")
///   - NPC conditional one-shots ("npc_oneshot_innkeeper_0")
///   - Story progression ("intro_complete", "forest_unlocked")
///
/// SETUP:
///   1. Create a root-level GameObject called "WorldFlagManager"
///   2. Attach this script
///   3. It auto-persists via SaveManager's WorldFlagData block
/// </summary>
public class WorldFlagManager : MonoBehaviour
{
    public static WorldFlagManager Instance { get; private set; }

    private HashSet<string> _flags = new HashSet<string>();

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
    //  Public API
    // ─────────────────────────────────────────────

    public void SetFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        _flags.Add(flag);
    }

    public bool HasFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        return _flags.Contains(flag);
    }

    public void RemoveFlag(string flag)
    {
        _flags.Remove(flag);
    }

    public void ClearAll()
    {
        _flags.Clear();
    }

    // ─────────────────────────────────────────────
    //  Save / Load
    // ─────────────────────────────────────────────

    public WorldFlagData CaptureForSave()
    {
        WorldFlagData data = new WorldFlagData();
        data.flags = new List<string>(_flags);
        return data;
    }

    public void LoadFromSave(WorldFlagData data)
    {
        _flags.Clear();
        if (data != null && data.flags != null)
        {
            foreach (var flag in data.flags)
                _flags.Add(flag);
        }
        Debug.Log($"[WorldFlags] Loaded {_flags.Count} flags.");
    }

    public void ResetForNewGame()
    {
        _flags.Clear();
        Debug.Log("[WorldFlags] Reset for new game.");
    }
}
