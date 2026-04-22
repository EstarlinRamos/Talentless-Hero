using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Automatically adds a click sound to every Button under this GameObject.
/// Attach to a Canvas or parent panel — all child buttons get the sound.
///
/// Uses AudioManager.PlaySFX so it respects volume and mute settings.
///
/// SETUP:
///   1. Attach to your Canvas (or any parent of buttons)
///   2. Assign a click AudioClip in the Inspector
///   3. Every button underneath plays the sound on click
///   4. Buttons added at runtime are NOT auto-detected —
///      call RegisterButton() manually if needed
/// </summary>
public class ButtonClickSFX : MonoBehaviour
{
    [Header("Click Sound")]
    [Tooltip("Sound played when any button is clicked.")]
    [SerializeField] private AudioClip clickSound;

    [Tooltip("Volume scale for the click sound (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 0.6f;

    private void Start()
    {
        // Find every button in children and add the click listener
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            RegisterButton(button);
        }

        Debug.Log($"[ButtonSFX] Registered click sound on {buttons.Length} buttons.");
    }

    /// <summary>
    /// Register a button for click SFX. Call this for buttons created at runtime.
    /// </summary>
    public void RegisterButton(Button button)
    {
        button.onClick.AddListener(PlayClick);
    }

    private void PlayClick()
    {
        if (clickSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clickSound, clickVolume);
    }
}
