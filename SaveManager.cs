using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
// Requires UnityStandaloneFileBrowser: https://github.com/gkngkc/UnityStandaloneFileBrowser
using SFB;

/// <summary>
/// Singleton save/load manager with auto-save support.
/// Save format: TH_Save_##_yyyy-MM-dd_HHmmss.json in Application.persistentDataPath.
/// Each module (stats, EXP, inventory, etc.) captures and restores independently
/// with null checks, so old saves remain compatible as new systems are added.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveFolder = "Saves";
    [SerializeField] private string savePrefix = "TH_Save";
    [SerializeField] private string fileExtension = ".json";

    [Header("Auto-Save Settings")]
    [SerializeField] private bool autoSaveEnabled = true;
    [SerializeField] private float autoSaveInterval = 300f;
    [SerializeField] private int maxAutoSaves = 3;

    private string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, saveFolder);
    private float autoSaveTimer;
    private int currentSaveNumber = 0;
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

        currentSaveNumber = GetNextSaveNumber();
    }

    private void Update()
    {
        if (!autoSaveEnabled) return;

        autoSaveTimer += Time.deltaTime;
        if (autoSaveTimer >= autoSaveInterval)
        {
            autoSaveTimer = 0f;
            AutoSave();
        }
    }

    // ═════════════════════════════════════════════
    //  Public API
    // ═════════════════════════════════════════════

    public void NewGame()
    {
        Debug.Log("[SaveManager] Starting new game...");
        currentSaveNumber = GetNextSaveNumber();

        if (EXPRewardSystem.Instance != null)
            EXPRewardSystem.Instance.ResetForNewGame();
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ResetForNewGame();

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        var playerStats = playerObj != null ? playerObj.GetComponent<PlayerStats>() : null;
        if (playerStats != null)
            playerStats.ResetForNewGame();

        SceneManager.LoadScene("Game_Start");
    }

    public void SaveGame()
    {
        GameSaveData data = CaptureCurrentState();

        currentSaveNumber++;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string fileName = $"{savePrefix}_{currentSaveNumber:D2}_{timestamp}{fileExtension}";
        string filePath = Path.Combine(SaveDirectoryPath, fileName);

        data.saveName = fileName;
        data.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.saveNumber = currentSaveNumber;

        WriteToFile(filePath, data);
        Debug.Log($"[SaveManager] Game saved: {fileName}");
    }

    public void LoadGame()
    {
        var extensions = new[] {
            new ExtensionFilter("Save Files", fileExtension.TrimStart('.')),
            new ExtensionFilter("All Files", "*")
        };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
            "Load Save File",
            SaveDirectoryPath,
            extensions,
            false
        );

        if (paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
        {
            Debug.Log("[SaveManager] Load cancelled by user.");
            return;
        }

        LoadFromFile(paths[0]);
    }

    /// <summary>
    /// Load the most recent save file without opening a file browser.
    /// </summary>
    public void Continue()
    {
        string latestFile = GetMostRecentSave();

        if (latestFile == null)
        {
            Debug.LogWarning("[SaveManager] No save files found to continue.");
            return;
        }

        Debug.Log($"[SaveManager] Continuing from: {Path.GetFileName(latestFile)}");
        LoadFromFile(latestFile);
    }

    public bool HasAnySaves()
    {
        return Directory.Exists(SaveDirectoryPath) &&
               Directory.GetFiles(SaveDirectoryPath, $"*{fileExtension}").Length > 0;
    }

    // ─────────────────────────────────────────────
    //  Auto-Save
    // ─────────────────────────────────────────────

    private void AutoSave()
    {
        GameSaveData data = CaptureCurrentState();

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string fileName = $"TH_AutoSave_{timestamp}{fileExtension}";
        string filePath = Path.Combine(SaveDirectoryPath, fileName);

        data.saveName = fileName;
        data.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.saveNumber = -1;

        WriteToFile(filePath, data);
        Debug.Log($"[SaveManager] Auto-saved: {fileName}");

        CleanupOldAutoSaves();
    }

    private void CleanupOldAutoSaves()
    {
        var autoSaves = Directory.GetFiles(SaveDirectoryPath, $"TH_AutoSave_*{fileExtension}")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .ToArray();

        for (int i = maxAutoSaves; i < autoSaves.Length; i++)
        {
            File.Delete(autoSaves[i]);
            Debug.Log($"[SaveManager] Deleted old auto-save: {Path.GetFileName(autoSaves[i])}");
        }
    }

    // ─────────────────────────────────────────────
    //  File I/O
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
                Debug.LogError("[SaveManager] Failed to parse save data. File may be corrupted.");
                return;
            }

            Debug.Log($"[SaveManager] Loaded save: {data.saveName} from {data.dateTime}");
            ApplyLoadedState(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] Failed to load: {e.Message}");
        }
    }

    // ═════════════════════════════════════════════
    //  State Capture
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

        if (ps != null)
            return ps.ToSaveData();

        return new PlayerStatsData();
    }

    private EXPSaveData CaptureEXP()
    {
        if (EXPRewardSystem.Instance != null)
            return EXPRewardSystem.Instance.CaptureForSave();

        return new EXPSaveData();
    }

    private InventoryData CaptureInventory()
    {
        if (InventoryManager.Instance != null)
            return InventoryManager.Instance.CaptureForSave();

        return new InventoryData();
    }

    private QuestSaveData CaptureQuests()
    {
        return new QuestSaveData();
    }

    private NPCSaveData CaptureNPCs()
    {
        return new NPCSaveData();
    }

    private WorldFlagData CaptureWorldFlags()
    {
        return new WorldFlagData();
    }

    // ═════════════════════════════════════════════
    //  State Apply
    //  Load order: EXP first (sets authoritative level),
    //  then Stats (uses level for HP/MP), then everything else.
    // ═════════════════════════════════════════════

    private void ApplyLoadedState(GameSaveData data)
    {
        string targetScene = data.position?.sceneName ?? SceneManager.GetActiveScene().name;
        _pendingLoadData = data;

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
        ApplyQuests(data.quests);
        ApplyNPCs(data.npcs);
        ApplyWorldFlags(data.worldFlags);

        Debug.Log("[SaveManager] All modules applied successfully.");
    }

    private void ApplyPosition(PlayerPositionData pos)
    {
        if (pos == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.transform.position = new Vector3(pos.x, pos.y, pos.z);
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

    private void ApplyQuests(QuestSaveData quests)
    {
        if (quests == null) return;
        // Wire when quest system is built.
    }

    private void ApplyNPCs(NPCSaveData npcs)
    {
        if (npcs == null) return;
        // Wire when NPC state system is built.
    }

    private void ApplyWorldFlags(WorldFlagData flags)
    {
        if (flags == null) return;
        // Wire when world flag system is built.
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

        var files = Directory.GetFiles(SaveDirectoryPath, $"{savePrefix}_*{fileExtension}");
        if (files.Length == 0) return 0;

        int maxNumber = 0;
        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string[] parts = name.Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int num))
                maxNumber = Mathf.Max(maxNumber, num);
        }
        return maxNumber;
    }
}
