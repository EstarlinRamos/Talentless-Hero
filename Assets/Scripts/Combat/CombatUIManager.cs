using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main combat UI controller for Talentless Hero's plate-based combat system.
///
/// Handles:
///   - Turn flow (player select → targeting → execute → enemy AI)
///   - Dynamic skill/spell sub-panel population from PlayerCombatAbilities
///   - Player ability execution (damage, healing, status effects)
///   - Freeze check (frozen combatants skip their turn)
///   - Damage reduction from Guard and similar effects
///   - First-turn advantage via BeginCombatWithAdvantage
/// </summary>
public class CombatUIManager : MonoBehaviour
{
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

    [Header("Turn Display")]
    [Tooltip("Shows whose turn it is (e.g., 'Turn: Hero').")]
    [SerializeField] private TextMeshProUGUI currentTurnText;

    [Tooltip("Shows who goes next (e.g., 'Next: Slime').")]
    [SerializeField] private TextMeshProUGUI turnCounterText;

    [Header("Action Buttons Panel")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button magicButton;
    [SerializeField] private Button skillsButton;
    [SerializeField] private Button escapeButton;

    [Header("Spell Sub-Panel")]
    [Tooltip("Panel that shows available spells. Populated dynamically.")]
    [SerializeField] private GameObject magicPanel;
    [SerializeField] private Transform magicButtonContainer;
    [SerializeField] private Button abilityButtonPrefab;

    [Header("Skills Sub-Panel")]
    [Tooltip("Panel that shows available skills. Populated dynamically.")]
    [SerializeField] private GameObject skillsPanel;
    [SerializeField] private Transform skillsButtonContainer;

    [Header("Combat Log (Optional)")]
    [SerializeField] private TextMeshProUGUI combatLogText;
    [SerializeField] private int maxLogLines = 5;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private CombatState _state = CombatState.Inactive;
    private ICombatant _currentActor;
    private PendingAction _pendingAction = PendingAction.None;
    private PlayerAbility _selectedAbility;
    private ICombatant _selectedTarget;
    private List<string> _logLines = new List<string>();
    private PlayerCombatAbilities _playerAbilities;

    public static bool IsInCombat { get; private set; } = false;
    public CombatState CurrentState => _state;

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
        if (_state == CombatState.PlayerTargeting &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelTargeting();
        }
    }

    // ═════════════════════════════════════════════
    //  Public API — Starting / Ending Combat
    // ═════════════════════════════════════════════

    public void BeginCombat(List<ICombatant> allies, List<ICombatant> enemies)
    {
        SetupCombat(allies, enemies);

        var allCombatants = new List<ICombatant>();
        allCombatants.AddRange(allies.Where(a => a.IsAlive));
        allCombatants.AddRange(enemies.Where(e => e.IsAlive));
        turnManager.StartCombat(allCombatants);

        Debug.Log("[CombatUI] Combat started!");
    }

    public void BeginCombatWithAdvantage(
        List<ICombatant> allies,
        List<ICombatant> enemies,
        ICombatant firstTurnCombatant)
    {
        SetupCombat(allies, enemies);

        var allCombatants = new List<ICombatant>();
        allCombatants.AddRange(allies.Where(a => a.IsAlive));
        allCombatants.AddRange(enemies.Where(e => e.IsAlive));
        turnManager.StartCombat(allCombatants, firstTurnCombatant);

        string advantage = firstTurnCombatant?.IsPlayerSide == true ? "Player" : "Enemy";
        AddLogLine($"{advantage} strikes first!");
        Debug.Log("[CombatUI] Combat started with first-turn advantage!");
    }

    private void SetupCombat(List<ICombatant> allies, List<ICombatant> enemies)
    {
        IsInCombat = true;
        _state = CombatState.WaitingForTurn;

        plateManager.InitializePlates(allies, enemies);

        if (actionPanel != null)
            actionPanel.SetActive(true);

        _logLines.Clear();
        UpdateCombatLog();

        // Find player's abilities for sub-panel population
        _playerAbilities = null;
        foreach (var ally in allies)
        {
            if (ally is PlayerStats ps)
            {
                _playerAbilities = ps.GetComponent<PlayerCombatAbilities>();
                break;
            }
        }
    }

    public void EndCombat(bool victory)
    {
        _state = CombatState.CombatEnd;
        IsInCombat = false;

        plateManager.ExitTargetingMode();
        plateManager.RestoreSpotlight();
        HideAllPanels();

        if (currentTurnText != null) currentTurnText.text = "";
        if (turnCounterText != null) turnCounterText.text = "";

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

        if (currentTurnText != null)
            currentTurnText.text = $"Turn: {combatant.CombatName}";

        UpdateTurnCounter();
        plateManager.RefreshAllPlates();

        // Check for Freeze — frozen combatants skip their turn
        MonoBehaviour combatantMB = combatant as MonoBehaviour;
        if (combatantMB != null)
        {
            StatusEffectHandler effects = combatantMB.GetComponent<StatusEffectHandler>();
            if (effects != null && effects.IsFrozen)
            {
                AddLogLine($"{combatant.CombatName} is frozen and cannot act!");
                effects.OnTurnStart(); // Still ticks effects/decrements freeze
                plateManager.RefreshAllPlates();

                // Skip turn
                _currentActor = null;
                _state = CombatState.WaitingForTurn;
                turnManager.EndTurn();
                return;
            }
        }

        if (combatant.IsPlayerSide)
            StartPlayerTurn(combatant);
        else
            StartEnemyTurn(combatant);
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

    private void HandleTickUpdate(IReadOnlyList<CombatTurnManager.CombatantState> states) { }

    // ═════════════════════════════════════════════
    //  Player Turn Flow
    // ═════════════════════════════════════════════

    private void StartPlayerTurn(ICombatant player)
    {
        _state = CombatState.PlayerSelectAction;
        _pendingAction = PendingAction.None;
        _selectedAbility = null;
        _selectedTarget = null;

        // Process status effects
        if (player is PlayerStats ps)
        {
            int dotDamage = ps.Effects.OnTurnStart();
            if (dotDamage > 0)
            {
                ps.TakeDamage(dotDamage);
                AddLogLine($"{player.CombatName} took {dotDamage} DoT damage!");
            }

            int hotHeal = ps.Effects.GetHealPerTurn();
            if (hotHeal > 0)
                AddLogLine($"{player.CombatName} healed {hotHeal} HP from healing effect.");

            plateManager.RefreshPlate(player);
        }

        ShowActionButtons(true);
        HideSubPanels();

        // Silence check
        if (player is PlayerStats playerStats)
        {
            bool silenced = playerStats.Effects.IsSilenced;
            if (magicButton != null) magicButton.interactable = !silenced;
            if (skillsButton != null) skillsButton.interactable = !silenced;

            if (silenced)
                AddLogLine($"{player.CombatName} is silenced! Only basic attack available.");
        }

        AddLogLine($">> {player.CombatName}'s turn!");
    }

    // ─── Action Button Handlers ─────────────────

    private void OnAttackPressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;
        _pendingAction = PendingAction.BasicAttack;
        _selectedAbility = null;
        EnterTargeting();
    }

    private void OnMagicPressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;

        HideSubPanels();
        PopulateSubPanel(magicPanel, magicButtonContainer,
            _playerAbilities?.Spells, PendingAction.Magic);

        if (magicPanel != null)
            magicPanel.SetActive(true);
    }

    private void OnSkillsPressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;

        HideSubPanels();
        PopulateSubPanel(skillsPanel, skillsButtonContainer,
            _playerAbilities?.Skills, PendingAction.Skill);

        if (skillsPanel != null)
            skillsPanel.SetActive(true);
    }

    private void OnEscapePressed()
    {
        if (_state != CombatState.PlayerSelectAction) return;

        if (plateManager.HasAliveBoss())
        {
            AddLogLine("Cannot escape — a powerful presence blocks the way!");
            return;
        }

        AddLogLine("Escaped from battle!");
        EndCombat(false);
    }

    // ─── Sub-Panel Population ───────────────────

    private void PopulateSubPanel(GameObject panel, Transform container,
        IReadOnlyList<PlayerAbility> abilities, PendingAction actionType)
    {
        if (container == null || abilityButtonPrefab == null) return;

        // Clear existing buttons
        foreach (Transform child in container)
            Destroy(child.gameObject);

        if (abilities == null || abilities.Count == 0)
        {
            AddLogLine("No abilities available.");
            return;
        }

        PlayerStats player = _currentActor as PlayerStats;

        foreach (var ability in abilities)
        {
            Button btn = Instantiate(abilityButtonPrefab, container);
            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();

            // Show name and MP cost
            string label = ability.mpCost > 0
                ? $"{ability.abilityName} ({ability.mpCost} MP)"
                : ability.abilityName;

            if (btnText != null)
                btnText.text = label;

            // Disable if not enough MP
            bool canAfford = player != null && player.CurrentMP >= ability.mpCost;
            btn.interactable = canAfford;

            // Capture for closure
            PlayerAbility capturedAbility = ability;
            PendingAction capturedAction = actionType;

            btn.onClick.AddListener(() =>
            {
                OnAbilitySelected(capturedAbility, capturedAction);
            });
        }
    }

    private void OnAbilitySelected(PlayerAbility ability, PendingAction actionType)
    {
        _pendingAction = actionType;
        _selectedAbility = ability;

        HideSubPanels();

        // Self-targeting abilities execute immediately
        if (ability.targetType == PlayerAbility.TargetType.Self)
        {
            _selectedTarget = _currentActor;
            ExecutePlayerAction();
        }
        else
        {
            EnterTargeting();
        }
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
        _selectedAbility = null;
        _selectedTarget = null;
        _state = CombatState.PlayerSelectAction;
        ShowActionButtons(true);
        HideSubPanels();
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

        if (_selectedAbility != null)
        {
            ExecuteAbility();
        }
        else
        {
            switch (_pendingAction)
            {
                case PendingAction.BasicAttack:
                    ExecuteBasicAttack();
                    break;
                default:
                    FinishPlayerTurn();
                    break;
            }
        }
    }

    private void ExecuteBasicAttack()
    {
        PlayerStats player = _currentActor as PlayerStats;
        if (player == null) { FinishPlayerTurn(); return; }

        float rawDamage = player.PhysicalDamage;
        float defense = 0f;

        if (_selectedTarget is EnemyStats et) defense = et.PhysicalDefense;
        else if (_selectedTarget is PlayerStats pt) defense = pt.PhysicalDefense;

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
                    ? $"{player.CombatName} CRIT! {actualDamage} damage to {_selectedTarget.CombatName}!"
                    : $"{player.CombatName} attacks {_selectedTarget.CombatName} for {actualDamage} damage.";
                AddLogLine(logMsg);
            }
            else
            {
                AddLogLine($"{player.CombatName} attacked but missed!");
            }

            plateManager.RestoreSpotlight();
            plateManager.RefreshAllPlates();
            CheckForDeaths();
            FinishPlayerTurn();
        });
    }

    /// <summary>
    /// Execute a player ability (skill or spell) on the selected target.
    /// Handles damage, healing, MP cost, status effects, and proc rolls.
    /// </summary>
    private void ExecuteAbility()
    {
        PlayerStats player = _currentActor as PlayerStats;
        if (player == null || _selectedAbility == null) { FinishPlayerTurn(); return; }

        // Spend MP
        if (_selectedAbility.mpCost > 0)
        {
            if (!player.SpendMP(_selectedAbility.mpCost))
            {
                AddLogLine($"Not enough MP for {_selectedAbility.abilityName}!");
                _state = CombatState.PlayerSelectAction;
                ShowActionButtons(true);
                return;
            }
        }

        // Play cast SFX
        if (_selectedAbility.castSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(_selectedAbility.castSFX);

        // Apply self-buff if applicable
        if (_selectedAbility.appliesSelfEffect)
        {
            StatusEffect selfEffect = _selectedAbility.CreateSelfEffect(player);
            player.Effects.ApplyEffect(selfEffect);
            AddLogLine($"{player.CombatName} used {_selectedAbility.abilityName}! " +
                       $"{selfEffect.EffectName} applied.");
        }

        // If no damage and no target effect, we're done (self-buff only)
        if (_selectedAbility.damageType == PlayerAbility.DamageType.None && !_selectedAbility.appliesEffect)
        {
            plateManager.RefreshAllPlates();

            // Play heal/buff animation for self-targeting
            if (_selectedAbility.targetType == PlayerAbility.TargetType.Self)
            {
                CombatPlate selfPlate = plateManager.FindPlate(player);
                RectTransform selfRT = selfPlate != null ? selfPlate.GetComponent<RectTransform>() : null;
                actionAnimator.PlayHealEffect(selfRT, () =>
                {
                    FinishPlayerTurn();
                });
            }
            else
            {
                FinishPlayerTurn();
            }
            return;
        }

        // Damage-dealing ability
        float rawDamage = _selectedAbility.baseDamage;

        if (_selectedAbility.scalesWithStats)
        {
            if (_selectedAbility.damageType == PlayerAbility.DamageType.Physical)
                rawDamage += player.PhysicalDamage;
            else if (_selectedAbility.damageType == PlayerAbility.DamageType.Magic)
                rawDamage += player.MagicDamage;
        }

        // Defense
        float defense = 0f;
        if (_selectedAbility.damageType == PlayerAbility.DamageType.Physical)
        {
            if (_selectedTarget is EnemyStats et) defense = et.PhysicalDefense;
            else if (_selectedTarget is PlayerStats pt) defense = pt.PhysicalDefense;
        }
        else if (_selectedAbility.damageType == PlayerAbility.DamageType.Magic)
        {
            if (_selectedTarget is EnemyStats et) defense = et.MagicDefense;
            else if (_selectedTarget is PlayerStats pt) defense = pt.MagicDefense;
        }

        float reducedDamage = Mathf.Max(rawDamage - defense, 1f);
        int finalDamage = Mathf.RoundToInt(reducedDamage);

        // Crit check
        bool crit = player.RollCrit();
        if (crit)
            finalDamage = Mathf.RoundToInt(finalDamage * player.CritDamageMultiplier);

        CombatPlate targetPlate = plateManager.FindPlate(_selectedTarget);
        RectTransform targetRT = targetPlate != null ? targetPlate.GetComponent<RectTransform>() : null;

        // Choose animation based on damage type
        bool isMagic = _selectedAbility.damageType == PlayerAbility.DamageType.Magic;
        Action<RectTransform, bool, Action> playAnim = isMagic
            ? actionAnimator.PlayMagicAttack
            : actionAnimator.PlayBasicAttack;

        playAnim(targetRT, true, () =>
        {
            int actualDamage = ApplyDamageToTarget(_selectedTarget, finalDamage);
            if (targetRT != null)
                actionAnimator.ShowDamageNumber(targetRT, actualDamage, crit, false);

            string logMsg = crit
                ? $"{player.CombatName} CRIT {_selectedAbility.abilityName}! {actualDamage} damage!"
                : $"{player.CombatName} uses {_selectedAbility.abilityName} for {actualDamage} damage.";
            AddLogLine(logMsg);

            // Roll for status effect on target
            if (_selectedAbility.appliesEffect && _selectedAbility.RollEffectChance())
            {
                MonoBehaviour targetMB = _selectedTarget as MonoBehaviour;
                StatusEffectHandler targetEffects = targetMB?.GetComponent<StatusEffectHandler>();

                if (targetEffects != null)
                {
                    StatusEffect effect = _selectedAbility.CreateTargetEffect(player);

                    // Special case: Burn uses 5% of target's max HP
                    if (effect.Type == StatusEffect.EffectType.DamageOverTime &&
                        effect.EffectName == "Burn")
                    {
                        int maxHP = 0;
                        if (_selectedTarget is EnemyStats es) maxHP = es.MaxHP;
                        else if (_selectedTarget is PlayerStats ps2) maxHP = ps2.MaxHP;
                        effect.Value = Mathf.RoundToInt(maxHP * 0.05f);
                    }

                    targetEffects.ApplyEffect(effect);
                    AddLogLine($"{_selectedTarget.CombatName} is {effect.EffectName}!");
                }
            }

            plateManager.RestoreSpotlight();
            plateManager.RefreshAllPlates();
            CheckForDeaths();
            FinishPlayerTurn();
        });
    }

    // ─── Turn Completion ────────────────────────

    private void FinishPlayerTurn()
    {
        _pendingAction = PendingAction.None;
        _selectedAbility = null;
        _selectedTarget = null;
        _currentActor = null;

        ShowActionButtons(false);
        HideSubPanels();

        // Don't advance turns if combat already ended (e.g., enemy just died)
        if (_state == CombatState.CombatEnd || !IsInCombat) return;

        _state = CombatState.WaitingForTurn;
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
        if (enemyMB == null) { FinishEnemyTurn(); return; }

        // Process enemy status effects
        StatusEffectHandler enemyEffects = enemyMB.GetComponent<StatusEffectHandler>();
        if (enemyEffects != null)
        {
            int dotDamage = enemyEffects.OnTurnStart();
            if (dotDamage > 0)
            {
                EnemyStats es = enemyMB.GetComponent<EnemyStats>();
                if (es != null)
                {
                    es.TakeDamage(dotDamage);
                    AddLogLine($"{enemy.CombatName} took {dotDamage} burn damage!");
                    plateManager.RefreshPlate(enemy);

                    if (!es.IsAlive)
                    {
                        CheckForDeaths();
                        FinishEnemyTurn();
                        return;
                    }
                }
            }
        }

        EnemyAI ai = enemyMB.GetComponent<EnemyAI>();
        if (ai == null)
        {
            AddLogLine($"{enemy.CombatName} does nothing.");
            FinishEnemyTurn();
            return;
        }

        var alivePlayers = plateManager.GetAliveCombatants(true);
        if (alivePlayers.Count == 0) { FinishEnemyTurn(); return; }

        PlayerStats targetPlayer = alivePlayers[0] as PlayerStats;
        if (targetPlayer == null) { FinishEnemyTurn(); return; }

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

        // Don't advance turns if combat already ended
        if (_state == CombatState.CombatEnd || !IsInCombat) return;

        _state = CombatState.WaitingForTurn;
        turnManager.EndTurn();
    }

    // ═════════════════════════════════════════════
    //  Damage Application
    // ═════════════════════════════════════════════

    /// <summary>
    /// Apply damage to any combatant. Respects DamageReduction (Guard).
    /// </summary>
    private int ApplyDamageToTarget(ICombatant target, int damage)
    {
        // Check for damage reduction (Guard)
        MonoBehaviour targetMB = target as MonoBehaviour;
        if (targetMB != null)
        {
            StatusEffectHandler effects = targetMB.GetComponent<StatusEffectHandler>();
            if (effects != null)
            {
                float reduction = effects.GetDamageMultiplier();
                damage = Mathf.RoundToInt(damage * reduction);
                damage = Mathf.Max(damage, 1);
            }
        }

        if (target is PlayerStats player)
            return player.TakeDamage(damage);
        else if (target is EnemyStats enemy)
            return enemy.TakeDamage(damage);
        return 0;
    }

    private void CheckForDeaths()
    {
        // Snapshot the plates list — removal during iteration is safe
        // because we break after any death triggers combat end
        var allPlates = plateManager.AllyPlates.Concat(plateManager.EnemyPlates).ToList();

        foreach (var plate in allPlates)
        {
            // Plate may have been destroyed by a previous iteration
            if (plate == null) continue;

            if (plate.IsOccupied && !plate.IsAlive)
            {
                ICombatant deadCombatant = plate.Combatant;
                AddLogLine($"{deadCombatant.CombatName} has been defeated!");

                // RemoveCombatant may trigger EndCombat synchronously
                // if this was the last enemy/ally
                turnManager.RemoveCombatant(deadCombatant);
                plateManager.RemovePlate(deadCombatant);

                // If combat ended due to this death, stop processing
                if (_state == CombatState.CombatEnd || !IsInCombat)
                    return;
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
    //  UI Visibility Helpers
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
