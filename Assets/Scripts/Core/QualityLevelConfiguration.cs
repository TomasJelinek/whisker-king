using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhiskerKing.Core
{
    /// <summary>
    /// Quality level configuration for Whisker King
    /// Maps to Unity's built-in Quality Settings with game-specific parameters
    /// Based on PRD performance targets and device specifications
    /// </summary>
    [CreateAssetMenu(fileName = "QualityLevelConfiguration", menuName = "WhiskerKing/Quality Level Configuration")]
    public class QualityLevelConfiguration : ScriptableObject
    {
        [Header("Quality Level Settings")]
        public QualityLevelData[] qualityLevels = new QualityLevelData[3];

        [System.Serializable]
        public class QualityLevelData
        {
            [Header("General Settings")]
            public string levelName = "Medium";
            public QualityLevel level = QualityLevel.Medium;
            
            [Header("Performance Targets")]
            public int targetFrameRate = 60;
            public int memoryBudgetMB = 768;
            public int textureBudgetMB = 384;
            
            [Header("Rendering Settings")]
            [Range(0.5f, 2.0f)] public float renderScale = 1.0f;
            public bool enableHDR = true;
            public AntiAliasing antiAliasing = AntiAliasing.None;
            public int anisotropicFiltering = 2;
            public ShadowQuality shadowQuality = ShadowQuality.All;
            public ShadowResolution shadowResolution = ShadowResolution.Medium;
            public float shadowDistance = 50f;
            public int shadowCascades = 2;
            
            [Header("Lighting")]
            public int pixelLightCount = 4;
            public bool softParticles = true;
            public bool realtimeReflectionProbes = true;
            public bool billboardsFaceCameraPosition = true;
            
            [Header("Texture Settings")]
            public int textureQuality = 0; // 0 = Full Res, 1 = Half Res, 2 = Quarter Res
            [Range(0, 4)] public float lodBias = 1f;
            [Range(0, 7)] public int maximumLODLevel = 0;
            
            [Header("Particle Settings")]
            public int particleRaycastBudget = 1024;
            public bool softVegetation = true;
            
            [Header("Physics")]
            public bool asyncUpload = true;
            [Range(1, 16)] public int asyncUploadTimeSlice = 2;
            [Range(1, 32)] public int asyncUploadBufferSize = 16;
            
            [Header("VSync and Frame Rate")]
            public int vSyncCount = 0; // 0 = Don't Sync, 1 = Every V Blank, 2 = Every Second V Blank
            public bool enableFrameRateLimit = true;
            
            [Header("URP Specific")]
            public UniversalRenderPipelineAsset urpAsset;
            public bool enablePostProcessing = true;
            public bool enableBloom = false;
            public bool enableVignette = true;
            public bool enableMotionBlur = false;
        }

        public enum QualityLevel
        {
            Low = 0,
            Medium = 1,  
            High = 2
        }

        public enum AntiAliasing
        {
            None = 0,
            MSAA2x = 2,
            MSAA4x = 4,
            MSAA8x = 8
        }

        private void Reset()
        {
            // Initialize with default PRD-compliant settings
            SetupDefaultQualityLevels();
        }

        private void SetupDefaultQualityLevels()
        {
            qualityLevels = new QualityLevelData[3];

            // Low Quality - Minimum spec devices (Snapdragon 660, A10 Fusion)
            qualityLevels[0] = new QualityLevelData
            {
                levelName = "Low",
                level = QualityLevel.Low,
                targetFrameRate = 30,
                memoryBudgetMB = 512,
                textureBudgetMB = 256,
                renderScale = 0.8f,
                enableHDR = false,
                antiAliasing = AntiAliasing.None,
                anisotropicFiltering = 0,
                shadowQuality = ShadowQuality.Disable,
                shadowResolution = ShadowResolution.Low,
                shadowDistance = 25f,
                shadowCascades = 1,
                pixelLightCount = 1,
                softParticles = false,
                realtimeReflectionProbes = false,
                billboardsFaceCameraPosition = false,
                textureQuality = 2, // Quarter resolution
                lodBias = 0.7f,
                maximumLODLevel = 1,
                particleRaycastBudget = 256,
                softVegetation = false,
                vSyncCount = 0,
                enableBloom = false,
                enableVignette = false,
                enableMotionBlur = false
            };

            // Medium Quality - Recommended spec devices (Snapdragon 855, A12 Bionic)
            qualityLevels[1] = new QualityLevelData
            {
                levelName = "Medium",
                level = QualityLevel.Medium,
                targetFrameRate = 60,
                memoryBudgetMB = 768,
                textureBudgetMB = 384,
                renderScale = 1.0f,
                enableHDR = true,
                antiAliasing = AntiAliasing.MSAA2x,
                anisotropicFiltering = 2,
                shadowQuality = ShadowQuality.All,
                shadowResolution = ShadowResolution.Medium,
                shadowDistance = 50f,
                shadowCascades = 2,
                pixelLightCount = 4,
                softParticles = true,
                realtimeReflectionProbes = true,
                billboardsFaceCameraPosition = true,
                textureQuality = 1, // Half resolution
                lodBias = 1.0f,
                maximumLODLevel = 0,
                particleRaycastBudget = 1024,
                softVegetation = true,
                vSyncCount = 1,
                enableBloom = false,
                enableVignette = true,
                enableMotionBlur = false
            };

            // High Quality - High-end devices (Snapdragon 8 Gen 1, A14 Bionic+)
            qualityLevels[2] = new QualityLevelData
            {
                levelName = "High",
                level = QualityLevel.High,
                targetFrameRate = 60,
                memoryBudgetMB = 1024,
                textureBudgetMB = 512,
                renderScale = 1.0f,
                enableHDR = true,
                antiAliasing = AntiAliasing.MSAA4x,
                anisotropicFiltering = 4,
                shadowQuality = ShadowQuality.All,
                shadowResolution = ShadowResolution.High,
                shadowDistance = 100f,
                shadowCascades = 4,
                pixelLightCount = 8,
                softParticles = true,
                realtimeReflectionProbes = true,
                billboardsFaceCameraPosition = true,
                textureQuality = 0, // Full resolution
                lodBias = 1.2f,
                maximumLODLevel = 0,
                particleRaycastBudget = 2048,
                softVegetation = true,
                vSyncCount = 1,
                enableBloom = true,
                enableVignette = true,
                enableMotionBlur = false // Disabled for motion sickness concerns
            };
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Quality Settings")]
        public void ApplyQualitySettings()
        {
            ConfigureUnityQualitySettings();
            Debug.Log("Applied Whisker King quality settings to Unity");
        }

        [ContextMenu("Validate Quality Settings")]
        public void ValidateQualitySettings()
        {
            for (int i = 0; i < qualityLevels.Length; i++)
            {
                var level = qualityLevels[i];
                Debug.Log($"=== {level.levelName} Quality Validation ===");
                Debug.Log($"Target FPS: {level.targetFrameRate}");
                Debug.Log($"Memory Budget: {level.memoryBudgetMB}MB");
                Debug.Log($"Render Scale: {level.renderScale:F1}x");
                Debug.Log($"Shadows: {level.shadowQuality} ({level.shadowResolution})");
                Debug.Log($"Anti-Aliasing: {level.antiAliasing}");
                Debug.Log($"HDR: {level.enableHDR}");
                
                // Validate memory budget
                int estimatedMemory = EstimateMemoryUsage(level);
                if (estimatedMemory > level.memoryBudgetMB)
                {
                    Debug.LogWarning($"Estimated memory ({estimatedMemory}MB) exceeds budget ({level.memoryBudgetMB}MB)");
                }
                else
                {
                    Debug.Log($"Memory usage within budget: {estimatedMemory}MB / {level.memoryBudgetMB}MB");
                }
            }
        }

        private void ConfigureUnityQualitySettings()
        {
            // Set number of quality levels
            while (QualitySettings.names.Length < qualityLevels.Length)
            {
                QualitySettings.IncreaseLevel();
            }

            // Configure each quality level
            for (int i = 0; i < qualityLevels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                ApplyQualityLevel(qualityLevels[i]);
            }

            // Set default quality level to Medium
            QualitySettings.SetQualityLevel(1, true);
        }

        private void ApplyQualityLevel(QualityLevelData level)
        {
            // Rendering settings
            QualitySettings.renderPipeline = level.urpAsset;
            QualitySettings.pixelLightCount = level.pixelLightCount;
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)level.anisotropicFiltering;
            QualitySettings.antiAliasing = (int)level.antiAliasing;

            // Shadow settings
            QualitySettings.shadows = level.shadowQuality;
            QualitySettings.shadowResolution = level.shadowResolution;
            QualitySettings.shadowDistance = level.shadowDistance;
            QualitySettings.shadowCascades = level.shadowCascades;

            // Texture settings
            QualitySettings.masterTextureLimit = level.textureQuality;
            QualitySettings.lodBias = level.lodBias;
            QualitySettings.maximumLODLevel = level.maximumLODLevel;

            // Particle settings
            QualitySettings.particleRaycastBudget = level.particleRaycastBudget;
            QualitySettings.softParticles = level.softParticles;
            QualitySettings.softVegetation = level.softVegetation;

            // Other settings
            QualitySettings.realtimeReflectionProbes = level.realtimeReflectionProbes;
            QualitySettings.billboardsFaceCameraPosition = level.billboardsFaceCameraPosition;
            QualitySettings.vSyncCount = level.vSyncCount;
            QualitySettings.asyncUploadTimeSlice = level.asyncUploadTimeSlice;
            QualitySettings.asyncUploadBufferSize = level.asyncUploadBufferSize;

            // Set target frame rate
            if (level.enableFrameRateLimit)
            {
                Application.targetFrameRate = level.targetFrameRate;
            }
            else
            {
                Application.targetFrameRate = -1;
            }
        }

        private int EstimateMemoryUsage(QualityLevelData level)
        {
            // Rough estimation of memory usage based on settings
            int baseMemory = 128; // Base Unity overhead
            int textureMemory = level.textureBudgetMB;
            int renderingMemory = (int)(100 * level.renderScale * level.renderScale);
            int shadowMemory = level.shadowQuality == ShadowQuality.Disable ? 0 : 
                              (int)level.shadowResolution * level.shadowCascades / 4;
            int particleMemory = level.particleRaycastBudget / 32;

            return baseMemory + textureMemory + renderingMemory + shadowMemory + particleMemory;
        }

        [MenuItem("WhiskerKing/Quality/Setup Default Quality Levels")]
        public static void SetupDefaultQualityLevelsMenuItem()
        {
            var config = CreateInstance<QualityLevelConfiguration>();
            config.SetupDefaultQualityLevels();
            config.ApplyQualitySettings();
            
            // Save as asset
            string path = "Assets/Settings/DefaultQualityConfig.asset";
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"Created default quality configuration at {path}");
        }
#endif
    }
}
