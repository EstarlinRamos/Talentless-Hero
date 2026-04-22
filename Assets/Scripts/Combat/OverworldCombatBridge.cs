using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Static utility that routes overworld encounters to the combat bridge.
/// Both EnemyOverworld (enemy collision) and AttackHitbox (player swing)
/// call OverworldCombatInitiator.StartCombat().
/// </summary>
public static class OverworldCombatInitiator
{
    private static bool _initiating = false;
    private static float _lastCombatEndTime = -10f;
    private const float POST_COMBAT_COOLDOWN = 1.5f;

    /// <summary>
    /// Call this when combat ends to start the cooldown window.
    /// </summary>
    public static void NotifyCombatEnded()
    {
        _lastCombatEndTime = Time.time;
        _initiating = false;
    }

    public static void StartCombat(EnemyOverworld enemy, bool playerInitiated)
    {
        // Prevent double-entry
        if (_initiating) return;
        if (CombatUIManager.IsInCombat) return;

        // Post-combat cooldown — prevents OnCollisionStay from immediately restarting combat
        if (Time.time - _lastCombatEndTime < POST_COMBAT_COOLDOWN) return;

        _initiating = true;

        OverworldCombatBridge bridge = Object.FindFirstObjectByType<OverworldCombatBridge>();
        if (bridge == null)
        {
            Debug.LogError("[CombatInit] No OverworldCombatBridge found in scene!");
            _initiating = false;
            return;
        }

        Debug.Log($"[CombatInit] Starting combat...");
        bridge.InitiateCombat(enemy, playerInitiated);

        // Reset initiating flag after one frame (IsInCombat will be true by then)
        bridge.StartCoroutine(ResetInitiatingFlag());
    }

    private static System.Collections.IEnumerator ResetInitiatingFlag()
    {
        yield return null;
        _initiating = false;
    }
}

/// <summary>
/// Scene-level MonoBehaviour that wires overworld encounters to the combat UI.
/// Handles movement locking, battle music, first-turn advantage via CR boost,
/// combat background switching, and post-combat cleanup.
///
/// First-Turn Rules:
///   - Player swings sword → player gets 100 CR at start (acts first)
///   - Enemy touches player → enemy gets 100 CR at start (acts first)
///   - Boss encounter → boss ALWAYS gets 100 CR (regardless of who initiated)
///
/// SETUP:
///   1. Create an empty GameObject called "CombatBridge"
///   2. Attach this script
///   3. Assign all references: CombatUIManager, CombatTurnManager,
///      CombatBackground, and CombatSceneRoot
///   4. CombatSceneRoot should contain everything combat-related
///      (background, plates, action buttons) and start DISABLED
/// </summary>
public class OverworldCombatBridge : MonoBehaviour
{
    [Header("Combat System References")]
    [SerializeField] private CombatUIManager combatUI;
    [SerializeField] private CombatTurnManager turnManager;

    [Header("Combat Visuals")]
    [Tooltip("Switches the background image based on current zone.")]
    [SerializeField] private CombatBackground combatBackground;

    [Header("Combat Scene")]
    [Tooltip("CanvasGroup on the combat UI root. Hidden via alpha when not in combat. " +
             "Keep the GameObject ENABLED — only alpha and raycasts toggle.")]
    [SerializeField] private CanvasGroup combatSceneGroup;

    [Header("Death / Respawn")]
    [Tooltip("Handles player death — teleports to innkeeper with rescue dialogue.")]
    [SerializeField] private PlayerRespawnHandler respawnHandler;

    [Header("Combat Fade")]
    [Tooltip("Duration of fade to black when entering combat.")]
    [SerializeField] private float combatFadeOutDuration = 0.3f;

    [Tooltip("Duration of fade in when combat scene appears.")]
    [SerializeField] private float combatFadeInDuration = 0.3f;

    [Tooltip("Duration of fade out when leaving combat.")]
    [SerializeField] private float exitFadeOutDuration = 0.3f;

    [Tooltip("Duration of fade in back to overworld.")]
    [SerializeField] private float exitFadeInDuration = 0.3f;

    public static bool PlayerGoesFirst { get; private set; }

    private EnemyOverworld _currentEnemy;
    private PlayerMovement _playerMovement;
    private PlayerStats _playerStats;

    private void Start()
    {
        HideCombatScene();
    }

    public void InitiateCombat(EnemyOverworld enemy, bool playerInitiated)
    {
        Debug.Log("[CombatBridge] InitiateCombat called.");

        if (CombatUIManager.IsInCombat)
        {
            Debug.Log("[CombatBridge] Blocked — already in combat.");
            return;
        }

        _currentEnemy = enemy;

        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("[CombatBridge] Player not found! Is it tagged 'Player'?");
            return;
        }

        _playerMovement = playerObj.GetComponent<PlayerMovement>();
        _playerStats = playerObj.GetComponent<PlayerStats>();

        if (_playerStats == null)
        {
            Debug.LogError("[CombatBridge] Player has no PlayerStats component!");
            return;
        }

        if (!_playerStats.IsAlive)
        {
            Debug.LogError("[CombatBridge] Player is dead! Cannot start combat.");
            return;
        }

        EnemyStats enemyStats = enemy.GetComponent<EnemyStats>();
        if (enemyStats == null)
        {
            Debug.LogError("[CombatBridge] Enemy has no EnemyStats component!");
            return;
        }

        if (!enemyStats.IsAlive)
        {
            Debug.LogError("[CombatBridge] Enemy is already dead!");
            return;
        }

        Debug.Log($"[CombatBridge] All checks passed. Player={playerObj.name}, Enemy={enemyStats.EnemyName}");
        Debug.Log($"[CombatBridge] combatUI={combatUI != null}, turnManager={turnManager != null}, " +
                  $"combatSceneGroup={combatSceneGroup != null}");

        // Determine first turn — bosses ALWAYS go first
        ICombatant firstTurnCombatant;
        if (enemyStats.IsBoss)
        {
            PlayerGoesFirst = false;
            firstTurnCombatant = enemyStats;
            Debug.Log($"[CombatBridge] Boss encounter — {enemyStats.EnemyName} goes first!");
        }
        else if (playerInitiated)
        {
            PlayerGoesFirst = true;
            firstTurnCombatant = _playerStats;
            Debug.Log("[CombatBridge] Player struck first — player goes first.");
        }
        else
        {
            PlayerGoesFirst = false;
            firstTurnCombatant = enemyStats;
            Debug.Log($"[CombatBridge] Enemy initiated — {enemyStats.EnemyName} goes first.");
        }

        // Lock player
        if (_playerMovement != null)
            _playerMovement.LockMovement();

        // Stop and hide the enemy from the overworld during combat
        // Disable collider to prevent OnCollisionStay re-triggers
        // Disable renderer so the enemy isn't visible on the overworld
        Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null)
            enemyRb.linearVelocity = Vector2.zero;

        Collider2D enemyCol = enemy.GetComponent<Collider2D>();
        if (enemyCol != null)
            enemyCol.enabled = false;

        SpriteRenderer enemySprite = enemy.GetComponentInChildren<SpriteRenderer>();
        if (enemySprite != null)
            enemySprite.enabled = false;

        // Start the fade-in combat sequence
        StartCoroutine(CombatEntrySequence(enemy, enemyStats, firstTurnCombatant));
    }

    private System.Collections.IEnumerator CombatEntrySequence(
        EnemyOverworld enemy, EnemyStats enemyStats, ICombatant firstTurnCombatant)
    {
        // Fade to black
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(combatFadeOutDuration);

        // While screen is black: set up combat scene
        ShowCombatScene();

        if (combatBackground != null)
            combatBackground.SetBackground();

        // Build combatant lists
        var allies = new List<ICombatant> { _playerStats };
        var enemies = new List<ICombatant> { enemyStats };

        // Battle music
        bool isBoss = enemyStats.IsBoss;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleMusic(isBoss);

        // Subscribe to combat end
        combatUI.OnCombatFinished += HandleCombatFinished;

        // Reset enemy AI cooldowns
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null) ai.ResetCooldowns();

        // Initialize plates
        combatUI.BeginCombatWithAdvantage(allies, enemies, firstTurnCombatant);

        // Fade in to reveal the combat scene
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(combatFadeInDuration);
    }

    [Header("Post-Combat")]
    [Tooltip("Enemies within this radius of the player are despawned after combat " +
             "to prevent instant re-triggers.")]
    [SerializeField] private float postCombatDespawnRadius = 5f;

    private void HandleCombatFinished(bool victory)
    {
        combatUI.OnCombatFinished -= HandleCombatFinished;
        StartCoroutine(CombatExitSequence(victory));
    }

    private System.Collections.IEnumerator CombatExitSequence(bool victory)
    {
        // Start post-combat cooldown
        OverworldCombatInitiator.NotifyCombatEnded();

        // Fade to black
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(exitFadeOutDuration);

        // While screen is black: clean up combat
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBattleMusic();

        HideCombatScene();

        if (combatBackground != null)
            combatBackground.Hide();

        if (victory && _currentEnemy != null)
        {
            EnemyStats stats = _currentEnemy.GetComponent<EnemyStats>();

            if (stats != null && !stats.IsAlive)
            {
                if (EXPRewardSystem.Instance != null)
                    EXPRewardSystem.Instance.AwardCombatEXP(stats.ExpReward, stats.EnemyName);
            }

            _currentEnemy.OnCombatEnded(defeated: true);

            if (_playerMovement != null)
                _playerMovement.UnlockMovement();

            // Despawn nearby non-boss enemies
            DespawnNearbyEnemies();
        }
        else
        {
            // Defeat or escape — restore the fought enemy
            if (_currentEnemy != null)
            {
                Collider2D col = _currentEnemy.GetComponent<Collider2D>();
                if (col != null) col.enabled = true;

                SpriteRenderer sr = _currentEnemy.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.enabled = true;

                _currentEnemy.OnCombatEnded(defeated: false);
            }

            bool playerDead = _playerStats != null && !_playerStats.IsAlive;

            if (playerDead && respawnHandler != null)
            {
                respawnHandler.HandleDefeat();
            }
            else
            {
                if (_playerMovement != null)
                    _playerMovement.UnlockMovement();
            }
        }

        _currentEnemy = null;
        PlayerGoesFirst = false;

        // Fade back in to overworld (unless respawn handler is doing its own fade)
        bool playerDied = _playerStats != null && !_playerStats.IsAlive;
        if (!playerDied && ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(exitFadeInDuration);
    }

    /// <summary>
    /// Find all non-boss enemies within the despawn radius and deactivate them.
    /// Bosses are never despawned by proximity.
    /// </summary>
    private void DespawnNearbyEnemies()
    {
        if (_playerMovement == null) return;

        Vector2 playerPos = _playerMovement.transform.position;
        EnemyOverworld[] allEnemies = FindObjectsByType<EnemyOverworld>(FindObjectsSortMode.None);

        int despawned = 0;
        foreach (var enemy in allEnemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            if (enemy == _currentEnemy) continue;

            // Never despawn bosses
            EnemyStats stats = enemy.GetComponent<EnemyStats>();
            if (stats != null && stats.IsBoss) continue;

            float dist = Vector2.Distance(playerPos, enemy.transform.position);
            if (dist <= postCombatDespawnRadius)
            {
                enemy.gameObject.SetActive(false);
                despawned++;
            }
        }

        if (despawned > 0)
            Debug.Log($"[CombatBridge] Despawned {despawned} nearby enemies.");
    }

    // ─────────────────────────────────────────────
    //  Combat Scene Visibility
    //  Uses CanvasGroup so components stay active.
    // ─────────────────────────────────────────────

    private void ShowCombatScene()
    {
        if (combatSceneGroup == null) return;
        combatSceneGroup.alpha = 1f;
        combatSceneGroup.interactable = true;
        combatSceneGroup.blocksRaycasts = true;
    }

    private void HideCombatScene()
    {
        if (combatSceneGroup == null) return;
        combatSceneGroup.alpha = 0f;
        combatSceneGroup.interactable = false;
        combatSceneGroup.blocksRaycasts = false;
    }
}
