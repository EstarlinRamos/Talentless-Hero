using UnityEngine;

/// <summary>
/// Manages enemy respawning. Listens to the player's OnRested event
/// to respawn all mobs when the player rests at the inn.
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
    /// </summary>
    public void RespawnAllEnemies()
    {
        foreach (var enemy in allEnemies)
        {
            if (enemy != null)
                enemy.Respawn();
        }

        Debug.Log($"[EnemyManager] All enemies respawned ({allEnemies.Length} total).");
    }
}
