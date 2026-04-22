using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main combat UI controller for the plate-based combat system.
///
/// State machine:
///   Inactive → WaitingForTurn → PlayerSelectAction → PlayerTargeting
///   → PlayerExecuting → WaitingForTurn (loop)
///   EnemyTurn runs automatically when an enemy's CR hits 100%.
///   CombatEnd triggers on all-allies-dead or all-enemies-dead.
/// </summary>
public class CombatUIManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Combat State
    // ─────────────────────────────────────────────

    public enum CombatState
    {
        Inactive,
        WaitingForTurn,
        PlayerSelectAction,
        PlayerTargeting,
        PlayerExecuting,
        EnemyTurn,
        CombatEnd
    }

    private enum PendingAction
    {
        None,
        BasicAttack,
        Magic,
        Skill
    }

    // ─────────────────────────────────────────────
    //  Inspector References
    // ─────────────────────────────────────────────

    [Header("Core Systems")]
    [SerializeField] private CombatTurnManager turnManager;
    [SerializeField] private CombatPlateManager plateManager;
    [SerializeField] private CombatActionAnimator actionAnimator;

    [Header("Turn Counter")]
    [SerializeField] private TextMeshProUGUI turnCounterText;

    [Header("Action Buttons Panel")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button magicButton;
    [SerializeField] private Button skillsButton;
    [SerializeField] private Button escapeButton;

    [Header("Magic Sub-Panel")]
    [SerializeField] private GameObject magicPanel;
    [SerializeField] private Transform magicButtonContainer;
    [SerializeField] private Button magicButtonPrefab;

    [Header("Skills Sub-Panel")]
    [SerializeField] private GameObject skillsPanel;
    [SerializeField] private Transform skillsButtonContainer;
    [SerializeField] private Button skillsButtonPrefab;

    [Header("Combat Log (Optional)")]
    [SerializeField] private TextMeshProUGUI combatLogText;
    [SerializeField] private int maxLogLines = 5;

    [Header("Escape Fail Message")]
    [SerializeField] private TextMeshProUGUI escapeFailText;
    [SerializeField] private float escapeFailDisplayTime = 1.5f;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private CombatState _state = CombatState.Inactive;
    private ICombatant _currentActor;
    private PendingAction _pendingAction = PendingAction.None;
    private ICombatant _selectedTarget;
    private List<string> _logLines = new List<string>();

    /// <summary>Static flag for other systems to check (e.g. SettingsMenuUI).</summary>
    public static bool IsInCombat { get; private set; } = false;

    public CombatState CurrentState => _state;

    /// <summary>Fired when combat ends. True = victory, False = defeat/escape.</summary>
    public event Action<bool> OnCombatFinished;

    // ═════════════════════════════════════════════
    //  Lifecycle
    // ═════════════════════════════════════════════

    private void Awake()
    {
        HideAllPanels();
    }

    private void OnEnable()
    {
        if (attackButton != null)  attackButton.onClick.AddListener(OnAttackPressed);
        if (magicButton != null)   magicButton.onClick.AddListener(OnMagicPressed);
        if (skillsButton != null)  skillsButton.onClick.AddListener(OnSkillsPressed);
        if (escapeButton != null)  escapeButton.onClick.AddListener(OnEscapePressed);

        if (turnManager != null)
        {
            turnManager.OnTurnReady += HandleTurnReady;
            turnManager.OnCombatEnded += HandleCombatEnded;
            turnManager.OnTickUpdate += HandleTickUpdate;
        }

        if (plateManager != null)
            plateManager.OnTargetSelected += HandleTargetSelected;
    }

    private void OnDisable()
    {
        if (attackButton != null)  attackButton.onClick.RemoveListener(OnAttackPressed);
        if (magicButton != null)   magicButton.onClick.RemoveListener(OnMagicPressed);
        if (skillsButton != null)  skillsButton.onClick.RemoveListener(OnSkillsPressed);
        if (escapeButton != null)  escapeButton.onClick.RemoveListener(OnEscapePressed);

        if (turnManager != null)
        {
            turnManager.OnTurnReady -= HandleTurnReady;
            turnManager.OnCombatEnded -= HandleCombatEnded;
            turnManager.OnTickUpdate -= HandleTickUpdate;
        }

        if (plateManager != null)
            plateManager.OnTargetSelected -= HandleTargetSelected;
    }

    private void Update()
    {
        if (_state == CombatState.PlayerTargeting && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTargeting();
        }
    }

    // ═════════════════════════════════════════════
    //  Public API
    // ═════════════════════════════════════════════

    /// <summary>
    /// Begin a combat encounter.
    /// </summary>
    public void BeginCombat(List<ICombatant> allies, List<ICombatant> enemies)
    {
        IsInCombat = true;
        _state = CombatState.WaitingForTurn;

        plateManager.InitializePlates(allies, enemies);

        if (actionPanel != null)
            actionPanel.SetActive(true);

        _logLines.Clear();
        UpdateCombatLog();

        var allCombatants = new List<ICombatant>();
        allCombatants.AddRange(allies.Where(a => a.IsAlive));
        allCombatants.AddRange(enemies.Where(e => e.IsAlive));
        turnManager.StartCombat(allCombatants);

        Debug.Log("[CombatUI] Combat started!");
    }

    /// <summary>
    /// End combat and clean up.
    /// </summary>
    public void EndCombat(bool victory)
    {
        _state = CombatState.CombatEnd;
        IsInCombat = false;

        plateManager.ExitTargetingMode();
        plateManager.RestoreSpotlight();
        HideAllPanels();

        if (turnManager != null)
            turnManager.EndCombat();

        plateManager.ClearAllPlates();

        OnCombatFinished?.Invoke(victory);
        Debug.Log($"[CombatUI] Combat ended. Victory: {victory}");
    }

    // ═════════════════════════════════════════════
    //  Turn Manager Event Handlers
    // ═════════════════════════════════════════════

    private void HandleTurnReady(ICombatant combatant)
    {
        _currentActor = combatant;
        UpdateTurnCounter();
        plateManager.RefreshAllPlates();

        if (combatant.IsPlayerSide)
        {
            StartPlayerTurn(combatant);
        }
        else
        {
            StartEnemyTurn(combatant);
        }
    }

    private void HandleCombatEnded()
    {
        bool anyAllies = plateManager.GetAliveCombatants(true).Count > 0;
        bool anyEnemies = plateManager.GetAliveCombatants(false).Count > 0;

        if (!anyEnemies)
            EndCombat(true);
        else if (!anyAllies)
            EndCombat(false);
    }

    private void HandleTickUpdate(IReadOnlyList<CombatTurnManager.CombatantState> states)
    {
        // Hook for CR bar / turn order UI updates.
    }

    // ═════════════════════════════════════════════
    //  Player Turn Flow
    // ═════════════════════════════════════════════

    private void StartPlayerTurn(ICombatant player)
    {
        _state = CombatState.PlayerSelectAction;
        _pendingAction = PendingAction.None;
        _selectedTarget = null;

        // Process status effects (DoT, etc.)
        if (player is PlayerStats ps)
        {
            int dotDamage = ps.Effects.OnTurnStart();
            if (dotDamage > 0)
            {
                ps.TakeDamage(dotDamage);
                AddLogLine($"{player.CombatName} took {dotDamage} DoT damage!");
                plateManager.RefreshPlate(player);
            }
        }

        ShowActionButtons(true);
        HideSubPanels();

        // Disable magic/skills if silenced
        if (player is PlayerStats playerStats)
        {
            bool silenced = playerStats.Effects.IsSilenced;
            if (magicButton != null) magicButton.interactable = !silenced;
            if (skillsButton != null) skillsButton.interactable = !silenced;

            if (silenced)
                AddLogLine($"{player.CombatName} is silenced! Only basic attack available.");
        }

        AddLogLine($">> {player.CombatName}'s turn!");
        Debug.Log($"[CombatUI] Player turn: {player.CombatName}");
    }

    // ─── Action Button Handlers ─────────────────

    private void OnAttackPressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;

        _pendingAction = PendingAction.BasicAttack;
        EnterTargeting();
    }

    private void OnMagicPressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;

        HideSubPanels();
        if (magicPanel != null)
            magicPanel.SetActive(true);

        _pendingAction = PendingAction.Magic;
        EnterTargeting();
    }

    private void OnSkillsPressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;

        HideSubPanels();
        if (skillsPanel != null)
            skillsPanel.SetActive(true);

        _pendingAction = PendingAction.Skill;
        EnterTargeting();
    }

    private void OnEscapePressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;

        if (plateManager.HasAliveBoss())
        {
            AddLogLine("Cannot escape — a boss blocks the way!");
            ShowEscapeFailMessage("A powerful presence prevents escape!");
            return;
        }

        AddLogLine("Escaped from battle!");
        EndCombat(false);
    }

    // ─── Targeting Flow ─────────────────────────

    private void EnterTargeting()
    {
        _state = CombatState.PlayerTargeting;
        ShowActionButtons(false);
        HideSubPanels();
        plateManager.EnterTargetingMode();
    }

    private void CancelTargeting()
    {
        plateManager.ExitTargetingMode();
        _pendingAction = PendingAction.None;
        _selectedTarget = null;

        _state = CombatState.PlayerSelectAction;
        ShowActionButtons(true);
        HideSubPanels();

        Debug.Log("[CombatUI] Targeting cancelled.");
    }

    private void HandleTargetSelected(ICombatant target)
    {
        if (_state != CombatState.PlayerTargeting) return;

        _selectedTarget = target;
        plateManager.ExitTargetingMode();
        ExecutePlayerAction();
    }

    // ─── Action Execution ───────────────────────

    private void ExecutePlayerAction()
    {
        _state = CombatState.PlayerExecuting;

        if (_currentActor == null || _selectedTarget == null)
        {
            FinishPlayerTurn();
            return;
        }

        switch (_pendingAction)
        {
            case PendingAction.BasicAttack:
                ExecuteBasicAttack();
                break;
            case PendingAction.Magic:
                ExecuteMagicAttack();
                break;
            case PendingAction.Skill:
                ExecuteSkillAttack();
                break;
            default:
                FinishPlayerTurn();
                break;
        }
    }

    private void ExecuteBasicAttack()
    {
        PlayerStats player = _currentActor as PlayerStats;
        if (player == null) { FinishPlayerTurn(); return; }

        float rawDamage = player.PhysicalDamage;
        float defense = 0f;

        if (_selectedTarget is EnemyStats enemyTarget)
            defense = enemyTarget.PhysicalDefense;
        else if (_selectedTarget is PlayerStats allyTarget)
            defense = allyTarget.PhysicalDefense;

        float reducedDamage = Mathf.Max(rawDamage - defense, 1f);
        int finalDamage = Mathf.RoundToInt(reducedDamage);

        float targetDodge = 0f;
        if (_selectedTarget is EnemyStats ed) targetDodge = ed.DodgeChance;
        else if (_selectedTarget is PlayerStats pd) targetDodge = pd.DodgeChance;

        bool hits = player.RollHit(targetDodge);

        bool crit = false;
        if (hits)
        {
            crit = player.RollCrit();
            if (crit)
                finalDamage = Mathf.RoundToInt(finalDamage * player.CritDamageMultiplier);
        }

        CombatPlate targetPlate = plateManager.FindPlate(_selectedTarget);
        RectTransform targetRT = targetPlate != null ? targetPlate.GetComponent<RectTransform>() : null;

        actionAnimator.PlayBasicAttack(targetRT, hits, () =>
        {
            if (hits)
            {
                int actualDamage = ApplyDamageToTarget(_selectedTarget, finalDamage);

                if (targetRT != null)
                    actionAnimator.ShowDamageNumber(targetRT, actualDamage, crit, false);

                string logMsg = crit
                    ? $"{player.CombatName} CRIT! {actualDamage} physical damage to {_selectedTarget.CombatName}!"
                    : $"{player.CombatName} attacks {_selectedTarget.CombatName} for {actualDamage} physical damage.";
                AddLogLine(logMsg);
            }
            else
            {
                AddLogLine($"{player.CombatName} attacked {_selectedTarget.CombatName} but missed!");
            }

            plateManager.RestoreSpotlight();
            plateManager.RefreshAllPlates();
            CheckForDeaths();
            FinishPlayerTurn();
        });
    }

    private void ExecuteMagicAttack()
    {
        PlayerStats player = _currentActor as PlayerStats;
        if (player == null) { FinishPlayerTurn(); return; }

        float rawDamage = player.MagicDamage;
        float defense = 0f;

        if (_selectedTarget is EnemyStats enemyTarget)
            defense = enemyTarget.MagicDefense;
        else if (_selectedTarget is PlayerStats allyTarget)
            defense = allyTarget.MagicDefense;

        float reducedDamage = Mathf.Max(rawDamage - defense, 1f);
        int finalDamage = Mathf.RoundToInt(reducedDamage);

        bool hits = true;
        bool crit = player.RollCrit();
        if (crit)
            finalDamage = Mathf.RoundToInt(finalDamage * player.CritDamageMultiplier);

        CombatPlate targetPlate = plateManager.FindPlate(_selectedTarget);
        RectTransform targetRT = targetPlate != null ? targetPlate.GetComponent<RectTransform>() : null;

        actionAnimator.PlayMagicAttack(targetRT, hits, () =>
        {
            int actualDamage = ApplyDamageToTarget(_selectedTarget, finalDamage);

            if (targetRT != null)
                actionAnimator.ShowDamageNumber(targetRT, actualDamage, crit, false);

            string logMsg = crit
                ? $"{player.CombatName} CRIT! {actualDamage} magic damage to {_selectedTarget.CombatName}!"
                : $"{player.CombatName} casts on {_selectedTarget.CombatName} for {actualDamage} magic damage.";
            AddLogLine(logMsg);

            plateManager.RestoreSpotlight();
            plateManager.RefreshAllPlates();
            CheckForDeaths();
            FinishPlayerTurn();
        });
    }

    private void ExecuteSkillAttack()
    {
        // Placeholder: behaves like a basic attack for the demo.
        ExecuteBasicAttack();
    }

    // ─── Turn Completion ────────────────────────

    private void FinishPlayerTurn()
    {
        _pendingAction = PendingAction.None;
        _selectedTarget = null;
        _currentActor = null;
        _state = CombatState.WaitingForTurn;

        ShowActionButtons(false);
        HideSubPanels();

        turnManager.EndTurn();
    }

    // ═════════════════════════════════════════════
    //  Enemy Turn Flow
    // ═════════════════════════════════════════════

    private void StartEnemyTurn(ICombatant enemy)
    {
        _state = CombatState.EnemyTurn;
        ShowActionButtons(false);

        AddLogLine($">> {enemy.CombatName}'s turn!");

        MonoBehaviour enemyMB = enemy as MonoBehaviour;
        if (enemyMB == null)
        {
            FinishEnemyTurn();
            return;
        }

        EnemyAI ai = enemyMB.GetComponent<EnemyAI>();
        if (ai == null)
        {
            AddLogLine($"{enemy.CombatName} does nothing.");
            FinishEnemyTurn();
            return;
        }

        var alivePlayers = plateManager.GetAliveCombatants(true);
        if (alivePlayers.Count == 0)
        {
            FinishEnemyTurn();
            return;
        }

        PlayerStats targetPlayer = alivePlayers[0] as PlayerStats;
        if (targetPlayer == null)
        {
            FinishEnemyTurn();
            return;
        }

        StatusEffectHandler playerEffects = targetPlayer.Effects;

        EnemySkill.SkillResult result = ai.DecideAndAct(targetPlayer, playerEffects, turnManager);

        CombatPlate targetPlate = plateManager.FindPlate(targetPlayer);
        if (targetPlate != null)
            plateManager.SpotlightTarget(targetPlate);

        RectTransform targetRT = targetPlate != null ? targetPlate.GetComponent<RectTransform>() : null;

        bool wasHit = result.DamageDealt > 0;
        bool wasHeal = result.HealingDone > 0;

        Action onAnimComplete = () =>
        {
            AddLogLine(result.LogMessage);

            if (wasHit && targetRT != null)
                actionAnimator.ShowDamageNumber(targetRT, result.DamageDealt, false, false);
            if (wasHeal)
            {
                CombatPlate selfPlate = plateManager.FindPlate(enemy);
                RectTransform selfRT = selfPlate != null ? selfPlate.GetComponent<RectTransform>() : null;
                if (selfRT != null)
                    actionAnimator.ShowDamageNumber(selfRT, result.HealingDone, false, true);
            }

            plateManager.RestoreSpotlight();
            plateManager.RefreshAllPlates();
            CheckForDeaths();
            FinishEnemyTurn();
        };

        if (wasHit)
            actionAnimator.PlayBasicAttack(targetRT, true, onAnimComplete);
        else if (wasHeal)
            actionAnimator.PlayHealEffect(targetRT, onAnimComplete);
        else
            actionAnimator.PlayMagicAttack(targetRT, true, onAnimComplete);
    }

    private void FinishEnemyTurn()
    {
        _currentActor = null;
        _state = CombatState.WaitingForTurn;
        turnManager.EndTurn();
    }

    // ═════════════════════════════════════════════
    //  Damage Application
    // ═════════════════════════════════════════════

    private int ApplyDamageToTarget(ICombatant target, int damage)
    {
        if (target is PlayerStats player)
            return player.TakeDamage(damage);
        else if (target is EnemyStats enemy)
            return enemy.TakeDamage(damage);
        return 0;
    }

    private void CheckForDeaths()
    {
        var allPlates = plateManager.AllyPlates.Concat(plateManager.EnemyPlates).ToList();

        foreach (var plate in allPlates)
        {
            if (plate.IsOccupied && !plate.IsAlive)
            {
                AddLogLine($"{plate.Combatant.CombatName} has been defeated!");
                turnManager.RemoveCombatant(plate.Combatant);
                plateManager.RemovePlate(plate.Combatant);
            }
        }
    }

    // ═════════════════════════════════════════════
    //  Turn Counter
    // ═════════════════════════════════════════════

    private void UpdateTurnCounter()
    {
        if (turnCounterText == null) return;

        var preview = turnManager.PreviewTurnOrder(1);
        if (preview.Count > 0)
            turnCounterText.text = $"Next: {preview[0].CombatName}";
        else
            turnCounterText.text = "";
    }

    // ═════════════════════════════════════════════
    //  Combat Log
    // ═════════════════════════════════════════════

    private void AddLogLine(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > maxLogLines)
            _logLines.RemoveAt(0);

        UpdateCombatLog();
        Debug.Log($"[CombatLog] {line}");
    }

    private void UpdateCombatLog()
    {
        if (combatLogText == null) return;
        combatLogText.text = string.Join("\n", _logLines);
    }

    // ═════════════════════════════════════════════
    //  Escape Fail Message
    // ═════════════════════════════════════════════

    private void ShowEscapeFailMessage(string message)
    {
        if (escapeFailText == null) return;

        escapeFailText.text = message;
        escapeFailText.gameObject.SetActive(true);

        CancelInvoke(nameof(HideEscapeFailMessage));
        Invoke(nameof(HideEscapeFailMessage), escapeFailDisplayTime);
    }

    private void HideEscapeFailMessage()
    {
        if (escapeFailText != null)
            escapeFailText.gameObject.SetActive(false);
    }

    // ═════════════════════════════════════════════
    //  UI Helpers
    // ═════════════════════════════════════════════

    private void ShowActionButtons(bool show)
    {
        if (attackButton != null)  attackButton.gameObject.SetActive(show);
        if (magicButton != null)   magicButton.gameObject.SetActive(show);
        if (skillsButton != null)  skillsButton.gameObject.SetActive(show);
        if (escapeButton != null)  escapeButton.gameObject.SetActive(show);
    }

    private void HideSubPanels()
    {
        if (magicPanel != null)  magicPanel.SetActive(false);
        if (skillsPanel != null) skillsPanel.SetActive(false);
    }

    private void HideAllPanels()
    {
        ShowActionButtons(false);
        HideSubPanels();

        if (actionPanel != null) actionPanel.SetActive(false);
    }
}
