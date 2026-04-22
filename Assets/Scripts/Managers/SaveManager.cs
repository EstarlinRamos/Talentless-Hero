using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SFB;

/// <summary>
/// Save system for Talentless Hero. Handles manual saves, auto-saves,
/// new game, continue, and load.
///
/// Save File Naming:
///   Player saves:  TH_Save_01_2026-04-20_153022.json   → "Save 01 — Apr 20, 3:30 PM"
///   Auto-saves:    TH_Auto_2026-04-20_153022.json       → "Auto Save — Apr 20, 3:30 PM"
///
/// Auto-saves are visually distinct from player saves. The meta field
/// isAutoSave is stored in the JSON so the UI can label them differently.
///
/// Auto-saves do NOT trigger during combat — the timer pauses and
/// resumes when combat ends.
///
/// SETUP:
///   1. Create a root-level GameObject called "SaveManager"
///   2. Attach this script
///   3. Wire from MainStartMenuUI and SettingsMenuUI
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveFolder = "Saves";
    [SerializeField] private string fileExtension = ".json";

    [Header("Auto-Save Settings")]
    [SerializeField] private bool autoSaveEnabled = true;

    [Tooltip("Seconds between auto-saves (default: 5 minutes).")]
    [SerializeField] private float autoSaveInterval = 300f;

    [Tooltip("Maximum auto-save files kept. Oldest are deleted.")]
    [SerializeField] private int maxAutoSaves = 3;

    private string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, saveFolder);
    private float _autoSaveTimer;
    private int _nextSaveNumber;
    private GameSaveData _pendingLoadData;

    // ─────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!Directory.Exists(SaveDirectoryPath))
            Directory.CreateDirectory(SaveDirectoryPath);

        _nextSaveNumber = GetNextSaveNumber();
    }

    private void Update()
    {
        if (!autoSaveEnabled) return;

        // Pause auto-save during combat, dialogue, and menus
        if (CombatUIManager.IsInCombat) return;
        if (DialogueManager.IsInDialogue) return;

        _autoSaveTimer += Time.deltaTime;
        if (_autoSaveTimer >= autoSaveInterval)
        {
            _autoSaveTimer = 0f;
            AutoSave();
        }
    }

    // ═════════════════════════════════════════════
    //  Public API
    // ═════════════════════════════════════════════

    /// <summary>
    /// Start a new game. Resets all systems and loads the starting scene.
    /// </summary>
    public void NewGame()
    {
        Debug.Log("[SaveManager] Starting new game...");
        _nextSaveNumber = GetNextSaveNumber();

        if (EXPRewardSystem.Instance != null)
            EXPRewardSystem.Instance.ResetForNewGame();
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ResetForNewGame();
        if (WorldFlagManager.Instance != null)
            WorldFlagManager.Instance.ResetForNewGame();

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        var playerStats = playerObj != null ? playerObj.GetComponent<PlayerStats>() : null;
        if (playerStats != null)
            playerStats.ResetForNewGame();

        SceneManager.LoadScene("Game_Start");
    }

    /// <summary>
    /// Manual save. Creates a numbered save file with timestamp.
    /// </summary>
    public void SaveGame()
    {
        GameSaveData data = CaptureCurrentState();

        _nextSaveNumber++;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string fileName = $"TH_Save_{_nextSaveNumber:D2}_{timestamp}{fileExtension}";
        string filePath = Path.Combine(SaveDirectoryPath, fileName);

        data.saveName = $"Save {_nextSaveNumber:D2}";
        data.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.saveNumber = _nextSaveNumber;
        data.isAutoSave = false;

        WriteToFile(filePath, data);
        Debug.Log($"[SaveManager] Game saved: {fileName}");
    }

    /// <summary>
    /// Opens the Windows file explorer so the player can pick a save file.
    /// Starts in the save directory. Filters to .json files.
    /// </summary>
    public void LoadGame()
    {
        // Ensure save directory exists before opening the browser
        if (!Directory.Exists(SaveDirectoryPath))
        {
            Directory.CreateDirectory(SaveDirectoryPath);
            Debug.Log("[SaveManager] No save directory found. Created one.");
        }

        try
        {
            var extensions = new[]
            {
                new ExtensionFilter("Save Files", fileExtension.TrimStart('.')),
                new ExtensionFilter("All Files", "*")
            };

            string[] paths = StandaloneFileBrowser.OpenFilePanel(
                "Load Save File",
                SaveDirectoryPath,
                extensions,
                false
            );

            if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
            {
                Debug.Log("[SaveManager] Load cancelled by player.");
                return;
            }

            // Resume time in case settings menu paused it
            Time.timeScale = 1f;

            Debug.Log($"[SaveManager] Loading: {Path.GetFileName(paths[0])}");
            LoadFromFile(paths[0]);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] File browser failed: {e.Message}");

            // Fallback — try loading the most recent save instead
            Debug.Log("[SaveManager] Falling back to most recent save...");
            Continue();
        }
    }

    /// <summary>
    /// Load the most recent save file (auto or manual).
    /// </summary>
    public void Continue()
    {
        string latestFile = GetMostRecentSave();

        if (latestFile == null)
        {
            Debug.LogWarning("[SaveManager] No save files found.");
            return;
        }

        Debug.Log($"[SaveManager] Continuing from: {Path.GetFileName(latestFile)}");
        LoadFromFile(latestFile);
    }

    /// <summary>
    /// Load a specific save file by index from GetAllSaves().
    /// </summary>
    public void LoadGameByIndex(int index)
    {
        var saves = GetAllSaves();
        if (index < 0 || index >= saves.Count)
        {
            Debug.LogWarning($"[SaveManager] Invalid save index: {index}");
            return;
        }

        LoadFromFile(saves[index].filePath);
    }

    /// <summary>
    /// Returns true if any save files exist.
    /// </summary>
    public bool HasAnySaves()
    {
        return Directory.Exists(SaveDirectoryPath) &&
               Directory.GetFiles(SaveDirectoryPath, $"*{fileExtension}").Length > 0;
    }

    /// <summary>
    /// Get all save files sorted by most recent, with metadata.
    /// Used by a load game UI to display save slots.
    /// </summary>
    public List<SaveFileInfo> GetAllSaves()
    {
        var result = new List<SaveFileInfo>();

        if (!Directory.Exists(SaveDirectoryPath))
            return result;

        var files = Directory.GetFiles(SaveDirectoryPath, $"*{fileExtension}")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .ToArray();

        foreach (var filePath in files)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

                if (data != null)
                {
                    result.Add(new SaveFileInfo
                    {
                        filePath = filePath,
                        displayName = data.isAutoSave
                            ? $"Auto Save — {data.dateTime}"
                            : $"{data.saveName} — {data.dateTime}",
                        dateTime = data.dateTime,
                        isAutoSave = data.isAutoSave,
                        saveNumber = data.saveNumber
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not read save file: {Path.GetFileName(filePath)} — {e.Message}");
            }
        }

        return result;
    }

    // ─────────────────────────────────────────────
    //  Auto-Save
    // ─────────────────────────────────────────────

    private void AutoSave()
    {
        // Don't auto-save during combat
        if (CombatUIManager.IsInCombat) return;

        GameSaveData data = CaptureCurrentState();

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string fileName = $"TH_Auto_{timestamp}{fileExtension}";
        string filePath = Path.Combine(SaveDirectoryPath, fileName);

        data.saveName = "Auto Save";
        data.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.saveNumber = -1;
        data.isAutoSave = true;

        WriteToFile(filePath, data);
        Debug.Log($"[SaveManager] Auto-saved: {fileName}");

        CleanupOldAutoSaves();
    }

    private void CleanupOldAutoSaves()
    {
        var autoSaves = Directory.GetFiles(SaveDirectoryPath, $"TH_Auto_*{fileExtension}")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .ToArray();

        for (int i = maxAutoSaves; i < autoSaves.Length; i++)
        {
            File.Delete(autoSaves[i]);
            Debug.Log($"[SaveManager] Deleted old auto-save: {Path.GetFileName(autoSaves[i])}");
        }
    }

    // ─────────────────────────────────────────────
    //  Core Read / Write
    // ─────────────────────────────────────────────

    private void WriteToFile(string filePath, GameSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to save: {e.Message}");
        }
    }

    private void LoadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[SaveManager] File not found: {filePath}");
                return;
            }

            string json = File.ReadAllText(filePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

            if (data == null)
            {
                Debug.LogError("[SaveManager] Failed to parse save data.");
                return;
            }

            Debug.Log($"[SaveManager] Loaded: {data.saveName} from {data.dateTime}");
            ApplyLoadedState(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to load: {e.Message}");
        }
    }

    // ═════════════════════════════════════════════
    //  State Capture — Each module is independent
    // ═════════════════════════════════════════════

    private GameSaveData CaptureCurrentState()
    {
        GameSaveData data = new GameSaveData();

        data.position = CapturePosition();
        data.stats = CaptureStats();
        data.exp = CaptureEXP();
        data.inventory = CaptureInventory();
        data.quests = CaptureQuests();
        data.npcs = CaptureNPCs();
        data.worldFlags = CaptureWorldFlags();

        return data;
    }

    private PlayerPositionData CapturePosition()
    {
        PlayerPositionData pos = new PlayerPositionData();
        pos.sceneName = SceneManager.GetActiveScene().name;

        // Save current zone for camera boundary restoration on load
        if (AudioManager.Instance != null)
            pos.zoneName = AudioManager.Instance.CurrentZone;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            pos.x = player.transform.position.x;
            pos.y = player.transform.position.y;
            pos.z = player.transform.position.z;
        }
        return pos;
    }

    private PlayerStatsData CaptureStats()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerStats ps = player != null ? player.GetComponent<PlayerStats>() : null;
        return ps != null ? ps.ToSaveData() : new PlayerStatsData();
    }

    private EXPSaveData CaptureEXP()
    {
        return EXPRewardSystem.Instance != null
            ? EXPRewardSystem.Instance.CaptureForSave()
            : new EXPSaveData();
    }

    private InventoryData CaptureInventory()
    {
        return InventoryManager.Instance != null
            ? InventoryManager.Instance.CaptureForSave()
            : new InventoryData();
    }

    private QuestSaveData CaptureQuests() => new QuestSaveData();
    private NPCSaveData CaptureNPCs() => new NPCSaveData();
    private WorldFlagData CaptureWorldFlags()
    {
        return WorldFlagManager.Instance != null
            ? WorldFlagManager.Instance.CaptureForSave()
            : new WorldFlagData();
    }

    // ═════════════════════════════════════════════
    //  State Apply — Load order matters:
    //    1. EXP (sets authoritative level)
    //    2. Stats (reads level for HP/MP)
    //    3. Everything else
    // ═════════════════════════════════════════════

    private void ApplyLoadedState(GameSaveData data)
    {
        string targetScene = data.position?.sceneName ?? SceneManager.GetActiveScene().name;
        _pendingLoadData = data;

        // Apply world flags BEFORE loading the scene.
        // EnemyStats.Awake checks flags to stay dead, so flags must
        // be set before the scene's GameObjects initialize.
        if (data.worldFlags != null && WorldFlagManager.Instance != null)
            WorldFlagManager.Instance.LoadFromSave(data.worldFlags);

        SceneManager.sceneLoaded += OnSceneLoadedFromSave;
        SceneManager.LoadScene(targetScene);
    }

    private void OnSceneLoadedFromSave(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedFromSave;

        if (_pendingLoadData == null) return;
        GameSaveData data = _pendingLoadData;
        _pendingLoadData = null;

        ApplyEXP(data.exp);
        ApplyPosition(data.position);
        ApplyStats(data.stats);
        ApplyInventory(data.inventory);

        Debug.Log("[SaveManager] All modules applied.");
    }

    private void ApplyPosition(PlayerPositionData pos)
    {
        if (pos == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.transform.position = new Vector3(pos.x, pos.y, pos.z);
            Physics2D.SyncTransforms();
        }

        // Restore camera boundary for the saved zone
        if (!string.IsNullOrEmpty(pos.zoneName))
        {
            if (ZoneBoundaryLookup.Instance != null)
                ZoneBoundaryLookup.Instance.ApplyZoneBoundary(pos.zoneName);

            if (AudioManager.Instance != null)
                AudioManager.Instance.EnterZone(pos.zoneName);
        }
    }

    private void ApplyStats(PlayerStatsData stats)
    {
        if (stats == null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerStats ps = player != null ? player.GetComponent<PlayerStats>() : null;
        if (ps != null)
            ps.LoadSaveData(stats);
    }

    private void ApplyEXP(EXPSaveData exp)
    {
        if (exp == null) return;
        if (EXPRewardSystem.Instance != null)
            EXPRewardSystem.Instance.LoadFromSave(exp);
    }

    private void ApplyInventory(InventoryData inv)
    {
        if (inv == null) return;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.LoadFromSave(inv);
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private string GetMostRecentSave()
    {
        if (!Directory.Exists(SaveDirectoryPath)) return null;

        return Directory.GetFiles(SaveDirectoryPath, $"*{fileExtension}")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .FirstOrDefault();
    }

    private int GetNextSaveNumber()
    {
        if (!Directory.Exists(SaveDirectoryPath)) return 0;

        var files = Directory.GetFiles(SaveDirectoryPath, $"TH_Save_*{fileExtension}");
        if (files.Length == 0) return 0;

        int maxNumber = 0;
        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string[] parts = name.Split('_');
            // TH_Save_01_timestamp → parts[2] = "01"
            if (parts.Length >= 3 && int.TryParse(parts[2], out int num))
                maxNumber = Mathf.Max(maxNumber, num);
        }
        return maxNumber;
    }

    // ─────────────────────────────────────────────
    //  Save File Info (for load UI)
    // ─────────────────────────────────────────────

    [Serializable]
    public class SaveFileInfo
    {
        public string filePath;
        public string displayName;
        public string dateTime;
        public bool isAutoSave;
        public int saveNumber;
    }
}
