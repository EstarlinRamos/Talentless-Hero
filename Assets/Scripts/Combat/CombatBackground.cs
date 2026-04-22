using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// Switches the combat background image based on which zone the player is in.
/// Sits behind the plates in the upper half of the combat screen.
///
/// SETUP:
///   1. Create a UI Image that fills the upper half of the combat canvas
///   2. Attach this script
///   3. Add zone-to-background entries (e.g., P1 → forest, C1 → cave)
///   4. CombatUIManager or OverworldCombatBridge calls SetBackground() at combat start
/// </summary>
public class CombatBackground : MonoBehaviour
{
    [Serializable]
    public class ZoneBackground
    {
        [Tooltip("Zone name (must match ZoneMusicConfig / AudioManager zones).")]
        public string zoneName;

        [Tooltip("Background sprite for this zone during combat.")]
        public Sprite backgroundSprite;
    }

    [Header("Background Image")]
    [Tooltip("The UI Image that displays the combat background.")]
    [SerializeField] private Image backgroundImage;

    [Header("Zone Backgrounds")]
    [Tooltip("Map zone names to background images.")]
    [SerializeField] private List<ZoneBackground> zoneBackgrounds = new List<ZoneBackground>();

    [Header("Fallback")]
    [Tooltip("Default background if the current zone has no entry.")]
    [SerializeField] private Sprite defaultBackground;

    private Dictionary<string, Sprite> _lookup;

    private void Awake()
    {
        _lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zoneBackgrounds)
        {
            if (!string.IsNullOrEmpty(entry.zoneName) && entry.backgroundSprite != null)
                _lookup[entry.zoneName] = entry.backgroundSprite;
        }
    }

    /// <summary>
    /// Set the background based on the current zone.
    /// Call at combat start. Reads from AudioManager.CurrentZone if no zone is provided.
    /// </summary>
    public void SetBackground(string zoneName = null)
    {
        if (string.IsNullOrEmpty(zoneName) && AudioManager.Instance != null)
            zoneName = AudioManager.Instance.CurrentZone;

        Sprite bg = defaultBackground;

        if (!string.IsNullOrEmpty(zoneName) && _lookup.TryGetValue(zoneName, out Sprite zoneBg))
            bg = zoneBg;

        if (backgroundImage != null && bg != null)
        {
            backgroundImage.sprite = bg;
            backgroundImage.enabled = true;
        }

        Debug.Log($"[CombatBG] Set background for zone: {zoneName ?? "default"}");
    }

    /// <summary>
    /// Hide the background (combat ended).
    /// </summary>
    public void Hide()
    {
        if (backgroundImage != null)
            backgroundImage.enabled = false;
    }
}
