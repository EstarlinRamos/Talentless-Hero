using UnityEngine;
using System.Collections;

/// <summary>
/// Handles gameplay-affecting dialogue tags: "rest" and "kick".
/// Subscribes to DialogueManager events and executes the appropriate actions.
///
/// "rest" — Fully restores player HP and MP. Triggered by the Innkeeper's
///          dialogue when the player chooses to rest.
///
/// "kick" — Pushes the player backward. Triggered by Guards C/D when
///          the player tries to pass the blocked road.
///
/// SETUP:
///   1. Create an empty GameObject called "DialogueTagHandler"
///   2. Attach this script
///   3. Assign the player references
///   4. Optionally assign SFX for rest and kick actions
///   5. For kick, assign the pushback direction and distance
/// </summary>
public class DialogueTagHandler : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Rest Settings")]
    [Tooltip("Sound played when the player rests at the inn.")]
    [SerializeField] private AudioClip restSFX;

    [Range(0f, 1f)]
    [SerializeField] private float restSFXVolume = 0.6f;

    [Header("Kick Settings")]
    [Tooltip("How far the player is pushed back (in world units).")]
    [SerializeField] private float kickDistance = 3f;

    [Tooltip("Sound played when the player is kicked back by guards.")]
    [SerializeField] private AudioClip kickSFX;

    [Range(0f, 1f)]
    [SerializeField] private float kickSFXVolume = 0.5f;

    private void Start()
    {
        // Auto-find player if not assigned
        if (playerStats == null || playerMovement == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                if (playerStats == null)
                    playerStats = player.GetComponent<PlayerStats>();
                if (playerMovement == null)
                    playerMovement = player.GetComponent<PlayerMovement>();
            }
        }
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnRestRequested -= HandleRest;
            DialogueManager.Instance.OnKickRequested -= HandleKick;
        }
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (DialogueManager.Instance == null)
            yield return null;

        DialogueManager.Instance.OnRestRequested += HandleRest;
        DialogueManager.Instance.OnKickRequested += HandleKick;
        Debug.Log("[DialogueTagHandler] Subscribed to rest/kick events.");
    }

    // ─────────────────────────────────────────────
    //  Rest — Innkeeper heals the player
    // ─────────────────────────────────────────────

    private void HandleRest()
    {
        if (playerStats == null)
        {
            Debug.LogWarning("[DialogueTagHandler] Cannot rest — PlayerStats not found!");
            return;
        }

        playerStats.Rest();

        if (restSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(restSFX, restSFXVolume);

        Debug.Log("[DialogueTagHandler] Player rested. HP/MP fully restored.");
    }

    // ─────────────────────────────────────────────
    //  Kick — Guards push the player back
    // ─────────────────────────────────────────────

    private void HandleKick()
    {
        if (playerMovement == null)
        {
            Debug.LogWarning("[DialogueTagHandler] Cannot kick — PlayerMovement not found!");
            return;
        }

        // Push the player backward based on their facing direction
        Animator anim = playerMovement.GetComponent<Animator>();
        Vector2 pushDir = Vector2.down; // Default fallback

        if (anim != null)
        {
            float lx = anim.GetFloat("LastInputX");
            float ly = anim.GetFloat("LastInputY");

            if (Mathf.Abs(lx) > 0.01f || Mathf.Abs(ly) > 0.01f)
                pushDir = -new Vector2(lx, ly).normalized; // Opposite of facing
        }

        Vector3 pushOffset = (Vector3)(pushDir * kickDistance);
        playerMovement.transform.position += pushOffset;

        Physics2D.SyncTransforms();

        if (kickSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(kickSFX, kickSFXVolume);

        Debug.Log($"[DialogueTagHandler] Player kicked back by {kickDistance} units.");
    }
}
