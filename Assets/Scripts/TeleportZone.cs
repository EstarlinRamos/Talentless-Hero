using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

/// <summary>
/// Teleports the player to a specific spawn point in another zone.
/// Uses a walk-in trigger — the player steps into the zone and is
/// teleported to the destination.
///
/// Unlike MapTransition (which nudges the player by a fixed amount),
/// this uses an explicit spawn point. The player always lands exactly
/// where you place the destination marker, regardless of direction
/// or boundary shape.
///
/// Use for:
///   - Zone connections that MapTransition struggles with (horizontal, etc.)
///   - Cave entrances/exits
///   - Door transitions
///   - Any non-adjacent zone link
///
/// SETUP:
///   1. Create an empty GameObject at the zone exit (e.g., "V1_Exit_South")
///   2. Add BoxCollider2D → Is Trigger checked → thin strip across the path
///   3. Attach this script
///   4. Create an empty at the destination inside the next zone (e.g., "P1_Entrance_North")
///   5. Assign the spawn point, boundary, zone name, and facing direction
///   6. Create a paired TeleportZone in the other zone pointing back
///
/// IMPORTANT:
///   Place spawn points several units INSIDE the destination zone,
///   not right at the edge. This prevents the player from immediately
///   hitting the return trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TeleportZone : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Where the player appears. Place INSIDE the destination zone, " +
             "several units away from the return trigger.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Camera boundary for the destination zone.")]
    [SerializeField] private PolygonCollider2D destinationBoundary;

    [Header("Zone")]
    [Tooltip("Zone name for music switching (must match ZoneMusicConfig). " +
             "Leave empty to keep current music.")]
    [SerializeField] private string zoneName = "";

    [Header("Player Facing")]
    [Tooltip("Direction the player faces on arrival. " +
             "(0,-1)=down, (0,1)=up, (-1,0)=left, (1,0)=right.")]
    [SerializeField] private Vector2 facingDirection = new Vector2(0, -1);

    [Header("Fade")]
    [Tooltip("Fade to black during transition.")]
    [SerializeField] private bool useFade = false;

    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float fadeInDuration = 0.25f;

    [Header("Audio")]
    [Tooltip("Sound played during transition.")]
    [SerializeField] private AudioClip transitionSFX;

    [Range(0f, 1f)]
    [SerializeField] private float transitionSFXVolume = 0.5f;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────
    private CinemachineConfiner2D _confiner;
    private static bool _transitioning = false;

    private void Awake()
    {
        _confiner = FindFirstObjectByType<CinemachineConfiner2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_transitioning) return;
        if (DialogueManager.IsInDialogue) return;
        if (CombatUIManager.IsInCombat) return;

        if (spawnPoint == null)
        {
            Debug.LogError($"[TeleportZone] {gameObject.name}: No spawn point assigned!");
            return;
        }

        if (useFade)
            StartCoroutine(FadeTransition(other.transform));
        else
            ExecuteTransition(other.transform);
    }

    private void ExecuteTransition(Transform player)
    {
        _transitioning = true;

        // Play SFX
        if (transitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(transitionSFX, transitionSFXVolume);

        // Move player to spawn point
        player.position = new Vector3(spawnPoint.position.x, spawnPoint.position.y, 0f);
        Physics2D.SyncTransforms();

        // Update camera
        if (_confiner != null && destinationBoundary != null)
            _confiner.BoundingShape2D = destinationBoundary;

        // Set facing
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.SetFacingDirection(facingDirection.x, facingDirection.y);

        // Switch music
        if (!string.IsNullOrEmpty(zoneName) && AudioManager.Instance != null)
            AudioManager.Instance.EnterZone(zoneName);

        StartCoroutine(ResetFlag());
    }

    private IEnumerator FadeTransition(Transform player)
    {
        _transitioning = true;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.LockMovement();

        // Play SFX
        if (transitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(transitionSFX, transitionSFXVolume);

        // Fade out
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        // Teleport
        player.position = new Vector3(spawnPoint.position.x, spawnPoint.position.y, 0f);
        Physics2D.SyncTransforms();

        // Camera
        if (_confiner != null && destinationBoundary != null)
            _confiner.BoundingShape2D = destinationBoundary;

        // Facing
        if (movement != null)
            movement.SetFacingDirection(facingDirection.x, facingDirection.y);

        // Music
        if (!string.IsNullOrEmpty(zoneName) && AudioManager.Instance != null)
            AudioManager.Instance.EnterZone(zoneName);

        // Fade in
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        if (movement != null)
            movement.UnlockMovement();

        yield return ResetFlag();
    }

    private IEnumerator ResetFlag()
    {
        // Wait a few frames to prevent the destination's return trigger
        // from immediately sending the player back
        yield return null;
        yield return null;
        yield return null;
        _transitioning = false;
    }

    // ─────────────────────────────────────────────
    //  Gizmos
    // ─────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (spawnPoint != null)
        {
            // Cyan line from trigger to destination
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawLine(transform.position, spawnPoint.position);

            // Destination marker
            Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
            Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);

            // Facing arrow
            Gizmos.color = Color.yellow;
            Vector3 arrowEnd = spawnPoint.position + (Vector3)(facingDirection.normalized * 0.5f);
            Gizmos.DrawLine(spawnPoint.position, arrowEnd);
        }

        // Trigger zone
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
            Vector3 center = transform.position + (Vector3)box.offset;
            Gizmos.DrawCube(center, box.size);
        }
    }
}
