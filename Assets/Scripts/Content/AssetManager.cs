using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using WhiskerKing.Core;
using WhiskerKing.Performance;

namespace WhiskerKing.Content
{
    /// <summary>
    /// Comprehensive Asset Manager for Whisker King
    /// Handles loading, caching, and optimization of all game assets
    /// Integrates with Addressable Assets and provides streaming capabilities
    /// </summary>
    public class AssetManager : MonoBehaviour
    {
        [System.Serializable]
        public class AssetConfiguration
        {
            [Header("Loading Settings")]
            public bool enableAsyncLoading = true;
            public bool enablePreloading = true;
            public int maxConcurrentLoads = 4;
            public float loadTimeoutSeconds = 30f;

            [Header("Caching")]
            public bool enableAssetCaching = true;
            public int maxCachedAssets = 200;
            public float assetCacheTimeoutMinutes = 10f;
            public bool unloadUnusedAssetsOnLevelChange = true;

            [Header("Streaming")]
            public bool enableAssetStreaming = true;
            public float streamingDistanceMeters = 100f;
            public bool predictiveLoading = true;
            public float predictionTimeSeconds = 3f;

            [Header("Performance")]
            public int assetLoadBudgetPerFrame = 2;
            public float maxLoadTimePerFrameMS = 8f;
            public bool enableLODOptimization = true;
            public bool enableTextureStreaming = true;
        }

        [System.Serializable]
        public class AssetReference
        {
            public string assetKey;
            public AssetReferenceT<Object> addressableReference;
            public Object cachedAsset;
            public float lastAccessTime;
            public int usageCount;
            public bool isLoaded;
            public bool isPersistent;
            public int priority;
            public long memorySize;
            public AssetType type;
        }

        public enum AssetType
        {
            Character,
            Environment,
            Audio,
            UI,
            VFX,
            Animation,
            Material,
            Texture,
            Mesh,
            Prefab
        }

        public enum LoadPriority
        {
            Critical = 0,
            High = 1,
            Medium = 2,
            Low = 3,
            Background = 4
        }

        // Singleton pattern
        private static AssetManager instance;
        public static AssetManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<AssetManager>();
                    if (instance == null)
                    {
                        GameObject assetManagerGO = new GameObject("AssetManager");
                        instance = assetManagerGO.AddComponent<AssetManager>();
                        DontDestroyOnLoad(assetManagerGO);
                    }
                }
                return instance;
            }
        }

        [Header("Asset Configuration")]
        [SerializeField] private AssetConfiguration configuration = new AssetConfiguration();
        [SerializeField] private bool debugMode = true;

        // Asset management
        private Dictionary<string, AssetReference> managedAssets = new Dictionary<string, AssetReference>();
        private Dictionary<AssetType, List<string>> assetsByType = new Dictionary<AssetType, List<string>>();
        private Queue<AssetLoadRequest> loadQueue = new Queue<AssetLoadRequest>();
        
        // Loading state
        private List<AsyncOperationHandle> activeOperations = new List<AsyncOperationHandle>();
        private Dictionary<string, AsyncOperationHandle> pendingLoads = new Dictionary<string, AsyncOperationHandle>();
        
        // Performance tracking
        private float frameLoadTimeUsed = 0f;
        private int assetsLoadedThisFrame = 0;
        private Dictionary<AssetType, long> memoryUsageByType = new Dictionary<AssetType, long>();

        // Component references
        private MemoryManager memoryManager;

        // Events
        public System.Action<string, Object> OnAssetLoaded;
        public System.Action<string> OnAssetUnloaded;
        public System.Action<string, string> OnAssetLoadFailed;

        private class AssetLoadRequest
        {
            public string assetKey;
            public LoadPriority priority;
            public System.Action<Object> onLoaded;
            public System.Action<string> onFailed;
            public float requestTime;
        }

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAssetManager();
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
            InitializeAssetTypes();
            StartCoroutine(AssetLoadingCoroutine());
            StartCoroutine(PeriodicCleanupCoroutine());
        }

        private void Update()
        {
            // Reset per-frame counters
            if (Time.unscaledTime - Mathf.Floor(Time.unscaledTime) < Time.unscaledDeltaTime)
            {
                frameLoadTimeUsed = 0f;
                assetsLoadedThisFrame = 0;
            }

            UpdateAssetStreaming();
        }

        #endregion

        #region Initialization

        private void InitializeAssetManager()
        {
            // Get component references
            memoryManager = MemoryManager.Instance;

            // Initialize Addressables
            if (!Addressables.RuntimePath.Contains("aa"))
            {
                Debug.LogWarning("Addressables not properly initialized. Some assets may not load correctly.");
            }

            Debug.Log("AssetManager initialized");
        }

        private void LoadConfiguration()
        {
            if (GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                // Apply configuration from GameConfig if available
                Debug.Log("AssetManager: Configuration loaded from GameConfig");
            }
            else
            {
                Debug.Log("AssetManager: Using default configuration");
            }
        }

        private void InitializeAssetTypes()
        {
            // Initialize asset type dictionaries
            foreach (AssetType type in System.Enum.GetValues(typeof(AssetType)))
            {
                assetsByType[type] = new List<string>();
                memoryUsageByType[type] = 0;
            }
        }

        #endregion

        #region Asset Loading

        /// <summary>
        /// Load asset asynchronously
        /// </summary>
        public void LoadAssetAsync<T>(string assetKey, LoadPriority priority = LoadPriority.Medium,
                                     System.Action<T> onLoaded = null, System.Action<string> onFailed = null) where T : Object
        {
            if (string.IsNullOrEmpty(assetKey))
            {
                onFailed?.Invoke("Invalid asset key");
                return;
            }

            // Check if already loaded
            if (managedAssets.ContainsKey(assetKey) && managedAssets[assetKey].isLoaded)
            {
                var asset = managedAssets[assetKey].cachedAsset as T;
                if (asset != null)
                {
                    onLoaded?.Invoke(asset);
                    UpdateAssetAccess(assetKey);
                    return;
                }
            }

            // Add to load queue
            var request = new AssetLoadRequest
            {
                assetKey = assetKey,
                priority = priority,
                onLoaded = (obj) => onLoaded?.Invoke(obj as T),
                onFailed = onFailed,
                requestTime = Time.time
            };

            loadQueue.Enqueue(request);
        }

        /// <summary>
        /// Load asset synchronously (use sparingly)
        /// </summary>
        public T LoadAssetSync<T>(string assetKey) where T : Object
        {
            if (managedAssets.ContainsKey(assetKey) && managedAssets[assetKey].isLoaded)
            {
                UpdateAssetAccess(assetKey);
                return managedAssets[assetKey].cachedAsset as T;
            }

            // Synchronous load via Resources (fallback)
            var asset = Resources.Load<T>(assetKey);
            if (asset != null)
            {
                RegisterAsset(assetKey, asset, AssetType.Prefab, true);
            }

            return asset;
        }

        /// <summary>
        /// Preload assets for a specific type
        /// </summary>
        public void PreloadAssetsByType(AssetType type, LoadPriority priority = LoadPriority.Background)
        {
            if (!configuration.enablePreloading) return;

            var assetsOfType = assetsByType.GetValueOrDefault(type, new List<string>());
            
            foreach (string assetKey in assetsOfType.Take(10)) // Limit preloading
            {
                if (!managedAssets.ContainsKey(assetKey) || !managedAssets[assetKey].isLoaded)
                {
                    LoadAssetAsync<Object>(assetKey, priority);
                }
            }
        }

        private IEnumerator AssetLoadingCoroutine()
        {
            while (true)
            {
                // Process load queue within performance budget
                while (loadQueue.Count > 0 && 
                       assetsLoadedThisFrame < configuration.assetLoadBudgetPerFrame &&
                       frameLoadTimeUsed < configuration.maxLoadTimePerFrameMS)
                {
                    var request = loadQueue.Dequeue();
                    yield return StartCoroutine(ProcessLoadRequest(request));
                }

                yield return null;
            }
        }

        private IEnumerator ProcessLoadRequest(AssetLoadRequest request)
        {
            float loadStartTime = Time.realtimeSinceStartup;

            try
            {
                // Check if load is already pending
                if (pendingLoads.ContainsKey(request.assetKey))
                {
                    yield return pendingLoads[request.assetKey];
                    var existingAsset = managedAssets.GetValueOrDefault(request.assetKey)?.cachedAsset;
                    if (existingAsset != null)
                    {
                        request.onLoaded?.Invoke(existingAsset);
                        yield break;
                    }
                }

                // Start Addressable load
                var handle = Addressables.LoadAssetAsync<Object>(request.assetKey);
                pendingLoads[request.assetKey] = handle;
                activeOperations.Add(handle);

                yield return handle;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    var asset = handle.Result;
                    var assetType = DetermineAssetType(asset);
                    
                    RegisterAsset(request.assetKey, asset, assetType, false);
                    request.onLoaded?.Invoke(asset);
                    
                    OnAssetLoaded?.Invoke(request.assetKey, asset);
                    
                    if (debugMode)
                    {
                        Debug.Log($"Loaded asset: {request.assetKey} ({assetType})");
                    }
                }
                else
                {
                    string errorMessage = $"Failed to load asset: {request.assetKey} - {handle.OperationException?.Message}";
                    request.onFailed?.Invoke(errorMessage);
                    OnAssetLoadFailed?.Invoke(request.assetKey, errorMessage);
                    
                    Debug.LogError(errorMessage);
                }
            }
            catch (System.Exception e)
            {
                string errorMessage = $"Exception loading asset {request.assetKey}: {e.Message}";
                request.onFailed?.Invoke(errorMessage);
                OnAssetLoadFailed?.Invoke(request.assetKey, errorMessage);
                Debug.LogException(e);
            }
            finally
            {
                pendingLoads.Remove(request.assetKey);
                
                // Track performance
                float loadTime = (Time.realtimeSinceStartup - loadStartTime) * 1000f;
                frameLoadTimeUsed += loadTime;
                assetsLoadedThisFrame++;
            }
        }

        #endregion

        #region Asset Management

        private void RegisterAsset(string assetKey, Object asset, AssetType type, bool isPersistent)
        {
            var assetRef = new AssetReference
            {
                assetKey = assetKey,
                cachedAsset = asset,
                lastAccessTime = Time.time,
                usageCount = 1,
                isLoaded = true,
                isPersistent = isPersistent,
                type = type,
                memorySize = EstimateAssetMemorySize(asset)
            };

            managedAssets[assetKey] = assetRef;
            
            // Add to type-specific list
            if (!assetsByType[type].Contains(assetKey))
            {
                assetsByType[type].Add(assetKey);
            }

            // Update memory tracking
            memoryUsageByType[type] += assetRef.memorySize;

            // Register with MemoryManager if available
            if (memoryManager != null)
            {
                memoryManager.RegisterAsset(asset, isPersistent, (int)type);
            }
        }

        private void UpdateAssetAccess(string assetKey)
        {
            if (managedAssets.ContainsKey(assetKey))
            {
                var assetRef = managedAssets[assetKey];
                assetRef.lastAccessTime = Time.time;
                assetRef.usageCount++;

                // Update MemoryManager access tracking
                if (memoryManager != null)
                {
                    memoryManager.MarkAssetAccessed(assetRef.cachedAsset);
                }
            }
        }

        private AssetType DetermineAssetType(Object asset)
        {
            switch (asset)
            {
                case AudioClip _:
                    return AssetType.Audio;
                case Texture _:
                    return AssetType.Texture;
                case Material _:
                    return AssetType.Material;
                case Mesh _:
                    return AssetType.Mesh;
                case GameObject go:
                    if (go.GetComponent<Animator>() != null)
                        return AssetType.Character;
                    else if (go.GetComponent<ParticleSystem>() != null)
                        return AssetType.VFX;
                    else
                        return AssetType.Prefab;
                case AnimationClip _:
                    return AssetType.Animation;
                default:
                    return AssetType.Prefab;
            }
        }

        private long EstimateAssetMemorySize(Object asset)
        {
            switch (asset)
            {
                case Texture texture:
                    return EstimateTextureMemorySize(texture);
                case Mesh mesh:
                    return EstimateMeshMemorySize(mesh);
                case AudioClip audioClip:
                    return EstimateAudioMemorySize(audioClip);
                case GameObject prefab:
                    return EstimatePrefabMemorySize(prefab);
                default:
                    return 1024 * 1024; // 1MB default
            }
        }

        private long EstimateTextureMemorySize(Texture texture)
        {
            if (texture is Texture2D tex2D)
            {
                int bytesPerPixel = GetBytesPerPixelForFormat(tex2D.format);
                return tex2D.width * tex2D.height * bytesPerPixel;
            }
            return 1024 * 1024; // 1MB default
        }

        private int GetBytesPerPixelForFormat(TextureFormat format)
        {
            return format switch
            {
                TextureFormat.RGBA32 => 4,
                TextureFormat.RGB24 => 3,
                TextureFormat.RGBA4444 => 2,
                TextureFormat.RGB565 => 2,
                TextureFormat.Alpha8 => 1,
                TextureFormat.DXT1 => 1,
                TextureFormat.DXT5 => 1,
                _ => 4
            };
        }

        private long EstimateMeshMemorySize(Mesh mesh)
        {
            return (mesh.vertexCount * 32) + (mesh.triangles.Length * 4); // Approximate
        }

        private long EstimateAudioMemorySize(AudioClip clip)
        {
            return (long)(clip.frequency * clip.length * clip.channels * 2); // 16-bit audio
        }

        private long EstimatePrefabMemorySize(GameObject prefab)
        {
            long totalSize = 0;
            
            var renderers = prefab.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null && material.mainTexture != null)
                    {
                        totalSize += EstimateTextureMemorySize(material.mainTexture);
                    }
                }
            }

            var meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null)
                {
                    totalSize += EstimateMeshMemorySize(meshFilter.sharedMesh);
                }
            }

            return totalSize > 0 ? totalSize : 1024 * 1024; // 1MB minimum
        }

        #endregion

        #region Asset Streaming

        private void UpdateAssetStreaming()
        {
            if (!configuration.enableAssetStreaming) return;

            // Get player position for streaming
            var playerTransform = GetPlayerTransform();
            if (playerTransform == null) return;

            Vector3 playerPosition = playerTransform.position;
            
            // Find assets within streaming distance
            var nearbyAssets = FindNearbyAssets(playerPosition, configuration.streamingDistanceMeters);
            
            // Load nearby assets
            foreach (string assetKey in nearbyAssets)
            {
                if (!managedAssets.ContainsKey(assetKey) || !managedAssets[assetKey].isLoaded)
                {
                    LoadAssetAsync<Object>(assetKey, LoadPriority.Background);
                }
            }

            // Predictive loading
            if (configuration.predictiveLoading)
            {
                PerformPredictiveLoading(playerPosition, playerTransform);
            }
        }

        private Transform GetPlayerTransform()
        {
            // Try to find player controller
            var playerController = FindObjectOfType<Player.PlayerController>();
            return playerController?.transform;
        }

        private List<string> FindNearbyAssets(Vector3 position, float distance)
        {
            // This would normally query a spatial database of asset positions
            // For now, return a placeholder list
            return new List<string>();
        }

        private void PerformPredictiveLoading(Vector3 currentPosition, Transform playerTransform)
        {
            // Predict future position based on velocity
            var rigidbody = playerTransform.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                Vector3 predictedPosition = currentPosition + (rigidbody.velocity * configuration.predictionTimeSeconds);
                var predictedAssets = FindNearbyAssets(predictedPosition, configuration.streamingDistanceMeters * 0.5f);
                
                foreach (string assetKey in predictedAssets)
                {
                    LoadAssetAsync<Object>(assetKey, LoadPriority.Low);
                }
            }
        }

        #endregion

        #region Asset Cleanup

        private IEnumerator PeriodicCleanupCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(configuration.assetCacheTimeoutMinutes * 60f);
                PerformAssetCleanup();
            }
        }

        private void PerformAssetCleanup()
        {
            if (!configuration.enableAssetCaching) return;

            var assetsToUnload = new List<string>();
            float currentTime = Time.time;
            float timeoutSeconds = configuration.assetCacheTimeoutMinutes * 60f;

            foreach (var kvp in managedAssets)
            {
                var assetRef = kvp.Value;
                
                if (!assetRef.isPersistent && 
                    assetRef.isLoaded && 
                    (currentTime - assetRef.lastAccessTime) > timeoutSeconds)
                {
                    assetsToUnload.Add(kvp.Key);
                }
            }

            foreach (string assetKey in assetsToUnload.Take(10)) // Limit cleanup per cycle
            {
                UnloadAsset(assetKey);
            }

            if (assetsToUnload.Count > 0)
            {
                Debug.Log($"AssetManager: Cleaned up {assetsToUnload.Count} unused assets");
            }
        }

        public void UnloadAsset(string assetKey)
        {
            if (!managedAssets.ContainsKey(assetKey)) return;

            var assetRef = managedAssets[assetKey];
            
            if (assetRef.isLoaded && !assetRef.isPersistent)
            {
                // Release Addressable handle if it exists
                if (pendingLoads.ContainsKey(assetKey))
                {
                    Addressables.Release(pendingLoads[assetKey]);
                    pendingLoads.Remove(assetKey);
                }

                // Update memory tracking
                memoryUsageByType[assetRef.type] -= assetRef.memorySize;

                // Remove from type list
                assetsByType[assetRef.type].Remove(assetKey);

                // Remove from managed assets
                managedAssets.Remove(assetKey);

                OnAssetUnloaded?.Invoke(assetKey);

                if (debugMode)
                {
                    Debug.Log($"Unloaded asset: {assetKey}");
                }
            }
        }

        public void UnloadUnusedAssets()
        {
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get asset by key (loads if not cached)
        /// </summary>
        public T GetAsset<T>(string assetKey) where T : Object
        {
            if (managedAssets.ContainsKey(assetKey) && managedAssets[assetKey].isLoaded)
            {
                UpdateAssetAccess(assetKey);
                return managedAssets[assetKey].cachedAsset as T;
            }

            // Try to load synchronously (not recommended for large assets)
            return LoadAssetSync<T>(assetKey);
        }

        /// <summary>
        /// Check if asset is loaded
        /// </summary>
        public bool IsAssetLoaded(string assetKey)
        {
            return managedAssets.ContainsKey(assetKey) && managedAssets[assetKey].isLoaded;
        }

        /// <summary>
        /// Get memory usage statistics
        /// </summary>
        public Dictionary<AssetType, long> GetMemoryUsageByType()
        {
            return new Dictionary<AssetType, long>(memoryUsageByType);
        }

        /// <summary>
        /// Get loaded asset count by type
        /// </summary>
        public Dictionary<AssetType, int> GetLoadedAssetCountByType()
        {
            var counts = new Dictionary<AssetType, int>();
            
            foreach (AssetType type in System.Enum.GetValues(typeof(AssetType)))
            {
                counts[type] = assetsByType[type].Count(key => managedAssets.ContainsKey(key) && managedAssets[key].isLoaded);
            }
            
            return counts;
        }

        /// <summary>
        /// Force cleanup of all non-persistent assets
        /// </summary>
        public void ForceCleanup()
        {
            var assetsToUnload = managedAssets.Where(kvp => !kvp.Value.isPersistent)
                                            .Select(kvp => kvp.Key)
                                            .ToList();
            
            foreach (string assetKey in assetsToUnload)
            {
                UnloadAsset(assetKey);
            }

            UnloadUnusedAssets();
        }

        /// <summary>
        /// Get asset manager statistics
        /// </summary>
        public string GetStatistics()
        {
            int totalLoaded = managedAssets.Count(kvp => kvp.Value.isLoaded);
            long totalMemory = memoryUsageByType.Values.Sum();
            
            return $"Assets Loaded: {totalLoaded}/{managedAssets.Count}, " +
                   $"Memory Used: {totalMemory / (1024 * 1024):F1} MB, " +
                   $"Active Operations: {activeOperations.Count}, " +
                   $"Pending Loads: {pendingLoads.Count}";
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, 50, 290, 350));
            
            GUILayout.Label("=== ASSET MANAGER ===");
            GUILayout.Label($"Loaded Assets: {managedAssets.Count(kvp => kvp.Value.isLoaded)}/{managedAssets.Count}");
            GUILayout.Label($"Pending Loads: {pendingLoads.Count}");
            GUILayout.Label($"Queue Size: {loadQueue.Count}");
            
            long totalMemory = memoryUsageByType.Values.Sum();
            GUILayout.Label($"Total Memory: {totalMemory / (1024 * 1024):F1} MB");
            
            GUILayout.Space(10);
            GUILayout.Label("=== MEMORY BY TYPE ===");
            
            foreach (var kvp in memoryUsageByType.Take(5))
            {
                float memoryMB = kvp.Value / (1024f * 1024f);
                GUILayout.Label($"{kvp.Key}: {memoryMB:F1} MB");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            if (GUILayout.Button("Force Cleanup"))
            {
                ForceCleanup();
            }
            
            if (GUILayout.Button("Unload Unused"))
            {
                UnloadUnusedAssets();
            }
            
            configuration.enableAssetStreaming = GUILayout.Toggle(configuration.enableAssetStreaming, "Asset Streaming");
            configuration.enablePreloading = GUILayout.Toggle(configuration.enablePreloading, "Preloading");
            
            GUILayout.EndArea();
        }

        #endregion
    }
}
