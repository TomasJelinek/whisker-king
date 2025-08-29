using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using WhiskerKing.Core;

namespace WhiskerKing.Performance
{
    /// <summary>
    /// Performance Benchmark System for Whisker King
    /// Monitors rendering performance, enforces budgets, and provides automatic quality adjustment
    /// Tracks draw calls, triangles, textures, and maintains PRD-compliant performance targets
    /// </summary>
    public class PerformanceBenchmark : MonoBehaviour
    {
        [System.Serializable]
        public class PerformanceBudgets
        {
            [Header("Rendering Budgets")]
            public int maxDrawCalls = 300;
            public int maxTriangles = 50000;
            public int maxVertices = 75000;
            public int maxSetPassCalls = 50;
            public int maxBatches = 100;

            [Header("Memory Budgets")]
            public int maxTextureMemoryMB = 128;
            public int maxMeshMemoryMB = 64;
            public float maxTotalMemoryMB = 512f;

            [Header("Frame Time Budgets (ms)")]
            public float maxFrameTime = 16.67f;     // 60 FPS = 16.67ms
            public float maxCPUTime = 10f;
            public float maxGPUTime = 12f;
            public float maxRenderTime = 8f;

            [Header("Budget Thresholds")]
            public float warningThreshold = 0.8f;   // 80%
            public float criticalThreshold = 0.9f; // 90%
            public float emergencyThreshold = 0.95f; // 95%
        }

        [System.Serializable]
        public class PerformanceSnapshot
        {
            [Header("Frame Statistics")]
            public float frameTime;
            public float deltaTime;
            public float fps;
            public float averageFPS;
            public float minFPS;
            public float maxFPS;

            [Header("Rendering Statistics")]
            public int drawCalls;
            public int triangles;
            public int vertices;
            public int setPassCalls;
            public int batches;
            public int shadowCasters;

            [Header("Memory Statistics")]
            public float totalMemoryMB;
            public float textureMemoryMB;
            public float meshMemoryMB;
            public float audioMemoryMB;

            [Header("Timing Breakdown")]
            public float cpuMainThreadTime;
            public float cpuRenderThreadTime;
            public float gpuTime;
            public float vsyncTime;

            [Header("Quality Metrics")]
            public float performanceScore; // 0-1 where 1 is perfect performance
            public bool isWithinBudget;
            public bool hasWarningLevel;
            public bool hasCriticalLevel;
            public bool hasEmergencyLevel;

            public System.DateTime timestamp;
        }

        [System.Serializable]
        public class BenchmarkSettings
        {
            [Header("Benchmark Configuration")]
            public bool enableContinuousBenchmarking = true;
            public float benchmarkInterval = 0.5f;
            public int historySize = 120; // 1 minute at 0.5s intervals
            public bool enablePredictiveAdjustment = true;

            [Header("Automatic Quality Adjustment")]
            public bool enableAutoQualityAdjustment = true;
            public int consecutiveThresholdFrames = 5;
            public float qualityAdjustmentCooldown = 3f;
            public float aggressiveAdjustmentThreshold = 0.5f;

            [Header("Platform Optimization")]
            public bool adaptBudgetsForPlatform = true;
            public float mobileBudgetMultiplier = 0.7f;
            public float webglBudgetMultiplier = 0.6f;
            public float desktopBudgetMultiplier = 1.5f;
        }

        // Singleton pattern
        private static PerformanceBenchmark instance;
        public static PerformanceBenchmark Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<PerformanceBenchmark>();
                    if (instance == null)
                    {
                        GameObject benchmarkGO = new GameObject("PerformanceBenchmark");
                        instance = benchmarkGO.AddComponent<PerformanceBenchmark>();
                        DontDestroyOnLoad(benchmarkGO);
                    }
                }
                return instance;
            }
        }

        [Header("Benchmark Configuration")]
        [SerializeField] private PerformanceBudgets budgets = new PerformanceBudgets();
        [SerializeField] private BenchmarkSettings settings = new BenchmarkSettings();
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool enableDetailedProfiling = true;

        [Header("Quality Management")]
        [SerializeField] private bool integrateWithQualityManager = true;
        [SerializeField] private bool integrateWithPerformanceManager = true;

        // Performance tracking
        private List<PerformanceSnapshot> performanceHistory = new List<PerformanceSnapshot>();
        private PerformanceSnapshot currentSnapshot = new PerformanceSnapshot();
        private Queue<float> fpsHistory = new Queue<float>();

        // Automatic adjustment tracking
        private int consecutiveWarningFrames = 0;
        private int consecutiveCriticalFrames = 0;
        private float lastQualityAdjustment = 0f;

        // Component references
        private PerformanceManager performanceManager;
        private QualityManager qualityManager;

        // Benchmark state
        private bool isBenchmarking = false;
        private float lastBenchmarkTime = 0f;
        private Coroutine benchmarkCoroutine;

        // Platform-adjusted budgets
        private PerformanceBudgets adjustedBudgets;

        // Events
        public System.Action<PerformanceSnapshot> OnPerformanceSnapshotTaken;
        public System.Action<PerformanceBudgets> OnBudgetExceeded;
        public System.Action<float> OnPerformanceScoreUpdated;
        public System.Action OnQualityAdjustmentTriggered;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeBenchmark();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            SetupBenchmarking();
            StartBenchmarking();
        }

        private void Update()
        {
            if (isBenchmarking && Time.time - lastBenchmarkTime >= settings.benchmarkInterval)
            {
                TakePerformanceSnapshot();
                lastBenchmarkTime = Time.time;
            }

            UpdatePerformanceTracking();

            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDestroy()
        {
            StopBenchmarking();
        }

        #endregion

        #region Initialization

        private void InitializeBenchmark()
        {
            // Get component references
            if (integrateWithPerformanceManager)
            {
                performanceManager = PerformanceManager.Instance;
            }

            if (integrateWithQualityManager)
            {
                qualityManager = QualityManager.Instance;
            }

            // Apply platform-specific budget adjustments
            ApplyPlatformBudgetAdjustments();

            Debug.Log("PerformanceBenchmark initialized");
        }

        private void SetupBenchmarking()
        {
            // Initialize performance history
            performanceHistory.Clear();
            fpsHistory.Clear();

            // Set up Unity profiler if available
            if (enableDetailedProfiling)
            {
                UnityEngine.Profiling.Profiler.enabled = true;
            }
        }

        private void ApplyPlatformBudgetAdjustments()
        {
            adjustedBudgets = new PerformanceBudgets
            {
                maxDrawCalls = budgets.maxDrawCalls,
                maxTriangles = budgets.maxTriangles,
                maxVertices = budgets.maxVertices,
                maxSetPassCalls = budgets.maxSetPassCalls,
                maxBatches = budgets.maxBatches,
                maxTextureMemoryMB = budgets.maxTextureMemoryMB,
                maxMeshMemoryMB = budgets.maxMeshMemoryMB,
                maxTotalMemoryMB = budgets.maxTotalMemoryMB,
                maxFrameTime = budgets.maxFrameTime,
                maxCPUTime = budgets.maxCPUTime,
                maxGPUTime = budgets.maxGPUTime,
                maxRenderTime = budgets.maxRenderTime,
                warningThreshold = budgets.warningThreshold,
                criticalThreshold = budgets.criticalThreshold,
                emergencyThreshold = budgets.emergencyThreshold
            };

            if (!settings.adaptBudgetsForPlatform) return;

            float multiplier = Application.platform switch
            {
                RuntimePlatform.Android or RuntimePlatform.IPhonePlayer => settings.mobileBudgetMultiplier,
                RuntimePlatform.WebGLPlayer => settings.webglBudgetMultiplier,
                _ => settings.desktopBudgetMultiplier
            };

            // Apply multiplier to rendering budgets
            adjustedBudgets.maxDrawCalls = Mathf.RoundToInt(adjustedBudgets.maxDrawCalls * multiplier);
            adjustedBudgets.maxTriangles = Mathf.RoundToInt(adjustedBudgets.maxTriangles * multiplier);
            adjustedBudgets.maxVertices = Mathf.RoundToInt(adjustedBudgets.maxVertices * multiplier);
            adjustedBudgets.maxSetPassCalls = Mathf.RoundToInt(adjustedBudgets.maxSetPassCalls * multiplier);
            adjustedBudgets.maxBatches = Mathf.RoundToInt(adjustedBudgets.maxBatches * multiplier);

            // Apply multiplier to memory budgets
            adjustedBudgets.maxTextureMemoryMB = Mathf.RoundToInt(adjustedBudgets.maxTextureMemoryMB * multiplier);
            adjustedBudgets.maxMeshMemoryMB = Mathf.RoundToInt(adjustedBudgets.maxMeshMemoryMB * multiplier);
            adjustedBudgets.maxTotalMemoryMB = adjustedBudgets.maxTotalMemoryMB * multiplier;

            // Frame time adjustments for different platforms
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                adjustedBudgets.maxFrameTime = 33.33f; // 30 FPS target for WebGL
            }
        }

        #endregion

        #region Benchmarking

        public void StartBenchmarking()
        {
            if (isBenchmarking) return;

            isBenchmarking = true;
            lastBenchmarkTime = Time.time;

            if (settings.enableContinuousBenchmarking)
            {
                benchmarkCoroutine = StartCoroutine(ContinuousBenchmarkCoroutine());
            }

            Debug.Log("Performance benchmarking started");
        }

        public void StopBenchmarking()
        {
            isBenchmarking = false;

            if (benchmarkCoroutine != null)
            {
                StopCoroutine(benchmarkCoroutine);
                benchmarkCoroutine = null;
            }

            Debug.Log("Performance benchmarking stopped");
        }

        private IEnumerator ContinuousBenchmarkCoroutine()
        {
            while (isBenchmarking)
            {
                yield return new WaitForSeconds(settings.benchmarkInterval);
                
                if (settings.enableAutoQualityAdjustment)
                {
                    CheckForQualityAdjustment();
                }
            }
        }

        private void TakePerformanceSnapshot()
        {
            currentSnapshot = new PerformanceSnapshot
            {
                timestamp = System.DateTime.Now,
                
                // Frame statistics
                frameTime = Time.unscaledDeltaTime * 1000f, // Convert to milliseconds
                deltaTime = Time.unscaledDeltaTime,
                fps = 1f / Time.unscaledDeltaTime
            };

            // Update FPS history for averages
            fpsHistory.Enqueue(currentSnapshot.fps);
            if (fpsHistory.Count > 60) // Keep 1 second of history
            {
                fpsHistory.Dequeue();
            }

            if (fpsHistory.Count > 0)
            {
                currentSnapshot.averageFPS = fpsHistory.Average();
                currentSnapshot.minFPS = fpsHistory.Min();
                currentSnapshot.maxFPS = fpsHistory.Max();
            }

            // Gather rendering statistics
            GatherRenderingStatistics();

            // Gather memory statistics
            GatherMemoryStatistics();

            // Gather timing breakdown
            GatherTimingStatistics();

            // Calculate performance metrics
            CalculatePerformanceMetrics();

            // Add to history
            performanceHistory.Add(currentSnapshot);
            if (performanceHistory.Count > settings.historySize)
            {
                performanceHistory.RemoveAt(0);
            }

            // Trigger events
            OnPerformanceSnapshotTaken?.Invoke(currentSnapshot);
            OnPerformanceScoreUpdated?.Invoke(currentSnapshot.performanceScore);

            if (!currentSnapshot.isWithinBudget)
            {
                OnBudgetExceeded?.Invoke(adjustedBudgets);
            }
        }

        private void GatherRenderingStatistics()
        {
            // These are approximations since Unity doesn't provide exact real-time stats
            // In a real implementation, you'd use Unity's FrameDebugger or custom profiling
            
            currentSnapshot.drawCalls = EstimateDrawCalls();
            currentSnapshot.triangles = EstimateTriangles();
            currentSnapshot.vertices = currentSnapshot.triangles * 3;
            currentSnapshot.setPassCalls = EstimateSetPassCalls();
            currentSnapshot.batches = EstimateBatches();
            currentSnapshot.shadowCasters = EstimateShadowCasters();
        }

        private void GatherMemoryStatistics()
        {
            // Get memory statistics from MemoryManager if available
            if (MemoryManager.Instance != null)
            {
                var memoryMetrics = MemoryManager.Instance.GetMemoryMetrics();
                currentSnapshot.totalMemoryMB = memoryMetrics.totalAllocatedMemory;
                currentSnapshot.textureMemoryMB = memoryMetrics.textureMemory;
                currentSnapshot.meshMemoryMB = memoryMetrics.meshMemory;
                currentSnapshot.audioMemoryMB = memoryMetrics.audioMemory;
            }
            else
            {
                // Fallback to Unity's profiler
                currentSnapshot.totalMemoryMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin) / (1024f * 1024f);
            }
        }

        private void GatherTimingStatistics()
        {
            // These would require deeper integration with Unity's profiler
            // For now, we'll use approximations based on frame time
            
            currentSnapshot.cpuMainThreadTime = currentSnapshot.frameTime * 0.6f;
            currentSnapshot.cpuRenderThreadTime = currentSnapshot.frameTime * 0.2f;
            currentSnapshot.gpuTime = currentSnapshot.frameTime * 0.15f;
            currentSnapshot.vsyncTime = currentSnapshot.frameTime * 0.05f;
        }

        private void CalculatePerformanceMetrics()
        {
            // Calculate performance score (0-1 where 1 is perfect)
            float[] scores = new float[]
            {
                CalculateBudgetScore(currentSnapshot.drawCalls, adjustedBudgets.maxDrawCalls),
                CalculateBudgetScore(currentSnapshot.triangles, adjustedBudgets.maxTriangles),
                CalculateBudgetScore(currentSnapshot.setPassCalls, adjustedBudgets.maxSetPassCalls),
                CalculateBudgetScore(currentSnapshot.totalMemoryMB, adjustedBudgets.maxTotalMemoryMB),
                CalculateBudgetScore(currentSnapshot.frameTime, adjustedBudgets.maxFrameTime)
            };

            currentSnapshot.performanceScore = scores.Average();

            // Determine budget status
            float worstScore = scores.Min();
            currentSnapshot.isWithinBudget = worstScore > adjustedBudgets.warningThreshold;
            currentSnapshot.hasWarningLevel = worstScore <= adjustedBudgets.warningThreshold && worstScore > adjustedBudgets.criticalThreshold;
            currentSnapshot.hasCriticalLevel = worstScore <= adjustedBudgets.criticalThreshold && worstScore > adjustedBudgets.emergencyThreshold;
            currentSnapshot.hasEmergencyLevel = worstScore <= adjustedBudgets.emergencyThreshold;
        }

        private float CalculateBudgetScore(float current, float budget)
        {
            if (budget <= 0) return 1f;
            return Mathf.Clamp01(1f - (current / budget));
        }

        #endregion

        #region Estimation Methods

        private int EstimateDrawCalls()
        {
            // Estimate based on visible renderers and materials
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            int drawCalls = 0;

            foreach (var renderer in renderers)
            {
                if (renderer.isVisible)
                {
                    drawCalls += renderer.sharedMaterials.Length;
                }
            }

            return drawCalls;
        }

        private int EstimateTriangles()
        {
            // Estimate based on visible meshes
            MeshRenderer[] meshRenderers = FindObjectsOfType<MeshRenderer>();
            int totalTriangles = 0;

            foreach (var meshRenderer in meshRenderers)
            {
                if (meshRenderer.isVisible)
                {
                    MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        totalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
                    }
                }
            }

            return totalTriangles;
        }

        private int EstimateSetPassCalls()
        {
            // Estimate based on materials and lighting
            return Mathf.RoundToInt(currentSnapshot.drawCalls * 0.3f);
        }

        private int EstimateBatches()
        {
            // Estimate based on draw calls and batching efficiency
            return Mathf.RoundToInt(currentSnapshot.drawCalls * 0.7f);
        }

        private int EstimateShadowCasters()
        {
            // Count objects that cast shadows
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            return renderers.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
        }

        #endregion

        #region Performance Tracking

        private void UpdatePerformanceTracking()
        {
            // Track consecutive warning/critical frames
            if (currentSnapshot.hasWarningLevel)
            {
                consecutiveWarningFrames++;
            }
            else
            {
                consecutiveWarningFrames = 0;
            }

            if (currentSnapshot.hasCriticalLevel)
            {
                consecutiveCriticalFrames++;
            }
            else
            {
                consecutiveCriticalFrames = 0;
            }
        }

        private void CheckForQualityAdjustment()
        {
            if (Time.time - lastQualityAdjustment < settings.qualityAdjustmentCooldown)
                return;

            bool shouldAdjustDown = consecutiveCriticalFrames >= settings.consecutiveThresholdFrames ||
                                   (consecutiveWarningFrames >= settings.consecutiveThresholdFrames * 2) ||
                                   currentSnapshot.performanceScore < settings.aggressiveAdjustmentThreshold;

            bool shouldAdjustUp = consecutiveWarningFrames == 0 && consecutiveCriticalFrames == 0 &&
                                 currentSnapshot.performanceScore > 0.9f &&
                                 GetAveragePerformanceScore() > 0.85f;

            if (shouldAdjustDown)
            {
                TriggerQualityAdjustment(false);
            }
            else if (shouldAdjustUp)
            {
                TriggerQualityAdjustment(true);
            }
        }

        private void TriggerQualityAdjustment(bool increaseQuality)
        {
            lastQualityAdjustment = Time.time;
            
            if (performanceManager != null)
            {
                if (increaseQuality)
                {
                    // Let PerformanceManager handle quality upgrade
                    performanceManager.ForcePerformanceAnalysis();
                }
                else
                {
                    // Force quality downgrade due to benchmark results
                    performanceManager.ForcePerformanceAnalysis();
                }
            }

            OnQualityAdjustmentTriggered?.Invoke();

            Debug.Log($"Performance benchmark triggered quality adjustment: {(increaseQuality ? "UP" : "DOWN")}");
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current performance snapshot
        /// </summary>
        public PerformanceSnapshot GetCurrentSnapshot()
        {
            return currentSnapshot;
        }

        /// <summary>
        /// Get performance history
        /// </summary>
        public List<PerformanceSnapshot> GetPerformanceHistory()
        {
            return new List<PerformanceSnapshot>(performanceHistory);
        }

        /// <summary>
        /// Get adjusted budgets for current platform
        /// </summary>
        public PerformanceBudgets GetAdjustedBudgets()
        {
            return adjustedBudgets;
        }

        /// <summary>
        /// Check if currently within performance budget
        /// </summary>
        public bool IsWithinBudget()
        {
            return currentSnapshot.isWithinBudget;
        }

        /// <summary>
        /// Get average performance score over recent history
        /// </summary>
        public float GetAveragePerformanceScore()
        {
            if (performanceHistory.Count == 0) return 1f;
            
            int recentCount = Mathf.Min(performanceHistory.Count, 20); // Last 20 snapshots
            return performanceHistory.TakeLast(recentCount).Average(s => s.performanceScore);
        }

        /// <summary>
        /// Get performance statistics string
        /// </summary>
        public string GetPerformanceStatistics()
        {
            return $"Performance: {currentSnapshot.performanceScore:P1} | " +
                   $"FPS: {currentSnapshot.fps:F1} ({currentSnapshot.averageFPS:F1} avg) | " +
                   $"Draw Calls: {currentSnapshot.drawCalls}/{adjustedBudgets.maxDrawCalls} | " +
                   $"Triangles: {currentSnapshot.triangles:N0}/{adjustedBudgets.maxTriangles:N0} | " +
                   $"Memory: {currentSnapshot.totalMemoryMB:F1}/{adjustedBudgets.maxTotalMemoryMB:F1} MB | " +
                   $"Budget: {(currentSnapshot.isWithinBudget ? "OK" : "EXCEEDED")}";
        }

        /// <summary>
        /// Force a performance snapshot
        /// </summary>
        public PerformanceSnapshot ForceSnapshot()
        {
            TakePerformanceSnapshot();
            return currentSnapshot;
        }

        /// <summary>
        /// Set custom budgets
        /// </summary>
        public void SetBudgets(PerformanceBudgets customBudgets)
        {
            budgets = customBudgets;
            ApplyPlatformBudgetAdjustments();
        }

        /// <summary>
        /// Clear performance history
        /// </summary>
        public void ClearHistory()
        {
            performanceHistory.Clear();
            fpsHistory.Clear();
            consecutiveWarningFrames = 0;
            consecutiveCriticalFrames = 0;
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
                GUILayout.BeginArea(new Rect(Screen.width - 450, 400, 440, 350));
                
                GUILayout.Label("=== PERFORMANCE BENCHMARK ===");
                GUILayout.Label($"Performance Score: {currentSnapshot.performanceScore:P1}");
                GUILayout.Label($"FPS: {currentSnapshot.fps:F1} (Avg: {currentSnapshot.averageFPS:F1})");
                GUILayout.Label($"Frame Time: {currentSnapshot.frameTime:F2}ms");
                
                GUILayout.Space(5);
                GUILayout.Label("=== RENDERING STATS ===");
                GUILayout.Label($"Draw Calls: {currentSnapshot.drawCalls}/{adjustedBudgets.maxDrawCalls}");
                GUILayout.Label($"Triangles: {currentSnapshot.triangles:N0}/{adjustedBudgets.maxTriangles:N0}");
                GUILayout.Label($"Set Pass: {currentSnapshot.setPassCalls}/{adjustedBudgets.maxSetPassCalls}");
                GUILayout.Label($"Batches: {currentSnapshot.batches}/{adjustedBudgets.maxBatches}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== MEMORY STATS ===");
                GUILayout.Label($"Total: {currentSnapshot.totalMemoryMB:F1}/{adjustedBudgets.maxTotalMemoryMB:F1} MB");
                GUILayout.Label($"Texture: {currentSnapshot.textureMemoryMB:F1}/{adjustedBudgets.maxTextureMemoryMB} MB");
                GUILayout.Label($"Mesh: {currentSnapshot.meshMemoryMB:F1}/{adjustedBudgets.maxMeshMemoryMB} MB");
                
                GUILayout.Space(5);
                GUILayout.Label("=== STATUS ===");
                GUILayout.Label($"Within Budget: {currentSnapshot.isWithinBudget}");
                GUILayout.Label($"Warning: {currentSnapshot.hasWarningLevel}");
                GUILayout.Label($"Critical: {currentSnapshot.hasCriticalLevel}");
                GUILayout.Label($"Emergency: {currentSnapshot.hasEmergencyLevel}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== CONSECUTIVE FRAMES ===");
                GUILayout.Label($"Warning: {consecutiveWarningFrames}");
                GUILayout.Label($"Critical: {consecutiveCriticalFrames}");
                
                GUILayout.Space(5);
                if (GUILayout.Button("Force Snapshot"))
                {
                    ForceSnapshot();
                }
                
                if (GUILayout.Button("Clear History"))
                {
                    ClearHistory();
                }
                
                if (GUILayout.Button(isBenchmarking ? "Stop Benchmark" : "Start Benchmark"))
                {
                    if (isBenchmarking)
                        StopBenchmarking();
                    else
                        StartBenchmarking();
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }
}
