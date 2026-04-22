using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Title screen menu with New Game, Load Game, Music toggle, and Exit buttons.
/// </summary>
public class MainStartMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private Button exitButton;

    [Header("Music Button Text")]
    [SerializeField] private TextMeshProUGUI musicButtonText;

    private void Start()
    {
        newGameButton.onClick.AddListener(OnNewGame);
        loadGameButton.onClick.AddListener(OnLoadGame);
        musicButton.onClick.AddListener(OnToggleMusic);
        exitButton.onClick.AddListener(OnExit);

        if (loadGameButton != null)
            loadGameButton.interactable = SaveManager.Instance != null && SaveManager.Instance.HasAnySaves();

        RefreshMusicButtonText();
    }

    private void OnNewGame()
    {
        SaveManager.Instance.NewGame();
    }

    private void OnLoadGame()
    {
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
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void RefreshMusicButtonText()
    {
        if (musicButtonText == null) return;

        bool muted = AudioManager.Instance != null && AudioManager.Instance.IsMusicMuted;
        musicButtonText.text = muted ? "Music: OFF" : "Music: ON";
    }
}
