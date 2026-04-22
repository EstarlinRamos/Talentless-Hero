using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;

/// <summary>
/// Maps zone names to their camera boundary PolygonCollider2Ds.
/// Used by the save system to restore the correct camera boundary on load.
///
/// SETUP:
///   1. Create a root-level GameObject called "ZoneBoundaryLookup"
///   2. Attach this script
///   3. Add an entry for each zone (T1, G1, V1, P1, C1, K1)
///   4. Drag the corresponding PolygonCollider2D into each entry
/// </summary>
public class ZoneBoundaryLookup : MonoBehaviour
{
    public static ZoneBoundaryLookup Instance { get; private set; }

    [System.Serializable]
    public class ZoneEntry
    {
        public string zoneName;
        public PolygonCollider2D boundary;
    }

    [Header("Zone Boundaries")]
    [SerializeField] private List<ZoneEntry> zones = new List<ZoneEntry>();

    [Header("Default")]
    [Tooltip("Fallback boundary if the zone name isn't found.")]
    [SerializeField] private PolygonCollider2D defaultBoundary;

    private Dictionary<string, PolygonCollider2D> _lookup;

    private void Awake()
    {
        Instance = this;

        _lookup = new Dictionary<string, PolygonCollider2D>();
        foreach (var entry in zones)
        {
            if (!string.IsNullOrEmpty(entry.zoneName) && entry.boundary != null)
                _lookup[entry.zoneName] = entry.boundary;
        }
    }

    /// <summary>
    /// Get the camera boundary for a zone. Returns default if not found.
    /// </summary>
    public PolygonCollider2D GetBoundary(string zoneName)
    {
        if (!string.IsNullOrEmpty(zoneName) && _lookup.TryGetValue(zoneName, out var boundary))
            return boundary;

        return defaultBoundary;
    }

    /// <summary>
    /// Set the Cinemachine confiner to the boundary for the given zone.
    /// </summary>
    public void ApplyZoneBoundary(string zoneName)
    {
        PolygonCollider2D boundary = GetBoundary(zoneName);
        if (boundary == null) return;

        CinemachineConfiner2D confiner = FindFirstObjectByType<CinemachineConfiner2D>();
        if (confiner != null)
        {
            confiner.BoundingShape2D = boundary;
            Debug.Log($"[ZoneLookup] Camera boundary set to {zoneName}.");
        }
    }
}
