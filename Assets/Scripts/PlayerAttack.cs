using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Player sword swing attack on the overworld.
/// Press Spacebar to swing in the direction the player is facing.
///
/// Supports rapid swinging — each new press interrupts the previous swing
/// and restarts the animation immediately. A short minimum cooldown prevents
/// input spam from breaking things.
///
/// If the swing hitbox overlaps an enemy, combat starts with player advantage.
/// If the player doesn't swing and an enemy touches them instead, enemy advantage.
///
/// SETUP:
///   1. Attach to the Player GameObject
///   2. Create a child "AttackHitbox" with a trigger Collider2D (disabled by default)
///   3. Create a child "Sword" with SpriteRenderer + Animator (enabled, sprite hidden)
///   4. Sword Animator needs: Float InputX, Float InputY, Blend Tree for directional swings
///   5. The Animator's default state should be the swing Blend Tree (NOT an idle state)
///      since visibility is controlled by the SpriteRenderer, not the Animator
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Hitbox")]
    [Tooltip("Child GameObject with a trigger Collider2D for the attack area.")]
    [SerializeField] private GameObject attackHitbox;

    [Tooltip("The Sword visual GameObject (sprite + Animator). " +
             "Keep this ENABLED — visibility is controlled via SpriteRenderer.")]
    [SerializeField] private GameObject swordVisual;

    [Tooltip("Offset distance from the player center to position the hitbox.")]
    [SerializeField] private float hitboxOffset = 0.8f;

    [Tooltip("Offset distance from the player center to position the sword visual.")]
    [SerializeField] private float swordVisualOffset = 1.0f;

    [Header("Timing")]
    [Tooltip("How long the hitbox stays active per swing (the damage window).")]
    [SerializeField] private float swingDuration = 0.15f;

    [Tooltip("How long the sword visual stays visible. Match your animation clip length.")]
    [SerializeField] private float swordVisualDuration = 0.35f;

    [Tooltip("Minimum time between swings. Keep short for responsive rapid attacks. " +
             "Must be less than or equal to swordVisualDuration.")]
    [SerializeField] private float minSwingInterval = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip swingSFX;

    [Range(0f, 1f)]
    [SerializeField] private float swingSFXVolume = 0.5f;

    [Header("Animation")]
    [Tooltip("Animator on the PLAYER — used to read facing direction.")]
    [SerializeField] private Animator playerAnimator;

    [Tooltip("Animator on the SWORD — plays directional swing animations.")]
    [SerializeField] private Animator swordAnimator;

    [Tooltip("Name of the Blend Tree state in the Sword Animator. " +
             "Used with Animator.Play() for instant restarts.")]
    [SerializeField] private string swingStateName = "SwingBlend";

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────
    private Vector2 _facingDirection = Vector2.down;
    private PlayerMovement _movement;
    private SpriteRenderer _swordRenderer;
    private Coroutine _activeSwing;
    private float _lastSwingTime = -10f;

    private void Start()
    {
        _movement = GetComponent<PlayerMovement>();

        if (attackHitbox != null)
            attackHitbox.SetActive(false);

        if (swordVisual != null)
        {
            _swordRenderer = swordVisual.GetComponent<SpriteRenderer>();
            if (_swordRenderer != null)
                _swordRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (CombatUIManager.IsInCombat) return;
        if (DialogueManager.IsInDialogue) return;

        // Post-dialogue cooldown — prevent sword swing from the same Space press
        // that closed the dialogue
        if (Time.time - DialogueManager.LastDialogueEndTime < 0.3f) return;

        // Always track facing direction
        UpdateFacingDirection();

        // Check for swing input
        if (Keyboard.current == null) return;
        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

        // Enforce minimum interval between swings
        if (Time.time - _lastSwingTime < minSwingInterval) return;

        // Interrupt any active swing and start a new one
        if (_activeSwing != null)
            StopCoroutine(_activeSwing);

        _activeSwing = StartCoroutine(SwingAttack());
    }

    private void UpdateFacingDirection()
    {
        if (playerAnimator == null) return;

        float ix = playerAnimator.GetFloat("InputX");
        float iy = playerAnimator.GetFloat("InputY");

        if (Mathf.Abs(ix) > 0.01f || Mathf.Abs(iy) > 0.01f)
        {
            _facingDirection = new Vector2(ix, iy).normalized;
        }
        else
        {
            float lx = playerAnimator.GetFloat("LastInputX");
            float ly = playerAnimator.GetFloat("LastInputY");
            if (Mathf.Abs(lx) > 0.01f || Mathf.Abs(ly) > 0.01f)
                _facingDirection = new Vector2(lx, ly).normalized;
        }
    }

    private IEnumerator SwingAttack()
    {
        _lastSwingTime = Time.time;

        // Position and show sword visual
        if (swordVisual != null)
            swordVisual.transform.localPosition = (Vector3)(_facingDirection * swordVisualOffset);

        if (_swordRenderer != null)
            _swordRenderer.enabled = true;

        // Position hitbox
        if (attackHitbox != null)
        {
            attackHitbox.transform.localPosition = (Vector3)(_facingDirection * hitboxOffset);

            float angle = Mathf.Atan2(_facingDirection.y, _facingDirection.x) * Mathf.Rad2Deg;
            attackHitbox.transform.localRotation = Quaternion.Euler(0, 0, angle);

            attackHitbox.SetActive(true);
        }

        // Force-play the swing animation from the start
        // Using Play() instead of SetTrigger() guarantees it restarts
        // even if the previous swing hasn't finished
        if (swordAnimator != null)
        {
            swordAnimator.SetFloat("InputX", _facingDirection.x);
            swordAnimator.SetFloat("InputY", _facingDirection.y);
            swordAnimator.Play(swingStateName, 0, 0f);
        }

        // Play SFX
        if (swingSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(swingSFX, swingSFXVolume);

        // Damage window
        yield return new WaitForSeconds(swingDuration);

        if (attackHitbox != null)
            attackHitbox.SetActive(false);

        // Wait for animation to finish visually
        float remainingVisual = swordVisualDuration - swingDuration;
        if (remainingVisual > 0f)
            yield return new WaitForSeconds(remainingVisual);

        // Hide sword
        if (_swordRenderer != null)
            _swordRenderer.enabled = false;

        _activeSwing = null;
    }
}
