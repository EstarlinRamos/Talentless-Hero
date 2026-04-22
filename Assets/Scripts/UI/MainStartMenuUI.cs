using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Main menu for Talentless Hero.
/// Buttons: New Game, Continue, Load Game, Exit
///
/// Continue: Loads the most recent save file automatically.
///           Disabled if no saves exist.
///
/// Load Game: Opens the Windows file explorer for manual save selection.
///            Disabled if no saves exist.
///
/// SETUP:
///   1. Canvas should be Screen Space - Overlay
///   2. Assign all 4 buttons in the Inspector
///   3. SaveManager and AudioManager must exist in this scene
///      as root-level singletons (they persist via DontDestroyOnLoad)
///   4. EventSystem must be in the GAME scene, not nested here
/// </summary>
public class MainStartMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitButton;

    [Header("Optional Text")]
    [Tooltip("Text on the Continue button (shows save info if available).")]
    [SerializeField] private TextMeshProUGUI continueButtonText;

    private bool _loading = false;

    private IEnumerator Start()
    {
        // Wait for singletons to initialize
        while (SaveManager.Instance == null)
            yield return null;

        // Wire buttons
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGame);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);

        if (loadGameButton != null)
            loadGameButton.onClick.AddListener(OnLoadGame);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExit);

        RefreshButtonStates();
    }

    /// <summary>
    /// Enable/disable Continue and Load Game based on whether saves exist.
    /// </summary>
    private void RefreshButtonStates()
    {
        bool hasSaves = SaveManager.Instance != null && SaveManager.Instance.HasAnySaves();

        if (continueButton != null)
            continueButton.interactable = hasSaves;

        if (loadGameButton != null)
            loadGameButton.interactable = hasSaves;

        // Optionally show which save Continue will load
        if (continueButtonText != null && hasSaves)
        {
            var saves = SaveManager.Instance.GetAllSaves();
            if (saves.Count > 0)
                continueButtonText.text = $"Continue";
        }
    }

    // ─────────────────────────────────────────────
    //  Button Handlers
    // ─────────────────────────────────────────────

    private void OnNewGame()
    {
        if (_loading) return;
        _loading = true;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.NewGame();
        }
        else
        {
            Debug.LogError("[MainMenu] SaveManager not found! Cannot start new game.");
            _loading = false;
        }
    }

    private void OnContinue()
    {
        if (_loading) return;
        _loading = true;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Continue();
        }
        else
        {
            Debug.LogError("[MainMenu] SaveManager not found!");
            _loading = false;
        }
    }

    private void OnLoadGame()
    {
        if (_loading) return;

        if (SaveManager.Instance != null)
        {
            // Don't set _loading here — the file browser is a dialog
            // that can be cancelled, and we want the menu to stay usable
            SaveManager.Instance.LoadGame();
        }
        else
        {
            Debug.LogError("[MainMenu] SaveManager not found!");
        }
    }

    private void OnExit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
