using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

/// <summary>
/// Progression-aware NPC dialogue trigger using distance-based detection.
/// No trigger collider needed — just checks distance to the player every frame.
///
/// Shows an interaction prompt (E) when the player is within range,
/// starts dialogue when E is pressed.
///
/// Dialogue Priority (checked in order):
///   1. Conditional dialogues — if any condition is met:
///      a. One-shot (first time condition is true)
///      b. Repeat (subsequent interactions while condition holds)
///   2. First meeting dialogue — plays once
///   3. Default dialogue — all subsequent interactions
///
/// SETUP:
///   1. Attach to any NPC GameObject (no collider needed)
///   2. Assign dialogue assets for each state
///   3. Create an InteractionPrompt child (disabled by default)
///   4. Set the interaction range in the Inspector
///   5. Yellow gizmo shows the range in Scene view
/// </summary>
public class NPCDialogueTrigger : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Dialogue Condition
    // ─────────────────────────────────────────────

    [Serializable]
    public class DialogueCondition
    {
        [Tooltip("Label for organization in the Inspector.")]
        public string editorLabel = "Condition";

        [Tooltip("If this enemy's IsPermanentlyDefeated is true, this condition is met.")]
        public EnemyStats bossToCheck;

        [Tooltip("Dialogue played the first time this condition is met.")]
        public DialogueData oneShotDialogue;

        [Tooltip("Dialogue played on all interactions after the one-shot.")]
        public DialogueData repeatDialogue;

        [HideInInspector]
        public bool oneShotPlayed = false;

        public bool IsMet()
        {
            if (bossToCheck != null)
                return bossToCheck.IsPermanentlyDefeated;
            return false;
        }
    }

    // ─────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────

    [Header("Dialogue — First Meeting")]
    [Tooltip("Full dialogue tree played the first time the player talks to this NPC.")]
    [SerializeField] private DialogueData firstMeetingDialogue;

    [Header("Dialogue — Default (Repeating)")]
    [Tooltip("Short dialogue played on all interactions after the first meeting.")]
    [SerializeField] private DialogueData defaultDialogue;

    [Header("Dialogue — Conditional (Game Events)")]
    [Tooltip("Dialogues that activate when conditions are met. First match wins.")]
    [SerializeField] private List<DialogueCondition> conditionalDialogues = new List<DialogueCondition>();

    [Header("Identity")]
    [Tooltip("Unique ID for this NPC (used to persist met/one-shot state across saves). " +
             "Use lowercase with no spaces, e.g. 'innkeeper', 'guard_a', 'old_man'.")]
    [SerializeField] private string npcID = "";

    [Header("Interaction")]
    [Tooltip("How close the player must be to interact (in world units).")]
    [SerializeField] private float interactionRange = 1.5f;

    [Tooltip("CanvasGroup on the interaction prompt. Stays enabled, hidden via alpha.")]
    [SerializeField] private CanvasGroup promptGroup;

    [Header("Settings")]
    [Tooltip("If true, locks player movement during dialogue.")]
    [SerializeField] private bool lockPlayerDuringDialogue = true;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private bool _playerInRange = false;
    private bool _hasMetPlayer = false;
    private Transform _playerTransform;
    private PlayerMovement _playerMovement;

    private void Start()
    {
        HidePrompt();

        // Cache player reference
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerMovement = player.GetComponent<PlayerMovement>();
        }

        // Restore persisted state from WorldFlagManager
        RestoreFromFlags();
    }

    /// <summary>
    /// Read flags to restore whether we've met the player and which
    /// conditional one-shots have already played.
    /// </summary>
    private void RestoreFromFlags()
    {
        if (WorldFlagManager.Instance == null || string.IsNullOrEmpty(npcID)) return;

        _hasMetPlayer = WorldFlagManager.Instance.HasFlag($"npc_met_{npcID}");

        for (int i = 0; i < conditionalDialogues.Count; i++)
        {
            conditionalDialogues[i].oneShotPlayed =
                WorldFlagManager.Instance.HasFlag($"npc_oneshot_{npcID}_{i}");
        }
    }

    private void Update()
    {
        if (_playerTransform == null) return;

        // Check distance to player
        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        bool inRange = dist <= interactionRange;

        // Player entered range
        if (inRange && !_playerInRange)
        {
            _playerInRange = true;
            ShowPrompt();
        }
        // Player left range
        else if (!inRange && _playerInRange)
        {
            _playerInRange = false;
            HidePrompt();
        }

        // Check for E key press while in range
        if (!_playerInRange) return;
        if (Keyboard.current == null) return;
        if (!Keyboard.current.eKey.wasPressedThisFrame) return;
        if (DialogueManager.IsInDialogue) return;
        if (CombatUIManager.IsInCombat) return;

        StartNPCDialogue();
    }

    // ─────────────────────────────────────────────
    //  Dialogue Selection
    // ─────────────────────────────────────────────

    private void StartNPCDialogue()
    {
        DialogueData dialogue = ResolveDialogue();

        if (dialogue == null)
        {
            Debug.LogWarning($"[NPC] {gameObject.name} has no dialogue to play!");
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[NPC] No DialogueManager found in scene!");
            return;
        }

        // Hide prompt during dialogue
        HidePrompt();

        // Lock player
        if (lockPlayerDuringDialogue && _playerMovement != null)
            _playerMovement.LockMovement();

        // Subscribe to dialogue end
        DialogueManager.Instance.OnDialogueEnded += HandleDialogueEnded;

        // Start the conversation
        DialogueManager.Instance.StartDialogue(dialogue);
    }

    /// <summary>
    /// Determine which dialogue to play based on current game state.
    /// Priority: conditional (one-shot then repeat) > first meeting > default.
    /// </summary>
    private DialogueData ResolveDialogue()
    {
        bool hasFlags = WorldFlagManager.Instance != null && !string.IsNullOrEmpty(npcID);

        // 1. Check conditional dialogues (first matching condition wins)
        for (int i = 0; i < conditionalDialogues.Count; i++)
        {
            var condition = conditionalDialogues[i];
            if (condition.IsMet())
            {
                if (!condition.oneShotPlayed && condition.oneShotDialogue != null)
                {
                    condition.oneShotPlayed = true;
                    if (hasFlags)
                        WorldFlagManager.Instance.SetFlag($"npc_oneshot_{npcID}_{i}");
                    return condition.oneShotDialogue;
                }

                if (condition.repeatDialogue != null)
                    return condition.repeatDialogue;
            }
        }

        // 2. First meeting (one-shot)
        if (!_hasMetPlayer && firstMeetingDialogue != null)
        {
            _hasMetPlayer = true;
            if (hasFlags)
                WorldFlagManager.Instance.SetFlag($"npc_met_{npcID}");
            return firstMeetingDialogue;
        }

        // 3. Default repeating dialogue
        if (!_hasMetPlayer)
        {
            _hasMetPlayer = true;
            if (hasFlags)
                WorldFlagManager.Instance.SetFlag($"npc_met_{npcID}");
        }
        return defaultDialogue;
    }

    // ─────────────────────────────────────────────
    //  Cleanup
    // ─────────────────────────────────────────────

    private void HandleDialogueEnded()
    {
        DialogueManager.Instance.OnDialogueEnded -= HandleDialogueEnded;

        // Unlock player
        if (lockPlayerDuringDialogue && _playerMovement != null)
            _playerMovement.UnlockMovement();

        // Re-show prompt if still in range
        if (_playerInRange)
            ShowPrompt();
    }

    // ─────────────────────────────────────────────
    //  Prompt Visibility
    // ─────────────────────────────────────────────

    private void ShowPrompt()
    {
        if (promptGroup == null) return;
        promptGroup.alpha = 1f;
    }

    private void HidePrompt()
    {
        if (promptGroup == null) return;
        promptGroup.alpha = 0f;
    }

    // ─────────────────────────────────────────────
    //  Gizmos
    // ─────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
