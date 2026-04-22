using UnityEngine;

/// <summary>
/// Monitors a boss enemy and disables this NPC when the boss is killed.
/// Used for NPCs that are narratively linked to a boss — like the Old Man
/// who is secretly the Wraith.
///
/// Checks every frame (cheap boolean check) so it catches the defeat
/// regardless of when or how the boss dies.
///
/// SETUP:
///   1. Attach to the NPC GameObject (e.g., the Old Man)
///   2. Drag the boss's EnemyStats into the bossToWatch slot
///   3. When the boss dies, this entire GameObject deactivates
/// </summary>
public class WatchedNPC : MonoBehaviour
{
    [Header("Boss Link")]
    [Tooltip("When this boss is permanently defeated, this NPC disappears.")]
    [SerializeField] private EnemyStats bossToWatch;

    [Tooltip("Optional: message logged when the NPC disappears.")]
    [SerializeField] private string disappearLog = "The old man is gone. His cabin sits empty.";

    private bool _hasDisappeared = false;

    private void Update()
    {
        if (_hasDisappeared) return;
        if (bossToWatch == null) return;

        if (bossToWatch.IsPermanentlyDefeated)
        {
            _hasDisappeared = true;
            Debug.Log($"[WatchedNPC] {gameObject.name} has disappeared. {disappearLog}");
            gameObject.SetActive(false);
        }
    }
}
