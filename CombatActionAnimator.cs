using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// Handles visual and audio feedback for combat actions.
/// Uses callbacks so the combat flow waits for animations to finish before proceeding.
/// </summary>
public class CombatActionAnimator : MonoBehaviour
{
    [Header("Slash Effect")]
    [Tooltip("Image used for the slash/swipe overlay. Should have a diagonal slash sprite.")]
    [SerializeField] private Image slashImage;

    [Tooltip("How long the slash is visible in seconds.")]
    [SerializeField] private float slashDuration = 0.15f;

    [Tooltip("Pause after the slash before combat continues (impact feel).")]
    [SerializeField] private float postHitPause = 0.25f;

    [Header("Screen Flash")]
    [Tooltip("Full-screen overlay image for hit flash effect. Set alpha to 0 by default.")]
    [SerializeField] private Image hitFlashImage;

    [Tooltip("Color of the flash on physical hit.")]
    [SerializeField] private Color physicalHitFlash = new Color(1f, 1f, 1f, 0.3f);

    [Tooltip("Color of the flash on magic hit.")]
    [SerializeField] private Color magicHitFlash = new Color(0.5f, 0.3f, 1f, 0.3f);

    [Tooltip("How quickly the flash fades out.")]
    [SerializeField] private float flashFadeDuration = 0.15f;

    [Header("Audio")]
    [Tooltip("AudioSource for combat sound effects.")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Whoosh sound for basic physical attacks.")]
    [SerializeField] private AudioClip basicAttackSFX;

    [Tooltip("Sound for magic attacks.")]
    [SerializeField] private AudioClip magicAttackSFX;

    [Tooltip("Sound for skill attacks.")]
    [SerializeField] private AudioClip skillAttackSFX;

    [Tooltip("Sound when an attack misses.")]
    [SerializeField] private AudioClip missSFX;

    [Tooltip("Sound for healing effects.")]
    [SerializeField] private AudioClip healSFX;

    [Header("Damage Number (Optional)")]
    [Tooltip("Prefab for floating damage number text. Spawns at the target plate.")]
    [SerializeField] private GameObject damageNumberPrefab;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private bool _animating = false;

    /// <summary>True while an animation is playing. Combat should wait.</summary>
    public bool IsAnimating => _animating;

    // ═════════════════════════════════════════════
    //  Initialization
    // ═════════════════════════════════════════════

    private void Awake()
    {
        if (slashImage != null)
            slashImage.gameObject.SetActive(false);

        if (hitFlashImage != null)
        {
            Color c = hitFlashImage.color;
            c.a = 0f;
            hitFlashImage.color = c;
        }
    }

    // ═════════════════════════════════════════════
    //  Public API
    // ═════════════════════════════════════════════

    /// <summary>
    /// Play a basic physical attack animation on a target plate.
    /// </summary>
    public void PlayBasicAttack(RectTransform targetPosition, bool hit, Action onComplete)
    {
        StartCoroutine(BasicAttackRoutine(targetPosition, hit, onComplete));
    }

    /// <summary>
    /// Play a magic attack animation on a target plate.
    /// </summary>
    public void PlayMagicAttack(RectTransform targetPosition, bool hit, Action onComplete)
    {
        StartCoroutine(MagicAttackRoutine(targetPosition, hit, onComplete));
    }

    /// <summary>
    /// Play a skill attack animation on a target plate.
    /// </summary>
    public void PlaySkillAttack(RectTransform targetPosition, bool hit, Action onComplete)
    {
        StartCoroutine(SkillAttackRoutine(targetPosition, hit, onComplete));
    }

    /// <summary>
    /// Play a heal animation on a target plate.
    /// </summary>
    public void PlayHealEffect(RectTransform targetPosition, Action onComplete)
    {
        StartCoroutine(HealRoutine(targetPosition, onComplete));
    }

    /// <summary>
    /// Show a floating damage number at a plate position.
    /// </summary>
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
            yield return StartCoroutine(FlashScreen(physicalHitFlash));
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
            yield return StartCoroutine(FlashScreen(magicHitFlash));
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
            yield return StartCoroutine(FlashScreen(physicalHitFlash));
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
        yield return StartCoroutine(FlashScreen(healFlash));

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

    private IEnumerator FlashScreen(Color flashColor)
    {
        if (hitFlashImage == null) yield break;

        hitFlashImage.color = flashColor;

        float elapsed = 0f;
        while (elapsed < flashFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(flashColor.a, 0f, elapsed / flashFadeDuration);
            Color c = hitFlashImage.color;
            c.a = alpha;
            hitFlashImage.color = c;
            yield return null;
        }

        Color final_c = hitFlashImage.color;
        final_c.a = 0f;
        hitFlashImage.color = final_c;
    }

    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }
}
