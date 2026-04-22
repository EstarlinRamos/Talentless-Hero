using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Spawns enemy instances from a prefab at this location.
///
/// IMPORTANT SETUP:
///   - The prefab must be from the PROJECT WINDOW, not a scene object
///   - The prefab should be ENABLED when saved
///   - Required components on prefab: EnemyOverworld, EnemyStats,
///     StatusEffectHandler, Rigidbody2D (Dynamic), Collider2D (NOT trigger)
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefab")]
    [Tooltip("Drag from the PROJECT WINDOW. Must have EnemyOverworld + EnemyStats.")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int maxEnemies = 3;
    [SerializeField] private float spawnRadius = 2f;
    [SerializeField] private float respawnDelay = 10f;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float initialSpawnStagger = 0.5f;

    private List<GameObject> _activeEnemies = new List<GameObject>();
    private int _pendingRespawns = 0;

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError($"[Spawner] {gameObject.name}: No enemy prefab assigned!");
            return;
        }

        // Validate prefab has required components
        if (enemyPrefab.GetComponent<Rigidbody2D>() == null)
            Debug.LogError($"[Spawner] Prefab missing Rigidbody2D!");
        if (enemyPrefab.GetComponent<Collider2D>() == null)
            Debug.LogError($"[Spawner] Prefab missing Collider2D!");
        if (enemyPrefab.GetComponent<EnemyOverworld>() == null)
            Debug.LogError($"[Spawner] Prefab missing EnemyOverworld!");
        if (enemyPrefab.GetComponent<EnemyStats>() == null)
            Debug.LogError($"[Spawner] Prefab missing EnemyStats!");

        if (spawnOnStart)
            StartCoroutine(InitialSpawn());
    }

    private IEnumerator InitialSpawn()
    {
        for (int i = 0; i < maxEnemies; i++)
        {
            SpawnEnemy();
            if (initialSpawnStagger > 0f)
                yield return new WaitForSeconds(initialSpawnStagger);
        }
    }

    private void Update()
    {
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
        {
            if (_activeEnemies[i] == null || !_activeEnemies[i].activeInHierarchy)
            {
                _activeEnemies.RemoveAt(i);

                if (_activeEnemies.Count + _pendingRespawns < maxEnemies)
                {
                    _pendingRespawns++;
                    StartCoroutine(RespawnAfterDelay());
                }
            }
        }
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        _pendingRespawns--;
        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null) return;
        if (_activeEnemies.Count >= maxEnemies) return;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        // Ensure Z = 0 so 2D physics works properly
        Vector3 spawnPos = new Vector3(
            transform.position.x + offset.x,
            transform.position.y + offset.y,
            0f
        );

        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        enemy.SetActive(true);

        // Force physics to recognize the new collider
        Physics2D.SyncTransforms();

        // Validate and fix the spawned instance
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        Collider2D col = enemy.GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
            Debug.Log($"[Spawner] Spawned enemy at {spawnPos}. " +
                      $"Collider: {col.GetType().Name}, isTrigger={col.isTrigger}, " +
                      $"size={col.bounds.size}, layer={LayerMask.LayerToName(enemy.layer)}");
        }
        else
        {
            Debug.LogError("[Spawner] Spawned enemy has NO collider!");
        }

        // Initialize overworld AI
        EnemyOverworld overworld = enemy.GetComponent<EnemyOverworld>();
        if (overworld != null)
            overworld.Activate(spawnPos);

        _activeEnemies.Add(enemy);
    }

    public void DespawnAll()
    {
        foreach (var enemy in _activeEnemies)
        {
            if (enemy != null)
                enemy.SetActive(false);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.1f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
    }
}
