using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using WhiskerKing.Core;

namespace WhiskerKing.Performance
{
    /// <summary>
    /// Memory Manager for Whisker King
    /// Handles asset streaming, memory cleanup, and maintains performance within memory budgets
    /// Implements garbage collection optimization and texture memory management
    /// </summary>
    public class MemoryManager : MonoBehaviour
    {
        [System.Serializable]
        public class MemoryBudget
        {
            [Header("Memory Limits (MB)")]
            public float totalMemoryBudget = 512f;
            public float textureMemoryBudget = 256f;
            public float meshMemoryBudget = 64f;
            public float audioMemoryBudget = 32f;
            public float scriptMemoryBudget = 16f;
            
            [Header("Warning Thresholds")]
            public float warningThreshold = 0.8f;      // 80%
            public float criticalThreshold = 0.9f;     // 90%
            public float emergencyThreshold = 0.95f;   // 95%
            
            [Header("Cleanup Settings")]
            public bool enableAutomaticCleanup = true;
            public float cleanupInterval = 30f;
            public int maxItemsPerCleanupCycle = 10;
        }

        [System.Serializable]
        public class MemoryMetrics
        {
            public float totalAllocatedMemory;
            public float totalReservedMemory;
            public float totalUnusedReservedMemory;
            public float textureMemory;
            public float meshMemory;
            public float audioMemory;
            public float scriptMemory;
            public float gcAllocatedInFrame;
            public int gcCollectionCount;
            public float memoryUsagePercentage;
            public bool isMemoryPressureHigh;
            public bool isMemoryCritical;
        }

        [System.Serializable]
        public class AssetReference
        {
            public Object asset;
            public System.Type assetType;
            public float memorySize;
            public float lastAccessTime;
            public int accessCount;
            public bool isPersistent;
            public bool isLoaded;
            public string assetPath;
            public int priority; // 0 = highest priority, higher numbers = lower priority
        }

        [System.Serializable]
        public class StreamingSettings
        {
            public bool enableAssetStreaming = true;
            public float streamingDistance = 100f;
            public int maxConcurrentLoads = 3;
            public float preloadDistance = 50f;
            public bool predictiveLoading = true;
            public float predictionTime = 2f;
        }

        // Singleton pattern
        private static MemoryManager instance;
        public static MemoryManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<MemoryManager>();
                    if (instance == null)
                    {
                        GameObject memoryManagerGO = new GameObject("MemoryManager");
                        instance = memoryManagerGO.AddComponent<MemoryManager>();
                        DontDestroyOnLoad(memoryManagerGO);
                    }
                }
                return instance;
            }
        }

        [Header("Memory Configuration")]
        [SerializeField] private MemoryBudget memoryBudget = new MemoryBudget();
        [SerializeField] private StreamingSettings streamingSettings = new StreamingSettings();
        [SerializeField] private bool debugMode = false;
        [SerializeField] private float memoryUpdateInterval = 1f;

        [Header("Garbage Collection")]
        [SerializeField] private bool optimizeGarbageCollection = true;
        [SerializeField] private float gcOptimizationInterval = 10f;
        [SerializeField] private int framesBetweenGCChecks = 60;

        [Header("Asset Management")]
        [SerializeField] private bool enableAssetCaching = true;
        [SerializeField] private int maxCachedAssets = 100;
        [SerializeField] private float assetTimeoutDuration = 300f; // 5 minutes

        // Current state
        private MemoryMetrics currentMetrics = new MemoryMetrics();
        private Dictionary<Object, AssetReference> managedAssets = new Dictionary<Object, AssetReference>();
        private List<AssetReference> assetsByAccessTime = new List<AssetReference>();
        private Queue<AssetReference> assetsToUnload = new Queue<AssetReference>();
        
        // Performance tracking
        private float lastMemoryUpdate = 0f;
        private float lastCleanupTime = 0f;
        private float lastGCOptimization = 0f;
        private int frameCounter = 0;
        private float gcAllocationsThisFrame = 0f;

        // Component references
        private PerformanceManager performanceManager;

        // Configuration cache
        private MemoryData memoryConfig;

        // Coroutines
        private Coroutine cleanupCoroutine;
        private Coroutine streamingCoroutine;

        // Events
        public System.Action<MemoryMetrics> OnMemoryUpdated;
        public System.Action OnMemoryPressure;
        public System.Action OnMemoryCritical;
        public System.Action<AssetReference> OnAssetLoaded;
        public System.Action<AssetReference> OnAssetUnloaded;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeMemoryManager();
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
            StartMemoryManagement();
        }

        private void Update()
        {
            frameCounter++;
            
            if (Time.time - lastMemoryUpdate >= memoryUpdateInterval)
            {
                UpdateMemoryMetrics();
                lastMemoryUpdate = Time.time;
            }

            if (optimizeGarbageCollection && frameCounter % framesBetweenGCChecks == 0)
            {
                CheckGarbageCollectionNeeds();
            }

            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDestroy()
        {
            StopMemoryManagement();
            CleanupAllAssets();
        }

        #endregion

        #region Initialization

        private void InitializeMemoryManager()
        {
            // Get component references
            performanceManager = PerformanceManager.Instance;

            // Apply platform-specific memory budgets
            ApplyPlatformMemoryBudgets();

            Debug.Log("MemoryManager initialized");
        }

        private void LoadConfiguration()
        {
            if (GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                memoryConfig = GameConfiguration.Instance.Config.memory;
                ApplyMemoryConfiguration();
                Debug.Log("MemoryManager: Configuration loaded from GameConfig");
            }
            else
            {
                UseDefaultConfiguration();
            }
        }

        private void ApplyMemoryConfiguration()
        {
            if (memoryConfig == null) return;

            // Apply memory budget from configuration
            memoryBudget.totalMemoryBudget = memoryConfig.memoryBudget;
            memoryBudget.textureMemoryBudget = memoryBudget.totalMemoryBudget * 0.5f;
            memoryBudget.meshMemoryBudget = memoryBudget.totalMemoryBudget * 0.125f;
            memoryBudget.audioMemoryBudget = memoryBudget.totalMemoryBudget * 0.0625f;
        }

        private void UseDefaultConfiguration()
        {
            ApplyPlatformMemoryBudgets();
        }

        private void ApplyPlatformMemoryBudgets()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    // Mobile devices - conservative memory budgets
                    memoryBudget.totalMemoryBudget = 512f;
                    memoryBudget.textureMemoryBudget = 256f;
                    memoryBudget.meshMemoryBudget = 64f;
                    memoryBudget.audioMemoryBudget = 32f;
                    break;

                case RuntimePlatform.WebGLPlayer:
                    // WebGL - very conservative due to browser limitations
                    memoryBudget.totalMemoryBudget = 256f;
                    memoryBudget.textureMemoryBudget = 128f;
                    memoryBudget.meshMemoryBudget = 32f;
                    memoryBudget.audioMemoryBudget = 16f;
                    break;

                default:
                    // Desktop - more generous budgets
                    memoryBudget.totalMemoryBudget = 1024f;
                    memoryBudget.textureMemoryBudget = 512f;
                    memoryBudget.meshMemoryBudget = 128f;
                    memoryBudget.audioMemoryBudget = 64f;
                    break;
            }
        }

        private void StartMemoryManagement()
        {
            if (memoryBudget.enableAutomaticCleanup)
            {
                cleanupCoroutine = StartCoroutine(AutomaticCleanupCoroutine());
            }

            if (streamingSettings.enableAssetStreaming)
            {
                streamingCoroutine = StartCoroutine(AssetStreamingCoroutine());
            }
        }

        private void StopMemoryManagement()
        {
            if (cleanupCoroutine != null)
            {
                StopCoroutine(cleanupCoroutine);
                cleanupCoroutine = null;
            }

            if (streamingCoroutine != null)
            {
                StopCoroutine(streamingCoroutine);
                streamingCoroutine = null;
            }
        }

        #endregion

        #region Memory Monitoring

        private void UpdateMemoryMetrics()
        {
            // Get Unity memory statistics
            currentMetrics.totalAllocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin) / (1024f * 1024f);
            currentMetrics.totalReservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin) / (1024f * 1024f);
            currentMetrics.totalUnusedReservedMemory = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin) / (1024f * 1024f);

            // Estimate breakdown (approximations)
            EstimateMemoryBreakdown();

            // Calculate usage percentage
            currentMetrics.memoryUsagePercentage = currentMetrics.totalAllocatedMemory / memoryBudget.totalMemoryBudget;

            // Determine memory pressure status
            UpdateMemoryPressureStatus();

            // Track garbage collection
            currentMetrics.gcCollectionCount = System.GC.CollectionCount(0);

            // Trigger events
            OnMemoryUpdated?.Invoke(currentMetrics);

            if (currentMetrics.isMemoryPressureHigh)
            {
                OnMemoryPressure?.Invoke();
            }

            if (currentMetrics.isMemoryCritical)
            {
                OnMemoryCritical?.Invoke();
                TriggerEmergencyCleanup();
            }
        }

        private void EstimateMemoryBreakdown()
        {
            // These are approximations - Unity doesn't provide exact breakdowns
            float totalMemory = currentMetrics.totalAllocatedMemory;
            
            currentMetrics.textureMemory = totalMemory * 0.6f; // Textures typically use most memory
            currentMetrics.meshMemory = totalMemory * 0.2f;    // Meshes
            currentMetrics.audioMemory = totalMemory * 0.1f;   // Audio
            currentMetrics.scriptMemory = totalMemory * 0.1f;  // Scripts and other
        }

        private void UpdateMemoryPressureStatus()
        {
            float memoryRatio = currentMetrics.memoryUsagePercentage;

            currentMetrics.isMemoryPressureHigh = memoryRatio > memoryBudget.warningThreshold;
            currentMetrics.isMemoryCritical = memoryRatio > memoryBudget.criticalThreshold;

            // Emergency threshold triggers immediate aggressive cleanup
            if (memoryRatio > memoryBudget.emergencyThreshold)
            {
                TriggerEmergencyCleanup();
            }
        }

        #endregion

        #region Asset Management

        /// <summary>
        /// Register an asset for memory management
        /// </summary>
        public void RegisterAsset(Object asset, bool isPersistent = false, int priority = 5)
        {
            if (asset == null || managedAssets.ContainsKey(asset))
                return;

            var assetRef = new AssetReference
            {
                asset = asset,
                assetType = asset.GetType(),
                memorySize = EstimateAssetMemorySize(asset),
                lastAccessTime = Time.time,
                accessCount = 0,
                isPersistent = isPersistent,
                isLoaded = true,
                priority = priority
            };

            managedAssets[asset] = assetRef;
            assetsByAccessTime.Add(assetRef);

            OnAssetLoaded?.Invoke(assetRef);

            if (debugMode)
            {
                Debug.Log($"MemoryManager: Registered {asset.name} ({assetRef.memorySize:F2} MB)");
            }
        }

        /// <summary>
        /// Unregister an asset from memory management
        /// </summary>
        public void UnregisterAsset(Object asset)
        {
            if (asset == null || !managedAssets.ContainsKey(asset))
                return;

            var assetRef = managedAssets[asset];
            managedAssets.Remove(asset);
            assetsByAccessTime.Remove(assetRef);

            OnAssetUnloaded?.Invoke(assetRef);

            if (debugMode)
            {
                Debug.Log($"MemoryManager: Unregistered {asset.name}");
            }
        }

        /// <summary>
        /// Mark an asset as accessed (updates LRU tracking)
        /// </summary>
        public void MarkAssetAccessed(Object asset)
        {
            if (asset != null && managedAssets.ContainsKey(asset))
            {
                var assetRef = managedAssets[asset];
                assetRef.lastAccessTime = Time.time;
                assetRef.accessCount++;
            }
        }

        /// <summary>
        /// Estimate memory size of an asset
        /// </summary>
        private float EstimateAssetMemorySize(Object asset)
        {
            switch (asset)
            {
                case Texture texture:
                    return EstimateTextureMemorySize(texture);
                    
                case Mesh mesh:
                    return EstimateMeshMemorySize(mesh);
                    
                case AudioClip audioClip:
                    return EstimateAudioMemorySize(audioClip);
                    
                default:
                    return 1f; // Default 1MB estimate
            }
        }

        private float EstimateTextureMemorySize(Texture texture)
        {
            if (texture is Texture2D texture2D)
            {
                int bytesPerPixel = GetBytesPerPixel(texture2D.format);
                long totalBytes = (long)texture2D.width * texture2D.height * bytesPerPixel;
                
                // Account for mipmaps (adds about 33% more memory)
                if (texture2D.mipmapCount > 1)
                {
                    totalBytes = (long)(totalBytes * 1.33f);
                }
                
                return totalBytes / (1024f * 1024f); // Convert to MB
            }
            
            return 1f; // Default estimate
        }

        private int GetBytesPerPixel(TextureFormat format)
        {
            return format switch
            {
                TextureFormat.RGBA32 => 4,
                TextureFormat.RGB24 => 3,
                TextureFormat.RGBA4444 => 2,
                TextureFormat.RGB565 => 2,
                TextureFormat.Alpha8 => 1,
                TextureFormat.DXT1 => 0, // Compressed
                TextureFormat.DXT5 => 1, // Compressed
                _ => 4 // Default to 4 bytes
            };
        }

        private float EstimateMeshMemorySize(Mesh mesh)
        {
            if (mesh == null) return 0f;

            long totalBytes = 0;
            
            // Vertices (Vector3 = 12 bytes)
            totalBytes += mesh.vertexCount * 12;
            
            // Normals (Vector3 = 12 bytes)
            if (mesh.normals.Length > 0)
                totalBytes += mesh.vertexCount * 12;
            
            // UVs (Vector2 = 8 bytes)
            if (mesh.uv.Length > 0)
                totalBytes += mesh.vertexCount * 8;
            
            // Triangles (int = 4 bytes)
            totalBytes += mesh.triangles.Length * 4;
            
            return totalBytes / (1024f * 1024f);
        }

        private float EstimateAudioMemorySize(AudioClip audioClip)
        {
            if (audioClip == null) return 0f;
            
            // Estimate based on sample rate, channels, and length
            int bytesPerSample = 2; // 16-bit audio
            long totalBytes = (long)(audioClip.frequency * audioClip.channels * audioClip.length * bytesPerSample);
            
            return totalBytes / (1024f * 1024f);
        }

        #endregion

        #region Memory Cleanup

        private IEnumerator AutomaticCleanupCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(memoryBudget.cleanupInterval);
                
                if (currentMetrics.isMemoryPressureHigh)
                {
                    PerformMemoryCleanup();
                }
            }
        }

        private void PerformMemoryCleanup()
        {
            int itemsCleaned = 0;
            float memoryFreed = 0f;

            // Sort assets by priority and last access time (LRU)
            var assetsToConsider = assetsByAccessTime
                .Where(asset => !asset.isPersistent && asset.isLoaded)
                .OrderBy(asset => asset.priority)
                .ThenBy(asset => asset.lastAccessTime)
                .ToList();

            foreach (var assetRef in assetsToConsider)
            {
                if (itemsCleaned >= memoryBudget.maxItemsPerCleanupCycle)
                    break;

                // Check if asset hasn't been accessed recently
                float timeSinceAccess = Time.time - assetRef.lastAccessTime;
                if (timeSinceAccess > assetTimeoutDuration || currentMetrics.isMemoryCritical)
                {
                    if (UnloadAsset(assetRef))
                    {
                        memoryFreed += assetRef.memorySize;
                        itemsCleaned++;
                    }
                }
            }

            if (itemsCleaned > 0)
            {
                // Force garbage collection after cleanup
                System.GC.Collect();
                Resources.UnloadUnusedAssets();

                if (debugMode)
                {
                    Debug.Log($"MemoryManager: Cleaned {itemsCleaned} assets, freed ~{memoryFreed:F2} MB");
                }
            }
        }

        private bool UnloadAsset(AssetReference assetRef)
        {
            try
            {
                if (assetRef.asset != null && assetRef.isLoaded)
                {
                    // Don't destroy the asset, just mark as unloaded
                    // The actual unloading will be handled by Unity's garbage collection
                    assetRef.isLoaded = false;
                    assetsToUnload.Enqueue(assetRef);
                    
                    OnAssetUnloaded?.Invoke(assetRef);
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MemoryManager: Error unloading asset: {e.Message}");
            }

            return false;
        }

        private void TriggerEmergencyCleanup()
        {
            if (Time.time - lastCleanupTime < 1f) // Prevent spam
                return;

            lastCleanupTime = Time.time;
            
            Debug.LogWarning("MemoryManager: Emergency cleanup triggered!");

            // Aggressive cleanup - unload all non-persistent assets
            var assetsToUnload = assetsByAccessTime
                .Where(asset => !asset.isPersistent && asset.isLoaded)
                .ToList();

            int unloadedCount = 0;
            float memoryFreed = 0f;

            foreach (var assetRef in assetsToUnload)
            {
                if (UnloadAsset(assetRef))
                {
                    memoryFreed += assetRef.memorySize;
                    unloadedCount++;
                }
            }

            // Force aggressive garbage collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            Resources.UnloadUnusedAssets();

            Debug.LogWarning($"MemoryManager: Emergency cleanup completed - {unloadedCount} assets unloaded, ~{memoryFreed:F2} MB freed");
        }

        #endregion

        #region Garbage Collection Optimization

        private void CheckGarbageCollectionNeeds()
        {
            if (!optimizeGarbageCollection || Time.time - lastGCOptimization < gcOptimizationInterval)
                return;

            // Check if we should trigger GC proactively
            bool shouldTriggerGC = false;

            // Trigger GC if memory pressure is high
            if (currentMetrics.isMemoryPressureHigh)
            {
                shouldTriggerGC = true;
            }

            // Trigger GC if we haven't had one in a while and memory usage is moderate
            if (Time.time - lastGCOptimization > gcOptimizationInterval * 2 && 
                currentMetrics.memoryUsagePercentage > 0.5f)
            {
                shouldTriggerGC = true;
            }

            if (shouldTriggerGC)
            {
                StartCoroutine(OptimizedGarbageCollection());
            }
        }

        private IEnumerator OptimizedGarbageCollection()
        {
            lastGCOptimization = Time.time;
            
            // Spread GC over multiple frames to avoid hitches
            yield return null;
            
            System.GC.Collect(0, System.GCCollectionMode.Optimized);
            yield return null;
            
            System.GC.Collect(1, System.GCCollectionMode.Optimized);
            yield return null;
            
            Resources.UnloadUnusedAssets();
            
            if (debugMode)
            {
                Debug.Log("MemoryManager: Optimized garbage collection completed");
            }
        }

        #endregion

        #region Asset Streaming

        private IEnumerator AssetStreamingCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                
                if (streamingSettings.enableAssetStreaming)
                {
                    UpdateAssetStreaming();
                }
            }
        }

        private void UpdateAssetStreaming()
        {
            // This is a placeholder for asset streaming logic
            // In a real implementation, you would:
            // 1. Check player position and movement direction
            // 2. Predict what assets will be needed soon
            // 3. Preload assets within streaming distance
            // 4. Unload assets that are too far away
            
            if (debugMode)
            {
                // Debug.Log("MemoryManager: Asset streaming update");
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current memory metrics
        /// </summary>
        public MemoryMetrics GetMemoryMetrics()
        {
            return currentMetrics;
        }

        /// <summary>
        /// Get memory budget
        /// </summary>
        public MemoryBudget GetMemoryBudget()
        {
            return memoryBudget;
        }

        /// <summary>
        /// Check if memory usage is within budget
        /// </summary>
        public bool IsWithinMemoryBudget()
        {
            return currentMetrics.memoryUsagePercentage < memoryBudget.warningThreshold;
        }

        /// <summary>
        /// Force immediate memory cleanup
        /// </summary>
        public void ForceMemoryCleanup()
        {
            PerformMemoryCleanup();
        }

        /// <summary>
        /// Force immediate garbage collection
        /// </summary>
        public void ForceGarbageCollection()
        {
            StartCoroutine(OptimizedGarbageCollection());
        }

        /// <summary>
        /// Get memory statistics string
        /// </summary>
        public string GetMemoryStatistics()
        {
            return $"Memory: {currentMetrics.totalAllocatedMemory:F1}/{memoryBudget.totalMemoryBudget:F1} MB " +
                   $"({currentMetrics.memoryUsagePercentage:P1}), " +
                   $"Assets: {managedAssets.Count}, " +
                   $"Pressure: {(currentMetrics.isMemoryPressureHigh ? "HIGH" : "NORMAL")}, " +
                   $"Critical: {currentMetrics.isMemoryCritical}";
        }

        /// <summary>
        /// Clear all managed assets
        /// </summary>
        public void ClearAllManagedAssets()
        {
            managedAssets.Clear();
            assetsByAccessTime.Clear();
            assetsToUnload.Clear();
        }

        /// <summary>
        /// Set memory budget limits
        /// </summary>
        public void SetMemoryBudget(float totalMB, float textureMB, float meshMB, float audioMB)
        {
            memoryBudget.totalMemoryBudget = totalMB;
            memoryBudget.textureMemoryBudget = textureMB;
            memoryBudget.meshMemoryBudget = meshMB;
            memoryBudget.audioMemoryBudget = audioMB;
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
                GUILayout.BeginArea(new Rect(10, 970, 450, 250));
                
                GUILayout.Label("=== MEMORY MANAGER DEBUG ===");
                GUILayout.Label($"Total Memory: {currentMetrics.totalAllocatedMemory:F1}/{memoryBudget.totalMemoryBudget:F1} MB ({currentMetrics.memoryUsagePercentage:P1})");
                GUILayout.Label($"Reserved: {currentMetrics.totalReservedMemory:F1} MB");
                GUILayout.Label($"Unused Reserved: {currentMetrics.totalUnusedReservedMemory:F1} MB");
                
                GUILayout.Space(5);
                GUILayout.Label("=== MEMORY BREAKDOWN ===");
                GUILayout.Label($"Textures: {currentMetrics.textureMemory:F1}/{memoryBudget.textureMemoryBudget:F1} MB");
                GUILayout.Label($"Meshes: {currentMetrics.meshMemory:F1}/{memoryBudget.meshMemoryBudget:F1} MB");
                GUILayout.Label($"Audio: {currentMetrics.audioMemory:F1}/{memoryBudget.audioMemoryBudget:F1} MB");
                GUILayout.Label($"Scripts: {currentMetrics.scriptMemory:F1}/{memoryBudget.scriptMemoryBudget:F1} MB");
                
                GUILayout.Space(5);
                GUILayout.Label("=== STATUS ===");
                GUILayout.Label($"Managed Assets: {managedAssets.Count}");
                GUILayout.Label($"Memory Pressure: {(currentMetrics.isMemoryPressureHigh ? "HIGH" : "NORMAL")}");
                GUILayout.Label($"Critical Memory: {currentMetrics.isMemoryCritical}");
                GUILayout.Label($"GC Collections: {currentMetrics.gcCollectionCount}");
                
                GUILayout.Space(5);
                if (GUILayout.Button("Force Cleanup"))
                {
                    ForceMemoryCleanup();
                }
                
                if (GUILayout.Button("Force GC"))
                {
                    ForceGarbageCollection();
                }
                
                if (GUILayout.Button("Emergency Cleanup"))
                {
                    TriggerEmergencyCleanup();
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion

        #region Cleanup

        private void CleanupAllAssets()
        {
            StopMemoryManagement();
            ClearAllManagedAssets();
        }

        #endregion
    }
}
