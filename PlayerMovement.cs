using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator animator;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;

    private Vector2 moveInput;

    /// <summary>
    /// When true, player cannot move. Used by cutscenes, dialogue, etc.
    /// </summary>
    private bool _movementLocked = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // If locked, kill velocity and skip movement logic
        if (_movementLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Direct check: Is the Left Shift key currently held down?
        bool isShiftHeld = Keyboard.current.leftShiftKey.isPressed;

        // Determine speed based on shift key
        float currentSpeed = isShiftHeld ? runSpeed : walkSpeed;

        // Apply movement
        rb.linearVelocity = moveInput * currentSpeed;
    }

    public void move(InputAction.CallbackContext context)
    {
        // Don't process new input if movement is locked
        if (_movementLocked)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (context.canceled)
        {
            // Record the direction while moveInput still has a value for idle transitions
            if (moveInput.magnitude > 0)
            {
                animator.SetFloat("LastInputX", moveInput.x);
                animator.SetFloat("LastInputY", moveInput.y);
            }

            moveInput = Vector2.zero;
            animator.SetBool("IsWalking", false);
        }
        else
        {
            moveInput = context.ReadValue<Vector2>();
            animator.SetBool("IsWalking", true);
            
            // Also update LastInput while moving so it's always ready for the Blend Tree
            animator.SetFloat("LastInputX", moveInput.x);
            animator.SetFloat("LastInputY", moveInput.y);
        }

        animator.SetFloat("InputX", moveInput.x);
        animator.SetFloat("InputY", moveInput.y);
    }

    /// <summary>
    /// Freeze the player in place. Stops velocity and ignores input.
    /// </summary>
    public void LockMovement()
    {
        _movementLocked = true;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        animator.SetBool("IsWalking", false);
    }

    /// <summary>
    /// Restore player control.
    /// </summary>
    public void UnlockMovement()
    {
        _movementLocked = false;
    }
}