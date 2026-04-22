using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player movement with walking, sprinting, and footstep sounds.
///
/// Controls:
///   - WASD / Arrow Keys: Move
///   - Left Shift (hold): Sprint
///
/// Footstep SFX plays at timed intervals while the player is moving.
/// The interval shortens when sprinting for a faster pace.
/// Multiple footstep clips can be assigned for variety — one is
/// picked randomly each step to avoid repetition.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator animator;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    [Header("Footstep Audio")]
    [Tooltip("One or more footstep clips. A random one is picked each step. " +
             "Assign at least one for footstep sounds to play.")]
    [SerializeField] private AudioClip[] footstepClips;

    [Tooltip("Volume for footstep sounds (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float footstepVolume = 0.4f;

    [Tooltip("Seconds between footstep sounds while walking.")]
    [SerializeField] private float walkStepInterval = 0.4f;

    [Tooltip("Seconds between footstep sounds while sprinting.")]
    [SerializeField] private float sprintStepInterval = 0.25f;

    [Tooltip("Slight random pitch variation per step for natural feel.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float footstepPitchVariation = 0.1f;

    private Vector2 moveInput;
    private bool _movementLocked = false;
    private bool _isSprinting = false;
    private float _footstepTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (_movementLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Check sprint input
        _isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        // Apply movement
        float currentSpeed = _isSprinting ? sprintSpeed : walkSpeed;
        rb.linearVelocity = moveInput * currentSpeed;

        // Update animator speed parameter if you want faster walk animation while sprinting
        animator.SetFloat("MoveSpeed", _isSprinting ? 1.5f : 1f);

        // Footstep sounds
        UpdateFootsteps();
    }

    public void move(InputAction.CallbackContext context)
    {
        if (_movementLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (context.canceled)
        {
            animator.SetBool("IsWalking", false);
            animator.SetFloat("LastInputX", moveInput.x);
            animator.SetFloat("LastInputY", moveInput.y);
            moveInput = Vector2.zero;
            _footstepTimer = 0f;
        }
        else
        {
            moveInput = context.ReadValue<Vector2>();
            animator.SetBool("IsWalking", true);
        }

        animator.SetFloat("InputX", moveInput.x);
        animator.SetFloat("InputY", moveInput.y);
    }

    // ═════════════════════════════════════════════
    //  Footstep Audio
    // ═════════════════════════════════════════════

    private void UpdateFootsteps()
    {
        // Only play footsteps when actually moving
        if (moveInput == Vector2.zero || footstepClips == null || footstepClips.Length == 0)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer += Time.deltaTime;

        float interval = _isSprinting ? sprintStepInterval : walkStepInterval;

        if (_footstepTimer >= interval)
        {
            _footstepTimer = 0f;
            PlayFootstep();
        }
    }

    private void PlayFootstep()
    {
        if (AudioManager.Instance == null) return;

        // Pick a random clip for variety
        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];

        // Slight pitch variation so steps don't sound robotic
        float pitch = 1f + Random.Range(-footstepPitchVariation, footstepPitchVariation);

        AudioManager.Instance.PlaySFX(clip, footstepVolume * pitch);
    }

    // ═════════════════════════════════════════════
    //  Movement Lock (cutscenes, dialogue, menus)
    // ═════════════════════════════════════════════

    /// <summary>
    /// Freeze the player in place. Stops velocity and ignores input.
    /// Preserves the direction the player was facing.
    /// </summary>
    public void LockMovement()
    {
        _movementLocked = true;

        // Save the current facing direction before zeroing input
        if (moveInput != Vector2.zero)
        {
            animator.SetFloat("LastInputX", moveInput.x);
            animator.SetFloat("LastInputY", moveInput.y);
        }

        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("IsWalking", false);
        animator.SetFloat("InputX", 0f);
        animator.SetFloat("InputY", 0f);
        _footstepTimer = 0f;
    }

    /// <summary>
    /// Restore player control.
    /// </summary>
    public void UnlockMovement()
    {
        _movementLocked = false;
    }

    /// <summary>
    /// Force the player's idle facing direction.
    /// Useful for cutscenes where the player needs to face a specific way.
    /// Example: SetFacingDirection(0, 1) = face up toward the goddess.
    /// </summary>
    public void SetFacingDirection(float x, float y)
    {
        animator.SetFloat("LastInputX", x);
        animator.SetFloat("LastInputY", y);
        animator.SetFloat("InputX", 0f);
        animator.SetFloat("InputY", 0f);
    }
}
