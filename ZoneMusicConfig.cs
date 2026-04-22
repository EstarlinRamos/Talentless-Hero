using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Maps zone names to their exploration music clips.
/// Create one asset: Right-click > Create > Talentless Hero > Zone Music Config
///
/// Zone naming convention:
///   T1  = Tutorial / starting area (meets the goddess)
///   G1  = Goddess shrine area
///   V1  = Village 1
///   P1  = Path to the boss
///   C1  = Cave (boss area)
///
/// If a zone has no entry here, the current music keeps playing.
/// If a zone's clip is null, exploration music goes silent in that zone.
/// </summary>
[CreateAssetMenu(fileName = "ZoneMusicConfig", menuName = "Talentless Hero/Zone Music Config")]
public class ZoneMusicConfig : ScriptableObject
{
    [System.Serializable]
    public class ZoneEntry
    {
        [Tooltip("Zone name — must match the MapBounds GameObject name (e.g. T1, G1, V1, P1, C1).")]
        public string zoneName;

        [Tooltip("Exploration music for this zone. Null = silence.")]
        public AudioClip explorationClip;

        [Tooltip("Volume override for this zone (0 = use global default).")]
        [Range(0f, 1f)]
        public float volumeOverride = 0f;
    }

    [Header("Zone Music Mappings")]
    [Tooltip("Each entry maps a zone name to its background music.")]
    [SerializeField] private List<ZoneEntry> zones = new List<ZoneEntry>();

    [Header("Fallback")]
    [Tooltip("Default exploration clip if the current zone has no entry.")]
    [SerializeField] private AudioClip defaultExplorationClip;

    // Runtime lookup — built once on first query
    private Dictionary<string, ZoneEntry> _lookup;

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, ZoneEntry>();
        foreach (var entry in zones)
        {
            if (string.IsNullOrEmpty(entry.zoneName)) continue;

            if (!_lookup.ContainsKey(entry.zoneName))
                _lookup[entry.zoneName] = entry;
            else
                Debug.LogWarning($"[ZoneMusic] Duplicate zone entry: {entry.zoneName}");
        }
    }

    /// <summary>
    /// Get the music entry for a zone. Returns null if no mapping exists.
    /// </summary>
    public ZoneEntry GetZoneEntry(string zoneName)
    {
        if (_lookup == null) BuildLookup();

        if (_lookup.TryGetValue(zoneName, out ZoneEntry entry))
            return entry;

        return null;
    }

    /// <summary>
    /// Get the clip for a zone, falling back to the default if no mapping exists.
    /// </summary>
    public AudioClip GetClipForZone(string zoneName)
    {
        var entry = GetZoneEntry(zoneName);
        return entry != null ? entry.explorationClip : defaultExplorationClip;
    }

    /// <summary>
    /// Get the volume for a zone. Returns 0 if no override is set
    /// (caller should use its own default in that case).
    /// </summary>
    public float GetVolumeForZone(string zoneName)
    {
        var entry = GetZoneEntry(zoneName);
        return entry != null ? entry.volumeOverride : 0f;
    }

    public AudioClip DefaultClip => defaultExplorationClip;
}
