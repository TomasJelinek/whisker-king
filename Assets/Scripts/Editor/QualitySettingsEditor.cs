#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using WhiskerKing.Core;

namespace WhiskerKing.Editor
{
    /// <summary>
    /// Custom editor for Quality Settings management in Whisker King
    /// Provides easy access to configure and validate quality presets
    /// </summary>
    public static class QualitySettingsEditor
    {
        [MenuItem("WhiskerKing/Quality/Configure All Quality Levels", false, 1)]
        public static void ConfigureAllQualityLevels()
        {
            var config = GetOrCreateQualityConfiguration();
            if (config != null)
            {
                config.ApplyQualitySettings();
                EditorUtility.DisplayDialog(
                    "Quality Settings Applied", 
                    "All quality levels have been configured according to Whisker King PRD specifications.",
                    "OK");
            }
        }

        [MenuItem("WhiskerKing/Quality/Validate Current Settings", false, 2)]
        public static void ValidateCurrentSettings()
        {
            var config = GetOrCreateQualityConfiguration();
            if (config != null)
            {
                config.ValidateQualitySettings();
                Debug.Log("Quality settings validation complete. Check console for details.");
            }
        }

        [MenuItem("WhiskerKing/Quality/Open Quality Settings Window", false, 11)]
        public static void OpenQualitySettingsWindow()
        {
            EditorWindow.GetWindow(typeof(QualitySettingsInspector), false, "Whisker King Quality Settings");
        }

        [MenuItem("WhiskerKing/Quality/Test Low Quality", false, 21)]
        public static void TestLowQuality()
        {
            SetQualityLevelForTesting(0);
            Debug.Log("Switched to Low Quality for testing");
        }

        [MenuItem("WhiskerKing/Quality/Test Medium Quality", false, 22)]
        public static void TestMediumQuality()
        {
            SetQualityLevelForTesting(1);
            Debug.Log("Switched to Medium Quality for testing");
        }

        [MenuItem("WhiskerKing/Quality/Test High Quality", false, 23)]
        public static void TestHighQuality()
        {
            SetQualityLevelForTesting(2);
            Debug.Log("Switched to High Quality for testing");
        }

        private static QualityLevelConfiguration GetOrCreateQualityConfiguration()
        {
            string configPath = "Assets/Settings/DefaultQualityConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<QualityLevelConfiguration>(configPath);
            
            if (config == null)
            {
                Debug.Log("Quality configuration not found. Creating default configuration...");
                QualityLevelConfiguration.SetupDefaultQualityLevelsMenuItem();
                config = AssetDatabase.LoadAssetAtPath<QualityLevelConfiguration>(configPath);
            }
            
            return config;
        }

        private static void SetQualityLevelForTesting(int qualityIndex)
        {
            if (qualityIndex >= 0 && qualityIndex < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(qualityIndex, true);
                
                // Update scene view to reflect changes
                SceneView.RepaintAll();
                
                // Show current settings in console
                Debug.Log($"=== {QualitySettings.names[qualityIndex]} Quality Settings ===");
                Debug.Log($"Pixel Light Count: {QualitySettings.pixelLightCount}");
                Debug.Log($"Shadows: {QualitySettings.shadows}");
                Debug.Log($"Shadow Resolution: {QualitySettings.shadowResolution}");
                Debug.Log($"Shadow Distance: {QualitySettings.shadowDistance}");
                Debug.Log($"Anti-Aliasing: {QualitySettings.antiAliasing}x");
                Debug.Log($"Texture Quality: {GetTextureQualityName(QualitySettings.masterTextureLimit)}");
                Debug.Log($"VSync Count: {QualitySettings.vSyncCount}");
            }
        }

        private static string GetTextureQualityName(int textureQuality)
        {
            return textureQuality switch
            {
                0 => "Full Resolution",
                1 => "Half Resolution", 
                2 => "Quarter Resolution",
                _ => "Unknown"
            };
        }

        [MenuItem("WhiskerKing/Quality/Performance Test", false, 31)]
        public static void RunPerformanceTest()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Performance test should be run in Play Mode for accurate results");
                return;
            }

            Debug.Log("=== Performance Test Results ===");
            
            for (int i = 0; i < QualitySettings.names.Length && i < 3; i++)
            {
                QualitySettings.SetQualityLevel(i, true);
                var stats = GetEstimatedPerformanceStats();
                
                Debug.Log($"{QualitySettings.names[i]} Quality:");
                Debug.Log($"  - Estimated Draw Calls: {stats.drawCalls}");
                Debug.Log($"  - Estimated Triangle Count: {stats.triangles}");
                Debug.Log($"  - Estimated Memory Usage: {stats.memoryMB}MB");
                Debug.Log($"  - Target Frame Rate: {Application.targetFrameRate}");
            }
        }

        private static (int drawCalls, int triangles, int memoryMB) GetEstimatedPerformanceStats()
        {
            // Estimate performance stats based on current quality settings
            int baseDrawCalls = 150;
            int baseTriangles = 50000;
            int baseMemoryMB = 256;

            // Adjust based on quality settings
            float qualityMultiplier = QualitySettings.lodBias;
            float shadowMultiplier = QualitySettings.shadows == ShadowQuality.Disable ? 0.8f : 1.2f;
            float aaMultiplier = 1.0f + (QualitySettings.antiAliasing / 8.0f);

            int drawCalls = (int)(baseDrawCalls * qualityMultiplier * shadowMultiplier);
            int triangles = (int)(baseTriangles * qualityMultiplier * shadowMultiplier);
            int memoryMB = (int)(baseMemoryMB * qualityMultiplier * aaMultiplier);

            return (drawCalls, triangles, memoryMB);
        }
    }

    /// <summary>
    /// Custom inspector window for Quality Settings management
    /// </summary>
    public class QualitySettingsInspector : EditorWindow
    {
        private QualityLevelConfiguration config;
        private Vector2 scrollPosition;
        private int selectedQualityLevel = 0;
        private bool showAdvancedSettings = false;

        private void OnEnable()
        {
            titleContent = new GUIContent("WK Quality Settings");
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            string configPath = "Assets/Settings/DefaultQualityConfig.asset";
            config = AssetDatabase.LoadAssetAtPath<QualityLevelConfiguration>(configPath);
        }

        private void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox(
                    "Quality configuration not found. Create default configuration first.",
                    MessageType.Warning);
                
                if (GUILayout.Button("Create Default Configuration"))
                {
                    QualityLevelConfiguration.SetupDefaultQualityLevelsMenuItem();
                    LoadConfiguration();
                }
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            DrawQualityLevelSelector();
            DrawCurrentQualitySettings();
            DrawActionButtons();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Whisker King Quality Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox(
                "Configure quality presets for different device specifications. " +
                "Settings are based on PRD performance targets.",
                MessageType.Info);
            
            EditorGUILayout.Space();
        }

        private void DrawQualityLevelSelector()
        {
            EditorGUILayout.LabelField("Quality Level", EditorStyles.boldLabel);
            
            string[] qualityNames = { "Low Quality", "Medium Quality", "High Quality" };
            selectedQualityLevel = GUILayout.SelectionGrid(selectedQualityLevel, qualityNames, 3);
            
            EditorGUILayout.Space();
        }

        private void DrawCurrentQualitySettings()
        {
            if (selectedQualityLevel < config.qualityLevels.Length)
            {
                var level = config.qualityLevels[selectedQualityLevel];
                
                EditorGUILayout.LabelField($"{level.levelName} Quality Settings", EditorStyles.boldLabel);
                
                // Performance targets
                EditorGUILayout.LabelField("Performance Targets", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Target Frame Rate: {level.targetFrameRate} FPS");
                EditorGUILayout.LabelField($"Memory Budget: {level.memoryBudgetMB} MB");
                EditorGUILayout.LabelField($"Texture Budget: {level.textureBudgetMB} MB");
                
                EditorGUILayout.Space();
                
                // Rendering settings
                EditorGUILayout.LabelField("Rendering Settings", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Render Scale: {level.renderScale:F1}x");
                EditorGUILayout.LabelField($"HDR: {(level.enableHDR ? "Enabled" : "Disabled")}");
                EditorGUILayout.LabelField($"Anti-Aliasing: {level.antiAliasing}");
                EditorGUILayout.LabelField($"Shadow Quality: {level.shadowQuality}");
                EditorGUILayout.LabelField($"Shadow Resolution: {level.shadowResolution}");
                
                EditorGUILayout.Space();
                
                showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings");
                if (showAdvancedSettings)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Pixel Light Count: {level.pixelLightCount}");
                    EditorGUILayout.LabelField($"Texture Quality: {GetTextureQualityName(level.textureQuality)}");
                    EditorGUILayout.LabelField($"LOD Bias: {level.lodBias:F1}");
                    EditorGUILayout.LabelField($"Particle Budget: {level.particleRaycastBudget}");
                    EditorGUILayout.LabelField($"Soft Particles: {(level.softParticles ? "Enabled" : "Disabled")}");
                    EditorGUILayout.LabelField($"VSync: {GetVSyncName(level.vSyncCount)}");
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply All Settings"))
            {
                config.ApplyQualitySettings();
                ShowNotification(new GUIContent("Quality settings applied!"));
            }
            
            if (GUILayout.Button("Validate Settings"))
            {
                config.ValidateQualitySettings();
                ShowNotification(new GUIContent("Validation complete - check console"));
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button($"Test {config.qualityLevels[selectedQualityLevel].levelName}"))
            {
                QualitySettings.SetQualityLevel(selectedQualityLevel, true);
                ShowNotification(new GUIContent($"Switched to {config.qualityLevels[selectedQualityLevel].levelName} quality"));
            }
            
            if (GUILayout.Button("Performance Test"))
            {
                QualitySettingsEditor.RunPerformanceTest();
                ShowNotification(new GUIContent("Performance test complete - check console"));
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private string GetTextureQualityName(int textureQuality)
        {
            return textureQuality switch
            {
                0 => "Full Resolution",
                1 => "Half Resolution",
                2 => "Quarter Resolution", 
                _ => "Unknown"
            };
        }

        private string GetVSyncName(int vSyncCount)
        {
            return vSyncCount switch
            {
                0 => "Disabled",
                1 => "Every V Blank",
                2 => "Every Second V Blank",
                _ => "Unknown"
            };
        }
    }
}
#endif
