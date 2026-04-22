using UnityEngine;

/// <summary>
/// Overworld enemy behavior: wander, detect player, chase, and initiate combat.
///
/// REQUIRED PREFAB SETUP:
///   Root GameObject:
///     - EnemyOverworld (this script)
///     - EnemyStats
///     - StatusEffectHandler
///     - Rigidbody2D → Dynamic, Gravity Scale 0, Freeze Rotation Z
///     - BoxCollider2D → Is Trigger UNCHECKED, size matching sprite
///     - Animator (optional)
///
///   The collider and Rigidbody2D MUST be on the SAME GameObject.
///   If your sprite is on a child, the collider still goes on the ROOT
///   (or on the child but with the Rigidbody2D also on the child).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(EnemyStats))]
public class EnemyOverworld : MonoBehaviour
{
    public enum EnemyState { Idle, Wander, Chase, Returning }

    [Header("Movement")]
    [SerializeField] private float wanderSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3.5f;

    [Header("Wander")]
    [SerializeField] private float wanderRadius = 3f;
    [SerializeField] private float idleDuration = 2f;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float loseInterestRange = 8f;

    [Header("Leash")]
    [SerializeField] private float leashDistance = 12f;

    [Header("Animation (Optional)")]
    [SerializeField] private Animator animator;

    // ─────────────────────────────────────────────
    //  Runtime
    // ─────────────────────────────────────────────
    private Rigidbody2D _rb;
    private EnemyStats _stats;
    private Collider2D _collider;
    private EnemyState _state = EnemyState.Idle;
    private Vector3 _spawnPoint;
    private Vector2 _wanderTarget;
    private Transform _playerTarget;
    private float _idleTimer;
    private bool _isActive = true;

    public Vector3 SpawnOrigin { get => _spawnPoint; set => _spawnPoint = value; }
    public EnemyState CurrentState => _state;

    // ─────────────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<EnemyStats>();
        _collider = GetComponent<Collider2D>();
        _spawnPoint = transform.position;

        ValidatePhysics();
    }

    private void Start()
    {
        _idleTimer = Random.Range(0f, idleDuration);
    }

    /// <summary>
    /// Validate that physics is properly configured.
    /// Logs errors if something will prevent collision.
    /// </summary>
    private void ValidatePhysics()
    {
        if (_rb == null)
        {
            Debug.LogError($"[Enemy] {gameObject.name}: Missing Rigidbody2D!");
            return;
        }

        if (!_rb.simulated)
            Debug.LogError($"[Enemy] {gameObject.name}: Rigidbody2D.simulated is FALSE! Physics disabled.");

        if (_rb.bodyType != RigidbodyType2D.Dynamic)
            Debug.LogWarning($"[Enemy] {gameObject.name}: Rigidbody2D is {_rb.bodyType}, should be Dynamic.");

        if (_collider == null)
        {
            Debug.LogError($"[Enemy] {gameObject.name}: Missing Collider2D!");
            return;
        }

        if (_collider.isTrigger)
            Debug.LogError($"[Enemy] {gameObject.name}: Collider Is Trigger = TRUE! " +
                           "This prevents OnCollisionEnter2D. Uncheck Is Trigger.");

        if (!_collider.enabled)
            Debug.LogError($"[Enemy] {gameObject.name}: Collider is DISABLED!");

        // Check if collider is absurdly small
        if (_collider.bounds.size.magnitude < 0.01f)
            Debug.LogWarning($"[Enemy] {gameObject.name}: Collider bounds are tiny ({_collider.bounds.size}). " +
                             "Check collider size in Inspector.");

        Debug.Log($"[Enemy] {gameObject.name} physics OK. Layer={LayerMask.LayerToName(gameObject.layer)}, " +
                  $"Collider={_collider.GetType().Name}(trigger={_collider.isTrigger}), " +
                  $"RB=Dynamic(simulated={_rb.simulated}), Z={transform.position.z}");
    }

    /// <summary>
    /// Called by EnemySpawner after instantiation.
    /// </summary>
    public void Activate(Vector3 spawnPos)
    {
        _spawnPoint = spawnPos;
        // Ensure Z = 0 for 2D physics
        transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
        _state = EnemyState.Idle;
        _idleTimer = Random.Range(0f, idleDuration);
        _playerTarget = null;
        _isActive = true;
        gameObject.SetActive(true);

        // Re-cache in case Awake ran before components were ready
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        if (_stats == null) _stats = GetComponent<EnemyStats>();
        if (_collider == null) _collider = GetComponent<Collider2D>();

        // Force physics setup
        if (_rb != null)
        {
            _rb.simulated = true;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
        }

        if (_collider != null)
            _collider.enabled = true;

        Physics2D.SyncTransforms();

        if (animator != null)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            animator.SetFloat("LastInputX", randomDir.x);
            animator.SetFloat("LastInputY", randomDir.y);
        }
    }

    // ─────────────────────────────────────────────
    //  Update
    // ─────────────────────────────────────────────

    private void Update()
    {
        if (!_isActive || !_stats.IsAlive) return;
        if (CombatUIManager.IsInCombat)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (_state)
        {
            case EnemyState.Idle:     UpdateIdle();      break;
            case EnemyState.Wander:   UpdateWander();    break;
            case EnemyState.Chase:    UpdateChase();     break;
            case EnemyState.Returning: UpdateReturning(); break;
        }

        if (_state != EnemyState.Returning)
            CheckForPlayer();

        CheckLeash();
        UpdateAnimation();
    }

    // ─────────────────────────────────────────────
    //  State Updates
    // ─────────────────────────────────────────────

    private void UpdateIdle()
    {
        _rb.linearVelocity = Vector2.zero;
        _idleTimer -= Time.deltaTime;

        if (_idleTimer <= 0f)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            _wanderTarget = (Vector2)_spawnPoint + offset;
            _state = EnemyState.Wander;
        }
    }

    private void UpdateWander()
    {
        Vector2 dir = (_wanderTarget - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * wanderSpeed;

        if (Vector2.Distance(transform.position, _wanderTarget) < 0.3f)
        {
            _state = EnemyState.Idle;
            _idleTimer = Random.Range(idleDuration * 0.5f, idleDuration * 1.5f);
        }
    }

    private void UpdateChase()
    {
        if (_playerTarget == null)
        {
            _state = EnemyState.Idle;
            _idleTimer = 1f;
            return;
        }

        Vector2 dir = ((Vector2)_playerTarget.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * chaseSpeed;

        float distToPlayer = Vector2.Distance(transform.position, _playerTarget.position);
        if (distToPlayer > loseInterestRange)
        {
            _playerTarget = null;
            _state = EnemyState.Idle;
            _idleTimer = 1f;
        }
    }

    private void UpdateReturning()
    {
        Vector2 dir = ((Vector2)_spawnPoint - (Vector2)transform.position).normalized;
        _rb.linearVelocity = dir * wanderSpeed;

        if (Vector2.Distance(transform.position, _spawnPoint) < 0.5f)
        {
            _rb.linearVelocity = Vector2.zero;
            _isActive = false;
            gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────
    //  Detection & Leash
    // ─────────────────────────────────────────────

    private void CheckForPlayer()
    {
        if (_state == EnemyState.Chase) return;

        if (_playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            _playerTarget = player.transform;
        }

        float dist = Vector2.Distance(transform.position, _playerTarget.position);
        if (dist <= detectionRange)
            _state = EnemyState.Chase;
    }

    private void CheckLeash()
    {
        float distFromSpawn = Vector2.Distance(transform.position, _spawnPoint);
        if (distFromSpawn > leashDistance && _state != EnemyState.Returning)
        {
            _playerTarget = null;
            _state = EnemyState.Returning;
        }
    }

    // ─────────────────────────────────────────────
    //  Animation
    // ─────────────────────────────────────────────

    private void UpdateAnimation()
    {
        if (animator == null) return;

        Vector2 vel = _rb.linearVelocity;
        bool moving = vel.sqrMagnitude > 0.01f;

        animator.SetBool("IsWalking", moving);

        if (moving)
        {
            Vector2 dir = vel.normalized;
            animator.SetFloat("InputX", dir.x);
            animator.SetFloat("InputY", dir.y);
            animator.SetFloat("LastInputX", dir.x);
            animator.SetFloat("LastInputY", dir.y);
        }
        else
        {
            animator.SetFloat("InputX", 0f);
            animator.SetFloat("InputY", 0f);
        }
    }

    // ─────────────────────────────────────────────
    //  Collision — Combat Initiation
    // ─────────────────────────────────────────────

    /// <summary>
    /// Physical collision with the player — enemy initiated combat.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log($"[Enemy] {_stats.EnemyName} collided with {collision.collider.name} " +
                  $"(tag={collision.collider.tag})");

        if (!collision.collider.CompareTag("Player")) return;
        if (!_isActive || !_stats.IsAlive) return;
        if (CombatUIManager.IsInCombat) return;
        if (DialogueManager.IsInDialogue) return;

        Debug.Log($"[Enemy] {_stats.EnemyName} touched player! Starting combat.");
        OverworldCombatInitiator.StartCombat(this, playerInitiated: false);
    }

    /// <summary>
    /// Also check Stay in case Enter was missed (e.g., spawned on top of player).
    /// Only fires once per encounter.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        if (!_isActive || !_stats.IsAlive) return;
        if (CombatUIManager.IsInCombat) return;
        if (DialogueManager.IsInDialogue) return;

        Debug.Log($"[Enemy] {_stats.EnemyName} OnCollisionStay with player — starting combat.");
        OverworldCombatInitiator.StartCombat(this, playerInitiated: false);
    }

    // ─────────────────────────────────────────────
    //  Post-Combat
    // ─────────────────────────────────────────────

    public void OnCombatEnded(bool defeated)
    {
        if (defeated)
        {
            _isActive = false;
            gameObject.SetActive(false);
        }
        else
        {
            // Player escaped — reset enemy to full health
            _stats.Respawn();
            _state = EnemyState.Idle;
            _idleTimer = 2f;
            _playerTarget = null;
        }
    }

    // ─────────────────────────────────────────────
    //  Gizmos
    // ─────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? _spawnPoint : transform.position;

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(origin, wanderRadius);

        Gizmos.color = new Color(1f, 0.8f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, loseInterestRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawWireSphere(origin, leashDistance);
    }
}
