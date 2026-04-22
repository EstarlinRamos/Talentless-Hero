using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Handles player death by teleporting them to the innkeeper instead of
/// triggering a game over. The innkeeper found them unconscious and
/// hauled them back.
///
/// Flow:
///   1. Player dies in combat → combat ends with defeat
///   2. OverworldCombatBridge calls HandleDefeat() on this component
///   3. Screen fades to black
///   4. Player is moved to the innkeeper spawn point, healed to full
///   5. Camera boundary updates to V1
///   6. Zone music switches to V1
///   7. Screen fades in
///   8. Innkeeper rescue dialogue triggers automatically
///   9. Player has full control after dialogue
///
/// There is no game over screen, no save reload — just a loss of
/// positioning progress. The player wakes up at the inn, patched up,
/// and can head right back out.
///
/// SETUP:
///   1. Create an empty GameObject called "RespawnHandler"
///   2. Attach this script
///   3. Assign: innkeeper spawn point, V1 boundary, rescue dialogue,
///      DialogueManager, and PlayerMovement references
///   4. Wire OverworldCombatBridge to call HandleDefeat() on combat loss
/// </summary>
public class PlayerRespawnHandler : MonoBehaviour
{
    [Header("Respawn Location")]
    [Tooltip("Where the player appears after being rescued (in front of the innkeeper).")]
    [SerializeField] private Transform innkeeperSpawnPoint;

    [Tooltip("Direction the player faces after respawning.")]
    [SerializeField] private Vector2 respawnFacingDirection = new Vector2(0, 1);

    [Header("Camera")]
    [Tooltip("Village map boundary for Cinemachine confiner.")]
    [SerializeField] private PolygonCollider2D villageBoundary;

    [Header("Dialogue")]
    [Tooltip("The rescue dialogue triggered when the player wakes up at the inn.")]
    [SerializeField] private DialogueData rescueDialogue;

    [Tooltip("Reference to the DialogueManager.")]
    [SerializeField] private DialogueManager dialogueManager;

    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerStats playerStats;

    [Header("Zone")]
    [Tooltip("Zone name for the village (for music switching).")]
    [SerializeField] private string villageZoneName = "V1";

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 1.5f;
    [SerializeField] private float blackScreenHold = 1.0f;
    [SerializeField] private float fadeInDuration = 1.5f;

    /// <summary>
    /// Call this when the player loses combat.
    /// Handles the full respawn sequence — fade, teleport, heal, dialogue.
    /// </summary>
    public void HandleDefeat()
    {
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        Debug.Log("[Respawn] Player defeated — starting rescue sequence...");

        // Keep player locked (should already be locked from combat)
        if (playerMovement != null)
            playerMovement.LockMovement();

        // Fade to black
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        // Hold on black (feels like time passing — the innkeeper finding them)
        yield return new WaitForSeconds(blackScreenHold);

        // Heal to full HP and MP
        if (playerStats != null)
        {
            playerStats.HealHP(playerStats.MaxHP);
            playerStats.RestoreMP(playerStats.MaxMP);
            Debug.Log("[Respawn] Player fully healed.");
        }

        // Clear status effects
        if (playerStats != null && playerStats.Effects != null)
            playerStats.Effects.ClearAll();

        // Teleport to innkeeper
        if (innkeeperSpawnPoint != null && playerMovement != null)
        {
            playerMovement.transform.position = innkeeperSpawnPoint.position;
            playerMovement.SetFacingDirection(respawnFacingDirection.x, respawnFacingDirection.y);
            Debug.Log("[Respawn] Player teleported to innkeeper.");
        }

        // Update camera boundary to village
        if (villageBoundary != null)
        {
            CinemachineConfiner2D confiner = FindFirstObjectByType<CinemachineConfiner2D>();
            if (confiner != null)
                confiner.BoundingShape2D = villageBoundary;
        }

        // Switch to village music
        if (AudioManager.Instance != null && !string.IsNullOrEmpty(villageZoneName))
            AudioManager.Instance.EnterZone(villageZoneName);

        // Sync physics after teleport
        Physics2D.SyncTransforms();

        // Fade in
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        // Start rescue dialogue (player stays locked during dialogue)
        if (dialogueManager != null && rescueDialogue != null)
        {
            // Subscribe to dialogue end to unlock movement
            dialogueManager.OnDialogueEnded += HandleRescueDialogueEnded;
            dialogueManager.StartDialogue(rescueDialogue);
        }
        else
        {
            // No dialogue set — just unlock
            if (playerMovement != null)
                playerMovement.UnlockMovement();
        }

        Debug.Log("[Respawn] Rescue sequence complete.");
    }

    private void HandleRescueDialogueEnded()
    {
        if (dialogueManager != null)
            dialogueManager.OnDialogueEnded -= HandleRescueDialogueEnded;

        // Now unlock player movement
        if (playerMovement != null)
            playerMovement.UnlockMovement();

        Debug.Log("[Respawn] Rescue dialogue finished. Player has control.");
    }
}
