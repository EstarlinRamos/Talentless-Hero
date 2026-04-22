using UnityEngine;
using Unity.Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Orchestrates the goddess intro cutscene in the starting area (T1/G1).
///
/// Flow:
///   1. CutsceneTrigger calls StartCutscene() when the player crosses the line
///   2. Player movement is locked
///   3. Goddess sprite appears
///   4. Dialogue plays via DialogueManager using the assigned DialogueData
///   5. Emotes are routed to the correct character via speaker name mapping
///   6. When dialogue emits the "teleport" tag:
///      - Fade to black
///      - Move player to village spawn point
///      - Update Cinemachine confiner boundary
///      - Fade back in
///   7. When dialogue ends, player control is restored
///
/// SPEAKER MAPPING:
///   The speakerEmotes list maps speaker names (as typed in DialogueLine.speaker)
///   to EmoteDisplay components on the corresponding character GameObjects.
///   This allows multiple characters to show emotes during the same conversation.
///
/// SETUP:
///   - Place on a GameObject in the starting scene
///   - Create a DialogueData asset and fill in the goddess dialogue
///   - Add EmoteDisplay components to each speaking character
///   - Map speaker names to EmoteDisplays in the speakerEmotes list
///   - Assign all other references
/// </summary>
public class CutsceneDirector : MonoBehaviour
{
    /// <summary>
    /// Maps a speaker name (from DialogueLine) to the EmoteDisplay on that character.
    /// </summary>
    [Serializable]
    public class SpeakerEmoteMapping
    {
        [Tooltip("Speaker name exactly as it appears in DialogueLine.speaker")]
        public string speakerName;

        [Tooltip("The EmoteDisplay component on this character's GameObject.")]
        public EmoteDisplay emoteDisplay;
    }

    [Header("Core References")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Dialogue")]
    [Tooltip("The DialogueData ScriptableObject containing the conversation.")]
    [SerializeField] private DialogueData dialogueData;

    [Header("Cutscene Positioning")]
    [Tooltip("Where the player is moved to when the cutscene starts. " +
             "Place this in front of the goddess so the scene is consistent.")]
    [SerializeField] private Transform cutscenePlayerPosition;

    [Tooltip("Direction the player faces during the cutscene. " +
             "(0,1) = up, (0,-1) = down, (-1,0) = left, (1,0) = right.")]
    [SerializeField] private Vector2 cutsceneFacingDirection = new Vector2(0, 1);

    [Header("Character Visuals")]
    [Tooltip("Characters to show when the cutscene starts. Disabled by default.")]
    [SerializeField] private List<GameObject> cutsceneCharacters = new List<GameObject>();

    [Header("Speaker → Emote Mapping")]
    [Tooltip("Maps speaker names to their EmoteDisplay components. " +
             "Names must match the speaker field in DialogueLine exactly.")]
    [SerializeField] private List<SpeakerEmoteMapping> speakerEmotes = new List<SpeakerEmoteMapping>();

    [Header("Teleport Destination")]
    [Tooltip("Where the player appears in the village after the cutscene.")]
    [SerializeField] private Transform villageSpawnPoint;

    [Tooltip("The village map boundary for Cinemachine confiner.")]
    [SerializeField] private PolygonCollider2D villageBoundary;

    [Tooltip("Zone name for the teleport destination. Must match ZoneMusicConfig exactly (e.g., V1). " +
             "Tells AudioManager which zone's music to play after teleport.")]
    [SerializeField] private string villageZoneName = "V1";

    [Tooltip("Direction the player faces after arriving in the village. " +
             "(0,-1) = down is typical for arriving in a new area.")]
    [SerializeField] private Vector2 villageFacingDirection = new Vector2(0, -1);

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 1.0f;
    [SerializeField] private float blackScreenHold = 0.5f;
    [SerializeField] private float fadeInDuration = 1.0f;

    private bool _cutsceneActive = false;
    private Dictionary<string, EmoteDisplay> _emoteLookup;

    private void Awake()
    {
        // Build lookup for speaker → emote routing
        _emoteLookup = new Dictionary<string, EmoteDisplay>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in speakerEmotes)
        {
            if (!string.IsNullOrEmpty(mapping.speakerName) && mapping.emoteDisplay != null)
                _emoteLookup[mapping.speakerName] = mapping.emoteDisplay;
        }
    }

    // ═════════════════════════════════════════════
    //  Public API — called by CutsceneTrigger
    // ═════════════════════════════════════════════

    public void StartCutscene()
    {
        if (_cutsceneActive) return;
        _cutsceneActive = true;

        // Lock the player
        if (playerMovement != null)
        {
            playerMovement.LockMovement();

            // Snap player to the cutscene position (in front of the goddess)
            if (cutscenePlayerPosition != null)
                playerMovement.transform.position = cutscenePlayerPosition.position;

            // Face the player toward the goddess
            playerMovement.SetFacingDirection(cutsceneFacingDirection.x, cutsceneFacingDirection.y);
        }

        // Show cutscene characters (goddess, angel guard, etc.)
        foreach (var character in cutsceneCharacters)
        {
            if (character != null)
                character.SetActive(true);
        }

        // Subscribe to dialogue events
        if (dialogueManager != null)
        {
            dialogueManager.OnTeleportRequested += HandleTeleport;
            dialogueManager.OnDialogueEnded += HandleDialogueEnded;
            dialogueManager.OnCutsceneEnd += HandleCutsceneEnd;
            dialogueManager.OnEmoteRequested += HandleEmote;

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

    private void HandleEmote(string speaker, string emote)
    {
        if (_emoteLookup.TryGetValue(speaker, out EmoteDisplay display))
        {
            display.ShowEmote(emote);
        }
        else
        {
            Debug.LogWarning($"[Director] No EmoteDisplay mapped for speaker '{speaker}'. " +
                             "Add an entry to speakerEmotes.");
        }
    }

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

        yield return new WaitForSeconds(blackScreenHold);

        // Move the player
        if (villageSpawnPoint != null && playerMovement != null)
        {
            playerMovement.transform.position = villageSpawnPoint.position;
            playerMovement.SetFacingDirection(villageFacingDirection.x, villageFacingDirection.y);
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

        // Hide cutscene characters
        foreach (var character in cutsceneCharacters)
        {
            if (character != null)
                character.SetActive(false);
        }

        // Play the village zone music
        if (AudioManager.Instance != null && !string.IsNullOrEmpty(villageZoneName))
            AudioManager.Instance.EnterZone(villageZoneName);

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
            dialogueManager.OnEmoteRequested -= HandleEmote;
        }

        // Hide any lingering emotes
        foreach (var mapping in speakerEmotes)
        {
            if (mapping.emoteDisplay != null)
                mapping.emoteDisplay.HideEmote();
        }

        // Restore player control
        if (playerMovement != null)
            playerMovement.UnlockMovement();

        _cutsceneActive = false;
        Debug.Log("[Director] Cutscene complete. Player has control.");
    }
}
