using UnityEngine;
using System.Collections;

/// <summary>
/// Audio manager for Talentless Hero.
/// Handles background music (zone-based and combat), SFX playback,
/// and track switching with crossfade.
///
/// Music Tracks:
///   - Zone-based exploration music (driven by ZoneMusicConfig)
///   - Battle (Mob): Generic combat music for normal enemy encounters
///   - Battle (Boss): Unique track for boss encounters
///
/// Zone Music:
///   When the player enters a new zone (via MapTransition triggers),
///   EnterZone() looks up the zone's clip in ZoneMusicConfig and crossfades.
///   If a zone has no entry, the current music keeps playing.
///
/// SFX:
///   Played via a separate AudioSource so they don't interrupt music.
///   Any system can call PlaySFX() for one-shot sound effects.
///
/// SETUP:
///   1. Attach to a persistent ROOT-LEVEL GameObject in the MainMenu scene
///   2. Add TWO AudioSource components to the same GameObject
///   3. Assign one as musicSource, the other as sfxSource
///   4. Create a ZoneMusicConfig asset (Create → Talentless Hero → Zone Music Config)
///      and assign it to the zoneMusicConfig slot
///   5. Assign battle music clips
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ─────────────────────────────────────────────
    //  Audio Sources
    // ─────────────────────────────────────────────

    [Header("Audio Sources")]
    [Tooltip("AudioSource dedicated to background music.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("AudioSource dedicated to sound effects. " +
             "Add a second AudioSource component to this GameObject.")]
    [SerializeField] private AudioSource sfxSource;

    // ─────────────────────────────────────────────
    //  Zone Music
    // ─────────────────────────────────────────────

    [Header("Zone Music")]
    [Tooltip("ScriptableObject mapping zone names to exploration tracks. " +
             "Create via: Create → Talentless Hero → Zone Music Config")]
    [SerializeField] private ZoneMusicConfig zoneMusicConfig;

    [Tooltip("Default exploration track if no ZoneMusicConfig is assigned " +
             "or the current zone has no entry.")]
    [SerializeField] private AudioClip overworldMusic;

    // ─────────────────────────────────────────────
    //  Battle Music
    // ─────────────────────────────────────────────

    [Header("Battle Music")]
    [Tooltip("Generic battle music for normal mob encounters.")]
    [SerializeField] private AudioClip mobBattleMusic;

    [Tooltip("Unique battle music for boss encounters.")]
    [SerializeField] private AudioClip bossBattleMusic;

    // ─────────────────────────────────────────────
    //  Volume Settings
    // ─────────────────────────────────────────────

    [Header("Crossfade")]
    [Tooltip("Duration of the crossfade between tracks in seconds.")]
    [SerializeField] private float crossfadeDuration = 0.5f;

    [Tooltip("Volume when music is fully faded in (0-1).")]
    [SerializeField] private float musicVolume = 1.0f;

    [Header("SFX")]
    [Tooltip("Master volume for all sound effects (0-1).")]
    [SerializeField] private float sfxVolume = 1.0f;

    // ─────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────

    private const string MUSIC_MUTED_KEY = "MusicMuted";
    private const string SFX_MUTED_KEY = "SFXMuted";

    private float _overworldResumeTime = 0f;
    private AudioClip _savedOverworldClip;
    private string _currentZone = "";
    private bool _isFading = false;
    private MusicTrackType _currentTrack = MusicTrackType.Overworld;

    public enum MusicTrackType
    {
        Overworld,
        MobBattle,
        BossBattle
    }

    // ─────────────────────────────────────────────
    //  Public Properties
    // ─────────────────────────────────────────────

    public bool IsMusicMuted => musicSource != null && musicSource.mute;
    public bool IsSFXMuted => sfxSource != null && sfxSource.mute;
    public MusicTrackType CurrentTrack => _currentTrack;
    public string CurrentZone => _currentZone;

    // ═════════════════════════════════════════════
    //  Lifecycle
    // ═════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource != null)
            musicSource.mute = PlayerPrefs.GetInt(MUSIC_MUTED_KEY, 0) == 1;

        if (sfxSource != null)
            sfxSource.mute = PlayerPrefs.GetInt(SFX_MUTED_KEY, 0) == 1;
    }

    // ═════════════════════════════════════════════
    //  ZONE MUSIC — Called by MapTransition triggers
    // ═════════════════════════════════════════════

    /// <summary>
    /// Called when the player enters a new zone. Looks up the zone's
    /// exploration track in ZoneMusicConfig and crossfades to it.
    /// If the zone has no entry, the current music keeps playing.
    /// If the zone's clip is null, music goes silent.
    /// </summary>
    public void EnterZone(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return;

        // Don't restart the same zone's music
        if (zoneName == _currentZone) return;

        _currentZone = zoneName;

        // Only switch music if we're not in combat
        if (_currentTrack != MusicTrackType.Overworld)
        {
            Debug.Log($"[Audio] Entered zone {zoneName} during combat — music will change after battle.");
            return;
        }

        AudioClip zoneClip = null;
        float zoneVolume = 0f;

        if (zoneMusicConfig != null)
        {
            var entry = zoneMusicConfig.GetZoneEntry(zoneName);
            if (entry != null)
            {
                zoneClip = entry.explorationClip;
                zoneVolume = entry.volumeOverride;
            }
            else
            {
                // No entry for this zone — keep current music playing
                Debug.Log($"[Audio] Zone {zoneName} has no music entry. Keeping current track.");
                return;
            }
        }

        if (zoneClip == null)
        {
            // Zone entry exists but clip is null — silence
            Debug.Log($"[Audio] Zone {zoneName} has no clip. Fading to silence.");
            FadeToSilence();
            return;
        }

        // Don't restart if it's already the same clip
        if (musicSource.clip == zoneClip && musicSource.isPlaying)
        {
            Debug.Log($"[Audio] Zone {zoneName} already playing.");
            return;
        }

        // Apply volume override if set, otherwise use default
        if (zoneVolume > 0f)
            musicVolume = zoneVolume;

        Debug.Log($"[Audio] Entering zone {zoneName} — playing: {zoneClip.name}");
        _savedOverworldClip = zoneClip;
        _overworldResumeTime = 0f;
        CrossfadeTo(zoneClip, true);
    }

    // ═════════════════════════════════════════════
    //  SFX — Public API
    // ═════════════════════════════════════════════

    /// <summary>
    /// Play a one-shot sound effect. Does not interrupt other SFX or music.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip, volumeScale * sfxVolume);
    }

    /// <summary>
    /// Play a sound effect at a specific world position (3D falloff).
    /// </summary>
    public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, volumeScale * sfxVolume);
    }

    // ═════════════════════════════════════════════
    //  Mute / Unmute
    // ═════════════════════════════════════════════

    public void ToggleMusic()
    {
        if (musicSource == null) return;

        musicSource.mute = !musicSource.mute;
        PlayerPrefs.SetInt(MUSIC_MUTED_KEY, musicSource.mute ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetMusicMuted(bool muted)
    {
        if (musicSource == null) return;

        musicSource.mute = muted;
        PlayerPrefs.SetInt(MUSIC_MUTED_KEY, muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ToggleSFX()
    {
        if (sfxSource == null) return;

        sfxSource.mute = !sfxSource.mute;
        PlayerPrefs.SetInt(SFX_MUTED_KEY, sfxSource.mute ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetSFXMuted(bool muted)
    {
        if (sfxSource == null) return;

        sfxSource.mute = muted;
        PlayerPrefs.SetInt(SFX_MUTED_KEY, muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null && !_isFading)
            musicSource.volume = musicVolume;
    }

    // ═════════════════════════════════════════════
    //  Battle Music — Public API
    // ═════════════════════════════════════════════

    /// <summary>
    /// Start battle music. Saves overworld track position for resumption.
    /// </summary>
    public void PlayBattleMusic(bool isBossEncounter)
    {
        if (musicSource == null) return;

        SaveOverworldState();

        AudioClip battleClip = isBossEncounter ? bossBattleMusic : mobBattleMusic;
        MusicTrackType trackType = isBossEncounter ? MusicTrackType.BossBattle : MusicTrackType.MobBattle;

        if (battleClip == null)
        {
            Debug.LogWarning($"[Audio] No {trackType} music clip assigned!");
            return;
        }

        _currentTrack = trackType;
        Debug.Log($"[Audio] Playing {trackType} music: {battleClip.name}");
        CrossfadeTo(battleClip, true);
    }

    /// <summary>
    /// Stop battle music and resume the overworld/zone track from where it left off.
    /// If the player changed zones during combat, plays the new zone's music instead.
    /// </summary>
    public void StopBattleMusic()
    {
        if (musicSource == null) return;
        if (_currentTrack == MusicTrackType.Overworld) return;

        _currentTrack = MusicTrackType.Overworld;

        // Check if the player moved to a new zone during combat
        AudioClip resumeClip = null;
        if (!string.IsNullOrEmpty(_currentZone) && zoneMusicConfig != null)
        {
            resumeClip = zoneMusicConfig.GetClipForZone(_currentZone);
        }

        // Fall back to saved overworld clip, then default
        if (resumeClip == null)
            resumeClip = _savedOverworldClip != null ? _savedOverworldClip : overworldMusic;

        if (resumeClip == null)
        {
            Debug.LogWarning("[Audio] No overworld music to resume!");
            musicSource.Stop();
            return;
        }

        // If the zone changed during combat, start fresh; otherwise resume position
        float resumeTime = (resumeClip == _savedOverworldClip) ? _overworldResumeTime : 0f;

        Debug.Log($"[Audio] Resuming exploration music at {resumeTime:F1}s");
        CrossfadeTo(resumeClip, true, resumeTime);
    }

    /// <summary>
    /// Play a specific overworld track directly. Resets resume position.
    /// Used by CutsceneDirector after teleportation.
    /// </summary>
    public void PlayOverworldMusic(AudioClip clip = null)
    {
        if (musicSource == null) return;

        AudioClip targetClip = clip;

        // If no clip specified, try the current zone's music
        if (targetClip == null && !string.IsNullOrEmpty(_currentZone) && zoneMusicConfig != null)
            targetClip = zoneMusicConfig.GetClipForZone(_currentZone);

        // Final fallback to default overworld
        if (targetClip == null)
            targetClip = overworldMusic;

        if (targetClip == null) return;

        _currentTrack = MusicTrackType.Overworld;
        _overworldResumeTime = 0f;
        _savedOverworldClip = targetClip;

        CrossfadeTo(targetClip, true);
    }

    // ═════════════════════════════════════════════
    //  Overworld State Save / Restore
    // ═════════════════════════════════════════════

    private void SaveOverworldState()
    {
        if (musicSource == null) return;

        if (_currentTrack == MusicTrackType.Overworld && musicSource.isPlaying)
        {
            _overworldResumeTime = musicSource.time;
            _savedOverworldClip = musicSource.clip;
            Debug.Log($"[Audio] Saved overworld state: {musicSource.clip?.name} at {_overworldResumeTime:F1}s");
        }
    }

    // ═════════════════════════════════════════════
    //  Crossfade
    // ═════════════════════════════════════════════

    private void CrossfadeTo(AudioClip newClip, bool loop, float resumeTime = 0f)
    {
        if (_isFading)
            StopAllCoroutines();

        StartCoroutine(CrossfadeCoroutine(newClip, loop, resumeTime));
    }

    /// <summary>
    /// Fade current music to silence without switching to a new clip.
    /// </summary>
    private void FadeToSilence()
    {
        if (_isFading)
            StopAllCoroutines();

        StartCoroutine(FadeToSilenceCoroutine());
    }

    private IEnumerator FadeToSilenceCoroutine()
    {
        _isFading = true;

        if (musicSource.isPlaying && crossfadeDuration > 0f)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / crossfadeDuration);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.volume = musicVolume;
        _isFading = false;
    }

    private IEnumerator CrossfadeCoroutine(AudioClip newClip, bool loop, float resumeTime)
    {
        _isFading = true;

        float halfFade = crossfadeDuration * 0.5f;

        // Fade out current track
        if (musicSource.isPlaying && halfFade > 0f)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < halfFade)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfFade);
                yield return null;
            }
        }

        musicSource.volume = 0f;

        // Switch clip
        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.loop = loop;
        musicSource.time = Mathf.Clamp(resumeTime, 0f, newClip.length - 0.1f);
        musicSource.Play();

        // Fade in new track
        if (halfFade > 0f)
        {
            float elapsed = 0f;

            while (elapsed < halfFade)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0f, musicVolume, elapsed / halfFade);
                yield return null;
            }
        }

        musicSource.volume = musicVolume;
        _isFading = false;
    }
}
