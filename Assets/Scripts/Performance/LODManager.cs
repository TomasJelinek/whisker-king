using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WhiskerKing.Performance
{
    /// <summary>
    /// LOD (Level of Detail) Manager for Whisker King
    /// Automatically manages mesh quality based on distance from camera and performance requirements
    /// Optimizes rendering performance by reducing polygon count for distant objects
    /// </summary>
    public class LODManager : MonoBehaviour
    {
        [System.Serializable]
        public class LODSettings
        {
            [Header("Distance Thresholds")]
            public float highDetailDistance = 15f;
            public float mediumDetailDistance = 35f;
            public float lowDetailDistance = 75f;
            public float cullDistance = 150f;

            [Header("Quality Multipliers")]
            [Range(0.1f, 1f)] public float highDetailQuality = 1f;
            [Range(0.1f, 1f)] public float mediumDetailQuality = 0.6f;
            [Range(0.1f, 1f)] public float lowDetailQuality = 0.3f;

            [Header("Performance Scaling")]
            public bool adaptiveQuality = true;
            public float performanceThreshold = 0.8f;
            public float qualityAdjustmentSpeed = 0.1f;

            [Header("Platform Overrides")]
            public bool useMobileOptimization = true;
            public float mobileDistanceMultiplier = 0.7f;
            public float webglDistanceMultiplier = 0.8f;
        }

        [System.Serializable]
        public class LODConfiguration
        {
            public string configurationName = "Default";
            public LODSettings settings = new LODSettings();
            public List<GameObject> managedObjects = new List<GameObject>();
            public bool autoDetectObjects = true;
            public LayerMask objectLayers = -1;
        }

        public enum LODLevel
        {
            HighDetail = 0,
            MediumDetail = 1,
            LowDetail = 2,
            Culled = 3
        }

        [System.Serializable]
        public class ManagedLODObject
        {
            public GameObject gameObject;
            public LODGroup lodGroup;
            public Renderer[] renderers;
            public float lastDistance;
            public LODLevel currentLODLevel;
            public bool isVisible;
            public bool hasCustomLODs;
            public Vector3 lastKnownPosition;
            public Bounds bounds;
            
            // Performance tracking
            public int triangleCountHigh;
            public int triangleCountMedium;
            public int triangleCountLow;
            public float lastUpdateTime;
        }

        // Singleton pattern
        private static LODManager instance;
        public static LODManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<LODManager>();
                    if (instance == null)
                    {
                        GameObject lodManagerGO = new GameObject("LODManager");
                        instance = lodManagerGO.AddComponent<LODManager>();
                        DontDestroyOnLoad(lodManagerGO);
                    }
                }
                return instance;
            }
        }

        [Header("LOD Configuration")]
        [SerializeField] private LODConfiguration configuration = new LODConfiguration();
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool enableLODSystem = true;
        [SerializeField] private float updateInterval = 0.1f;

        [Header("Performance Integration")]
        [SerializeField] private bool integrateWithPerformanceManager = true;
        [SerializeField] private float performanceBasedQualityScaling = 1f;

        [Header("Culling Settings")]
        [SerializeField] private bool enableFrustumCulling = true;
        [SerializeField] private bool enableOcclusionCulling = true;
        [SerializeField] private float occlusionCullingAccuracy = 4f;

        // Component references
        private Camera mainCamera;
        private PerformanceManager performanceManager;

        // Managed objects
        private List<ManagedLODObject> managedObjects = new List<ManagedLODObject>();
        private Dictionary<GameObject, ManagedLODObject> objectLookup = new Dictionary<GameObject, ManagedLODObject>();

        // Performance tracking
        private float lastUpdateTime = 0f;
        private int totalTrianglesRendered = 0;
        private int totalObjectsRendered = 0;
        private int culledObjectsCount = 0;

        // Current settings (can be adjusted at runtime)
        private LODSettings currentSettings;
        private float currentQualityMultiplier = 1f;

        // Events
        public System.Action<ManagedLODObject, LODLevel> OnLODChanged;
        public System.Action<int, int> OnPerformanceUpdated; // triangles, objects

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeLODManager();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            SetupLODSystem();
            
            if (configuration.autoDetectObjects)
            {
                AutoDetectLODObjects();
            }
        }

        private void Update()
        {
            if (enableLODSystem && Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateLODSystem();
                lastUpdateTime = Time.time;
            }
        }

        #endregion

        #region Initialization

        private void InitializeLODManager()
        {
            // Get component references
            mainCamera = Camera.main ?? FindObjectOfType<Camera>();
            
            if (integrateWithPerformanceManager)
            {
                performanceManager = PerformanceManager.Instance;
            }

            // Initialize current settings
            currentSettings = configuration.settings;
            ApplyPlatformOptimizations();

            Debug.Log("LODManager initialized");
        }

        private void SetupLODSystem()
        {
            // Configure LOD bias based on quality settings
            QualitySettings.lodBias = currentQualityMultiplier;
            
            // Setup occlusion culling
            if (enableOcclusionCulling)
            {
                Camera.main.useOcclusionCulling = true;
            }
        }

        private void ApplyPlatformOptimizations()
        {
            // Apply platform-specific optimizations
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    if (currentSettings.useMobileOptimization)
                    {
                        currentSettings.highDetailDistance *= currentSettings.mobileDistanceMultiplier;
                        currentSettings.mediumDetailDistance *= currentSettings.mobileDistanceMultiplier;
                        currentSettings.lowDetailDistance *= currentSettings.mobileDistanceMultiplier;
                        currentSettings.cullDistance *= currentSettings.mobileDistanceMultiplier;
                    }
                    break;

                case RuntimePlatform.WebGLPlayer:
                    currentSettings.highDetailDistance *= currentSettings.webglDistanceMultiplier;
                    currentSettings.mediumDetailDistance *= currentSettings.webglDistanceMultiplier;
                    currentSettings.lowDetailDistance *= currentSettings.webglDistanceMultiplier;
                    currentSettings.cullDistance *= currentSettings.webglDistanceMultiplier;
                    break;
            }
        }

        #endregion

        #region Object Management

        /// <summary>
        /// Automatically detect and add LOD objects in the scene
        /// </summary>
        public void AutoDetectLODObjects()
        {
            Renderer[] allRenderers = FindObjectsOfType<Renderer>();
            int addedCount = 0;

            foreach (var renderer in allRenderers)
            {
                // Check if object is in the specified layers
                if (((1 << renderer.gameObject.layer) & configuration.objectLayers) != 0)
                {
                    if (!objectLookup.ContainsKey(renderer.gameObject))
                    {
                        AddLODObject(renderer.gameObject);
                        addedCount++;
                    }
                }
            }

            Debug.Log($"LODManager: Auto-detected and added {addedCount} LOD objects");
        }

        /// <summary>
        /// Add a GameObject to LOD management
        /// </summary>
        public ManagedLODObject AddLODObject(GameObject obj)
        {
            if (obj == null || objectLookup.ContainsKey(obj))
            {
                return objectLookup.GetValueOrDefault(obj);
            }

            var managedObject = new ManagedLODObject
            {
                gameObject = obj,
                lodGroup = obj.GetComponent<LODGroup>(),
                renderers = obj.GetComponentsInChildren<Renderer>(),
                currentLODLevel = LODLevel.HighDetail,
                isVisible = true,
                hasCustomLODs = obj.GetComponent<LODGroup>() != null,
                lastKnownPosition = obj.transform.position,
                bounds = CalculateObjectBounds(obj),
                lastUpdateTime = Time.time
            };

            // Calculate triangle counts for performance tracking
            CalculateTriangleCounts(managedObject);

            // Setup LOD Group if it doesn't exist
            if (!managedObject.hasCustomLODs)
            {
                SetupAutomaticLOD(managedObject);
            }

            managedObjects.Add(managedObject);
            objectLookup[obj] = managedObject;

            return managedObject;
        }

        /// <summary>
        /// Remove a GameObject from LOD management
        /// </summary>
        public void RemoveLODObject(GameObject obj)
        {
            if (objectLookup.TryGetValue(obj, out ManagedLODObject managedObject))
            {
                managedObjects.Remove(managedObject);
                objectLookup.Remove(obj);
            }
        }

        private void SetupAutomaticLOD(ManagedLODObject managedObject)
        {
            if (managedObject.renderers.Length == 0) return;

            // Add LODGroup component
            LODGroup lodGroup = managedObject.gameObject.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = managedObject.gameObject.AddComponent<LODGroup>();
                managedObject.lodGroup = lodGroup;
            }

            // Create LOD levels
            LOD[] lods = new LOD[3];
            
            // High detail (100% quality)
            lods[0] = new LOD(currentSettings.highDetailQuality, managedObject.renderers);
            
            // Medium detail (60% quality) - would need actual lower poly meshes
            lods[1] = new LOD(currentSettings.mediumDetailQuality, managedObject.renderers);
            
            // Low detail (30% quality) - would need actual lower poly meshes
            lods[2] = new LOD(currentSettings.lowDetailQuality, managedObject.renderers);

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            
            managedObject.hasCustomLODs = true;
        }

        private Bounds CalculateObjectBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(obj.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private void CalculateTriangleCounts(ManagedLODObject managedObject)
        {
            int totalTriangles = 0;
            
            foreach (var renderer in managedObject.renderers)
            {
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    totalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
                }
            }

            managedObject.triangleCountHigh = totalTriangles;
            managedObject.triangleCountMedium = Mathf.RoundToInt(totalTriangles * currentSettings.mediumDetailQuality);
            managedObject.triangleCountLow = Mathf.RoundToInt(totalTriangles * currentSettings.lowDetailQuality);
        }

        #endregion

        #region LOD System Update

        private void UpdateLODSystem()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main ?? FindObjectOfType<Camera>();
                if (mainCamera == null) return;
            }

            // Update quality multiplier based on performance
            UpdatePerformanceBasedQuality();

            // Reset counters
            totalTrianglesRendered = 0;
            totalObjectsRendered = 0;
            culledObjectsCount = 0;

            Vector3 cameraPosition = mainCamera.transform.position;
            Plane[] frustumPlanes = enableFrustumCulling ? GeometryUtility.CalculateFrustumPlanes(mainCamera) : null;

            // Update each managed object
            foreach (var managedObject in managedObjects)
            {
                if (managedObject.gameObject == null)
                {
                    continue; // Skip destroyed objects
                }

                UpdateManagedObject(managedObject, cameraPosition, frustumPlanes);
            }

            // Trigger performance update event
            OnPerformanceUpdated?.Invoke(totalTrianglesRendered, totalObjectsRendered);

            // Clean up destroyed objects
            CleanupDestroyedObjects();
        }

        private void UpdateManagedObject(ManagedLODObject managedObject, Vector3 cameraPosition, Plane[] frustumPlanes)
        {
            // Update position and bounds
            managedObject.lastKnownPosition = managedObject.gameObject.transform.position;
            managedObject.bounds = CalculateObjectBounds(managedObject.gameObject);

            // Calculate distance to camera
            float distance = Vector3.Distance(cameraPosition, managedObject.lastKnownPosition);
            managedObject.lastDistance = distance;

            // Frustum culling check
            bool inFrustum = true;
            if (enableFrustumCulling && frustumPlanes != null)
            {
                inFrustum = GeometryUtility.TestPlanesAABB(frustumPlanes, managedObject.bounds);
            }

            // Determine appropriate LOD level
            LODLevel newLODLevel = CalculateLODLevel(distance, inFrustum);
            
            // Apply LOD changes
            if (newLODLevel != managedObject.currentLODLevel)
            {
                ApplyLODLevel(managedObject, newLODLevel);
                managedObject.currentLODLevel = newLODLevel;
                OnLODChanged?.Invoke(managedObject, newLODLevel);
            }

            // Update performance counters
            if (managedObject.isVisible && newLODLevel != LODLevel.Culled)
            {
                totalObjectsRendered++;
                totalTrianglesRendered += GetTriangleCountForLOD(managedObject, newLODLevel);
            }
            else
            {
                culledObjectsCount++;
            }

            managedObject.lastUpdateTime = Time.time;
        }

        private LODLevel CalculateLODLevel(float distance, bool inFrustum)
        {
            if (!inFrustum || distance > currentSettings.cullDistance * currentQualityMultiplier)
            {
                return LODLevel.Culled;
            }
            else if (distance > currentSettings.lowDetailDistance * currentQualityMultiplier)
            {
                return LODLevel.LowDetail;
            }
            else if (distance > currentSettings.mediumDetailDistance * currentQualityMultiplier)
            {
                return LODLevel.MediumDetail;
            }
            else
            {
                return LODLevel.HighDetail;
            }
        }

        private void ApplyLODLevel(ManagedLODObject managedObject, LODLevel lodLevel)
        {
            bool shouldBeVisible = lodLevel != LODLevel.Culled;

            // Update visibility
            managedObject.isVisible = shouldBeVisible;

            // If object has custom LODGroup, let it handle the switching
            if (managedObject.hasCustomLODs && managedObject.lodGroup != null)
            {
                // LODGroup handles this automatically based on distance
                foreach (var renderer in managedObject.renderers)
                {
                    renderer.enabled = shouldBeVisible;
                }
            }
            else
            {
                // Manual LOD switching for objects without LODGroup
                foreach (var renderer in managedObject.renderers)
                {
                    renderer.enabled = shouldBeVisible;
                    
                    if (shouldBeVisible)
                    {
                        // Apply quality-based material settings if needed
                        ApplyQualitySettings(renderer, lodLevel);
                    }
                }
            }
        }

        private void ApplyQualitySettings(Renderer renderer, LODLevel lodLevel)
        {
            // Apply material quality settings based on LOD level
            // This is where you might swap materials, adjust texture quality, etc.
            
            switch (lodLevel)
            {
                case LODLevel.HighDetail:
                    // Full quality materials and textures
                    break;
                    
                case LODLevel.MediumDetail:
                    // Medium quality materials
                    break;
                    
                case LODLevel.LowDetail:
                    // Low quality materials, possibly unlit shaders
                    break;
            }
        }

        private int GetTriangleCountForLOD(ManagedLODObject managedObject, LODLevel lodLevel)
        {
            return lodLevel switch
            {
                LODLevel.HighDetail => managedObject.triangleCountHigh,
                LODLevel.MediumDetail => managedObject.triangleCountMedium,
                LODLevel.LowDetail => managedObject.triangleCountLow,
                _ => 0
            };
        }

        private void UpdatePerformanceBasedQuality()
        {
            if (!currentSettings.adaptiveQuality || performanceManager == null)
            {
                return;
            }

            var performanceMetrics = performanceManager.GetPerformanceMetrics();
            float performanceRatio = performanceMetrics.averageFrameRate / performanceManager.GetPerformanceBudget().targetFrameRate;

            float targetQuality = performanceRatio > currentSettings.performanceThreshold ? 1f : 
                                 Mathf.Lerp(0.5f, 1f, performanceRatio / currentSettings.performanceThreshold);

            currentQualityMultiplier = Mathf.Lerp(currentQualityMultiplier, targetQuality, 
                                                 currentSettings.qualityAdjustmentSpeed * Time.deltaTime);

            QualitySettings.lodBias = currentQualityMultiplier;
        }

        private void CleanupDestroyedObjects()
        {
            managedObjects.RemoveAll(obj => obj.gameObject == null);
            
            var keysToRemove = objectLookup.Where(kvp => kvp.Key == null).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                objectLookup.Remove(key);
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get LOD statistics
        /// </summary>
        public string GetLODStatistics()
        {
            int highDetailCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.HighDetail);
            int mediumDetailCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.MediumDetail);
            int lowDetailCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.LowDetail);
            int culledCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.Culled);

            return $"LOD Statistics: High={highDetailCount}, Medium={mediumDetailCount}, " +
                   $"Low={lowDetailCount}, Culled={culledCount}, " +
                   $"Triangles={totalTrianglesRendered:N0}, Quality={currentQualityMultiplier:F2}";
        }

        /// <summary>
        /// Set LOD distances manually
        /// </summary>
        public void SetLODDistances(float high, float medium, float low, float cull)
        {
            currentSettings.highDetailDistance = high;
            currentSettings.mediumDetailDistance = medium;
            currentSettings.lowDetailDistance = low;
            currentSettings.cullDistance = cull;
        }

        /// <summary>
        /// Enable/disable LOD system
        /// </summary>
        public void SetLODEnabled(bool enabled)
        {
            enableLODSystem = enabled;
            
            if (!enabled)
            {
                // Reset all objects to high detail
                foreach (var managedObject in managedObjects)
                {
                    ApplyLODLevel(managedObject, LODLevel.HighDetail);
                }
            }
        }

        /// <summary>
        /// Force update all LOD objects
        /// </summary>
        public void ForceUpdateAllLODs()
        {
            UpdateLODSystem();
        }

        /// <summary>
        /// Get managed object info
        /// </summary>
        public ManagedLODObject GetManagedObject(GameObject obj)
        {
            return objectLookup.GetValueOrDefault(obj);
        }

        /// <summary>
        /// Get total managed objects count
        /// </summary>
        public int GetManagedObjectsCount()
        {
            return managedObjects.Count;
        }

        #endregion

        #region Debug

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(10, 760, 450, 200));
                
                GUILayout.Label("=== LOD MANAGER DEBUG ===");
                GUILayout.Label($"Managed Objects: {managedObjects.Count}");
                GUILayout.Label($"Rendered Objects: {totalObjectsRendered}");
                GUILayout.Label($"Culled Objects: {culledObjectsCount}");
                GUILayout.Label($"Total Triangles: {totalTrianglesRendered:N0}");
                GUILayout.Label($"Quality Multiplier: {currentQualityMultiplier:F2}");
                GUILayout.Label($"System Enabled: {enableLODSystem}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== LOD DISTRIBUTION ===");
                int highCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.HighDetail);
                int mediumCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.MediumDetail);
                int lowCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.LowDetail);
                int culledCount = managedObjects.Count(obj => obj.currentLODLevel == LODLevel.Culled);
                
                GUILayout.Label($"High Detail: {highCount}");
                GUILayout.Label($"Medium Detail: {mediumCount}");
                GUILayout.Label($"Low Detail: {lowCount}");
                GUILayout.Label($"Culled: {culledCount}");
                
                GUILayout.Space(5);
                if (GUILayout.Button("Force Update All LODs"))
                {
                    ForceUpdateAllLODs();
                }
                
                if (GUILayout.Button("Auto-Detect Objects"))
                {
                    AutoDetectLODObjects();
                }
                
                GUILayout.EndArea();
            }
        }

        private void OnDrawGizmos()
        {
            if (debugMode && mainCamera != null)
            {
                // Draw LOD distance spheres
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(mainCamera.transform.position, currentSettings.highDetailDistance);
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(mainCamera.transform.position, currentSettings.mediumDetailDistance);
                
                Gizmos.color = Color.orange;
                Gizmos.DrawWireSphere(mainCamera.transform.position, currentSettings.lowDetailDistance);
                
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(mainCamera.transform.position, currentSettings.cullDistance);
            }
        }

        #endregion
    }
}
