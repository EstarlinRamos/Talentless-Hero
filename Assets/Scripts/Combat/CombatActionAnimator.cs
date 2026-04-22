using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// Handles visual and audio feedback for combat actions.
///
/// Slash: SetActive toggled (works fine disabled).
/// Screen Flash: Uses a CanvasGroup — stays ENABLED, alpha controlled.
/// SFX: Routes through AudioManager (no local AudioSource needed).
///
/// SETUP:
///   HitFlash overlay:
///     1. Create a full-screen Image under the combat canvas
///     2. Color: white, alpha 0
///     3. Raycast Target: UNCHECKED
///     4. Add CanvasGroup, set alpha to 0
///     5. Keep the GameObject ENABLED — never disable it
///     6. Drag the CanvasGroup into "Flash Group"
///     7. Drag the Image into "Flash Image"
///
///   Slash overlay:
///     1. Create an Image with a diagonal slash sprite
///     2. Can start disabled — the script toggles SetActive
/// </summary>
public class CombatActionAnimator : MonoBehaviour
{
    [Header("Slash Effect")]
    [SerializeField] private Image slashImage;
    [SerializeField] private float slashDuration = 0.15f;
    [SerializeField] private float postHitPause = 0.25f;

    [Header("Screen Flash")]
    [Tooltip("CanvasGroup on the flash overlay. Stays enabled, hidden via alpha.")]
    [SerializeField] private CanvasGroup flashGroup;

    [Tooltip("Image on the flash overlay (for setting flash color).")]
    [SerializeField] private Image flashImage;

    [SerializeField] private Color physicalHitFlash = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] private Color magicHitFlash = new Color(0.5f, 0.3f, 1f, 0.4f);
    [SerializeField] private float flashFadeDuration = 0.15f;

    [Header("Audio — Attack Sounds")]
    [SerializeField] private AudioClip basicAttackSFX;
    [SerializeField] private AudioClip magicAttackSFX;
    [SerializeField] private AudioClip skillAttackSFX;
    [SerializeField] private AudioClip missSFX;
    [SerializeField] private AudioClip healSFX;

    [Header("Audio — Hit Impact Sounds")]
    [Tooltip("Played when a physical attack connects.")]
    [SerializeField] private AudioClip physicalHitSFX;

    [Tooltip("Played when a magic attack connects.")]
    [SerializeField] private AudioClip magicHitSFX;

    [Header("Damage Number (Optional)")]
    [SerializeField] private GameObject damageNumberPrefab;

    private bool _animating = false;
    public bool IsAnimating => _animating;

    private void Awake()
    {
        if (slashImage != null)
            slashImage.gameObject.SetActive(false);

        if (flashGroup != null)
            flashGroup.alpha = 0f;
    }

    // ═════════════════════════════════════════════
    //  Public API
    // ═════════════════════════════════════════════

    public void PlayBasicAttack(RectTransform targetPosition, bool hit, Action onComplete)
    {
        StartCoroutine(BasicAttackRoutine(targetPosition, hit, onComplete));
    }

    public void PlayMagicAttack(RectTransform targetPosition, bool hit, Action onComplete)
    {
        StartCoroutine(MagicAttackRoutine(targetPosition, hit, onComplete));
    }

    public void PlaySkillAttack(RectTransform targetPosition, bool hit, Action onComplete)
    {
        StartCoroutine(SkillAttackRoutine(targetPosition, hit, onComplete));
    }

    public void PlayHealEffect(RectTransform targetPosition, Action onComplete)
    {
        StartCoroutine(HealRoutine(targetPosition, onComplete));
    }

    public void ShowDamageNumber(RectTransform position, int amount, bool isCrit, bool isHeal)
    {
        if (damageNumberPrefab == null || position == null) return;

        GameObject numObj = Instantiate(damageNumberPrefab, position.parent);
        RectTransform numRT = numObj.GetComponent<RectTransform>();
        if (numRT != null)
            numRT.anchoredPosition = position.anchoredPosition + new Vector2(0, 40f);

        var tmp = numObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null)
        {
            if (isHeal)
            {
                tmp.text = $"+{amount}";
                tmp.color = Color.green;
            }
            else
            {
                tmp.text = isCrit ? $"{amount}!" : $"{amount}";
                tmp.color = isCrit ? Color.yellow : Color.white;
            }

            if (isCrit)
                tmp.fontSize *= 1.3f;
        }

        Destroy(numObj, 1.0f);
    }

    // ═════════════════════════════════════════════
    //  Animation Coroutines
    // ═════════════════════════════════════════════

    private IEnumerator BasicAttackRoutine(RectTransform target, bool hit, Action onComplete)
    {
        _animating = true;

        PlaySFX(basicAttackSFX);

        if (slashImage != null && target != null)
        {
            PositionSlashAtTarget(target);
            slashImage.gameObject.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(slashDuration);

        if (slashImage != null)
            slashImage.gameObject.SetActive(false);

        if (hit)
        {
            PlaySFX(physicalHitSFX);
            yield return FlashScreen(physicalHitFlash);
        }
        else
        {
            PlaySFX(missSFX);
        }

        yield return new WaitForSecondsRealtime(postHitPause);

        _animating = false;
        onComplete?.Invoke();
    }

    private IEnumerator MagicAttackRoutine(RectTransform target, bool hit, Action onComplete)
    {
        _animating = true;

        PlaySFX(magicAttackSFX);

        if (hit)
        {
            PlaySFX(magicHitSFX);
            yield return FlashScreen(magicHitFlash);
        }
        else
        {
            PlaySFX(missSFX);
            yield return new WaitForSecondsRealtime(slashDuration);
        }

        yield return new WaitForSecondsRealtime(postHitPause);

        _animating = false;
        onComplete?.Invoke();
    }

    private IEnumerator SkillAttackRoutine(RectTransform target, bool hit, Action onComplete)
    {
        _animating = true;

        PlaySFX(skillAttackSFX != null ? skillAttackSFX : basicAttackSFX);

        if (slashImage != null && target != null)
        {
            PositionSlashAtTarget(target);
            slashImage.gameObject.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(slashDuration);

        if (slashImage != null)
            slashImage.gameObject.SetActive(false);

        if (hit)
        {
            PlaySFX(physicalHitSFX);
            yield return FlashScreen(physicalHitFlash);
        }
        else
        {
            PlaySFX(missSFX);
        }

        yield return new WaitForSecondsRealtime(postHitPause);

        _animating = false;
        onComplete?.Invoke();
    }

    private IEnumerator HealRoutine(RectTransform target, Action onComplete)
    {
        _animating = true;

        PlaySFX(healSFX);

        Color healFlash = new Color(0.2f, 1f, 0.3f, 0.25f);
        yield return FlashScreen(healFlash);

        yield return new WaitForSecondsRealtime(postHitPause);

        _animating = false;
        onComplete?.Invoke();
    }

    // ═════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════

    private void PositionSlashAtTarget(RectTransform target)
    {
        if (slashImage == null || target == null) return;

        RectTransform slashRT = slashImage.GetComponent<RectTransform>();
        if (slashRT != null)
            slashRT.position = target.position;
    }

    /// <summary>
    /// Flash the screen overlay. Uses CanvasGroup alpha so the
    /// GameObject never needs to be disabled/enabled.
    /// </summary>
    private IEnumerator FlashScreen(Color flashColor)
    {
        if (flashGroup == null || flashImage == null) yield break;

        // Set color and show at full intensity
        flashImage.color = flashColor;
        flashGroup.alpha = 1f;

        // Fade out
        float elapsed = 0f;
        while (elapsed < flashFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            flashGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / flashFadeDuration);
            yield return null;
        }

        flashGroup.alpha = 0f;
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip);
    }
}
