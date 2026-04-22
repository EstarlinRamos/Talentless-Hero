using UnityEngine;

/// <summary>
/// Manages enemy respawning. Place in the scene and assign all enemies.
/// Listens to the player's OnRested event to respawn all mobs.
///
/// Boss enemies that have been permanently defeated are skipped
/// during respawn — they stay dead for the rest of the session.
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [Tooltip("Drag all enemy GameObjects here.")]
    [SerializeField] private EnemyStats[] allEnemies;

    [Tooltip("Reference to the player stats (for resting event).")]
    [SerializeField] private PlayerStats playerStats;

    private void OnEnable()
    {
        if (playerStats != null)
            playerStats.OnRested += RespawnAllEnemies;
    }

    private void OnDisable()
    {
        if (playerStats != null)
            playerStats.OnRested -= RespawnAllEnemies;
    }

    /// <summary>
    /// Reactivate and heal all enemies in the scene.
    /// Bosses that have been permanently defeated are skipped.
    /// Called automatically when the player rests at the inn.
    /// </summary>
    public void RespawnAllEnemies()
    {
        int respawned = 0;
        int skipped = 0;

        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;

            if (enemy.IsPermanentlyDefeated)
            {
                skipped++;
                continue;
            }

            enemy.Respawn();
            respawned++;
        }

        Debug.Log($"[EnemyManager] Respawned {respawned} enemies. " +
                  $"Skipped {skipped} permanently defeated bosses.");
    }
}
