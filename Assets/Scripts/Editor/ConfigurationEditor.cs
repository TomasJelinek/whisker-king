#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using WhiskerKing.Core;
using System.IO;
using Newtonsoft.Json;

namespace WhiskerKing.Editor
{
    /// <summary>
    /// Editor tools for Whisker King configuration management
    /// Provides validation, editing, and maintenance tools for GameConfig.json
    /// </summary>
    public static class ConfigurationEditor
    {
        private const string CONFIG_PATH = "Assets/StreamingAssets/Config/GameConfig.json";
        private const string TEMPLATE_PATH = "GameConfig_Template.json";

        [MenuItem("WhiskerKing/Configuration/Reload Configuration", false, 1)]
        public static void ReloadConfiguration()
        {
            if (GameConfiguration.Instance != null)
            {
                GameConfiguration.Instance.ReloadConfiguration();
                Debug.Log("Configuration reloaded successfully");
            }
            else
            {
                Debug.LogWarning("GameConfiguration instance not found. Add GameConfiguration to scene.");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Validate Configuration", false, 2)]
        public static void ValidateConfiguration()
        {
            if (GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                GameConfiguration.Instance.ValidateConfigurationEditor();
                ValidatePRDCompliance();
            }
            else
            {
                Debug.LogError("No configuration loaded. Load configuration first.");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Open Configuration File", false, 11)]
        public static void OpenConfigurationFile()
        {
            if (File.Exists(CONFIG_PATH))
            {
                System.Diagnostics.Process.Start(CONFIG_PATH);
                Debug.Log($"Opened configuration file: {CONFIG_PATH}");
            }
            else
            {
                Debug.LogError($"Configuration file not found: {CONFIG_PATH}");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Validate JSON Syntax", false, 12)]
        public static void ValidateJSONSyntax()
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Debug.LogError($"Configuration file not found: {CONFIG_PATH}");
                return;
            }

            try
            {
                string json = File.ReadAllText(CONFIG_PATH);
                var config = JsonConvert.DeserializeObject<GameConfigData>(json);
                
                if (config != null)
                {
                    Debug.Log("✓ JSON syntax is valid");
                }
                else
                {
                    Debug.LogError("✗ JSON parsing returned null");
                }
            }
            catch (JsonException ex)
            {
                Debug.LogError($"✗ JSON syntax error: {ex.Message}");
                Debug.LogError($"Line: {ex.LineNumber}, Position: {ex.LinePosition}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"✗ Configuration validation error: {ex.Message}");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Generate Template", false, 21)]
        public static void GenerateTemplate()
        {
            try
            {
                var template = CreateTemplateConfiguration();
                string json = JsonConvert.SerializeObject(template, Formatting.Indented);
                
                File.WriteAllText(TEMPLATE_PATH, json);
                Debug.Log($"Configuration template generated: {TEMPLATE_PATH}");
                
                // Refresh to show in Project window
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating template: {ex.Message}");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Backup Current Config", false, 22)]
        public static void BackupCurrentConfiguration()
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Debug.LogError($"Configuration file not found: {CONFIG_PATH}");
                return;
            }

            try
            {
                string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = $"GameConfig_Backup_{timestamp}.json";
                
                File.Copy(CONFIG_PATH, backupPath);
                Debug.Log($"Configuration backed up to: {backupPath}");
                
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error backing up configuration: {ex.Message}");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Compare with Template", false, 31)]
        public static void CompareWithTemplate()
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Debug.LogError($"Configuration file not found: {CONFIG_PATH}");
                return;
            }

            try
            {
                // Load current config
                string currentJson = File.ReadAllText(CONFIG_PATH);
                var currentConfig = JsonConvert.DeserializeObject<GameConfigData>(currentJson);

                // Generate template
                var template = CreateTemplateConfiguration();

                // Compare configurations
                CompareConfigurations(currentConfig, template);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error comparing configurations: {ex.Message}");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Export for Platform/WebGL", false, 41)]
        public static void ExportWebGLConfiguration()
        {
            ExportPlatformConfiguration("webgl");
        }

        [MenuItem("WhiskerKing/Configuration/Export for Platform/Android", false, 42)]
        public static void ExportAndroidConfiguration()
        {
            ExportPlatformConfiguration("android");
        }

        [MenuItem("WhiskerKing/Configuration/Export for Platform/iOS", false, 43)]
        public static void ExportiOSConfiguration()
        {
            ExportPlatformConfiguration("ios");
        }

        private static void ValidatePRDCompliance()
        {
            Debug.Log("=== PRD Compliance Validation ===");
            
            var config = GameConfiguration.Instance.Config;
            bool isCompliant = true;

            // Frame rate validation
            if (config.performanceTargets.frameRate.target < 60)
            {
                Debug.LogWarning("✗ Target frame rate below PRD requirement (60 FPS)");
                isCompliant = false;
            }
            else
            {
                Debug.Log("✓ Target frame rate meets PRD requirement");
            }

            // Memory budget validation
            if (config.performanceTargets.memoryBudgets.lowQuality.totalHeapMB > 512)
            {
                Debug.LogWarning("✗ Low quality memory budget exceeds PRD limit (512MB)");
                isCompliant = false;
            }
            else
            {
                Debug.Log("✓ Low quality memory budget within PRD limit");
            }

            if (config.performanceTargets.memoryBudgets.mediumQuality.totalHeapMB > 768)
            {
                Debug.LogWarning("✗ Medium quality memory budget exceeds PRD limit (768MB)");
                isCompliant = false;
            }
            else
            {
                Debug.Log("✓ Medium quality memory budget within PRD limit");
            }

            if (config.performanceTargets.memoryBudgets.highQuality.totalHeapMB > 1024)
            {
                Debug.LogWarning("✗ High quality memory budget exceeds PRD limit (1024MB)");
                isCompliant = false;
            }
            else
            {
                Debug.Log("✓ High quality memory budget within PRD limit");
            }

            // Load time validation
            if (config.performanceTargets.loadTimes.initialLoadMaxSeconds > 10.0f)
            {
                Debug.LogWarning("✗ Initial load time exceeds PRD requirement (10s)");
                isCompliant = false;
            }
            else
            {
                Debug.Log("✓ Initial load time meets PRD requirement");
            }

            if (config.performanceTargets.loadTimes.levelLoadMaxSeconds > 5.0f)
            {
                Debug.LogWarning("✗ Level load time exceeds PRD requirement (5s)");
                isCompliant = false;
            }
            else
            {
                Debug.Log("✓ Level load time meets PRD requirement");
            }

            // Player movement validation
            if (config.playerMovement.input.bufferTimeMS != 120)
            {
                Debug.LogWarning("✗ Input buffer time doesn't match PRD specification (120ms)");
                isCompliant = false;
            }
            else
            {
                Debug.Log("✓ Input buffer time matches PRD specification");
            }

            // Summary
            if (isCompliant)
            {
                Debug.Log("✅ Configuration is fully compliant with PRD requirements");
            }
            else
            {
                Debug.LogWarning("⚠️ Configuration has PRD compliance issues");
            }
        }

        private static GameConfigData CreateTemplateConfiguration()
        {
            return new GameConfigData
            {
                gameInfo = new GameInfoData
                {
                    name = "Whisker King",
                    version = "1.0.0",
                    buildNumber = 1,
                    configVersion = "1.0",
                    lastUpdated = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                },
                performanceTargets = new PerformanceTargetsData
                {
                    frameRate = new FrameRateData
                    {
                        target = 60,
                        minimum = 30,
                        maxFrameTime = 16.67f,
                        vSyncEnabled = true
                    },
                    memoryBudgets = new MemoryBudgetsData
                    {
                        lowQuality = new MemoryBudgetData { totalHeapMB = 512, textureMB = 256, audioMB = 64 },
                        mediumQuality = new MemoryBudgetData { totalHeapMB = 768, textureMB = 384, audioMB = 96 },
                        highQuality = new MemoryBudgetData { totalHeapMB = 1024, textureMB = 512, audioMB = 128 }
                    },
                    loadTimes = new LoadTimesData
                    {
                        initialLoadMaxSeconds = 10.0f,
                        levelLoadMaxSeconds = 5.0f,
                        assetStreamingMaxSeconds = 2.0f
                    }
                },
                playerMovement = new PlayerMovementData
                {
                    physics = new PhysicsData
                    {
                        runSpeed = 8.0f,
                        slideSpeed = 10.0f,
                        jumpHeight = 3.0f,
                        jumpHeightHold = 4.5f,
                        doubleJumpHeight = 2.5f,
                        gravity = -25.0f
                    },
                    input = new InputData
                    {
                        bufferTimeMS = 120,
                        coyoteTimeMS = 120,
                        slideDurationMS = 600,
                        slideMinCancelTimeMS = 250
                    }
                },
                cameraSettings = new CameraSettingsData
                {
                    followMode = new FollowModeData
                    {
                        distance = 8.0f,
                        height = 3.0f,
                        damping = 5.0f,
                        lookAheadDistance = 4.0f,
                        lookAheadHeight = 2.0f,
                        lookAheadDamping = 3.0f
                    },
                    chaseMode = new ChaseModeData
                    {
                        fov = 85.0f,
                        distance = 6.0f,
                        height = 2.5f
                    }
                }
            };
        }

        private static void CompareConfigurations(GameConfigData current, GameConfigData template)
        {
            Debug.Log("=== Configuration Comparison ===");

            // Compare frame rate settings
            if (current.performanceTargets.frameRate.target != template.performanceTargets.frameRate.target)
            {
                Debug.Log($"Frame rate difference - Current: {current.performanceTargets.frameRate.target}, Template: {template.performanceTargets.frameRate.target}");
            }

            // Compare memory budgets
            var currentLow = current.performanceTargets.memoryBudgets.lowQuality.totalHeapMB;
            var templateLow = template.performanceTargets.memoryBudgets.lowQuality.totalHeapMB;
            
            if (currentLow != templateLow)
            {
                Debug.Log($"Low quality memory difference - Current: {currentLow}MB, Template: {templateLow}MB");
            }

            Debug.Log("Configuration comparison completed");
        }

        private static void ExportPlatformConfiguration(string platform)
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Debug.LogError($"Configuration file not found: {CONFIG_PATH}");
                return;
            }

            try
            {
                string json = File.ReadAllText(CONFIG_PATH);
                var config = JsonConvert.DeserializeObject<GameConfigData>(json);

                // Apply platform-specific modifications
                ApplyPlatformOptimizations(config, platform);

                // Export modified configuration
                string exportPath = $"GameConfig_{platform.ToUpper()}.json";
                string exportJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                
                File.WriteAllText(exportPath, exportJson);
                Debug.Log($"Platform configuration exported: {exportPath}");
                
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error exporting {platform} configuration: {ex.Message}");
            }
        }

        private static void ApplyPlatformOptimizations(GameConfigData config, string platform)
        {
            switch (platform.ToLower())
            {
                case "webgl":
                    // WebGL optimizations
                    config.performanceTargets.memoryBudgets.lowQuality.totalHeapMB = 256;
                    config.performanceTargets.memoryBudgets.mediumQuality.totalHeapMB = 384;
                    config.performanceTargets.memoryBudgets.highQuality.totalHeapMB = 512;
                    break;

                case "android":
                    // Android optimizations
                    config.performanceTargets.frameRate.target = 60;
                    break;

                case "ios":
                    // iOS optimizations
                    config.performanceTargets.frameRate.target = 60;
                    break;
            }
        }

        [MenuItem("WhiskerKing/Configuration/Documentation/Open Configuration Guide", false, 51)]
        public static void OpenConfigurationGuide()
        {
            string guidePath = "CONFIGURATION-GUIDE.md";
            if (File.Exists(guidePath))
            {
                System.Diagnostics.Process.Start(guidePath);
            }
            else
            {
                Debug.LogError($"Configuration guide not found: {guidePath}");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Tools/Check File Permissions", false, 61)]
        public static void CheckFilePermissions()
        {
            string configDir = Path.GetDirectoryName(CONFIG_PATH);
            
            if (!Directory.Exists(configDir))
            {
                Debug.LogWarning($"Configuration directory doesn't exist: {configDir}");
                return;
            }

            try
            {
                // Test read permission
                if (File.Exists(CONFIG_PATH))
                {
                    File.ReadAllText(CONFIG_PATH);
                    Debug.Log("✓ Configuration file read permission OK");
                }

                // Test write permission
                string testFile = Path.Combine(configDir, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                Debug.Log("✓ Configuration directory write permission OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"✗ File permission error: {ex.Message}");
            }
        }

        [MenuItem("WhiskerKing/Configuration/Tools/Optimize Configuration Size", false, 62)]
        public static void OptimizeConfigurationSize()
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Debug.LogError($"Configuration file not found: {CONFIG_PATH}");
                return;
            }

            try
            {
                string json = File.ReadAllText(CONFIG_PATH);
                var config = JsonConvert.DeserializeObject<GameConfigData>(json);

                // Optimize by removing default values and compacting
                string optimizedJson = JsonConvert.SerializeObject(config, Formatting.None);
                
                long originalSize = new FileInfo(CONFIG_PATH).Length;
                long optimizedSize = System.Text.Encoding.UTF8.GetByteCount(optimizedJson);
                
                Debug.Log($"Configuration size optimization:");
                Debug.Log($"Original size: {originalSize} bytes");
                Debug.Log($"Optimized size: {optimizedSize} bytes");
                Debug.Log($"Size reduction: {((float)(originalSize - optimizedSize) / originalSize * 100):F1}%");

                bool saveOptimized = EditorUtility.DisplayDialog(
                    "Optimize Configuration",
                    $"Save optimized configuration?\n\nSize reduction: {((float)(originalSize - optimizedSize) / originalSize * 100):F1}%\nOriginal: {originalSize} bytes\nOptimized: {optimizedSize} bytes",
                    "Save Optimized",
                    "Keep Original");

                if (saveOptimized)
                {
                    File.WriteAllText(CONFIG_PATH, optimizedJson);
                    Debug.Log("Optimized configuration saved");
                    AssetDatabase.Refresh();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error optimizing configuration: {ex.Message}");
            }
        }
    }
}
#endif
