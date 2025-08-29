using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WhiskerKing.Core;

namespace WhiskerKing.Performance
{
    /// <summary>
    /// Comprehensive Performance Manager for Whisker King
    /// Implements dynamic quality adjustment, performance monitoring, and mobile optimization
    /// Maintains PRD-compliant performance targets across all platforms
    /// </summary>
    public class PerformanceManager : MonoBehaviour
    {
        [System.Serializable]
        public class PerformanceBudget
        {
            [Header("Frame Rate Targets")]
            public int targetFrameRate = 60;
            public int minimumFrameRate = 30;
            public int maximumFrameRate = 120;

            [Header("Rendering Budgets")]
            public int maxDrawCalls = 300;
            public int maxTriangles = 50000;
            public int maxVertices = 75000;
            public int maxSetPassCalls = 50;

            [Header("Memory Budgets (MB)")]
            public float maxTextureMemory = 128f;
            public float maxMeshMemory = 64f;
            public float maxAudioMemory = 32f;
            public float maxTotalMemory = 512f;

            [Header("Quality Thresholds")]
            public float qualityUpgradeThreshold = 0.9f;   // 90% performance headroom
            public float qualityDowngradeThreshold = 0.7f; // 70% performance threshold
            public float criticalPerformanceThreshold = 0.5f; // 50% critical threshold
        }

        [System.Serializable]
        public class PerformanceMetrics
        {
            public float currentFrameRate;
            public float averageFrameRate;
            public float minFrameRate;
            public float maxFrameRate;
            public int currentDrawCalls;
            public int currentTriangles;
            public int currentVertices;
            public int currentSetPassCalls;
            public float currentTextureMemory;
            public float currentMeshMemory;
            public float currentAudioMemory;
            public float totalMemoryUsage;
            public float cpuFrameTime;
            public float gpuFrameTime;
            public float renderThreadTime;
            public bool isPerformanceCritical;
        }

        [System.Serializable]
        public class QualitySettings
        {
            public string qualityName = "Medium";
            public float renderScale = 1.0f;
            public int msaaLevel = 1;
            public bool hdrEnabled = false;
            public int shadowCascades = 2;
            public float shadowDistance = 50f;
            public ShadowResolution shadowResolution = ShadowResolution._2048;
            public bool softShadows = true;
            public int maxLights = 8;
            public bool reflectionProbes = true;
            public bool bloomEnabled = true;
            public int textureQuality = 1;
            public int lodBias = 1;
            public float particleRaycastBudget = 64f;
            public bool dynamicBatching = true;
            public bool gpuInstancing = true;
        }

        // Singleton pattern
        private static PerformanceManager instance;
        public static PerformanceManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<PerformanceManager>();
                    if (instance == null)
                    {
                        GameObject performanceManagerGO = new GameObject("PerformanceManager");
                        instance = performanceManagerGO.AddComponent<PerformanceManager>();
                        DontDestroyOnLoad(performanceManagerGO);
                    }
                }
                return instance;
            }
        }

        [Header("Performance Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool autoQualityAdjustment = true;
        [SerializeField] private float performanceUpdateInterval = 1f;

        [Header("Performance Budgets")]
        [SerializeField] private PerformanceBudget mobileBudget = new PerformanceBudget();
        [SerializeField] private PerformanceBudget desktopBudget = new PerformanceBudget();
        [SerializeField] private PerformanceBudget webglBudget = new PerformanceBudget();

        [Header("Quality Presets")]
        [SerializeField] private QualitySettings lowQuality = new QualitySettings { qualityName = "Low" };
        [SerializeField] private QualitySettings mediumQuality = new QualitySettings { qualityName = "Medium" };
        [SerializeField] private QualitySettings highQuality = new QualitySettings { qualityName = "High" };

        [Header("Dynamic Resolution")]
        [SerializeField] private bool enableDynamicResolution = true;
        [SerializeField] private float minRenderScale = 0.5f;
        [SerializeField] private float maxRenderScale = 1.0f;
        [SerializeField] private float resolutionAdjustmentSpeed = 0.1f;

        [Header("Performance Monitoring")]
        [SerializeField] private int frameRateHistorySize = 60;
        [SerializeField] private float criticalPerformanceActionDelay = 2f;
        [SerializeField] private float qualityAdjustmentCooldown = 5f;

        // Current state
        private PerformanceBudget currentBudget;
        private QualitySettings currentQualitySettings;
        private PerformanceMetrics currentMetrics = new PerformanceMetrics();
        private Queue<float> frameRateHistory = new Queue<float>();
        private bool isInitialized = false;

        // Performance tracking
        private float lastPerformanceUpdate;
        private float lastQualityAdjustment;
        private int consecutiveLowFrames = 0;
        private int consecutiveHighFrames = 0;

        // Component references
        private UniversalRenderPipelineAsset urpAsset;
        private Camera mainCamera;
        private QualityManager qualityManager;

        // Configuration cache
        private PerformanceData performanceConfig;

        // Events
        public System.Action<PerformanceMetrics> OnPerformanceUpdated;
        public System.Action<QualitySettings> OnQualityChanged;
        public System.Action OnCriticalPerformance;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePerformanceManager();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            LoadConfiguration();
            SetupPerformanceSettings();
            StartCoroutine(PerformanceMonitoringCoroutine());
        }

        private void Update()
        {
            if (isInitialized)
            {
                UpdatePerformanceMetrics();
                
                if (enableDynamicResolution)
                {
                    UpdateDynamicResolution();
                }

                if (debugMode)
                {
                    UpdateDebugInfo();
                }
            }
        }

        #endregion

        #region Initialization

        private void InitializePerformanceManager()
        {
            // Get component references
            mainCamera = Camera.main;
            qualityManager = QualityManager.Instance;
            
            // Get URP asset
            if (GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset)
            {
                urpAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
            }

            // Set initial performance budget based on platform
            SetPerformanceBudgetForPlatform();

            // Initialize frame rate history
            frameRateHistory.Clear();

            isInitialized = true;

            Debug.Log("PerformanceManager initialized");
        }

        private void LoadConfiguration()
        {
            if (useGameConfiguration && GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                performanceConfig = GameConfiguration.Instance.Config.performance;
                ApplyConfiguration();
                Debug.Log("PerformanceManager: Configuration loaded from GameConfig");
            }
            else
            {
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            if (performanceConfig == null) return;

            // Apply performance targets from configuration
            mobileBudget.targetFrameRate = (int)performanceConfig.targetFrameRate;
            mobileBudget.minimumFrameRate = (int)performanceConfig.minimumFrameRate;
            
            // Apply memory budgets
            mobileBudget.maxTotalMemory = performanceConfig.memoryBudget;
            desktopBudget.maxTotalMemory = performanceConfig.memoryBudget * 2f;
            webglBudget.maxTotalMemory = performanceConfig.memoryBudget * 0.8f;

            // Apply rendering settings
            autoQualityAdjustment = performanceConfig.dynamicQualityEnabled;
            enableDynamicResolution = performanceConfig.dynamicResolutionEnabled;
        }

        private void UseDefaultConfiguration()
        {
            // Use default performance settings
            SetPerformanceBudgetForPlatform();
        }

        private void SetPerformanceBudgetForPlatform()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    currentBudget = mobileBudget;
                    currentQualitySettings = mediumQuality;
                    break;
                    
                case RuntimePlatform.WebGLPlayer:
                    currentBudget = webglBudget;
                    currentQualitySettings = lowQuality;
                    break;
                    
                default:
                    currentBudget = desktopBudget;
                    currentQualitySettings = highQuality;
                    break;
            }

            // Apply initial quality settings
            ApplyQualitySettings(currentQualitySettings);
        }

        private void SetupPerformanceSettings()
        {
            // Set target frame rate
            Application.targetFrameRate = currentBudget.targetFrameRate;
            
            // Configure VSync based on platform
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                QualitySettings.vSyncCount = 1; // Always use VSync on WebGL
            }
            else
            {
                QualitySettings.vSyncCount = currentBudget.targetFrameRate > 60 ? 0 : 1;
            }

            // Set initial render scale
            if (urpAsset != null)
            {
                urpAsset.renderScale = currentQualitySettings.renderScale;
            }

            // Configure shadow settings for mobile optimization
            ConfigureShadowSettings();
        }

        private void ConfigureShadowSettings()
        {
            if (urpAsset != null)
            {
                // PRD requirement: max 2 cascades, 50m distance
                urpAsset.shadowCascadeCount = Mathf.Min(currentQualitySettings.shadowCascades, 2);
                urpAsset.shadowDistance = Mathf.Min(currentQualitySettings.shadowDistance, 50f);
                urpAsset.mainLightShadowmapResolution = (int)currentQualitySettings.shadowResolution;
                
                // Mobile-specific shadow optimizations
                if (Application.platform == RuntimePlatform.Android || 
                    Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    urpAsset.shadowCascadeCount = 1; // Single cascade for mobile
                    urpAsset.shadowDistance = 25f;   // Reduced distance for mobile
                }
            }
        }

        #endregion

        #region Performance Monitoring

        private IEnumerator PerformanceMonitoringCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(performanceUpdateInterval);
                
                if (isInitialized)
                {
                    AnalyzePerformance();
                    
                    if (autoQualityAdjustment)
                    {
                        AutoAdjustQuality();
                    }
                }
            }
        }

        private void UpdatePerformanceMetrics()
        {
            // Frame rate metrics
            float currentFPS = 1f / Time.unscaledDeltaTime;
            currentMetrics.currentFrameRate = currentFPS;

            // Update frame rate history
            frameRateHistory.Enqueue(currentFPS);
            if (frameRateHistory.Count > frameRateHistorySize)
            {
                frameRateHistory.Dequeue();
            }

            // Calculate average, min, max frame rates
            if (frameRateHistory.Count > 0)
            {
                currentMetrics.averageFrameRate = frameRateHistory.Average();
                currentMetrics.minFrameRate = frameRateHistory.Min();
                currentMetrics.maxFrameRate = frameRateHistory.Max();
            }

            // Rendering metrics (approximate values for demonstration)
            currentMetrics.currentDrawCalls = UnityEngine.Profiling.Profiler.GetRuntimeMemorySize(null) / 1000; // Placeholder
            currentMetrics.currentTriangles = Mathf.RoundToInt(currentMetrics.currentDrawCalls * 100f); // Approximate
            currentMetrics.currentVertices = currentMetrics.currentTriangles * 3;

            // Memory metrics
            UpdateMemoryMetrics();

            // Performance status
            float performanceRatio = currentMetrics.averageFrameRate / currentBudget.targetFrameRate;
            currentMetrics.isPerformanceCritical = performanceRatio < currentBudget.criticalPerformanceThreshold;

            // Trigger events
            OnPerformanceUpdated?.Invoke(currentMetrics);

            if (currentMetrics.isPerformanceCritical)
            {
                OnCriticalPerformance?.Invoke();
            }
        }

        private void UpdateMemoryMetrics()
        {
            // Get approximate memory usage
            long totalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.Render);
            currentMetrics.totalMemoryUsage = totalMemory / (1024f * 1024f); // Convert to MB

            // Estimate breakdown (approximation)
            currentMetrics.currentTextureMemory = currentMetrics.totalMemoryUsage * 0.6f;
            currentMetrics.currentMeshMemory = currentMetrics.totalMemoryUsage * 0.2f;
            currentMetrics.currentAudioMemory = currentMetrics.totalMemoryUsage * 0.1f;
        }

        private void AnalyzePerformance()
        {
            float performanceRatio = currentMetrics.averageFrameRate / currentBudget.targetFrameRate;

            // Track consecutive performance issues
            if (performanceRatio < currentBudget.qualityDowngradeThreshold)
            {
                consecutiveLowFrames++;
                consecutiveHighFrames = 0;
            }
            else if (performanceRatio > currentBudget.qualityUpgradeThreshold)
            {
                consecutiveHighFrames++;
                consecutiveLowFrames = 0;
            }
            else
            {
                consecutiveLowFrames = 0;
                consecutiveHighFrames = 0;
            }
        }

        #endregion

        #region Quality Management

        private void AutoAdjustQuality()
        {
            // Check cooldown
            if (Time.time - lastQualityAdjustment < qualityAdjustmentCooldown)
            {
                return;
            }

            bool shouldDowngrade = consecutiveLowFrames >= 3;
            bool shouldUpgrade = consecutiveHighFrames >= 5;

            if (shouldDowngrade)
            {
                DowngradeQuality();
            }
            else if (shouldUpgrade)
            {
                UpgradeQuality();
            }
        }

        private void DowngradeQuality()
        {
            if (currentQualitySettings == highQuality)
            {
                ApplyQualitySettings(mediumQuality);
                Debug.Log("Performance: Downgraded to Medium quality");
            }
            else if (currentQualitySettings == mediumQuality)
            {
                ApplyQualitySettings(lowQuality);
                Debug.Log("Performance: Downgraded to Low quality");
            }
            else
            {
                // Already at lowest quality, reduce render scale
                if (enableDynamicResolution && urpAsset != null)
                {
                    float newScale = Mathf.Max(urpAsset.renderScale - 0.1f, minRenderScale);
                    urpAsset.renderScale = newScale;
                    Debug.Log($"Performance: Reduced render scale to {newScale:F2}");
                }
            }

            lastQualityAdjustment = Time.time;
            consecutiveLowFrames = 0;
        }

        private void UpgradeQuality()
        {
            // First, try to increase render scale if it's below maximum
            if (enableDynamicResolution && urpAsset != null && urpAsset.renderScale < maxRenderScale)
            {
                float newScale = Mathf.Min(urpAsset.renderScale + 0.05f, maxRenderScale);
                urpAsset.renderScale = newScale;
                Debug.Log($"Performance: Increased render scale to {newScale:F2}");
            }
            else if (currentQualitySettings == lowQuality)
            {
                ApplyQualitySettings(mediumQuality);
                Debug.Log("Performance: Upgraded to Medium quality");
            }
            else if (currentQualitySettings == mediumQuality)
            {
                ApplyQualitySettings(highQuality);
                Debug.Log("Performance: Upgraded to High quality");
            }

            lastQualityAdjustment = Time.time;
            consecutiveHighFrames = 0;
        }

        private void ApplyQualitySettings(QualitySettings settings)
        {
            currentQualitySettings = settings;

            if (urpAsset != null)
            {
                // Apply URP settings
                urpAsset.renderScale = settings.renderScale;
                urpAsset.msaaSampleCount = settings.msaaLevel;
                urpAsset.supportsHDR = settings.hdrEnabled;
                urpAsset.shadowCascadeCount = Mathf.Min(settings.shadowCascades, 2); // PRD limit
                urpAsset.shadowDistance = Mathf.Min(settings.shadowDistance, 50f);   // PRD limit
                urpAsset.mainLightShadowmapResolution = (int)settings.shadowResolution;
                
                // Additional light settings
                urpAsset.maxAdditionalLightsCount = settings.maxLights;
                urpAsset.supportsAdditionalLightShadows = settings.softShadows && settings.maxLights > 0;
            }

            // Apply Unity quality settings
            QualitySettings.masterTextureLimit = settings.textureQuality;
            QualitySettings.lodBias = settings.lodBias;
            QualitySettings.particleRaycastBudget = (int)settings.particleRaycastBudget;

            OnQualityChanged?.Invoke(settings);
        }

        #endregion

        #region Dynamic Resolution

        private void UpdateDynamicResolution()
        {
            if (!enableDynamicResolution || urpAsset == null) return;

            float performanceRatio = currentMetrics.currentFrameRate / currentBudget.targetFrameRate;
            float targetScale = urpAsset.renderScale;

            if (performanceRatio < 0.9f) // Below 90% of target FPS
            {
                targetScale = Mathf.Max(targetScale - resolutionAdjustmentSpeed * Time.deltaTime, minRenderScale);
            }
            else if (performanceRatio > 1.1f) // Above 110% of target FPS
            {
                targetScale = Mathf.Min(targetScale + resolutionAdjustmentSpeed * Time.deltaTime, maxRenderScale);
            }

            if (Mathf.Abs(targetScale - urpAsset.renderScale) > 0.01f)
            {
                urpAsset.renderScale = targetScale;
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current performance metrics
        /// </summary>
        public PerformanceMetrics GetPerformanceMetrics()
        {
            return currentMetrics;
        }

        /// <summary>
        /// Get current performance budget
        /// </summary>
        public PerformanceBudget GetPerformanceBudget()
        {
            return currentBudget;
        }

        /// <summary>
        /// Get current quality settings
        /// </summary>
        public QualitySettings GetCurrentQualitySettings()
        {
            return currentQualitySettings;
        }

        /// <summary>
        /// Set quality level manually
        /// </summary>
        public void SetQualityLevel(int level)
        {
            QualitySettings settings = level switch
            {
                0 => lowQuality,
                1 => mediumQuality,
                2 => highQuality,
                _ => mediumQuality
            };

            ApplyQualitySettings(settings);
        }

        /// <summary>
        /// Enable/disable auto quality adjustment
        /// </summary>
        public void SetAutoQualityAdjustment(bool enabled)
        {
            autoQualityAdjustment = enabled;
        }

        /// <summary>
        /// Set target frame rate
        /// </summary>
        public void SetTargetFrameRate(int frameRate)
        {
            currentBudget.targetFrameRate = frameRate;
            Application.targetFrameRate = frameRate;
        }

        /// <summary>
        /// Force performance analysis
        /// </summary>
        public void ForcePerformanceAnalysis()
        {
            AnalyzePerformance();
            if (autoQualityAdjustment)
            {
                AutoAdjustQuality();
            }
        }

        /// <summary>
        /// Check if performance is within budget
        /// </summary>
        public bool IsPerformanceWithinBudget()
        {
            return currentMetrics.currentDrawCalls <= currentBudget.maxDrawCalls &&
                   currentMetrics.currentTriangles <= currentBudget.maxTriangles &&
                   currentMetrics.totalMemoryUsage <= currentBudget.maxTotalMemory &&
                   currentMetrics.averageFrameRate >= currentBudget.minimumFrameRate;
        }

        /// <summary>
        /// Get performance efficiency ratio (0-1)
        /// </summary>
        public float GetPerformanceEfficiency()
        {
            float frameRateRatio = currentMetrics.averageFrameRate / currentBudget.targetFrameRate;
            float drawCallRatio = 1f - (float)currentMetrics.currentDrawCalls / currentBudget.maxDrawCalls;
            float memoryRatio = 1f - currentMetrics.totalMemoryUsage / currentBudget.maxTotalMemory;

            return (frameRateRatio + drawCallRatio + memoryRatio) / 3f;
        }

        /// <summary>
        /// Reset performance history
        /// </summary>
        public void ResetPerformanceHistory()
        {
            frameRateHistory.Clear();
            consecutiveLowFrames = 0;
            consecutiveHighFrames = 0;
            lastQualityAdjustment = 0f;
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
                GUILayout.BeginArea(new Rect(10, 350, 350, 400));
                
                GUILayout.Label("=== PERFORMANCE MANAGER ===");
                GUILayout.Label($"Quality: {currentQualitySettings.qualityName}");
                GUILayout.Label($"Render Scale: {(urpAsset != null ? urpAsset.renderScale.ToString("F2") : "N/A")}");
                GUILayout.Label($"Target FPS: {currentBudget.targetFrameRate}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== CURRENT METRICS ===");
                GUILayout.Label($"FPS: {currentMetrics.currentFrameRate:F1}");
                GUILayout.Label($"Avg FPS: {currentMetrics.averageFrameRate:F1}");
                GUILayout.Label($"Min FPS: {currentMetrics.minFrameRate:F1}");
                GUILayout.Label($"Draw Calls: {currentMetrics.currentDrawCalls}");
                GUILayout.Label($"Triangles: {currentMetrics.currentTriangles:N0}");
                GUILayout.Label($"Memory: {currentMetrics.totalMemoryUsage:F1} MB");
                
                GUILayout.Space(5);
                GUILayout.Label("=== PERFORMANCE STATUS ===");
                float efficiency = GetPerformanceEfficiency();
                GUILayout.Label($"Efficiency: {efficiency * 100:F1}%");
                GUILayout.Label($"Within Budget: {IsPerformanceWithinBudget()}");
                GUILayout.Label($"Critical: {currentMetrics.isPerformanceCritical}");
                GUILayout.Label($"Auto Quality: {autoQualityAdjustment}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== QUALITY CONTROL ===");
                GUILayout.Label($"Low Frames: {consecutiveLowFrames}");
                GUILayout.Label($"High Frames: {consecutiveHighFrames}");
                
                if (GUILayout.Button("Force Low Quality"))
                {
                    SetQualityLevel(0);
                }
                
                if (GUILayout.Button("Force Medium Quality"))
                {
                    SetQualityLevel(1);
                }
                
                if (GUILayout.Button("Force High Quality"))
                {
                    SetQualityLevel(2);
                }
                
                if (GUILayout.Button("Toggle Auto Quality"))
                {
                    SetAutoQualityAdjustment(!autoQualityAdjustment);
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }
}
