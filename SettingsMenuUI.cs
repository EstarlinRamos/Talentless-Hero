using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game settings / pause menu with Save, Load, Music toggle, and Exit.
/// Pauses the game while open. Blocked during combat — the panel closes
/// immediately if opened while CombatUIManager.IsInCombat is true.
/// </summary>
public class SettingsMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private Button exitButton;

    [Header("Music Button Text")]
    [SerializeField] private TextMeshProUGUI musicButtonText;

    [Header("Exit Behavior")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void OnEnable()
    {
        if (CombatUIManager.IsInCombat)
        {
            Debug.Log("[Settings] Cannot open settings during combat.");
            gameObject.SetActive(false);
            return;
        }

        saveGameButton.onClick.AddListener(OnSaveGame);
        loadGameButton.onClick.AddListener(OnLoadGame);
        musicButton.onClick.AddListener(OnToggleMusic);
        exitButton.onClick.AddListener(OnExit);

        RefreshMusicButtonText();

        Time.timeScale = 0f;
    }

    private void OnDisable()
    {
        saveGameButton.onClick.RemoveListener(OnSaveGame);
        loadGameButton.onClick.RemoveListener(OnLoadGame);
        musicButton.onClick.RemoveListener(OnToggleMusic);
        exitButton.onClick.RemoveListener(OnExit);

        Time.timeScale = 1f;
    }

    private void OnSaveGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
            Debug.Log("[Settings] Game saved.");
        }
    }

    private void OnLoadGame()
    {
        Time.timeScale = 1f;

        if (SaveManager.Instance != null)
            SaveManager.Instance.LoadGame();
    }

    private void OnToggleMusic()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ToggleMusic();
            RefreshMusicButtonText();
        }
    }

    private void OnExit()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void RefreshMusicButtonText()
    {
        if (musicButtonText == null) return;

        bool muted = AudioManager.Instance != null && AudioManager.Instance.IsMusicMuted;
        musicButtonText.text = muted ? "Music: OFF" : "Music: ON";
    }
}
