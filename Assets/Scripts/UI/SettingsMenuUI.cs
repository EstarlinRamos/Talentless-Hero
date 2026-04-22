using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// In-game Settings / Pause menu.
/// Buttons: Save Game, Load Game, Music (mute/unmute), Exit
/// Toggle this panel's GameObject on/off with your pause key.
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
        saveGameButton.onClick.AddListener(OnSaveGame);
        loadGameButton.onClick.AddListener(OnLoadGame);
        musicButton.onClick.AddListener(OnToggleMusic);
        exitButton.onClick.AddListener(OnExit);

        RefreshMusicButtonText();

        // Pause the game while settings are open
        Time.timeScale = 0f;
    }

    private void OnDisable()
    {
        saveGameButton.onClick.RemoveListener(OnSaveGame);
        loadGameButton.onClick.RemoveListener(OnLoadGame);
        musicButton.onClick.RemoveListener(OnToggleMusic);
        exitButton.onClick.RemoveListener(OnExit);

        // Resume the game when settings close
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
        // Resume time before loading so the loaded scene isn't paused
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
        // Resume time before exiting to main menu
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
