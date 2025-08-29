using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace WhiskerKing.Performance
{
    /// <summary>
    /// Generic Object Pooling System for Whisker King
    /// Optimizes performance by reusing GameObjects instead of creating/destroying them
    /// Supports multiple pool types, automatic cleanup, and performance monitoring
    /// </summary>
    public class ObjectPool<T> : IDisposable where T : MonoBehaviour
    {
        public class PoolItem
        {
            public T component;
            public GameObject gameObject;
            public bool isActive;
            public float lastUsedTime;
            public int usageCount;

            public PoolItem(T comp)
            {
                component = comp;
                gameObject = comp.gameObject;
                isActive = false;
                lastUsedTime = Time.time;
                usageCount = 0;
            }
        }

        private readonly Queue<PoolItem> availableItems = new Queue<PoolItem>();
        private readonly List<PoolItem> allItems = new List<PoolItem>();
        private readonly T prefab;
        private readonly Transform parent;
        private readonly int maxPoolSize;
        private readonly float itemLifetime;
        private readonly string poolName;
        private readonly bool autoCleanup;
        private readonly bool expandable;

        // Performance tracking
        private int totalCreated = 0;
        private int totalReused = 0;
        private int totalDestroyed = 0;
        private float lastCleanupTime = 0f;

        public int TotalCreated => totalCreated;
        public int TotalReused => totalReused;
        public int TotalDestroyed => totalDestroyed;
        public int ActiveCount => allItems.Count(item => item.isActive);
        public int AvailableCount => availableItems.Count;
        public int TotalCount => allItems.Count;
        public float PoolEfficiency => totalCreated > 0 ? (float)totalReused / totalCreated : 0f;

        public ObjectPool(T prefab, int initialSize = 10, int maxSize = 100, Transform parent = null, 
                         float itemLifetime = 300f, bool autoCleanup = true, bool expandable = true, string name = null)
        {
            this.prefab = prefab;
            this.maxPoolSize = maxSize;
            this.parent = parent;
            this.itemLifetime = itemLifetime;
            this.autoCleanup = autoCleanup;
            this.expandable = expandable;
            this.poolName = name ?? typeof(T).Name + "Pool";

            // Pre-populate pool
            PrePopulatePool(initialSize);

            Debug.Log($"ObjectPool '{poolName}' created with {initialSize} initial items");
        }

        private void PrePopulatePool(int count)
        {
            for (int i = 0; i < count && i < maxPoolSize; i++)
            {
                CreateNewItem();
            }
        }

        private PoolItem CreateNewItem()
        {
            GameObject newObject = UnityEngine.Object.Instantiate(prefab.gameObject, parent);
            newObject.SetActive(false);
            newObject.name = $"{prefab.name}_{totalCreated}";

            T component = newObject.GetComponent<T>();
            PoolItem item = new PoolItem(component);

            allItems.Add(item);
            availableItems.Enqueue(item);
            totalCreated++;

            return item;
        }

        /// <summary>
        /// Get an object from the pool
        /// </summary>
        public T Get(Vector3 position = default, Quaternion rotation = default)
        {
            PoolItem item = null;

            // Try to get from available items
            while (availableItems.Count > 0)
            {
                item = availableItems.Dequeue();
                if (item != null && item.gameObject != null)
                {
                    break;
                }
                item = null; // Item was destroyed externally
            }

            // If no available items and pool can expand, create new one
            if (item == null && expandable && allItems.Count < maxPoolSize)
            {
                item = CreateNewItem();
            }

            // If still no item available, force reuse oldest active item
            if (item == null)
            {
                item = GetOldestActiveItem();
                if (item != null)
                {
                    ReturnToPool(item.component); // Force return it first
                }
            }

            if (item != null)
            {
                // Activate and setup the item
                item.gameObject.transform.position = position;
                item.gameObject.transform.rotation = rotation;
                item.gameObject.SetActive(true);
                item.isActive = true;
                item.lastUsedTime = Time.time;
                item.usageCount++;
                totalReused++;

                return item.component;
            }

            Debug.LogWarning($"ObjectPool '{poolName}' failed to provide an item!");
            return null;
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void ReturnToPool(T item)
        {
            if (item == null) return;

            PoolItem poolItem = allItems.FirstOrDefault(pi => pi.component == item);
            if (poolItem != null && poolItem.isActive)
            {
                poolItem.gameObject.SetActive(false);
                poolItem.isActive = false;
                poolItem.lastUsedTime = Time.time;

                // Reset transform parent if it was changed
                if (poolItem.gameObject.transform.parent != parent)
                {
                    poolItem.gameObject.transform.SetParent(parent);
                }

                // Call cleanup method if exists
                if (item is IPoolable poolable)
                {
                    poolable.OnReturnToPool();
                }

                availableItems.Enqueue(poolItem);
            }
        }

        /// <summary>
        /// Get the oldest active item for forced reuse
        /// </summary>
        private PoolItem GetOldestActiveItem()
        {
            return allItems.Where(item => item.isActive)
                          .OrderBy(item => item.lastUsedTime)
                          .FirstOrDefault();
        }

        /// <summary>
        /// Clean up old unused items
        /// </summary>
        public void CleanupOldItems(float maxAge = -1f)
        {
            if (maxAge < 0f) maxAge = itemLifetime;

            float currentTime = Time.time;
            List<PoolItem> itemsToRemove = new List<PoolItem>();

            foreach (var item in allItems.ToList())
            {
                if (!item.isActive && (currentTime - item.lastUsedTime) > maxAge)
                {
                    itemsToRemove.Add(item);
                }
            }

            foreach (var item in itemsToRemove)
            {
                RemoveItem(item);
            }

            if (itemsToRemove.Count > 0)
            {
                Debug.Log($"ObjectPool '{poolName}' cleaned up {itemsToRemove.Count} old items");
            }

            lastCleanupTime = currentTime;
        }

        private void RemoveItem(PoolItem item)
        {
            allItems.Remove(item);
            
            // Remove from available queue if it's there
            var availableList = availableItems.ToList();
            availableList.Remove(item);
            availableItems.Clear();
            foreach (var availableItem in availableList)
            {
                availableItems.Enqueue(availableItem);
            }

            if (item.gameObject != null)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                totalDestroyed++;
            }
        }

        /// <summary>
        /// Automatic cleanup if enabled
        /// </summary>
        public void Update()
        {
            if (autoCleanup && Time.time - lastCleanupTime > 30f) // Cleanup every 30 seconds
            {
                CleanupOldItems();
            }
        }

        /// <summary>
        /// Get pool statistics
        /// </summary>
        public string GetPoolStatistics()
        {
            return $"Pool '{poolName}': Total={TotalCount}, Active={ActiveCount}, Available={AvailableCount}, " +
                   $"Created={TotalCreated}, Reused={TotalReused}, Destroyed={TotalDestroyed}, " +
                   $"Efficiency={PoolEfficiency:P2}";
        }

        /// <summary>
        /// Clear all items from the pool
        /// </summary>
        public void Clear()
        {
            foreach (var item in allItems.ToList())
            {
                if (item.gameObject != null)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }

            allItems.Clear();
            availableItems.Clear();
            totalDestroyed += totalCreated;
            totalCreated = 0;
            totalReused = 0;

            Debug.Log($"ObjectPool '{poolName}' cleared");
        }

        public void Dispose()
        {
            Clear();
        }
    }

    /// <summary>
    /// Interface for objects that need custom cleanup when returned to pool
    /// </summary>
    public interface IPoolable
    {
        void OnReturnToPool();
    }

    /// <summary>
    /// Global Pool Manager for centralized object pooling
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager instance;
        public static PoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<PoolManager>();
                    if (instance == null)
                    {
                        GameObject poolManagerGO = new GameObject("PoolManager");
                        instance = poolManagerGO.AddComponent<PoolManager>();
                        DontDestroyOnLoad(poolManagerGO);
                    }
                }
                return instance;
            }
        }

        [Header("Pool Configuration")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private float globalCleanupInterval = 60f;
        [SerializeField] private int defaultPoolSize = 20;
        [SerializeField] private int defaultMaxPoolSize = 100;

        private Dictionary<Type, object> pools = new Dictionary<Type, object>();
        private Dictionary<string, object> namedPools = new Dictionary<string, object>();
        private float lastGlobalCleanup = 0f;

        // Performance tracking
        private Dictionary<Type, PoolStats> poolStats = new Dictionary<Type, PoolStats>();

        private struct PoolStats
        {
            public int totalGets;
            public int totalReturns;
            public float totalGetTime;
            public float averageGetTime => totalGets > 0 ? totalGetTime / totalGets : 0f;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Global cleanup
            if (Time.time - lastGlobalCleanup > globalCleanupInterval)
            {
                CleanupAllPools();
                lastGlobalCleanup = Time.time;
            }

            // Update individual pools
            foreach (var pool in pools.Values)
            {
                if (pool is ObjectPool<MonoBehaviour> objectPool)
                {
                    objectPool.Update();
                }
            }
        }

        /// <summary>
        /// Get or create a pool for the specified type
        /// </summary>
        public ObjectPool<T> GetPool<T>(T prefab, int initialSize = -1, int maxSize = -1, string poolName = null) where T : MonoBehaviour
        {
            Type type = typeof(T);
            string key = poolName ?? type.Name;

            if (namedPools.ContainsKey(key))
            {
                return namedPools[key] as ObjectPool<T>;
            }

            if (pools.ContainsKey(type))
            {
                return pools[type] as ObjectPool<T>;
            }

            // Create new pool
            int actualInitialSize = initialSize > 0 ? initialSize : defaultPoolSize;
            int actualMaxSize = maxSize > 0 ? maxSize : defaultMaxPoolSize;
            
            var pool = new ObjectPool<T>(prefab, actualInitialSize, actualMaxSize, transform, 
                                       300f, true, true, key);

            if (poolName != null)
            {
                namedPools[key] = pool;
            }
            else
            {
                pools[type] = pool;
            }

            poolStats[type] = new PoolStats();

            Debug.Log($"PoolManager: Created pool for {type.Name}");
            return pool;
        }

        /// <summary>
        /// Get an object from the appropriate pool
        /// </summary>
        public T Get<T>(T prefab, Vector3 position = default, Quaternion rotation = default, string poolName = null) where T : MonoBehaviour
        {
            float startTime = Time.realtimeSinceStartup;
            
            var pool = GetPool(prefab, poolName: poolName);
            T item = pool.Get(position, rotation);

            // Update stats
            Type type = typeof(T);
            var stats = poolStats.GetValueOrDefault(type);
            stats.totalGets++;
            stats.totalGetTime += Time.realtimeSinceStartup - startTime;
            poolStats[type] = stats;

            return item;
        }

        /// <summary>
        /// Return an object to its pool
        /// </summary>
        public void ReturnToPool<T>(T item, string poolName = null) where T : MonoBehaviour
        {
            if (item == null) return;

            Type type = typeof(T);
            string key = poolName ?? type.Name;

            ObjectPool<T> pool = null;
            
            if (poolName != null && namedPools.ContainsKey(key))
            {
                pool = namedPools[key] as ObjectPool<T>;
            }
            else if (pools.ContainsKey(type))
            {
                pool = pools[type] as ObjectPool<T>;
            }

            if (pool != null)
            {
                pool.ReturnToPool(item);
                
                // Update stats
                var stats = poolStats.GetValueOrDefault(type);
                stats.totalReturns++;
                poolStats[type] = stats;
            }
            else
            {
                Debug.LogWarning($"No pool found for {type.Name}, destroying object");
                Destroy(item.gameObject);
            }
        }

        /// <summary>
        /// Clean up all pools
        /// </summary>
        public void CleanupAllPools()
        {
            int totalCleaned = 0;

            foreach (var pool in pools.Values)
            {
                if (pool is ObjectPool<MonoBehaviour> objectPool)
                {
                    int beforeCount = objectPool.TotalCount;
                    objectPool.CleanupOldItems();
                    totalCleaned += beforeCount - objectPool.TotalCount;
                }
            }

            foreach (var pool in namedPools.Values)
            {
                if (pool is ObjectPool<MonoBehaviour> objectPool)
                {
                    int beforeCount = objectPool.TotalCount;
                    objectPool.CleanupOldItems();
                    totalCleaned += beforeCount - objectPool.TotalCount;
                }
            }

            if (totalCleaned > 0 && debugMode)
            {
                Debug.Log($"PoolManager: Global cleanup removed {totalCleaned} items");
            }
        }

        /// <summary>
        /// Get statistics for all pools
        /// </summary>
        public string GetAllPoolStatistics()
        {
            var stats = new System.Text.StringBuilder();
            stats.AppendLine("=== POOL MANAGER STATISTICS ===");

            foreach (var kvp in pools)
            {
                if (kvp.Value is ObjectPool<MonoBehaviour> pool)
                {
                    stats.AppendLine(pool.GetPoolStatistics());
                    
                    if (poolStats.ContainsKey(kvp.Key))
                    {
                        var poolStat = poolStats[kvp.Key];
                        stats.AppendLine($"  Performance: Gets={poolStat.totalGets}, Returns={poolStat.totalReturns}, " +
                                       $"AvgGetTime={poolStat.averageGetTime * 1000:F2}ms");
                    }
                }
            }

            foreach (var kvp in namedPools)
            {
                if (kvp.Value is ObjectPool<MonoBehaviour> pool)
                {
                    stats.AppendLine(pool.GetPoolStatistics());
                }
            }

            return stats.ToString();
        }

        /// <summary>
        /// Clear all pools
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in pools.Values)
            {
                if (pool is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            foreach (var pool in namedPools.Values)
            {
                if (pool is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            pools.Clear();
            namedPools.Clear();
            poolStats.Clear();

            Debug.Log("PoolManager: All pools cleared");
        }

        /// <summary>
        /// Preload pools for common objects
        /// </summary>
        public void PreloadCommonPools()
        {
            // This would be called with actual prefabs in a real implementation
            Debug.Log("PoolManager: Preloading common object pools");
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(Screen.width - 400, 10, 390, 300));
                
                GUILayout.Label("=== POOL MANAGER DEBUG ===");
                GUILayout.Label($"Total Pools: {pools.Count + namedPools.Count}");
                
                GUILayout.Space(5);
                foreach (var kvp in pools.Take(5)) // Show first 5 pools
                {
                    if (kvp.Value is ObjectPool<MonoBehaviour> pool)
                    {
                        GUILayout.Label($"{kvp.Key.Name}: {pool.ActiveCount}/{pool.TotalCount} (Eff: {pool.PoolEfficiency:P1})");
                    }
                }

                if (pools.Count > 5)
                {
                    GUILayout.Label($"... and {pools.Count - 5} more pools");
                }
                
                GUILayout.Space(5);
                if (GUILayout.Button("Cleanup All Pools"))
                {
                    CleanupAllPools();
                }
                
                if (GUILayout.Button("Clear All Pools"))
                {
                    ClearAllPools();
                }
                
                GUILayout.EndArea();
            }
        }

        private void OnDestroy()
        {
            ClearAllPools();
        }
    }
}
