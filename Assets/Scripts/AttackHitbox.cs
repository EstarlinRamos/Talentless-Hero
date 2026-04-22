using UnityEngine;

/// <summary>
/// Attach to the player's attack hitbox child object.
/// When enabled briefly during a sword swing, detects overlapping enemies
/// and initiates combat with player advantage.
///
/// SETUP:
///   1. Create a child of the Player called "AttackHitbox"
///   2. Add a BoxCollider2D (or CircleCollider2D), check "Is Trigger"
///   3. Size it to represent the sword arc
///   4. Attach this script
///   5. Disable the GameObject by default (PlayerAttack enables it during swings)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    private bool _hitEnemy = false;

    private void OnEnable()
    {
        _hitEnemy = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only register the first hit per swing
        if (_hitEnemy) return;

        EnemyOverworld enemy = other.GetComponent<EnemyOverworld>();
        if (enemy == null) return;

        EnemyStats stats = other.GetComponent<EnemyStats>();
        if (stats == null || !stats.IsAlive) return;

        if (CombatUIManager.IsInCombat) return;

        _hitEnemy = true;

        // Player initiated combat — player gets first turn
        OverworldCombatInitiator.StartCombat(enemy, playerInitiated: true);

        Debug.Log($"[AttackHitbox] Struck {stats.EnemyName}! Player gets first turn.");
    }
}
