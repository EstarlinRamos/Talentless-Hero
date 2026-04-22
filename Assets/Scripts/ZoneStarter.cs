using UnityEngine;

/// <summary>
/// Sets the player's starting zone when the scene loads.
/// Tells AudioManager which zone the player is in so music
/// plays immediately without needing to cross a MapTransition.
///
/// Place one of these in any scene where the player starts
/// inside a zone rather than crossing into it.
///
/// SETUP:
///   1. Create an empty GameObject called "ZoneStarter"
///   2. Attach this script
///   3. Set the starting zone name (must match ZoneMusicConfig exactly)
/// </summary>
public class ZoneStarter : MonoBehaviour
{
    [Tooltip("The zone the player starts in. Must match ZoneMusicConfig exactly (e.g., T1, V1).")]
    [SerializeField] private string startingZone = "T1";

    private void Start()
    {
        if (AudioManager.Instance != null && !string.IsNullOrEmpty(startingZone))
        {
            AudioManager.Instance.EnterZone(startingZone);
            Debug.Log($"[ZoneStarter] Set starting zone to {startingZone}");
        }
    }
}
