using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Displays emote sprites with optional sound effects above a character.
///
/// Attach to any character that can show emotes (Goddess, Angel Guard, etc.).
/// Each emote entry maps a name to a sprite and an optional audio clip.
///
/// SETUP:
///   1. Attach to a character GameObject
///   2. Create a child SpriteRenderer positioned above the character's head
///      and assign it as the emoteRenderer
///   3. Add emote entries: name → sprite → optional sound
///   4. The emote renderer starts disabled
/// </summary>
public class EmoteDisplay : MonoBehaviour
{
    [Serializable]
    public class EmoteEntry
    {
        [Tooltip("Name used in DialogueLine.emote (e.g., exclamation, sweat, question).")]
        public string emoteName;

        [Tooltip("The sprite to display for this emote.")]
        public Sprite sprite;

        [Tooltip("Optional sound to play when this emote appears. " +
                 "Leave empty for a silent emote.")]
        public AudioClip sound;

        [Tooltip("Volume for the emote sound (0-1).")]
        [Range(0f, 1f)]
        public float soundVolume = 0.8f;
    }

    [Header("References")]
    [Tooltip("SpriteRenderer positioned above the character's head.")]
    [SerializeField] private SpriteRenderer emoteRenderer;

    [Header("Emote Library")]
    [Tooltip("Map of emote names to sprites and sounds.")]
    [SerializeField] private List<EmoteEntry> emotes = new List<EmoteEntry>();

    [Header("Display Settings")]
    [Tooltip("How long the emote stays visible before fading out.")]
    [SerializeField] private float displayDuration = 1.5f;

    [Tooltip("Fade out duration in seconds.")]
    [SerializeField] private float fadeDuration = 0.3f;

    [Tooltip("Slight bounce on appear (units above start position).")]
    [SerializeField] private float bounceHeight = 0.15f;

    [Tooltip("Duration of the bounce animation.")]
    [SerializeField] private float bounceDuration = 0.2f;

    private Dictionary<string, EmoteEntry> _lookup;
    private Coroutine _activeEmote;
    private Vector3 _emoteBasePos;

    private void Awake()
    {
        _lookup = new Dictionary<string, EmoteEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in emotes)
        {
            if (!string.IsNullOrEmpty(entry.emoteName) && entry.sprite != null)
                _lookup[entry.emoteName] = entry;
        }

        if (emoteRenderer != null)
        {
            _emoteBasePos = emoteRenderer.transform.localPosition;
            emoteRenderer.enabled = false;
        }
    }

    /// <summary>
    /// Show an emote by name with optional sound. Replaces any active emote.
    /// </summary>
    public void ShowEmote(string emoteName)
    {
        if (emoteRenderer == null) return;

        if (string.IsNullOrEmpty(emoteName))
        {
            HideEmote();
            return;
        }

        if (!_lookup.TryGetValue(emoteName, out EmoteEntry entry))
        {
            Debug.LogWarning($"[Emote] Unknown emote '{emoteName}' on {gameObject.name}. " +
                             "Add it to the EmoteDisplay emote list.");
            return;
        }

        if (_activeEmote != null)
            StopCoroutine(_activeEmote);

        _activeEmote = StartCoroutine(EmoteSequence(entry));
    }

    /// <summary>
    /// Immediately hide any active emote.
    /// </summary>
    public void HideEmote()
    {
        if (_activeEmote != null)
        {
            StopCoroutine(_activeEmote);
            _activeEmote = null;
        }

        if (emoteRenderer != null)
        {
            emoteRenderer.enabled = false;
            emoteRenderer.transform.localPosition = _emoteBasePos;
        }
    }

    private IEnumerator EmoteSequence(EmoteEntry entry)
    {
        emoteRenderer.sprite = entry.sprite;
        emoteRenderer.enabled = true;

        Color full = Color.white;
        emoteRenderer.color = full;

        // Play emote sound
        if (entry.sound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(entry.sound, entry.soundVolume);

        // Bounce up
        if (bounceHeight > 0f && bounceDuration > 0f)
        {
            Vector3 start = _emoteBasePos;
            Vector3 peak = _emoteBasePos + Vector3.up * bounceHeight;
            float elapsed = 0f;

            while (elapsed < bounceDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / bounceDuration;
                float curve = Mathf.Sin(t * Mathf.PI);
                emoteRenderer.transform.localPosition = Vector3.Lerp(start, peak, curve);
                yield return null;
            }
            emoteRenderer.transform.localPosition = _emoteBasePos;
        }

        // Hold
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        if (fadeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                emoteRenderer.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
        }

        emoteRenderer.enabled = false;
        emoteRenderer.color = full;
        emoteRenderer.transform.localPosition = _emoteBasePos;
        _activeEmote = null;
    }
}
