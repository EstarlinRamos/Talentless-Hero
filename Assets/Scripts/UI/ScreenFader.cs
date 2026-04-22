using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Full-screen fade effect for cutscenes and teleportation.
/// Uses a UI Image stretched across the entire canvas.
///
/// Singleton so any system can trigger fades without direct references.
/// Lives on the game Canvas — NOT DontDestroyOnLoad.
/// Each scene that needs fading should have its own ScreenFader.
///
/// SETUP:
///   1. Create a UI Image under your Canvas called "ScreenFade"
///   2. Anchor it to stretch-fill (all four corners pinned)
///   3. Set color to black with alpha 0 (fully transparent)
///   4. Place it as the LAST child of Canvas so it renders on top
///   5. Set Raycast Target to false (so it doesn't block clicks when transparent)
///   6. Attach this script to that Image GameObject
/// </summary>
[RequireComponent(typeof(Image))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    private Image _fadeImage;
    private Coroutine _activeFade;

    private void Awake()
    {
        // Scene-level singleton (no DontDestroyOnLoad)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _fadeImage = GetComponent<Image>();
        SetAlpha(0f);
        _fadeImage.raycastTarget = false;
    }

    /// <summary>
    /// Fade from transparent to black over the given duration.
    /// </summary>
    public Coroutine FadeOut(float duration = 0.8f)
    {
        return StartFade(0f, 1f, duration);
    }

    /// <summary>
    /// Fade from black to transparent over the given duration.
    /// </summary>
    public Coroutine FadeIn(float duration = 0.8f)
    {
        return StartFade(1f, 0f, duration);
    }

    /// <summary>
    /// Immediately snap to fully black (no animation).
    /// </summary>
    public void SetBlack()
    {
        StopActiveFade();
        SetAlpha(1f);
        _fadeImage.raycastTarget = true;
    }

    /// <summary>
    /// Immediately snap to fully transparent (no animation).
    /// </summary>
    public void SetClear()
    {
        StopActiveFade();
        SetAlpha(0f);
        _fadeImage.raycastTarget = false;
    }

    private Coroutine StartFade(float from, float to, float duration)
    {
        StopActiveFade();

        // Block raycasts during fade so nothing is clickable behind it
        _fadeImage.raycastTarget = true;
        _activeFade = StartCoroutine(FadeCoroutine(from, to, duration));
        return _activeFade;
    }

    private IEnumerator FadeCoroutine(float from, float to, float duration)
    {
        float elapsed = 0f;
        SetAlpha(from);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetAlpha(to);
        _activeFade = null;

        // Only stop blocking raycasts if we faded to transparent
        if (to <= 0f)
            _fadeImage.raycastTarget = false;
    }

    private void StopActiveFade()
    {
        if (_activeFade != null)
        {
            StopCoroutine(_activeFade);
            _activeFade = null;
        }
    }

    private void SetAlpha(float alpha)
    {
        Color c = _fadeImage.color;
        c.a = alpha;
        _fadeImage.color = c;

        // Never block raycasts when fully transparent
        _fadeImage.raycastTarget = alpha > 0.01f;
    }
}
