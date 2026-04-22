using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Triggers a plate-based combat encounter when the player enters a trigger zone.
/// Handles combat setup, battle music, and post-combat EXP rewards.
/// </summary>
public class CombatEncounterTrigger : MonoBehaviour
{
    [Header("Combat System References")]
    [SerializeField] private CombatUIManager combatUI;

    [Header("Enemies In This Encounter")]
    [Tooltip("Drag enemy GameObjects here. Up to 5.")]
    [SerializeField] private EnemyStats[] enemies;

    [Header("Allies (Optional — Hero is auto-included)")]
    [Tooltip("Additional allies beyond the player. Leave empty for solo fights.")]
    [SerializeField] private PlayerStats[] additionalAllies;

    [Header("Settings")]
    [Tooltip("Should this encounter only trigger once?")]
    [SerializeField] private bool oneTimeEncounter = false;

    [Tooltip("Disable player movement during combat.")]
    [SerializeField] private bool disableMovementInCombat = true;

    private bool _triggered = false;
    private PlayerStats _player;
    private PlayerMovement _playerMovement;

    // ─────────────────────────────────────────────
    //  Trigger Detection
    // ─────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_triggered && oneTimeEncounter) return;
        if (CombatUIManager.IsInCombat) return;

        _player = other.GetComponent<PlayerStats>();
        _playerMovement = other.GetComponent<PlayerMovement>();

        if (_player == null || !_player.IsAlive) return;

        bool anyAlive = enemies.Any(e => e != null && e.IsAlive);
        if (!anyAlive) return;

        _triggered = true;
        StartEncounter();
    }

    // ─────────────────────────────────────────────
    //  Combat Flow
    // ─────────────────────────────────────────────

    private void StartEncounter()
    {
        if (disableMovementInCombat && _playerMovement != null)
            _playerMovement.enabled = false;

        var allies = new List<ICombatant> { _player };
        if (additionalAllies != null)
        {
            foreach (var ally in additionalAllies)
            {
                if (ally != null && ally.IsAlive)
                    allies.Add(ally);
            }
        }

        var enemyList = new List<ICombatant>();
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.IsAlive)
                enemyList.Add(enemy);
        }

        combatUI.OnCombatFinished += HandleCombatFinished;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            EnemyAI ai = enemy.GetComponent<EnemyAI>();
            if (ai != null) ai.ResetCooldowns();
        }

        bool hasBoss = enemies.Any(e => e != null && e.IsAlive && e.IsBoss);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleMusic(hasBoss);

        combatUI.BeginCombat(allies, enemyList);

        Debug.Log($"[Encounter] Combat started! {allies.Count} allies vs {enemyList.Count} enemies." +
                  $"{(hasBoss ? " (BOSS ENCOUNTER)" : "")}");
    }

    private void HandleCombatFinished(bool victory)
    {
        combatUI.OnCombatFinished -= HandleCombatFinished;

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBattleMusic();

        if (_playerMovement != null)
            _playerMovement.enabled = true;

        if (victory)
        {
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                if (!enemy.IsAlive)
                {
                    if (EXPRewardSystem.Instance != null)
                        EXPRewardSystem.Instance.AwardCombatEXP(enemy.ExpReward, enemy.EnemyName);

                    enemy.gameObject.SetActive(false);
                }
            }

            Debug.Log("[Encounter] Victory! EXP awarded.");
        }
        else
        {
            Debug.Log("[Encounter] Escaped or defeated.");

            if (!oneTimeEncounter)
                _triggered = false;
        }
    }
}
