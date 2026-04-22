using UnityEngine;
using System.Collections;

/// <summary>
/// Fades the screen in from black when the scene loads.
/// Provides a smooth entrance instead of a jarring cut.
///
/// Works with ScreenFader — waits one frame for ScreenFader.Instance
/// to initialize, then triggers the fade.
///
/// SETUP:
///   1. Attach to any GameObject in the scene (can be on the ScreenFade object itself)
///   2. Set the fade duration
///   3. Make sure ScreenFader exists in the scene
/// </summary>
public class SceneFadeIn : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("Duration of the fade from black to clear.")]
    [SerializeField] private float fadeDuration = 1.0f;

    [Tooltip("Delay before the fade starts (lets other systems initialize).")]
    [SerializeField] private float startDelay = 0.1f;

    private IEnumerator Start()
    {
        // Wait a frame for ScreenFader to initialize
        yield return null;

        if (ScreenFader.Instance != null)
        {
            // Start fully black
            ScreenFader.Instance.SetBlack();

            // Wait the optional delay
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            // Fade to clear
            yield return ScreenFader.Instance.FadeIn(fadeDuration);
        }
        else
        {
            Debug.LogWarning("[SceneFadeIn] No ScreenFader found in scene.");
        }
    }
}
