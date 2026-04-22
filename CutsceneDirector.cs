using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Orchestrates the goddess intro cutscene in the starting area (T1/G1).
///
/// Flow:
///   1. CutsceneTrigger calls StartCutscene() when the player crosses the line
///   2. Player movement is locked
///   3. Goddess sprite appears
///   4. Dialogue plays via DialogueManager using the assigned DialogueData
///   5. When dialogue emits the "teleport" tag:
///      - Fade to black
///      - Move player to village spawn point
///      - Update Cinemachine confiner boundary
///      - Fade back in
///   6. When dialogue ends, player control is restored
///
/// SETUP:
///   - Place on a GameObject in the starting scene
///   - Create a DialogueData asset (Right-click → Create → Talentless Hero → Dialogue Data)
///   - Fill in the goddess dialogue nodes in the Inspector
///   - Assign all references
/// </summary>
public class CutsceneDirector : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Dialogue")]
    [Tooltip("The DialogueData ScriptableObject containing the goddess conversation.")]
    [SerializeField] private DialogueData dialogueData;

    [Header("Goddess Visuals")]
    [Tooltip("The Goddess sprite/GameObject. Disabled by default, enabled during cutscene.")]
    [SerializeField] private GameObject goddessObject;

    [Header("Teleport Destination")]
    [Tooltip("Where the player appears in the village after the cutscene.")]
    [SerializeField] private Transform villageSpawnPoint;

    [Tooltip("The village map boundary for Cinemachine confiner.")]
    [SerializeField] private PolygonCollider2D villageBoundary;

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 1.0f;
    [SerializeField] private float blackScreenHold = 0.5f;
    [SerializeField] private float fadeInDuration = 1.0f;

    private bool _cutsceneActive = false;

    // ═════════════════════════════════════════════
    //  Public API — called by CutsceneTrigger
    // ═════════════════════════════════════════════

    public void StartCutscene()
    {
        if (_cutsceneActive) return;
        _cutsceneActive = true;

        // Lock the player in place
        if (playerMovement != null)
            playerMovement.LockMovement();

        // Show the goddess
        if (goddessObject != null)
            goddessObject.SetActive(true);

        // Subscribe to dialogue events
        if (dialogueManager != null)
        {
            dialogueManager.OnTeleportRequested += HandleTeleport;
            dialogueManager.OnDialogueEnded += HandleDialogueEnded;
            dialogueManager.OnCutsceneEnd += HandleCutsceneEnd;

            // Start the dialogue
            dialogueManager.StartDialogue(dialogueData);
        }
        else
        {
            Debug.LogError("[Director] DialogueManager not assigned!");
        }
    }

    // ═════════════════════════════════════════════
    //  Event Handlers
    // ═════════════════════════════════════════════

    private void HandleTeleport()
    {
        StartCoroutine(TeleportSequence());
    }

    private void HandleDialogueEnded()
    {
        StartCoroutine(PostDialogueCleanup());
    }

    private void HandleCutsceneEnd()
    {
        StartCoroutine(PostDialogueCleanup());
    }

    // ═════════════════════════════════════════════
    //  Teleport Sequence
    // ═════════════════════════════════════════════

    private IEnumerator TeleportSequence()
    {
        Debug.Log("[Director] Teleport sequence starting...");

        // Fade to black
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        // Hold on black
        yield return new WaitForSeconds(blackScreenHold);

        // Move the player
        if (villageSpawnPoint != null && playerMovement != null)
        {
            playerMovement.transform.position = villageSpawnPoint.position;
            Debug.Log($"[Director] Player teleported to {villageSpawnPoint.position}");
        }

        // Update Cinemachine confiner to village boundary
        if (villageBoundary != null)
        {
            CinemachineConfiner2D confiner = FindFirstObjectByType<CinemachineConfiner2D>();
            if (confiner != null)
            {
                confiner.BoundingShape2D = villageBoundary;
                Debug.Log("[Director] Camera boundary updated to village.");
            }
        }

        // Hide the goddess
        if (goddessObject != null)
            goddessObject.SetActive(false);

        // Notify audio system of zone change
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayOverworldMusic();

        // Fade back in
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        Debug.Log("[Director] Teleport complete. Player is in Briarwood Village.");
    }

    // ═════════════════════════════════════════════
    //  Cleanup
    // ═════════════════════════════════════════════

    private IEnumerator PostDialogueCleanup()
    {
        yield return new WaitForSeconds(0.3f);

        // Unsubscribe
        if (dialogueManager != null)
        {
            dialogueManager.OnTeleportRequested -= HandleTeleport;
            dialogueManager.OnDialogueEnded -= HandleDialogueEnded;
            dialogueManager.OnCutsceneEnd -= HandleCutsceneEnd;
        }

        // Restore player control
        if (playerMovement != null)
            playerMovement.UnlockMovement();

        _cutsceneActive = false;
        Debug.Log("[Director] Cutscene complete. Player has control.");
    }
}
