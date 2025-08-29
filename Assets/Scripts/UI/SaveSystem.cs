using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using WhiskerKing.Core;
using WhiskerKing.Level;

namespace WhiskerKing.UI
{
    /// <summary>
    /// Comprehensive Save System for Whisker King
    /// Handles progress tracking, data persistence, and save file management
    /// Implements multiple save slots, backup system, and data validation
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        [System.Serializable]
        public class SaveData
        {
            public int version = 1;
            public DateTime saveTime;
            public string playerName = "Player";
            public float totalPlayTime = 0f;
            
            // Progress data
            public int currentLevel = 1;
            public int highestLevelUnlocked = 1;
            public int totalFishTreats = 0;
            public int totalYarn = 0;
            public int totalGoldenTokens = 0;
            public int totalLevelsCompleted = 0;
            
            // Level completion data
            public Dictionary<int, LevelCompletionData> levelProgress = new Dictionary<int, LevelCompletionData>();
            
            // Settings data
            public SettingsData settings = new SettingsData();
            
            // Cosmetics data
            public CosmeticsData cosmetics = new CosmeticsData();
            
            // Statistics
            public StatisticsData statistics = new StatisticsData();
        }

        [System.Serializable]
        public class LevelCompletionData
        {
            public bool isCompleted = false;
            public bool isUnlocked = false;
            public float bestTime = 0f;
            public int bestScore = 0;
            public UIManager.MedalType bestMedal = UIManager.MedalType.None;
            public int fishTreatsCollected = 0;
            public int yarnCollected = 0;
            public int tokensCollected = 0;
            public int timesPlayed = 0;
            public int timesCompleted = 0;
            public DateTime firstCompleted;
            public DateTime lastPlayed;
        }

        [System.Serializable]
        public class SettingsData
        {
            public float masterVolume = 1f;
            public float musicVolume = 0.8f;
            public float sfxVolume = 1f;
            public float voiceVolume = 1f;
            public string audioQuality = "High";
            public bool fullscreen = true;
            public string resolution = "1920x1080";
            public int qualityLevel = 2;
            public float mouseSensitivity = 1f;
            public bool invertY = false;
            public UIManager.AccessibilitySettings accessibility = new UIManager.AccessibilitySettings();
        }

        [System.Serializable]
        public class CosmeticsData
        {
            public Dictionary<string, bool> unlockedCosmetics = new Dictionary<string, bool>();
            public Dictionary<string, bool> equippedCosmetics = new Dictionary<string, bool>();
            public int yarnSpent = 0;
            public List<string> favoriteCosmetics = new List<string>();
        }

        [System.Serializable]
        public class StatisticsData
        {
            public int totalJumps = 0;
            public int totalDoubleJumps = 0;
            public int totalSlides = 0;
            public int totalTailWhips = 0;
            public int totalCratesDestroyed = 0;
            public int totalDeaths = 0;
            public int totalCheckpoints = 0;
            public float totalDistance = 0f;
            public Dictionary<string, int> crateTypesBroken = new Dictionary<string, int>();
            public Dictionary<string, int> levelAttempts = new Dictionary<string, int>();
        }

        [System.Serializable]
        public class SaveSlotInfo
        {
            public int slotIndex;
            public bool isOccupied;
            public DateTime saveTime;
            public string playerName;
            public int currentLevel;
            public float totalPlayTime;
            public int totalLevelsCompleted;
            public string previewImagePath;
        }

        // Singleton pattern
        private static SaveSystem instance;
        public static SaveSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<SaveSystem>();
                    if (instance == null)
                    {
                        GameObject saveSystemGO = new GameObject("SaveSystem");
                        instance = saveSystemGO.AddComponent<SaveSystem>();
                        DontDestroyOnLoad(saveSystemGO);
                    }
                }
                return instance;
            }
        }

        [Header("Save Configuration")]
        [SerializeField] private bool useEncryption = true;
        [SerializeField] private bool createBackups = true;
        [SerializeField] private int maxSaveSlots = 3;
        [SerializeField] private int maxBackupsPerSlot = 5;
        [SerializeField] private bool debugMode = false;

        [Header("Auto Save Settings")]
        [SerializeField] private bool autoSaveEnabled = true;
        [SerializeField] private float autoSaveInterval = 120f; // 2 minutes
        [SerializeField] private bool saveOnLevelComplete = true;
        [SerializeField] private bool saveOnCheckpoint = true;

        // File paths
        private string saveDirectory;
        private string backupDirectory;
        private const string SAVE_FILE_EXTENSION = ".sav";
        private const string BACKUP_FILE_EXTENSION = ".bak";
        private const string TEMP_FILE_EXTENSION = ".tmp";

        // Current save state
        private SaveData currentSave;
        private int currentSlot = 0;
        private bool hasUnsavedChanges = false;
        private float lastAutoSaveTime = 0f;

        // Component references
        private UIManager uiManager;
        private LevelManager levelManager;

        // Events
        public System.Action<SaveData> OnSaveLoaded;
        public System.Action<SaveData, int> OnGameSaved;
        public System.Action<int> OnSaveDeleted;
        public System.Action<string> OnSaveError;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSaveSystem();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Get component references
            uiManager = UIManager.Instance;
            levelManager = FindObjectOfType<LevelManager>();

            // Subscribe to events
            SubscribeToEvents();

            // Try to load most recent save or create new save
            LoadMostRecentSave();
        }

        private void Update()
        {
            // Handle auto-save
            if (autoSaveEnabled && hasUnsavedChanges)
            {
                if (Time.time - lastAutoSaveTime >= autoSaveInterval)
                {
                    AutoSave();
                }
            }

            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && hasUnsavedChanges)
            {
                AutoSave();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && hasUnsavedChanges)
            {
                AutoSave();
            }
        }

        #endregion

        #region Initialization

        private void InitializeSaveSystem()
        {
            // Set up save directories
            saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            backupDirectory = Path.Combine(Application.persistentDataPath, "Backups");

            // Create directories if they don't exist
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }

            if (createBackups && !Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            // Initialize current save
            currentSave = new SaveData();

            Debug.Log($"SaveSystem initialized - Save directory: {saveDirectory}");
        }

        private void SubscribeToEvents()
        {
            // Subscribe to level manager events
            if (levelManager != null)
            {
                levelManager.OnLevelCompleted += OnLevelCompleted;
                levelManager.OnCheckpointReached += OnCheckpointReached;
                levelManager.OnProgressUpdated += OnProgressUpdated;
            }

            // Subscribe to UI manager events
            if (uiManager != null)
            {
                uiManager.OnProgressionUpdated += OnProgressionUpdated;
            }
        }

        #endregion

        #region Save/Load Operations

        /// <summary>
        /// Save game data to specified slot
        /// </summary>
        public bool SaveGame(int slotIndex)
        {
            try
            {
                if (slotIndex < 0 || slotIndex >= maxSaveSlots)
                {
                    OnSaveError?.Invoke($"Invalid save slot: {slotIndex}");
                    return false;
                }

                UpdateCurrentSaveData();

                string savePath = GetSaveFilePath(slotIndex);
                string tempPath = savePath + TEMP_FILE_EXTENSION;

                // Create backup if file exists
                if (createBackups && File.Exists(savePath))
                {
                    CreateBackup(slotIndex);
                }

                // Save to temporary file first
                string jsonData = JsonUtility.ToJson(currentSave, true);
                
                if (useEncryption)
                {
                    jsonData = EncryptData(jsonData);
                }

                File.WriteAllText(tempPath, jsonData);

                // Move temp file to actual save file (atomic operation)
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }
                File.Move(tempPath, savePath);

                currentSlot = slotIndex;
                hasUnsavedChanges = false;
                lastAutoSaveTime = Time.time;

                OnGameSaved?.Invoke(currentSave, slotIndex);

                if (debugMode)
                {
                    Debug.Log($"Game saved to slot {slotIndex}");
                }

                return true;
            }
            catch (Exception e)
            {
                OnSaveError?.Invoke($"Failed to save game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load game data from specified slot
        /// </summary>
        public bool LoadGame(int slotIndex)
        {
            try
            {
                if (slotIndex < 0 || slotIndex >= maxSaveSlots)
                {
                    OnSaveError?.Invoke($"Invalid save slot: {slotIndex}");
                    return false;
                }

                string savePath = GetSaveFilePath(slotIndex);
                
                if (!File.Exists(savePath))
                {
                    OnSaveError?.Invoke($"Save file not found in slot {slotIndex}");
                    return false;
                }

                string jsonData = File.ReadAllText(savePath);
                
                if (useEncryption)
                {
                    jsonData = DecryptData(jsonData);
                }

                SaveData loadedData = JsonUtility.FromJson<SaveData>(jsonData);
                
                if (loadedData == null)
                {
                    OnSaveError?.Invoke("Failed to parse save data");
                    return false;
                }

                // Validate save data
                if (!ValidateSaveData(loadedData))
                {
                    OnSaveError?.Invoke("Save data validation failed");
                    return false;
                }

                currentSave = loadedData;
                currentSlot = slotIndex;
                hasUnsavedChanges = false;

                // Apply loaded data to game systems
                ApplySaveData();

                OnSaveLoaded?.Invoke(currentSave);

                if (debugMode)
                {
                    Debug.Log($"Game loaded from slot {slotIndex}");
                }

                return true;
            }
            catch (Exception e)
            {
                OnSaveError?.Invoke($"Failed to load game: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete save from specified slot
        /// </summary>
        public bool DeleteSave(int slotIndex)
        {
            try
            {
                if (slotIndex < 0 || slotIndex >= maxSaveSlots)
                {
                    OnSaveError?.Invoke($"Invalid save slot: {slotIndex}");
                    return false;
                }

                string savePath = GetSaveFilePath(slotIndex);
                
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }

                // Delete associated backups
                DeleteBackups(slotIndex);

                OnSaveDeleted?.Invoke(slotIndex);

                if (debugMode)
                {
                    Debug.Log($"Save deleted from slot {slotIndex}");
                }

                return true;
            }
            catch (Exception e)
            {
                OnSaveError?.Invoke($"Failed to delete save: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Auto-save current progress
        /// </summary>
        public void AutoSave()
        {
            if (currentSlot >= 0)
            {
                SaveGame(currentSlot);
            }
        }

        /// <summary>
        /// Quick save to last used slot
        /// </summary>
        public void QuickSave()
        {
            SaveGame(currentSlot);
        }

        /// <summary>
        /// Quick load from last used slot
        /// </summary>
        public void QuickLoad()
        {
            LoadGame(currentSlot);
        }

        #endregion

        #region Save Data Management

        private void UpdateCurrentSaveData()
        {
            currentSave.saveTime = DateTime.Now;
            currentSave.totalPlayTime += Time.realtimeSinceStartup;

            // Update from level manager
            if (levelManager != null)
            {
                var progress = levelManager.GetProgress();
                var levelData = levelManager.GetCurrentLevel();

                currentSave.currentLevel = levelData.levelNumber;
                currentSave.totalFishTreats = progress.collectedFishTreats;
                currentSave.totalYarn = progress.collectedYarn;
                currentSave.totalGoldenTokens = progress.collectedTokens;
            }

            // Update from UI manager
            if (uiManager != null)
            {
                var progression = uiManager.GetProgressionData();
                currentSave.totalLevelsCompleted = progression.levelsCompleted;
                
                // Update level progress data
                foreach (var medal in progression.levelMedals)
                {
                    if (!currentSave.levelProgress.ContainsKey(medal.Key))
                    {
                        currentSave.levelProgress[medal.Key] = new LevelCompletionData();
                    }
                    
                    var levelProgress = currentSave.levelProgress[medal.Key];
                    levelProgress.bestMedal = medal.Value;
                    levelProgress.isCompleted = medal.Value != UIManager.MedalType.None;
                    levelProgress.lastPlayed = DateTime.Now;
                }
            }
        }

        private void ApplySaveData()
        {
            // Apply to UI manager
            if (uiManager != null)
            {
                var progression = new UIManager.ProgressionData
                {
                    totalFishTreats = currentSave.totalFishTreats,
                    totalYarn = currentSave.totalYarn,
                    totalGoldenTokens = currentSave.totalGoldenTokens,
                    levelsCompleted = currentSave.totalLevelsCompleted,
                    totalPlayTime = (int)currentSave.totalPlayTime
                };

                // Apply level medals
                foreach (var levelData in currentSave.levelProgress)
                {
                    progression.levelMedals[levelData.Key] = levelData.Value.bestMedal;
                }

                // This would need to be implemented in UIManager
                // uiManager.ApplyProgressionData(progression);
            }

            // Apply settings
            ApplySettings();
        }

        private void ApplySettings()
        {
            var settings = currentSave.settings;
            
            // Apply audio settings
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMasterVolume(settings.masterVolume);
                AudioManager.Instance.SetMusicVolume(settings.musicVolume);
                AudioManager.Instance.SetSFXVolume(settings.sfxVolume);
                AudioManager.Instance.SetVoiceVolume(settings.voiceVolume);
            }

            // Apply video settings
            if (settings.fullscreen != Screen.fullScreen)
            {
                Screen.fullScreen = settings.fullscreen;
            }

            QualitySettings.SetQualityLevel(settings.qualityLevel);

            // Apply accessibility settings
            if (uiManager != null)
            {
                uiManager.SetAccessibilityOptions(settings.accessibility);
            }
        }

        private bool ValidateSaveData(SaveData data)
        {
            // Validate version compatibility
            if (data.version > currentSave.version)
            {
                return false; // Save from newer version
            }

            // Validate basic data integrity
            if (data.totalPlayTime < 0 || 
                data.totalFishTreats < 0 || 
                data.totalYarn < 0 || 
                data.totalGoldenTokens < 0)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Save Slot Management

        /// <summary>
        /// Get information about all save slots
        /// </summary>
        public SaveSlotInfo[] GetSaveSlotInfo()
        {
            SaveSlotInfo[] slots = new SaveSlotInfo[maxSaveSlots];

            for (int i = 0; i < maxSaveSlots; i++)
            {
                slots[i] = new SaveSlotInfo
                {
                    slotIndex = i,
                    isOccupied = false
                };

                string savePath = GetSaveFilePath(i);
                
                if (File.Exists(savePath))
                {
                    try
                    {
                        string jsonData = File.ReadAllText(savePath);
                        
                        if (useEncryption)
                        {
                            jsonData = DecryptData(jsonData);
                        }

                        SaveData saveData = JsonUtility.FromJson<SaveData>(jsonData);
                        
                        if (saveData != null)
                        {
                            slots[i].isOccupied = true;
                            slots[i].saveTime = saveData.saveTime;
                            slots[i].playerName = saveData.playerName;
                            slots[i].currentLevel = saveData.currentLevel;
                            slots[i].totalPlayTime = saveData.totalPlayTime;
                            slots[i].totalLevelsCompleted = saveData.totalLevelsCompleted;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to read save slot {i}: {e.Message}");
                    }
                }
            }

            return slots;
        }

        /// <summary>
        /// Load most recent save automatically
        /// </summary>
        public void LoadMostRecentSave()
        {
            var slots = GetSaveSlotInfo();
            SaveSlotInfo mostRecent = null;
            int mostRecentIndex = -1;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].isOccupied && (mostRecent == null || slots[i].saveTime > mostRecent.saveTime))
                {
                    mostRecent = slots[i];
                    mostRecentIndex = i;
                }
            }

            if (mostRecentIndex >= 0)
            {
                LoadGame(mostRecentIndex);
            }
            else
            {
                // Create new save in slot 0
                CreateNewSave(0);
            }
        }

        /// <summary>
        /// Create new save in specified slot
        /// </summary>
        public void CreateNewSave(int slotIndex)
        {
            currentSave = new SaveData();
            currentSave.saveTime = DateTime.Now;
            currentSlot = slotIndex;
            hasUnsavedChanges = true;
            
            SaveGame(slotIndex);
        }

        #endregion

        #region Backup System

        private void CreateBackup(int slotIndex)
        {
            try
            {
                string savePath = GetSaveFilePath(slotIndex);
                string backupPath = GetBackupFilePath(slotIndex, DateTime.Now);
                
                if (File.Exists(savePath))
                {
                    File.Copy(savePath, backupPath);
                    CleanupOldBackups(slotIndex);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create backup: {e.Message}");
            }
        }

        private void CleanupOldBackups(int slotIndex)
        {
            try
            {
                string backupPattern = $"save_slot_{slotIndex}_*.bak";
                string[] backupFiles = Directory.GetFiles(backupDirectory, backupPattern);
                
                if (backupFiles.Length > maxBackupsPerSlot)
                {
                    Array.Sort(backupFiles);
                    int filesToDelete = backupFiles.Length - maxBackupsPerSlot;
                    
                    for (int i = 0; i < filesToDelete; i++)
                    {
                        File.Delete(backupFiles[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to cleanup backups: {e.Message}");
            }
        }

        private void DeleteBackups(int slotIndex)
        {
            try
            {
                string backupPattern = $"save_slot_{slotIndex}_*.bak";
                string[] backupFiles = Directory.GetFiles(backupDirectory, backupPattern);
                
                foreach (string backupFile in backupFiles)
                {
                    File.Delete(backupFile);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete backups: {e.Message}");
            }
        }

        #endregion

        #region File Path Methods

        private string GetSaveFilePath(int slotIndex)
        {
            return Path.Combine(saveDirectory, $"save_slot_{slotIndex}{SAVE_FILE_EXTENSION}");
        }

        private string GetBackupFilePath(int slotIndex, DateTime timestamp)
        {
            string timeString = timestamp.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(backupDirectory, $"save_slot_{slotIndex}_{timeString}{BACKUP_FILE_EXTENSION}");
        }

        #endregion

        #region Encryption (Simple)

        private string EncryptData(string data)
        {
            // Simple XOR encryption (for basic obfuscation)
            char[] chars = data.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)(chars[i] ^ 123); // Simple key
            }
            return new string(chars);
        }

        private string DecryptData(string data)
        {
            // XOR decryption (same as encryption with XOR)
            return EncryptData(data);
        }

        #endregion

        #region Event Handlers

        private void OnLevelCompleted(LevelManager.LevelData levelData)
        {
            // Update level completion data
            if (!currentSave.levelProgress.ContainsKey(levelData.levelNumber))
            {
                currentSave.levelProgress[levelData.levelNumber] = new LevelCompletionData();
            }

            var completion = currentSave.levelProgress[levelData.levelNumber];
            completion.isCompleted = true;
            completion.timesCompleted++;
            completion.lastPlayed = DateTime.Now;
            
            if (completion.firstCompleted == default)
            {
                completion.firstCompleted = DateTime.Now;
            }

            hasUnsavedChanges = true;

            if (saveOnLevelComplete)
            {
                AutoSave();
            }
        }

        private void OnCheckpointReached(LevelManager.CheckpointData checkpoint)
        {
            hasUnsavedChanges = true;

            if (saveOnCheckpoint)
            {
                AutoSave();
            }
        }

        private void OnProgressUpdated(LevelManager.LevelProgress progress)
        {
            hasUnsavedChanges = true;
        }

        private void OnProgressionUpdated(UIManager.ProgressionData progression)
        {
            hasUnsavedChanges = true;
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current save data
        /// </summary>
        public SaveData GetCurrentSave()
        {
            return currentSave;
        }

        /// <summary>
        /// Get current slot index
        /// </summary>
        public int GetCurrentSlot()
        {
            return currentSlot;
        }

        /// <summary>
        /// Check if there are unsaved changes
        /// </summary>
        public bool HasUnsavedChanges()
        {
            return hasUnsavedChanges;
        }

        /// <summary>
        /// Force mark as having unsaved changes
        /// </summary>
        public void MarkAsChanged()
        {
            hasUnsavedChanges = true;
        }

        /// <summary>
        /// Update player statistics
        /// </summary>
        public void UpdateStatistics(string statName, int value)
        {
            switch (statName.ToLower())
            {
                case "jumps":
                    currentSave.statistics.totalJumps += value;
                    break;
                case "doublejumps":
                    currentSave.statistics.totalDoubleJumps += value;
                    break;
                case "slides":
                    currentSave.statistics.totalSlides += value;
                    break;
                case "tailwhips":
                    currentSave.statistics.totalTailWhips += value;
                    break;
                case "crates":
                    currentSave.statistics.totalCratesDestroyed += value;
                    break;
                case "deaths":
                    currentSave.statistics.totalDeaths += value;
                    break;
                case "checkpoints":
                    currentSave.statistics.totalCheckpoints += value;
                    break;
            }

            hasUnsavedChanges = true;
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            // Update debug information
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(1200, 10, 300, 400));
                
                GUILayout.Label("=== SAVE SYSTEM DEBUG ===");
                GUILayout.Label($"Current Slot: {currentSlot}");
                GUILayout.Label($"Unsaved Changes: {hasUnsavedChanges}");
                GUILayout.Label($"Auto-Save: {autoSaveEnabled}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== CURRENT SAVE ===");
                if (currentSave != null)
                {
                    GUILayout.Label($"Level: {currentSave.currentLevel}");
                    GUILayout.Label($"Play Time: {currentSave.totalPlayTime:F1}s");
                    GUILayout.Label($"Fish Treats: {currentSave.totalFishTreats}");
                    GUILayout.Label($"Yarn: {currentSave.totalYarn}");
                    GUILayout.Label($"Tokens: {currentSave.totalGoldenTokens}");
                    GUILayout.Label($"Levels Done: {currentSave.totalLevelsCompleted}");
                }
                
                GUILayout.Space(5);
                GUILayout.Label("=== STATISTICS ===");
                if (currentSave?.statistics != null)
                {
                    GUILayout.Label($"Jumps: {currentSave.statistics.totalJumps}");
                    GUILayout.Label($"Tail Whips: {currentSave.statistics.totalTailWhips}");
                    GUILayout.Label($"Crates: {currentSave.statistics.totalCratesDestroyed}");
                    GUILayout.Label($"Deaths: {currentSave.statistics.totalDeaths}");
                }
                
                GUILayout.Space(5);
                GUILayout.Label("=== ACTIONS ===");
                
                if (GUILayout.Button("Quick Save"))
                {
                    QuickSave();
                }
                
                if (GUILayout.Button("Quick Load"))
                {
                    QuickLoad();
                }
                
                if (GUILayout.Button("Auto Save"))
                {
                    AutoSave();
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }
}
