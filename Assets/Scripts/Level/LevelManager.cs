using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WhiskerKing.Core;
using WhiskerKing.Player;

namespace WhiskerKing.Level
{
    /// <summary>
    /// Comprehensive Level Manager for Whisker King
    /// Handles level progression, state management, checkpoints, and PRD-compliant level structure
    /// Implements standard level layout: Start→Mechanic→Checkpoint→Combination→Final
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public enum LevelState
        {
            Loading,
            Starting,
            InProgress,
            Paused,
            Checkpointed,
            Completing,
            Completed,
            Failed,
            Transitioning
        }

        public enum LevelSection
        {
            Start,        // Tutorial/warm-up section
            Mechanic,     // Introduce new mechanic
            Checkpoint,   // Save progress point
            Combination,  // Combine mechanics
            Final         // Challenging finale
        }

        [System.Serializable]
        public class LevelData
        {
            public int levelNumber;
            public string levelName;
            public string sceneName;
            public float targetTime;
            public int requiredScore;
            public bool isUnlocked;
            public bool isCompleted;
            public float bestTime;
            public int bestScore;
            public List<string> unlockedMechanics = new List<string>();
        }

        [System.Serializable]
        public class CheckpointData
        {
            public int checkpointId;
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
            public int collectedItems;
            public int score;
            public Dictionary<string, object> gameState = new Dictionary<string, object>();
        }

        [System.Serializable]
        public class LevelProgress
        {
            public float completionPercentage;
            public LevelSection currentSection;
            public int currentCheckpoint;
            public float elapsedTime;
            public int currentScore;
            public int collectedFishTreats;
            public int collectedYarn;
            public int collectedTokens;
        }

        [Header("Level Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;

        [Header("Current Level Settings")]
        [SerializeField] private LevelData currentLevel = new LevelData();
        [SerializeField] private float checkpointInterval = 30f; // 25-40s as per PRD
        [SerializeField] private int maxCheckpoints = 10;

        [Header("Progression Settings")]
        [SerializeField] private float difficultyScaling = 1.0f;
        [SerializeField] private bool adaptiveDifficulty = true;
        [SerializeField] private float playerSkillRating = 1.0f;

        [Header("Debug Settings")]
        [SerializeField] private bool showProgressUI = true;
        [SerializeField] private bool enableCheckpointVisualization = true;

        // Components
        private PlayerController playerController;
        
        // Level state
        private LevelState currentState = LevelState.Loading;
        private LevelSection currentSection = LevelSection.Start;
        private LevelProgress progress = new LevelProgress();
        
        // Checkpoint system
        private List<CheckpointData> checkpoints = new List<CheckpointData>();
        private CheckpointData lastCheckpoint;
        private float lastCheckpointTime;
        private int currentCheckpointId = 0;

        // Timing and scoring
        private float levelStartTime;
        private float sectionStartTime;
        private int baseScore = 0;
        private float timeMultiplier = 1.0f;

        // Level structure tracking
        private Dictionary<LevelSection, bool> sectionCompleted = new Dictionary<LevelSection, bool>();
        private Queue<LevelSection> sectionQueue = new Queue<LevelSection>();

        // Configuration cache
        private LevelDesignData levelConfig;

        // Events
        public System.Action<LevelState> OnLevelStateChanged;
        public System.Action<LevelSection> OnSectionChanged;
        public System.Action<CheckpointData> OnCheckpointReached;
        public System.Action<LevelProgress> OnProgressUpdated;
        public System.Action<LevelData> OnLevelCompleted;
        public System.Action<float> OnDifficultyScaled;

        #region Unity Lifecycle

        private void Awake()
        {
            // Find player controller
            playerController = FindObjectOfType<PlayerController>();
            if (playerController == null)
            {
                Debug.LogWarning("LevelManager: No PlayerController found in scene");
            }
        }

        private void Start()
        {
            LoadConfiguration();
            InitializeLevelSystem();
            StartLevel();
        }

        private void Update()
        {
            UpdateLevelState();
            UpdateProgress();
            CheckCheckpointTiming();
            
            if (adaptiveDifficulty)
            {
                UpdateDifficultyScaling();
            }

            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode && enableCheckpointVisualization)
            {
                DrawCheckpointGizmos();
            }
        }

        #endregion

        #region Configuration

        private void LoadConfiguration()
        {
            if (useGameConfiguration && GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                levelConfig = GameConfiguration.Instance.Config.levelDesign;
                ApplyConfiguration();
                Debug.Log("LevelManager: Configuration loaded from GameConfig");
            }
            else
            {
                Debug.LogWarning("LevelManager: GameConfiguration not available, using default values");
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            if (levelConfig == null) return;

            // Apply checkpoint settings
            checkpointInterval = levelConfig.checkpointIntervalSeconds;
            maxCheckpoints = levelConfig.maxCheckpointsPerLevel;

            // Apply difficulty settings
            difficultyScaling = levelConfig.difficultyScaling;
            adaptiveDifficulty = levelConfig.adaptiveDifficulty;

            // Apply target completion time if available
            if (levelConfig.targetCompletionTime > 0)
            {
                currentLevel.targetTime = levelConfig.targetCompletionTime;
            }
        }

        private void UseDefaultConfiguration()
        {
            // Use PRD-compliant defaults
            checkpointInterval = 30f; // Middle of 25-40s range
            maxCheckpoints = 10;
            difficultyScaling = 1.0f;
            adaptiveDifficulty = true;
        }

        #endregion

        #region Level Initialization

        private void InitializeLevelSystem()
        {
            // Initialize level structure based on PRD template
            InitializeSectionQueue();
            
            // Initialize progress tracking
            progress = new LevelProgress
            {
                completionPercentage = 0f,
                currentSection = LevelSection.Start,
                currentCheckpoint = 0,
                elapsedTime = 0f,
                currentScore = 0,
                collectedFishTreats = 0,
                collectedYarn = 0,
                collectedTokens = 0
            };

            // Initialize checkpoint system
            checkpoints.Clear();
            currentCheckpointId = 0;
            lastCheckpointTime = 0f;

            // Initialize section tracking
            sectionCompleted.Clear();
            foreach (LevelSection section in System.Enum.GetValues(typeof(LevelSection)))
            {
                sectionCompleted[section] = false;
            }

            Debug.Log("LevelManager initialized - Checkpoint interval: " + checkpointInterval + "s");
        }

        private void InitializeSectionQueue()
        {
            // Set up standard level layout as per PRD: Start→Mechanic→Checkpoint→Combination→Final
            sectionQueue.Clear();
            sectionQueue.Enqueue(LevelSection.Start);
            sectionQueue.Enqueue(LevelSection.Mechanic);
            sectionQueue.Enqueue(LevelSection.Checkpoint);
            sectionQueue.Enqueue(LevelSection.Combination);
            sectionQueue.Enqueue(LevelSection.Final);
        }

        #endregion

        #region Level State Management

        private void StartLevel()
        {
            SetLevelState(LevelState.Starting);
            
            levelStartTime = Time.time;
            sectionStartTime = Time.time;
            
            // Start with first section
            if (sectionQueue.Count > 0)
            {
                SetCurrentSection(sectionQueue.Dequeue());
            }
            
            SetLevelState(LevelState.InProgress);
            
            if (debugMode)
            {
                Debug.Log($"Level started - {currentLevel.levelName} (Section: {currentSection})");
            }
        }

        private void UpdateLevelState()
        {
            switch (currentState)
            {
                case LevelState.InProgress:
                    // Check for completion conditions
                    if (IsLevelComplete())
                    {
                        CompleteLevel();
                    }
                    break;

                case LevelState.Paused:
                    // Handle pause state
                    break;

                case LevelState.Failed:
                    // Handle failure state
                    break;
            }
        }

        private void SetLevelState(LevelState newState)
        {
            if (currentState == newState) return;

            LevelState previousState = currentState;
            currentState = newState;

            HandleStateTransition(previousState, newState);
            OnLevelStateChanged?.Invoke(newState);

            if (debugMode)
            {
                Debug.Log($"Level state changed: {previousState} → {newState}");
            }
        }

        private void HandleStateTransition(LevelState from, LevelState to)
        {
            switch (to)
            {
                case LevelState.Starting:
                    // Prepare level start
                    Time.timeScale = 1f;
                    break;

                case LevelState.InProgress:
                    // Resume normal gameplay
                    Time.timeScale = 1f;
                    break;

                case LevelState.Paused:
                    // Pause gameplay
                    Time.timeScale = 0f;
                    break;

                case LevelState.Completing:
                    // Begin completion sequence
                    break;

                case LevelState.Completed:
                    // Handle level completion
                    SaveLevelProgress();
                    UnlockNextLevel();
                    break;

                case LevelState.Failed:
                    // Handle level failure
                    break;
            }
        }

        #endregion

        #region Section Management

        private void SetCurrentSection(LevelSection newSection)
        {
            if (currentSection == newSection) return;

            LevelSection previousSection = currentSection;
            currentSection = newSection;
            progress.currentSection = newSection;

            sectionStartTime = Time.time;

            HandleSectionTransition(previousSection, newSection);
            OnSectionChanged?.Invoke(newSection);

            if (debugMode)
            {
                Debug.Log($"Section changed: {previousSection} → {newSection}");
            }
        }

        private void HandleSectionTransition(LevelSection from, LevelSection to)
        {
            // Mark previous section as completed
            if (from != to)
            {
                sectionCompleted[from] = true;
            }

            // Handle section-specific logic
            switch (to)
            {
                case LevelSection.Start:
                    // Tutorial/warm-up phase
                    SetDifficultyMultiplier(0.8f);
                    break;

                case LevelSection.Mechanic:
                    // Introduce new mechanics
                    SetDifficultyMultiplier(1.0f);
                    break;

                case LevelSection.Checkpoint:
                    // Safe zone with checkpoint
                    CreateCheckpoint();
                    break;

                case LevelSection.Combination:
                    // Combine learned mechanics
                    SetDifficultyMultiplier(1.2f);
                    break;

                case LevelSection.Final:
                    // Challenging finale
                    SetDifficultyMultiplier(1.5f);
                    break;
            }
        }

        public void CompleteCurrentSection()
        {
            sectionCompleted[currentSection] = true;
            
            // Move to next section if available
            if (sectionQueue.Count > 0)
            {
                SetCurrentSection(sectionQueue.Dequeue());
            }
            else
            {
                // All sections completed
                SetLevelState(LevelState.Completing);
            }
        }

        #endregion

        #region Checkpoint System

        private void CheckCheckpointTiming()
        {
            if (currentState != LevelState.InProgress) return;

            float timeSinceLastCheckpoint = Time.time - lastCheckpointTime;
            
            // Create checkpoint every 25-40 seconds (using configured interval)
            if (timeSinceLastCheckpoint >= checkpointInterval)
            {
                CreateCheckpoint();
            }
        }

        private void CreateCheckpoint()
        {
            if (checkpoints.Count >= maxCheckpoints)
            {
                Debug.LogWarning("LevelManager: Maximum checkpoints reached, skipping checkpoint creation");
                return;
            }

            Vector3 checkpointPosition = Vector3.zero;
            Quaternion checkpointRotation = Quaternion.identity;

            // Get player position if available
            if (playerController != null)
            {
                checkpointPosition = playerController.transform.position;
                checkpointRotation = playerController.transform.rotation;
            }

            CheckpointData checkpoint = new CheckpointData
            {
                checkpointId = currentCheckpointId++,
                position = checkpointPosition,
                rotation = checkpointRotation,
                timestamp = Time.time - levelStartTime,
                collectedItems = progress.collectedFishTreats + progress.collectedYarn + progress.collectedTokens,
                score = progress.currentScore
            };

            // Save additional game state
            checkpoint.gameState["section"] = currentSection.ToString();
            checkpoint.gameState["health"] = GetPlayerHealth();
            checkpoint.gameState["completionPercentage"] = progress.completionPercentage;

            checkpoints.Add(checkpoint);
            lastCheckpoint = checkpoint;
            lastCheckpointTime = Time.time;
            progress.currentCheckpoint = checkpoint.checkpointId;

            OnCheckpointReached?.Invoke(checkpoint);

            if (debugMode)
            {
                Debug.Log($"Checkpoint {checkpoint.checkpointId} created at {checkpoint.position} (Time: {checkpoint.timestamp:F1}s)");
            }
        }

        public void LoadCheckpoint(int checkpointId)
        {
            CheckpointData checkpoint = checkpoints.FirstOrDefault(cp => cp.checkpointId == checkpointId);
            if (checkpoint == null)
            {
                Debug.LogError($"LevelManager: Checkpoint {checkpointId} not found");
                return;
            }

            LoadCheckpoint(checkpoint);
        }

        public void LoadLastCheckpoint()
        {
            if (lastCheckpoint != null)
            {
                LoadCheckpoint(lastCheckpoint);
            }
            else
            {
                Debug.LogWarning("LevelManager: No checkpoint available to load");
                RestartLevel();
            }
        }

        private void LoadCheckpoint(CheckpointData checkpoint)
        {
            SetLevelState(LevelState.Loading);

            // Restore player position
            if (playerController != null)
            {
                playerController.transform.position = checkpoint.position;
                playerController.transform.rotation = checkpoint.rotation;
                
                // Reset player state
                playerController.ResetPlayerState();
            }

            // Restore game state
            progress.currentScore = checkpoint.score;
            progress.elapsedTime = checkpoint.timestamp;
            progress.currentCheckpoint = checkpoint.checkpointId;

            // Restore section state
            if (checkpoint.gameState.ContainsKey("section"))
            {
                System.Enum.TryParse(checkpoint.gameState["section"].ToString(), out LevelSection savedSection);
                SetCurrentSection(savedSection);
            }

            SetLevelState(LevelState.InProgress);

            if (debugMode)
            {
                Debug.Log($"Loaded checkpoint {checkpoint.checkpointId} - Position: {checkpoint.position}");
            }
        }

        #endregion

        #region Progress Tracking

        private void UpdateProgress()
        {
            if (currentState != LevelState.InProgress) return;

            // Update elapsed time
            progress.elapsedTime = Time.time - levelStartTime;

            // Calculate completion percentage based on section progress
            UpdateCompletionPercentage();

            // Update score based on performance
            UpdateScore();

            // Trigger progress update event
            OnProgressUpdated?.Invoke(progress);
        }

        private void UpdateCompletionPercentage()
        {
            // Calculate completion based on sections completed
            int completedSections = sectionCompleted.Count(kvp => kvp.Value);
            int totalSections = sectionCompleted.Count;
            
            // Add partial progress for current section
            float sectionProgress = GetCurrentSectionProgress();
            
            progress.completionPercentage = (completedSections + sectionProgress) / totalSections * 100f;
        }

        private float GetCurrentSectionProgress()
        {
            // Estimate progress within current section based on time
            float sectionElapsed = Time.time - sectionStartTime;
            float estimatedSectionDuration = checkpointInterval; // Use checkpoint interval as rough section estimate
            
            return Mathf.Clamp01(sectionElapsed / estimatedSectionDuration);
        }

        private void UpdateScore()
        {
            // Time-based scoring (bonus for fast completion)
            float timeBonus = Mathf.Max(0, currentLevel.targetTime - progress.elapsedTime) * 10f;
            
            // Collection-based scoring
            int collectionScore = (progress.collectedFishTreats * 10) + 
                                  (progress.collectedYarn * 20) + 
                                  (progress.collectedTokens * 100);
            
            progress.currentScore = baseScore + (int)timeBonus + collectionScore;
        }

        #endregion

        #region Difficulty Scaling

        private void UpdateDifficultyScaling()
        {
            // Assess player performance
            AssessPlayerPerformance();
            
            // Adjust difficulty based on performance
            AdjustDifficultyBasedOnPerformance();
        }

        private void AssessPlayerPerformance()
        {
            if (progress.elapsedTime <= 0) return;

            // Calculate performance metrics
            float completionRate = progress.completionPercentage / progress.elapsedTime;
            float targetRate = 100f / currentLevel.targetTime; // Target completion rate
            
            float deathRate = GetPlayerDeathCount() / (progress.elapsedTime / 60f); // Deaths per minute
            float collectionRate = (progress.collectedFishTreats + progress.collectedYarn) / progress.elapsedTime;

            // Update skill rating based on performance
            float performanceRatio = completionRate / targetRate;
            
            if (performanceRatio > 1.2f && deathRate < 0.5f)
            {
                // Player performing well
                playerSkillRating = Mathf.Min(2.0f, playerSkillRating + Time.deltaTime * 0.1f);
            }
            else if (performanceRatio < 0.8f || deathRate > 2.0f)
            {
                // Player struggling
                playerSkillRating = Mathf.Max(0.5f, playerSkillRating - Time.deltaTime * 0.1f);
            }
        }

        private void AdjustDifficultyBasedOnPerformance()
        {
            float targetDifficulty = playerSkillRating * difficultyScaling;
            
            // Gradually adjust difficulty
            float adjustmentSpeed = 0.5f * Time.deltaTime;
            float newDifficulty = Mathf.Lerp(GetCurrentDifficultyMultiplier(), targetDifficulty, adjustmentSpeed);
            
            SetDifficultyMultiplier(newDifficulty);
        }

        private void SetDifficultyMultiplier(float multiplier)
        {
            timeMultiplier = multiplier;
            OnDifficultyScaled?.Invoke(multiplier);

            if (debugMode)
            {
                Debug.Log($"Difficulty multiplier set to: {multiplier:F2}");
            }
        }

        private float GetCurrentDifficultyMultiplier()
        {
            return timeMultiplier;
        }

        #endregion

        #region Level Completion

        private bool IsLevelComplete()
        {
            // Check if all sections are completed
            return sectionCompleted.All(kvp => kvp.Value);
        }

        private void CompleteLevel()
        {
            SetLevelState(LevelState.Completing);

            // Calculate final score
            UpdateScore();
            
            // Update level data
            currentLevel.isCompleted = true;
            currentLevel.bestTime = Mathf.Min(currentLevel.bestTime > 0 ? currentLevel.bestTime : float.MaxValue, progress.elapsedTime);
            currentLevel.bestScore = Mathf.Max(currentLevel.bestScore, progress.currentScore);

            // Trigger completion event
            OnLevelCompleted?.Invoke(currentLevel);

            SetLevelState(LevelState.Completed);

            if (debugMode)
            {
                Debug.Log($"Level completed! Time: {progress.elapsedTime:F1}s, Score: {progress.currentScore}");
            }
        }

        private void SaveLevelProgress()
        {
            // Save progress to persistent storage
            string progressKey = $"Level_{currentLevel.levelNumber}_Progress";
            PlayerPrefs.SetString(progressKey, JsonUtility.ToJson(currentLevel));
            PlayerPrefs.Save();

            if (debugMode)
            {
                Debug.Log($"Level progress saved: {currentLevel.levelName}");
            }
        }

        private void UnlockNextLevel()
        {
            // Unlock next level if this one is completed
            if (currentLevel.isCompleted)
            {
                string nextLevelKey = $"Level_{currentLevel.levelNumber + 1}_Unlocked";
                PlayerPrefs.SetInt(nextLevelKey, 1);
                PlayerPrefs.Save();

                if (debugMode)
                {
                    Debug.Log($"Next level unlocked: Level {currentLevel.levelNumber + 1}");
                }
            }
        }

        #endregion

        #region Collectible System

        public void CollectItem(string itemType, int amount = 1)
        {
            switch (itemType.ToLower())
            {
                case "fishtreat":
                case "fishtreats":
                    progress.collectedFishTreats += amount;
                    break;

                case "yarn":
                    progress.collectedYarn += amount;
                    break;

                case "token":
                case "goldenmousetoken":
                    progress.collectedTokens += amount;
                    break;
            }

            UpdateScore();

            if (debugMode)
            {
                Debug.Log($"Collected {amount} {itemType} - Total: FT:{progress.collectedFishTreats}, Y:{progress.collectedYarn}, T:{progress.collectedTokens}");
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current level state
        /// </summary>
        public LevelState GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// Get current section
        /// </summary>
        public LevelSection GetCurrentSection()
        {
            return currentSection;
        }

        /// <summary>
        /// Get current progress
        /// </summary>
        public LevelProgress GetProgress()
        {
            return progress;
        }

        /// <summary>
        /// Get current level data
        /// </summary>
        public LevelData GetCurrentLevel()
        {
            return currentLevel;
        }

        /// <summary>
        /// Pause the level
        /// </summary>
        public void PauseLevel()
        {
            if (currentState == LevelState.InProgress)
            {
                SetLevelState(LevelState.Paused);
            }
        }

        /// <summary>
        /// Resume the level
        /// </summary>
        public void ResumeLevel()
        {
            if (currentState == LevelState.Paused)
            {
                SetLevelState(LevelState.InProgress);
            }
        }

        /// <summary>
        /// Restart the current level
        /// </summary>
        public void RestartLevel()
        {
            // Reset all progress
            InitializeLevelSystem();
            StartLevel();
        }

        /// <summary>
        /// Force create a checkpoint
        /// </summary>
        public void ForceCheckpoint()
        {
            CreateCheckpoint();
        }

        /// <summary>
        /// Get all checkpoints
        /// </summary>
        public List<CheckpointData> GetCheckpoints()
        {
            return new List<CheckpointData>(checkpoints);
        }

        /// <summary>
        /// Set level data
        /// </summary>
        public void SetLevelData(LevelData levelData)
        {
            currentLevel = levelData;
        }

        #endregion

        #region Utility Methods

        private float GetPlayerHealth()
        {
            // Placeholder for player health system
            return 100f;
        }

        private int GetPlayerDeathCount()
        {
            // Placeholder for death tracking
            return 0;
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            // Update debug information
        }

        private void DrawCheckpointGizmos()
        {
            // Draw checkpoint positions
            Gizmos.color = Color.green;
            foreach (var checkpoint in checkpoints)
            {
                Gizmos.DrawWireSphere(checkpoint.position, 1f);
                Gizmos.DrawRay(checkpoint.position, Vector3.up * 2f);
            }

            // Draw current player position if available
            if (playerController != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(playerController.transform.position, 0.5f);
            }
        }

        private void OnGUI()
        {
            if (debugMode && showProgressUI)
            {
                GUILayout.BeginArea(new Rect(10, 200, 300, 300));
                
                GUILayout.Label("=== LEVEL MANAGER DEBUG ===");
                GUILayout.Label($"State: {currentState}");
                GUILayout.Label($"Section: {currentSection}");
                GUILayout.Label($"Progress: {progress.completionPercentage:F1}%");
                
                GUILayout.Space(5);
                GUILayout.Label("=== TIMING ===");
                GUILayout.Label($"Elapsed: {progress.elapsedTime:F1}s");
                GUILayout.Label($"Target: {currentLevel.targetTime:F1}s");
                GUILayout.Label($"Next Checkpoint: {(checkpointInterval - (Time.time - lastCheckpointTime)):F1}s");
                
                GUILayout.Space(5);
                GUILayout.Label("=== SCORE ===");
                GUILayout.Label($"Current: {progress.currentScore}");
                GUILayout.Label($"Best: {currentLevel.bestScore}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== COLLECTIBLES ===");
                GUILayout.Label($"Fish Treats: {progress.collectedFishTreats}");
                GUILayout.Label($"Yarn: {progress.collectedYarn}");
                GUILayout.Label($"Tokens: {progress.collectedTokens}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== DIFFICULTY ===");
                GUILayout.Label($"Skill Rating: {playerSkillRating:F2}");
                GUILayout.Label($"Multiplier: {timeMultiplier:F2}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== CHECKPOINTS ===");
                GUILayout.Label($"Created: {checkpoints.Count}/{maxCheckpoints}");
                GUILayout.Label($"Current: {progress.currentCheckpoint}");
                
                if (GUILayout.Button("Force Checkpoint"))
                {
                    ForceCheckpoint();
                }
                
                if (GUILayout.Button("Load Last Checkpoint"))
                {
                    LoadLastCheckpoint();
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }
}
