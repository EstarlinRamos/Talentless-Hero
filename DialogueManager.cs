using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Runs dialogue from DialogueData ScriptableObjects with full audio
/// and character portrait support.
///
/// Portrait system:
///   - Each speaker has a default portrait mapped in the speakerPortraits list
///   - Individual lines can override the portrait (for expression changes)
///   - Portrait displays on the right side of the dialogue panel
///   - Speaker name displays on the upper left above the panel
///   - If a speaker has no portrait, the image is hidden
///
/// Audio features:
///   - Per-speaker typing sounds (blips during typewriter effect)
///   - Per-line SFX (dramatic stings, ambient sounds)
///   - Emote events routed to characters via CutsceneDirector
///
/// Supported tags (set per-line in the DialogueData Inspector):
///   teleport       — fires OnTeleportRequested
///   end_cutscene   — fires OnCutsceneEnd
///   shake          — fires OnScreenShake
///   flash          — fires OnScreenFlash
///
/// Menu Lockout:
///   DialogueManager.IsInDialogue is a static property that other systems
///   should check to prevent opening menus during dialogue.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    /// <summary>
    /// Static flag for other systems to check. When true, menus should not open.
    /// </summary>
    public static bool IsInDialogue => Instance != null && Instance._isPlaying;

    // ─────────────────────────────────────────────
    //  UI References
    // ─────────────────────────────────────────────

    [Header("Dialogue Panel")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject continueIndicator;

    [Header("Character Portrait")]
    [Tooltip("UI Image on the right side of the dialogue panel. " +
             "Displays the current speaker's portrait.")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Parent object of the portrait image. " +
             "Disabled when the current speaker has no portrait. " +
             "Use this if you have a frame/border around the portrait.")]
    [SerializeField] private GameObject portraitContainer;

    [Header("Choices")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private List<Button> choiceButtons;

    // ─────────────────────────────────────────────
    //  Typewriter Settings
    // ─────────────────────────────────────────────

    [Header("Typewriter Settings")]
    [SerializeField] private float typewriterSpeed = 0.03f;

    // ─────────────────────────────────────────────
    //  Typing Sounds
    // ─────────────────────────────────────────────

    [Header("Typing Sounds")]
    [Tooltip("Default typing blip used when no speaker-specific sound is assigned.")]
    [SerializeField] private AudioClip defaultTypingSound;

    [Tooltip("Play a typing blip every N characters.")]
    [SerializeField] private int typingSoundInterval = 3;

    [Tooltip("Volume scale for typing blips (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float typingSoundVolume = 0.5f;

    [Tooltip("Slight random pitch variation for natural feel.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float typingPitchVariation = 0.08f;

    [Tooltip("Map speaker names to unique typing sounds.")]
    [SerializeField] private List<SpeakerTypingSound> speakerTypingSounds = new List<SpeakerTypingSound>();

    // ─────────────────────────────────────────────
    //  Speaker Portraits
    // ─────────────────────────────────────────────

    [Header("Speaker Portraits")]
    [Tooltip("Map speaker names to their default portrait sprite. " +
             "Individual lines can override this with portraitOverride.")]
    [SerializeField] private List<SpeakerPortrait> speakerPortraits = new List<SpeakerPortrait>();

    // ─────────────────────────────────────────────
    //  Serializable Mappings
    // ─────────────────────────────────────────────

    [Serializable]
    public class SpeakerTypingSound
    {
        [Tooltip("Speaker name exactly as it appears in DialogueLine.speaker")]
        public string speakerName;

        [Tooltip("Typing blip sound for this speaker.")]
        public AudioClip typingClip;
    }

    [Serializable]
    public class SpeakerPortrait
    {
        [Tooltip("Speaker name exactly as it appears in DialogueLine.speaker")]
        public string speakerName;

        [Tooltip("Default portrait for this speaker. " +
                 "Use a front-facing character image or bust.")]
        public Sprite portrait;
    }

    // ─────────────────────────────────────────────
    //  Events — CutsceneDirector listens to these
    // ─────────────────────────────────────────────
    public event Action OnDialogueStarted;
    public event Action OnDialogueEnded;
    public event Action OnTeleportRequested;
    public event Action OnScreenShake;
    public event Action OnScreenFlash;
    public event Action OnCutsceneEnd;

    /// <summary>
    /// Fired when a line has an emote. Args: (speakerName, emoteName).
    /// </summary>
    public event Action<string, string> OnEmoteRequested;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────
    private DialogueData _currentDialogue;
    private int _currentNodeIndex;
    private int _currentLineIndex;
    private bool _isPlaying = false;
    private bool _isTyping = false;
    private bool _skipRequested = false;
    private bool _waitingForInput = false;
    private string _currentSpeaker = "";
    private Coroutine _typewriterCoroutine;

    private Dictionary<string, AudioClip> _typingSoundLookup;
    private Dictionary<string, Sprite> _portraitLookup;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        // Build typing sound lookup
        _typingSoundLookup = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in speakerTypingSounds)
        {
            if (!string.IsNullOrEmpty(entry.speakerName) && entry.typingClip != null)
                _typingSoundLookup[entry.speakerName] = entry.typingClip;
        }

        // Build portrait lookup
        _portraitLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in speakerPortraits)
        {
            if (!string.IsNullOrEmpty(entry.speakerName) && entry.portrait != null)
                _portraitLookup[entry.speakerName] = entry.portrait;
        }
    }

    // ═════════════════════════════════════════════
    //  Public API
    // ═════════════════════════════════════════════

    /// <summary>
    /// Start a dialogue conversation from a DialogueData asset.
    /// </summary>
    public void StartDialogue(DialogueData dialogue)
    {
        if (dialogue == null || dialogue.nodes.Count == 0)
        {
            Debug.LogError("[Dialogue] No dialogue data or empty nodes!");
            return;
        }

        _currentDialogue = dialogue;
        _currentNodeIndex = dialogue.startNodeIndex;
        _currentLineIndex = 0;
        _isPlaying = true;
        _waitingForInput = false;
        _currentSpeaker = "";

        dialoguePanel.SetActive(true);
        HideChoices();
        HidePortrait();

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        OnDialogueStarted?.Invoke();
        PlayCurrentLine();
    }

    // ═════════════════════════════════════════════
    //  Input
    // ═════════════════════════════════════════════

    private void Update()
    {
        if (!_isPlaying) return;

        bool advancePressed = (Keyboard.current != null) &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame);

        if (!advancePressed) return;

        if (_isTyping)
        {
            _skipRequested = true;
        }
        else if (_waitingForInput)
        {
            _waitingForInput = false;
            AdvanceToNextLine();
        }
    }

    // ═════════════════════════════════════════════
    //  Line Playback
    // ═════════════════════════════════════════════

    private void PlayCurrentLine()
    {
        DialogueNode node = _currentDialogue.GetNode(_currentNodeIndex);
        if (node == null)
        {
            EndDialogue();
            return;
        }

        if (_currentLineIndex >= node.lines.Count)
        {
            HandleNodeEnd(node);
            return;
        }

        DialogueLine line = node.lines[_currentLineIndex];

        // Update speaker name (keep previous if empty)
        if (!string.IsNullOrEmpty(line.speaker))
        {
            _currentSpeaker = line.speaker;
            if (speakerNameText != null)
                speakerNameText.text = _currentSpeaker;
        }

        // Update portrait
        UpdatePortrait(line);

        // Play per-line SFX
        if (line.sfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(line.sfx, line.sfxVolume);

        // Fire emote event
        if (!string.IsNullOrEmpty(line.emote))
            OnEmoteRequested?.Invoke(_currentSpeaker, line.emote);

        // Process tags
        ProcessTags(line.tags);

        // Start typewriter
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);

        _typewriterCoroutine = StartCoroutine(TypewriteLine(line));
    }

    private IEnumerator TypewriteLine(DialogueLine line)
    {
        _isTyping = true;
        _skipRequested = false;
        _waitingForInput = false;
        dialogueText.text = "";

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        AudioClip typingClip = ResolveTypingSound(_currentSpeaker);
        int charCount = 0;

        foreach (char c in line.text)
        {
            if (_skipRequested)
            {
                dialogueText.text = line.text;
                break;
            }

            dialogueText.text += c;
            charCount++;

            if (typingClip != null && charCount % typingSoundInterval == 0 && !char.IsWhiteSpace(c))
            {
                PlayTypingBlip(typingClip);
            }

            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        dialogueText.text = line.text;
        _isTyping = false;
        _typewriterCoroutine = null;

        if (line.autoAdvanceDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(line.autoAdvanceDelay);
            AdvanceToNextLine();
        }
        else
        {
            _waitingForInput = true;

            if (continueIndicator != null)
                continueIndicator.SetActive(true);
        }
    }

    private void AdvanceToNextLine()
    {
        _currentLineIndex++;
        PlayCurrentLine();
    }

    // ═════════════════════════════════════════════
    //  Portraits
    // ═════════════════════════════════════════════

    /// <summary>
    /// Update the portrait display. Uses the line's portraitOverride if set,
    /// otherwise falls back to the speaker's default portrait.
    /// Hides the portrait entirely if no sprite is available.
    /// </summary>
    private void UpdatePortrait(DialogueLine line)
    {
        if (portraitImage == null) return;

        // Priority: line override > speaker default > hide
        Sprite portrait = line.portraitOverride;

        if (portrait == null)
        {
            // Look up the current speaker's default portrait
            if (!string.IsNullOrEmpty(_currentSpeaker))
                _portraitLookup.TryGetValue(_currentSpeaker, out portrait);
        }

        if (portrait != null)
        {
            ShowPortrait(portrait);
        }
        else
        {
            HidePortrait();
        }
    }

    private void ShowPortrait(Sprite portrait)
    {
        portraitImage.sprite = portrait;
        portraitImage.enabled = true;

        if (portraitContainer != null)
            portraitContainer.SetActive(true);
    }

    private void HidePortrait()
    {
        if (portraitImage != null)
            portraitImage.enabled = false;

        if (portraitContainer != null)
            portraitContainer.SetActive(false);
    }

    // ═════════════════════════════════════════════
    //  Typing Sounds
    // ═════════════════════════════════════════════

    private AudioClip ResolveTypingSound(string speaker)
    {
        if (!string.IsNullOrEmpty(speaker) && _typingSoundLookup.TryGetValue(speaker, out AudioClip clip))
            return clip;

        return defaultTypingSound;
    }

    private void PlayTypingBlip(AudioClip clip)
    {
        if (AudioManager.Instance == null) return;

        float pitch = 1f + UnityEngine.Random.Range(-typingPitchVariation, typingPitchVariation);
        AudioManager.Instance.PlaySFX(clip, typingSoundVolume * pitch);
    }

    // ═════════════════════════════════════════════
    //  Node Branching
    // ═════════════════════════════════════════════

    private void HandleNodeEnd(DialogueNode node)
    {
        if (node.choices != null && node.choices.Count > 0)
        {
            ShowChoices(node.choices);
        }
        else if (node.nextNodeIndex >= 0)
        {
            JumpToNode(node.nextNodeIndex);
        }
        else
        {
            EndDialogue();
        }
    }

    private void JumpToNode(int nodeIndex)
    {
        _currentNodeIndex = nodeIndex;
        _currentLineIndex = 0;
        PlayCurrentLine();
    }

    // ═════════════════════════════════════════════
    //  Choices
    // ═════════════════════════════════════════════

    private void ShowChoices(List<DialogueChoice> choices)
    {
        choicePanel.SetActive(true);

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (i < choices.Count)
            {
                choiceButtons[i].gameObject.SetActive(true);

                TextMeshProUGUI buttonText = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                buttonText.text = choices[i].choiceText;

                int targetNode = choices[i].nextNodeIndex;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() =>
                {
                    HideChoices();
                    JumpToNode(targetNode);
                });
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void HideChoices()
    {
        if (choicePanel != null)
            choicePanel.SetActive(false);
    }

    // ═════════════════════════════════════════════
    //  Tag Processing
    // ═════════════════════════════════════════════

    private void ProcessTags(List<string> tags)
    {
        if (tags == null) return;

        foreach (string tag in tags)
        {
            string trimmed = tag.Trim().ToLower();

            switch (trimmed)
            {
                case "teleport":
                    OnTeleportRequested?.Invoke();
                    break;
                case "shake":
                    OnScreenShake?.Invoke();
                    break;
                case "flash":
                    OnScreenFlash?.Invoke();
                    break;
                case "end_cutscene":
                    OnCutsceneEnd?.Invoke();
                    break;
                default:
                    Debug.Log($"[Dialogue] Unknown tag: {tag}");
                    break;
            }
        }
    }

    // ═════════════════════════════════════════════
    //  End Dialogue
    // ═════════════════════════════════════════════

    private void EndDialogue()
    {
        _isPlaying = false;
        _waitingForInput = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        HideChoices();
        HidePortrait();
        OnDialogueEnded?.Invoke();
    }
}
