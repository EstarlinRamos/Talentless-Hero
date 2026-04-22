using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

/// <summary>
/// Handles zone-to-zone transitions within the same scene.
/// Updates camera confiner, repositions the player, changes zone music,
/// and optionally plays a fade effect and transition sound.
/// </summary>
public class MapTransition : MonoBehaviour
{
    [SerializeField] private PolygonCollider2D mapboundary;
    private CinemachineConfiner2D confiner;

    [SerializeField] private Direction direction;
    [SerializeField] private float additivePos = 2f;

    [Header("Zone Music")]
    [Tooltip("Zone name for this boundary (e.g. T1, G1, V1, P1, C1). " +
             "Must match the name in ZoneMusicConfig. " +
             "Leave empty to keep current music playing.")]
    [SerializeField] private string zoneName = "";

    [Header("Transition Effects")]
    [Tooltip("Enable a brief fade-to-black when crossing this boundary.")]
    [SerializeField] private bool useFade = true;

    [Tooltip("Duration of the fade out (to black).")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    [Tooltip("Duration of the fade in (from black).")]
    [SerializeField] private float fadeInDuration = 0.3f;

    [Tooltip("Optional sound effect played during the transition.")]
    [SerializeField] private AudioClip transitionSFX;

    [Tooltip("Volume for the transition sound (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float transitionSFXVolume = 0.7f;

    private bool _isTransitioning = false;

    enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    private void Awake()
    {
        confiner = FindFirstObjectByType<CinemachineConfiner2D>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        if (_isTransitioning) return;

        if (useFade && ScreenFader.Instance != null)
        {
            StartCoroutine(FadedTransition(collision.transform));
        }
        else
        {
            ExecuteTransition(collision.transform);
        }
    }

    /// <summary>
    /// Instant transition (no fade). Used when useFade is false.
    /// </summary>
    private void ExecuteTransition(Transform player)
    {
        confiner.BoundingShape2D = mapboundary;
        UpdatePlayerPosition(player);
        PlayTransitionSFX();
        UpdateZoneMusic();
    }

    /// <summary>
    /// Transition with a brief fade to black and back.
    /// </summary>
    private IEnumerator FadedTransition(Transform player)
    {
        _isTransitioning = true;

        // Lock player movement during fade
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.LockMovement();

        PlayTransitionSFX();

        // Fade out
        yield return ScreenFader.Instance.FadeOut(fadeOutDuration);

        // Move everything while the screen is black
        confiner.BoundingShape2D = mapboundary;
        UpdatePlayerPosition(player);
        UpdateZoneMusic();

        // Brief hold on black
        yield return new WaitForSeconds(0.1f);

        // Fade back in
        yield return ScreenFader.Instance.FadeIn(fadeInDuration);

        // Restore movement
        if (movement != null)
            movement.UnlockMovement();

        _isTransitioning = false;
    }

    private void UpdatePlayerPosition(Transform player)
    {
        Vector3 newPos = player.position;
        switch (direction)
        {
            case Direction.Up:
                newPos.y += additivePos;
                break;
            case Direction.Down:
                newPos.y -= additivePos;
                break;
            case Direction.Left:
                newPos.x -= additivePos;
                break;
            case Direction.Right:
                newPos.x += additivePos;
                break;
        }
        player.position = newPos;
    }

    private void PlayTransitionSFX()
    {
        if (transitionSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(transitionSFX, transitionSFXVolume);
    }

    private void UpdateZoneMusic()
    {
        if (!string.IsNullOrEmpty(zoneName) && AudioManager.Instance != null)
            AudioManager.Instance.EnterZone(zoneName);
    }
}
