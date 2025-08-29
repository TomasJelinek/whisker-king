using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WhiskerKing.Core
{
    /// <summary>
    /// Game Configuration Manager for Whisker King
    /// Loads and manages all game configuration from GameConfig.json
    /// Provides centralized access to performance targets, gameplay settings, and platform configurations
    /// </summary>
    public class GameConfiguration : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string configFileName = "GameConfig.json";
        [SerializeField] private bool loadOnAwake = true;
        [SerializeField] private bool validateOnLoad = true;

        // Singleton instance
        public static GameConfiguration Instance { get; private set; }

        // Configuration data
        public GameConfigData Config { get; private set; }

        // Events
        public event System.Action OnConfigurationLoaded;
        public event System.Action<string> OnConfigurationError;

        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (loadOnAwake)
                {
                    LoadConfiguration();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Load configuration from StreamingAssets
        /// </summary>
        public void LoadConfiguration()
        {
            try
            {
                string configPath = Path.Combine(Application.streamingAssetsPath, "Config", configFileName);
                string configJson = "";

                // Handle different platforms
                if (configPath.Contains("://") || configPath.Contains(":///"))
                {
                    // WebGL and some mobile platforms
                    StartCoroutine(LoadConfigurationAsync(configPath));
                    return;
                }
                else
                {
                    // Desktop and most platforms
                    if (File.Exists(configPath))
                    {
                        configJson = File.ReadAllText(configPath);
                    }
                    else
                    {
                        LogConfigurationError($"Configuration file not found: {configPath}");
                        LoadDefaultConfiguration();
                        return;
                    }
                }

                ParseConfiguration(configJson);
            }
            catch (System.Exception ex)
            {
                LogConfigurationError($"Error loading configuration: {ex.Message}");
                LoadDefaultConfiguration();
            }
        }

        private System.Collections.IEnumerator LoadConfigurationAsync(string path)
        {
            UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(path);
            yield return www.SendWebRequest();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                LogConfigurationError($"Error loading configuration: {www.error}");
                LoadDefaultConfiguration();
            }
            else
            {
                ParseConfiguration(www.downloadHandler.text);
            }
        }

        private void ParseConfiguration(string configJson)
        {
            try
            {
                Config = JsonConvert.DeserializeObject<GameConfigData>(configJson);
                
                if (validateOnLoad)
                {
                    ValidateConfiguration();
                }

                ApplyEnvironmentOverrides();
                
                Debug.Log($"Game configuration loaded successfully. Version: {Config.gameInfo.version}");
                OnConfigurationLoaded?.Invoke();
            }
            catch (System.Exception ex)
            {
                LogConfigurationError($"Error parsing configuration: {ex.Message}");
                LoadDefaultConfiguration();
            }
        }

        private void LoadDefaultConfiguration()
        {
            Debug.LogWarning("Loading default configuration...");
            
            Config = CreateDefaultConfiguration();
            OnConfigurationLoaded?.Invoke();
        }

        private GameConfigData CreateDefaultConfiguration()
        {
            return new GameConfigData
            {
                gameInfo = new GameInfoData
                {
                    name = "Whisker King",
                    version = "1.0.0",
                    buildNumber = 1,
                    configVersion = "1.0"
                },
                performanceTargets = new PerformanceTargetsData
                {
                    frameRate = new FrameRateData { target = 60, minimum = 30 },
                    memoryBudgets = new MemoryBudgetsData
                    {
                        lowQuality = new MemoryBudgetData { totalHeapMB = 512 },
                        mediumQuality = new MemoryBudgetData { totalHeapMB = 768 },
                        highQuality = new MemoryBudgetData { totalHeapMB = 1024 }
                    }
                },
                // Initialize other default values...
            };
        }

        private void ValidateConfiguration()
        {
            if (Config == null)
            {
                throw new System.Exception("Configuration is null");
            }

            // Validate performance targets
            if (Config.performanceTargets.frameRate.target <= 0 || Config.performanceTargets.frameRate.target > 120)
            {
                Debug.LogWarning($"Invalid target frame rate: {Config.performanceTargets.frameRate.target}. Using default: 60");
                Config.performanceTargets.frameRate.target = 60;
            }

            // Validate memory budgets
            ValidateMemoryBudget(Config.performanceTargets.memoryBudgets.lowQuality, "Low Quality");
            ValidateMemoryBudget(Config.performanceTargets.memoryBudgets.mediumQuality, "Medium Quality");
            ValidateMemoryBudget(Config.performanceTargets.memoryBudgets.highQuality, "High Quality");

            Debug.Log("Configuration validation completed");
        }

        private void ValidateMemoryBudget(MemoryBudgetData budget, string qualityLevel)
        {
            if (budget.totalHeapMB <= 0 || budget.totalHeapMB > 4096)
            {
                Debug.LogWarning($"Invalid total heap size for {qualityLevel}: {budget.totalHeapMB}MB");
            }
        }

        private void ApplyEnvironmentOverrides()
        {
            if (Config.metaSettings?.environmentOverrides == null)
                return;

            string environment = GetCurrentEnvironment();
            
            if (Config.metaSettings.environmentOverrides.ContainsKey(environment))
            {
                var overrides = Config.metaSettings.environmentOverrides[environment];
                ApplyOverrides(overrides);
                Debug.Log($"Applied {environment} environment overrides");
            }
        }

        private string GetCurrentEnvironment()
        {
            if (Debug.isDebugBuild)
                return "development";
            
            #if UNITY_EDITOR
                return "development";
            #elif DEVELOPMENT_BUILD
                return "testing";
            #else
                return "production";
            #endif
        }

        private void ApplyOverrides(Dictionary<string, object> overrides)
        {
            foreach (var kvp in overrides)
            {
                ApplyOverride(kvp.Key, kvp.Value);
            }
        }

        private void ApplyOverride(string path, object value)
        {
            // Simple implementation - would need more robust path parsing for complex overrides
            Debug.Log($"Applying override: {path} = {value}");
        }

        private void LogConfigurationError(string error)
        {
            Debug.LogError(error);
            OnConfigurationError?.Invoke(error);
        }

        // Utility methods for accessing configuration
        public float GetTargetFrameRate()
        {
            return Config?.performanceTargets?.frameRate?.target ?? 60f;
        }

        public int GetMemoryBudget(QualityLevel qualityLevel)
        {
            if (Config?.performanceTargets?.memoryBudgets == null)
                return 512;

            return qualityLevel switch
            {
                QualityLevel.Low => Config.performanceTargets.memoryBudgets.lowQuality.totalHeapMB,
                QualityLevel.Medium => Config.performanceTargets.memoryBudgets.mediumQuality.totalHeapMB,
                QualityLevel.High => Config.performanceTargets.memoryBudgets.highQuality.totalHeapMB,
                _ => 512
            };
        }

        public PlayerMovementData GetPlayerMovement()
        {
            return Config?.playerMovement ?? new PlayerMovementData();
        }

        public CameraSettingsData GetCameraSettings()
        {
            return Config?.cameraSettings ?? new CameraSettingsData();
        }

        public AudioSettingsData GetAudioSettings()
        {
            return Config?.audioSettings ?? new AudioSettingsData();
        }

        // Editor utilities
        #if UNITY_EDITOR
        [ContextMenu("Reload Configuration")]
        public void ReloadConfiguration()
        {
            LoadConfiguration();
        }

        [ContextMenu("Validate Configuration")]
        public void ValidateConfigurationEditor()
        {
            if (Config != null)
            {
                ValidateConfiguration();
                Debug.Log("Configuration validation completed");
            }
            else
            {
                Debug.LogError("No configuration loaded to validate");
            }
        }

        [ContextMenu("Log Configuration Summary")]
        public void LogConfigurationSummary()
        {
            if (Config == null)
            {
                Debug.LogError("No configuration loaded");
                return;
            }

            Debug.Log($"=== {Config.gameInfo.name} Configuration Summary ===");
            Debug.Log($"Version: {Config.gameInfo.version}");
            Debug.Log($"Target FPS: {Config.performanceTargets.frameRate.target}");
            Debug.Log($"Memory Budgets - Low: {Config.performanceTargets.memoryBudgets.lowQuality.totalHeapMB}MB, " +
                     $"Medium: {Config.performanceTargets.memoryBudgets.mediumQuality.totalHeapMB}MB, " +
                     $"High: {Config.performanceTargets.memoryBudgets.highQuality.totalHeapMB}MB");
        }
        #endif
    }

    // Configuration data structures
    [System.Serializable]
    public class GameConfigData
    {
        public GameInfoData gameInfo;
        public PerformanceTargetsData performanceTargets;
        public DeviceSpecificationsData deviceSpecifications;
        public QualitySettingsData qualitySettings;
        public PlayerMovementData playerMovement;
        public CameraSettingsData cameraSettings;
        public LevelDesignData levelDesign;
        public AudioSettingsData audioSettings;
        public UISettingsData uiSettings;
        public AnalyticsSettingsData analyticsSettings;
        public DebugSettingsData debugSettings;
        public BuildSettingsData buildSettings;
        public AssetSettingsData assetSettings;
        public WorldSettingsData worldSettings;
        public MetaSettingsData metaSettings;
    }

    [System.Serializable]
    public class GameInfoData
    {
        public string name;
        public string version;
        public int buildNumber;
        public string configVersion;
        public string lastUpdated;
    }

    [System.Serializable]
    public class PerformanceTargetsData
    {
        public FrameRateData frameRate;
        public MemoryBudgetsData memoryBudgets;
        public LoadTimesData loadTimes;
        public RenderingBudgetsData renderingBudgets;
    }

    [System.Serializable]
    public class FrameRateData
    {
        public int target;
        public int minimum;
        public float maxFrameTime;
        public bool vSyncEnabled;
    }

    [System.Serializable]
    public class MemoryBudgetsData
    {
        public MemoryBudgetData lowQuality;
        public MemoryBudgetData mediumQuality;
        public MemoryBudgetData highQuality;
    }

    [System.Serializable]
    public class MemoryBudgetData
    {
        public int totalHeapMB;
        public int textureMB;
        public int audioMB;
        public int scriptsMB;
        public int otherMB;
    }

    [System.Serializable]
    public class LoadTimesData
    {
        public float initialLoadMaxSeconds;
        public float levelLoadMaxSeconds;
        public float assetStreamingMaxSeconds;
    }

    [System.Serializable]
    public class RenderingBudgetsData
    {
        public RenderingBudgetData lowQuality;
        public RenderingBudgetData mediumQuality;
        public RenderingBudgetData highQuality;
    }

    [System.Serializable]
    public class RenderingBudgetData
    {
        public int maxDrawCalls;
        public int maxTriangles;
        public int maxTextureSize;
    }

    [System.Serializable]
    public class DeviceSpecificationsData
    {
        public DeviceSpecData minimumSpecs;
        public DeviceSpecData recommendedSpecs;
        public DeviceSpecData highEndSpecs;
    }

    [System.Serializable]
    public class DeviceSpecData
    {
        public PlatformSpecData android;
        public PlatformSpecData ios;
        public PlatformSpecData webgl;
    }

    [System.Serializable]
    public class PlatformSpecData
    {
        public string chipset;
        public string gpu;
        public int ramMB;
        public int apiLevel;
        public string openGLVersion;
        public string minimumVersion;
        public bool metalSupport;
        public bool vulkanSupport;
        public string webglVersion;
        public bool webgl1Fallback;
        public int memoryMB;
        public bool requiresHTTPS;
    }

    [System.Serializable]
    public class QualitySettingsData
    {
        public AutoQualityAdjustmentData autoQualityAdjustment;
        public Dictionary<string, QualityLevelConfigData> qualityLevels;
    }

    [System.Serializable]
    public class AutoQualityAdjustmentData
    {
        public bool enabled;
        public float checkIntervalSeconds;
        public int sampleFrames;
        public QualityThresholdsData thresholds;
    }

    [System.Serializable]
    public class QualityThresholdsData
    {
        public float lowQualityFPS;
        public float mediumQualityFPS;
        public float highQualityFPS;
    }

    [System.Serializable]
    public class QualityLevelConfigData
    {
        public float renderScale;
        public bool shadowsEnabled;
        public string antiAliasing;
        public string textureQuality;
        public int particleBudget;
        public bool postProcessingEnabled;
    }

    [System.Serializable]
    public class PlayerMovementData
    {
        public PhysicsData physics;
        public InputData input;
        public CombatData combat;
    }

    [System.Serializable]
    public class PhysicsData
    {
        public float runSpeed = 8.0f;
        public float slideSpeed = 10.0f;
        public float jumpHeight = 3.0f;
        public float jumpHeightHold = 4.5f;
        public float doubleJumpHeight = 2.5f;
        public float gravity = -25.0f;
        public float pounceGravity = -45.0f;
        public float airControl = 0.8f;
        public float groundFriction = 0.85f;
        public float airFriction = 0.95f;
        public float bounceDamping = 0.6f;
    }

    [System.Serializable]
    public class InputData
    {
        public int bufferTimeMS = 120;
        public int coyoteTimeMS = 120;
        public int slideDurationMS = 600;
        public int slideMinCancelTimeMS = 250;
    }

    [System.Serializable]
    public class CombatData
    {
        public TailWhipData tailWhip;
    }

    [System.Serializable]
    public class TailWhipData
    {
        public int windupTimeMS = 100;
        public int activeTimeMS = 180;
        public int recoveryTimeMS = 120;
        public float damage = 25.0f;
        public float stunDuration = 1.5f;
        public float range = 2.5f;
        public float angleDegreess = 270.0f;
    }

    [System.Serializable]
    public class CameraSettingsData
    {
        public FollowModeData followMode;
        public ChaseModeData chaseMode;
        public ComfortData comfort;
    }

    [System.Serializable]
    public class FollowModeData
    {
        public float distance = 8.0f;
        public float height = 3.0f;
        public float damping = 5.0f;
        public float lookAheadDistance = 4.0f;
        public float lookAheadHeight = 2.0f;
        public float lookAheadDamping = 3.0f;
    }

    [System.Serializable]
    public class ChaseModeData
    {
        public float fov = 85.0f;
        public float distance = 6.0f;
        public float height = 2.5f;
    }

    [System.Serializable]
    public class ComfortData
    {
        public bool motionBlurEnabled = false;
        public float shakeIntensity = 0.5f;
    }

    [System.Serializable]
    public class LevelDesignData
    {
        // Implement as needed
    }

    [System.Serializable]
    public class AudioSettingsData
    {
        // Implement as needed
    }

    [System.Serializable]
    public class UISettingsData
    {
        // Implement as needed
    }

    [System.Serializable]
    public class AnalyticsSettingsData
    {
        public bool enabled = true;
        public bool sessionTracking = true;
        public bool performanceTracking = true;
        public bool crashReporting = true;
        public bool customEvents = true;
        public int dataRetentionDays = 90;
        public string privacyMode = "OptIn";
    }

    [System.Serializable]
    public class DebugSettingsData
    {
        public bool developmentBuild = false;
        public bool profilerEnabled = false;
        public bool consoleEnabled = false;
        public bool debugUI = false;
        public bool performanceOverlay = false;
        public bool memoryTracker = false;
        public bool cheatCodesEnabled = false;
    }

    [System.Serializable]
    public class BuildSettingsData
    {
        // Implement as needed
    }

    [System.Serializable]
    public class AssetSettingsData
    {
        // Implement as needed
    }

    [System.Serializable]
    public class WorldSettingsData
    {
        // Implement as needed
    }

    [System.Serializable]
    public class MetaSettingsData
    {
        public ConfigValidationData configValidation;
        public Dictionary<string, Dictionary<string, object>> environmentOverrides;
    }

    [System.Serializable]
    public class ConfigValidationData
    {
        public bool strictMode = true;
        public bool validateOnLoad = true;
        public bool logMissingKeys = true;
    }

    public enum QualityLevel
    {
        Low = 0,
        Medium = 1,
        High = 2
    }
}
