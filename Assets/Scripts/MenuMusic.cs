using UnityEngine;

/// <summary>
/// Plays main menu background music when the MainMenu scene loads.
/// Also handles returning to the menu from the game — since AudioManager
/// persists via DontDestroyOnLoad, this script tells it to switch tracks.
///
/// SETUP:
///   1. Create an empty GameObject in the MainMenu scene called "MenuMusic"
///   2. Attach this script
///   3. Assign the menu music AudioClip
/// </summary>
public class MenuMusic : MonoBehaviour
{
    [Header("Main Menu Music")]
    [Tooltip("Background music for the main menu screen.")]
    [SerializeField] private AudioClip menuMusicClip;

    private void Start()
    {
        if (AudioManager.Instance != null && menuMusicClip != null)
        {
            AudioManager.Instance.PlayOverworldMusic(menuMusicClip);
            Debug.Log("[MenuMusic] Playing main menu music.");
        }
    }
}
