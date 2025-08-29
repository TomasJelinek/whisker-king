using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using WhiskerKing.Core;
using WhiskerKing.Interactables;

namespace WhiskerKing.Level
{
    /// <summary>
    /// Comprehensive spawn system for Whisker King
    /// Handles dynamic spawning of enemies, crates, collectibles, and power-ups
    /// Integrates with level progression and difficulty scaling
    /// </summary>
    public class SpawnSystem : MonoBehaviour
    {
        public enum SpawnType
        {
            Enemy,
            Crate,
            Collectible,
            PowerUp,
            Hazard
        }

        [System.Serializable]
        public class SpawnDefinition
        {
            public SpawnType type;
            public GameObject prefab;
            public float spawnWeight = 1f;
            public int minSpawnLevel = 1;
            public Vector2 spawnRateRange = new Vector2(1f, 5f);
            public bool scaleWithDifficulty = true;
        }

        [System.Serializable]
        public class SpawnZone
        {
            public string zoneName;
            public Transform spawnArea;
            public List<SpawnType> allowedTypes = new List<SpawnType>();
            public float spawnRadius = 5f;
            public int maxActiveSpawns = 10;
            public bool playerActivated = true;
            public float activationDistance = 20f;
        }

        [Header("Spawn Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;

        [Header("Spawn Definitions")]
        [SerializeField] private List<SpawnDefinition> spawnDefinitions = new List<SpawnDefinition>();

        [Header("Spawn Zones")]
        [SerializeField] private List<SpawnZone> spawnZones = new List<SpawnZone>();

        [Header("Spawn Settings")]
        [SerializeField] private float baseSpawnRate = 2f;
        [SerializeField] private float difficultySpawnMultiplier = 1.5f;
        [SerializeField] private int maxGlobalSpawns = 50;

        [Header("Collectible Settings")]
        [SerializeField] private float collectibleSpawnChance = 0.3f;
        [SerializeField] private Vector2 fishTreatsRange = new Vector2(5, 15);
        [SerializeField] private Vector2 yarnRange = new Vector2(10, 25);
        [SerializeField] private float tokenSpawnChance = 0.1f;

        // Components
        private LevelManager levelManager;
        private Transform playerTransform;
        
        // Spawn state
        private Dictionary<SpawnZone, List<GameObject>> activeSpawnsByZone = new Dictionary<SpawnZone, List<GameObject>>();
        private Dictionary<SpawnType, float> lastSpawnTimes = new Dictionary<SpawnType, float>();
        private int totalActiveSpawns = 0;

        // Configuration cache
        private LevelDesignData levelConfig;

        // Spawn pools for performance
        private Dictionary<GameObject, Queue<GameObject>> objectPools = new Dictionary<GameObject, Queue<GameObject>>();

        // Events
        public System.Action<GameObject, Vector3> OnObjectSpawned;
        public System.Action<GameObject> OnObjectDespawned;
        public System.Action<SpawnZone, bool> OnZoneActivated;

        #region Unity Lifecycle

        private void Awake()
        {
            // Find required components
            levelManager = FindObjectOfType<LevelManager>();
            
            var player = FindObjectOfType<WhiskerKing.Player.PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Start()
        {
            LoadConfiguration();
            InitializeSpawnSystem();
        }

        private void Update()
        {
            UpdateSpawnZones();
            UpdateSpawning();
            CleanupInactiveSpawns();
            
            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode)
            {
                DrawSpawnGizmos();
            }
        }

        #endregion

        #region Configuration

        private void LoadConfiguration()
        {
            if (useGameConfiguration && GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                levelConfig = GameConfiguration.Instance.Config.levelDesign;
                ApplyConfiguration();
                Debug.Log("SpawnSystem: Configuration loaded from GameConfig");
            }
            else
            {
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            if (levelConfig?.spawning == null) return;

            baseSpawnRate = levelConfig.spawning.baseSpawnRate;
            maxGlobalSpawns = levelConfig.spawning.maxActiveSpawns;
            collectibleSpawnChance = levelConfig.spawning.collectibleChance;

            // Apply collectible ranges
            if (levelConfig.collectibles != null)
            {
                fishTreatsRange = new Vector2(levelConfig.collectibles.fishTreatsMin, levelConfig.collectibles.fishTreatsMax);
                yarnRange = new Vector2(levelConfig.collectibles.yarnMin, levelConfig.collectibles.yarnMax);
                tokenSpawnChance = levelConfig.collectibles.tokenChance;
            }
        }

        private void UseDefaultConfiguration()
        {
            // Use PRD-compliant defaults
            baseSpawnRate = 2f;
            maxGlobalSpawns = 50;
            collectibleSpawnChance = 0.3f;
            fishTreatsRange = new Vector2(5, 15);
            yarnRange = new Vector2(10, 25);
            tokenSpawnChance = 0.1f;
        }

        #endregion

        #region Initialization

        private void InitializeSpawnSystem()
        {
            // Initialize spawn zone tracking
            foreach (var zone in spawnZones)
            {
                activeSpawnsByZone[zone] = new List<GameObject>();
            }

            // Initialize spawn timing
            foreach (SpawnType spawnType in System.Enum.GetValues(typeof(SpawnType)))
            {
                lastSpawnTimes[spawnType] = 0f;
            }

            // Initialize object pools
            InitializeObjectPools();

            Debug.Log($"SpawnSystem initialized - {spawnZones.Count} zones, {spawnDefinitions.Count} definitions");
        }

        private void InitializeObjectPools()
        {
            foreach (var definition in spawnDefinitions)
            {
                if (definition.prefab != null)
                {
                    objectPools[definition.prefab] = new Queue<GameObject>();
                    
                    // Pre-populate pools with initial objects
                    int initialPoolSize = 5;
                    for (int i = 0; i < initialPoolSize; i++)
                    {
                        GameObject pooledObject = Instantiate(definition.prefab);
                        pooledObject.SetActive(false);
                        objectPools[definition.prefab].Enqueue(pooledObject);
                    }
                }
            }
        }

        #endregion

        #region Spawn Zone Management

        private void UpdateSpawnZones()
        {
            if (playerTransform == null) return;

            foreach (var zone in spawnZones)
            {
                if (zone.playerActivated)
                {
                    float distanceToPlayer = Vector3.Distance(playerTransform.position, zone.spawnArea.position);
                    bool shouldBeActive = distanceToPlayer <= zone.activationDistance;
                    
                    bool wasActive = activeSpawnsByZone[zone].Count > 0 || IsZoneActiveForSpawning(zone);
                    
                    if (shouldBeActive && !wasActive)
                    {
                        ActivateZone(zone);
                    }
                    else if (!shouldBeActive && wasActive)
                    {
                        DeactivateZone(zone);
                    }
                }
            }
        }

        private void ActivateZone(SpawnZone zone)
        {
            OnZoneActivated?.Invoke(zone, true);
            
            if (debugMode)
            {
                Debug.Log($"Activated spawn zone: {zone.zoneName}");
            }
        }

        private void DeactivateZone(SpawnZone zone)
        {
            OnZoneActivated?.Invoke(zone, false);
            
            if (debugMode)
            {
                Debug.Log($"Deactivated spawn zone: {zone.zoneName}");
            }
        }

        private bool IsZoneActiveForSpawning(SpawnZone zone)
        {
            if (!zone.playerActivated) return true;
            if (playerTransform == null) return false;
            
            float distance = Vector3.Distance(playerTransform.position, zone.spawnArea.position);
            return distance <= zone.activationDistance;
        }

        #endregion

        #region Spawning Logic

        private void UpdateSpawning()
        {
            if (totalActiveSpawns >= maxGlobalSpawns) return;

            foreach (var zone in spawnZones)
            {
                if (!IsZoneActiveForSpawning(zone)) continue;
                if (activeSpawnsByZone[zone].Count >= zone.maxActiveSpawns) continue;

                // Try to spawn in this zone
                TrySpawnInZone(zone);
            }
        }

        private void TrySpawnInZone(SpawnZone zone)
        {
            foreach (var allowedType in zone.allowedTypes)
            {
                if (ShouldSpawnType(allowedType))
                {
                    SpawnInZone(zone, allowedType);
                    break; // Only spawn one type per frame per zone
                }
            }
        }

        private bool ShouldSpawnType(SpawnType spawnType)
        {
            float currentTime = Time.time;
            float timeSinceLastSpawn = currentTime - lastSpawnTimes[spawnType];
            
            // Get spawn rate for this type
            float spawnRate = GetSpawnRateForType(spawnType);
            float spawnInterval = 1f / spawnRate;
            
            return timeSinceLastSpawn >= spawnInterval;
        }

        private float GetSpawnRateForType(SpawnType spawnType)
        {
            float rate = baseSpawnRate;
            
            // Apply difficulty scaling
            if (levelManager != null)
            {
                float difficultyMultiplier = GetCurrentDifficultyMultiplier();
                rate *= difficultyMultiplier;
            }
            
            // Apply type-specific modifiers
            switch (spawnType)
            {
                case SpawnType.Collectible:
                    rate *= collectibleSpawnChance;
                    break;
                case SpawnType.Enemy:
                    rate *= difficultySpawnMultiplier;
                    break;
                case SpawnType.Crate:
                    rate *= 0.8f; // Crates spawn slightly less frequently
                    break;
                case SpawnType.PowerUp:
                    rate *= 0.3f; // Power-ups are rare
                    break;
                case SpawnType.Hazard:
                    rate *= 0.5f; // Moderate hazard frequency
                    break;
            }
            
            return rate;
        }

        private float GetCurrentDifficultyMultiplier()
        {
            // Get difficulty from level manager if available
            return 1.0f; // Placeholder - would integrate with level manager's difficulty system
        }

        private void SpawnInZone(SpawnZone zone, SpawnType spawnType)
        {
            // Find appropriate spawn definition
            var spawnDef = GetRandomSpawnDefinition(spawnType);
            if (spawnDef == null || spawnDef.prefab == null) return;

            // Get spawn position within zone
            Vector3 spawnPosition = GetRandomPositionInZone(zone);
            
            // Spawn object
            GameObject spawnedObject = SpawnObject(spawnDef, spawnPosition);
            if (spawnedObject != null)
            {
                // Track spawned object
                activeSpawnsByZone[zone].Add(spawnedObject);
                totalActiveSpawns++;
                lastSpawnTimes[spawnType] = Time.time;

                // Configure spawned object
                ConfigureSpawnedObject(spawnedObject, spawnType, zone);

                OnObjectSpawned?.Invoke(spawnedObject, spawnPosition);

                if (debugMode)
                {
                    Debug.Log($"Spawned {spawnType} in zone {zone.zoneName} at {spawnPosition}");
                }
            }
        }

        private SpawnDefinition GetRandomSpawnDefinition(SpawnType spawnType)
        {
            var validDefinitions = spawnDefinitions.FindAll(def => def.type == spawnType);
            if (validDefinitions.Count == 0) return null;

            // Weighted random selection
            float totalWeight = 0f;
            foreach (var def in validDefinitions)
            {
                totalWeight += def.spawnWeight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var def in validDefinitions)
            {
                currentWeight += def.spawnWeight;
                if (randomValue <= currentWeight)
                {
                    return def;
                }
            }

            return validDefinitions[0]; // Fallback
        }

        private Vector3 GetRandomPositionInZone(SpawnZone zone)
        {
            Vector3 zoneCenter = zone.spawnArea.position;
            Vector2 randomCircle = Random.insideUnitCircle * zone.spawnRadius;
            
            return zoneCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        private GameObject SpawnObject(SpawnDefinition spawnDef, Vector3 position)
        {
            GameObject spawnedObject = null;

            // Try to get from object pool first
            if (objectPools.ContainsKey(spawnDef.prefab) && objectPools[spawnDef.prefab].Count > 0)
            {
                spawnedObject = objectPools[spawnDef.prefab].Dequeue();
                spawnedObject.transform.position = position;
                spawnedObject.SetActive(true);
            }
            else
            {
                // Create new instance
                spawnedObject = Instantiate(spawnDef.prefab, position, Quaternion.identity);
            }

            return spawnedObject;
        }

        private void ConfigureSpawnedObject(GameObject spawnedObject, SpawnType spawnType, SpawnZone zone)
        {
            // Add despawn component for automatic cleanup
            var despawnComponent = spawnedObject.GetComponent<DespawnComponent>();
            if (despawnComponent == null)
            {
                despawnComponent = spawnedObject.AddComponent<DespawnComponent>();
            }
            
            despawnComponent.Initialize(this, zone, 30f); // 30 second lifetime

            // Configure based on spawn type
            switch (spawnType)
            {
                case SpawnType.Collectible:
                    ConfigureCollectible(spawnedObject);
                    break;
                case SpawnType.Crate:
                    ConfigureCrate(spawnedObject);
                    break;
                case SpawnType.Enemy:
                    ConfigureEnemy(spawnedObject);
                    break;
                case SpawnType.PowerUp:
                    ConfigurePowerUp(spawnedObject);
                    break;
            }
        }

        #endregion

        #region Object Configuration

        private void ConfigureCollectible(GameObject collectible)
        {
            // Set random collectible amount based on type
            var collectibleComponent = collectible.GetComponent<CollectibleItem>();
            if (collectibleComponent != null)
            {
                float randomValue = Random.value;
                
                if (randomValue < tokenSpawnChance)
                {
                    // Rare token
                    collectibleComponent.SetCollectible("GoldenMouseToken", 1);
                }
                else if (randomValue < 0.6f)
                {
                    // Fish treats
                    int amount = Random.Range((int)fishTreatsRange.x, (int)fishTreatsRange.y + 1);
                    collectibleComponent.SetCollectible("FishTreats", amount);
                }
                else
                {
                    // Yarn
                    int amount = Random.Range((int)yarnRange.x, (int)yarnRange.y + 1);
                    collectibleComponent.SetCollectible("Yarn", amount);
                }
            }
        }

        private void ConfigureCrate(GameObject crate)
        {
            var crateSystem = crate.GetComponent<CrateSystem>();
            if (crateSystem != null)
            {
                // Crates are pre-configured, but could add difficulty scaling here
            }
        }

        private void ConfigureEnemy(GameObject enemy)
        {
            // Configure enemy based on difficulty
            // This would integrate with enemy AI systems
        }

        private void ConfigurePowerUp(GameObject powerUp)
        {
            // Configure power-up effects
            // This would integrate with power-up systems
        }

        #endregion

        #region Cleanup

        private void CleanupInactiveSpawns()
        {
            foreach (var zoneSpawns in activeSpawnsByZone)
            {
                var zone = zoneSpawns.Key;
                var spawns = zoneSpawns.Value;
                
                for (int i = spawns.Count - 1; i >= 0; i--)
                {
                    if (spawns[i] == null || !spawns[i].activeInHierarchy)
                    {
                        spawns.RemoveAt(i);
                        totalActiveSpawns = Mathf.Max(0, totalActiveSpawns - 1);
                    }
                }
            }
        }

        public void DespawnObject(GameObject obj, SpawnZone zone)
        {
            if (activeSpawnsByZone.ContainsKey(zone))
            {
                activeSpawnsByZone[zone].Remove(obj);
            }

            totalActiveSpawns = Mathf.Max(0, totalActiveSpawns - 1);

            // Return to pool or destroy
            ReturnToPool(obj);

            OnObjectDespawned?.Invoke(obj);
        }

        private void ReturnToPool(GameObject obj)
        {
            // Find matching prefab for pooling
            GameObject originalPrefab = null;
            foreach (var definition in spawnDefinitions)
            {
                if (obj.name.Contains(definition.prefab.name))
                {
                    originalPrefab = definition.prefab;
                    break;
                }
            }

            if (originalPrefab != null && objectPools.ContainsKey(originalPrefab))
            {
                obj.SetActive(false);
                objectPools[originalPrefab].Enqueue(obj);
            }
            else
            {
                Destroy(obj);
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Force spawn object at position
        /// </summary>
        public GameObject ForceSpawn(SpawnType spawnType, Vector3 position)
        {
            var spawnDef = GetRandomSpawnDefinition(spawnType);
            if (spawnDef == null) return null;

            GameObject spawnedObject = SpawnObject(spawnDef, position);
            if (spawnedObject != null)
            {
                totalActiveSpawns++;
                OnObjectSpawned?.Invoke(spawnedObject, position);
            }

            return spawnedObject;
        }

        /// <summary>
        /// Get total active spawns
        /// </summary>
        public int GetTotalActiveSpawns()
        {
            return totalActiveSpawns;
        }

        /// <summary>
        /// Get spawns in zone
        /// </summary>
        public List<GameObject> GetSpawnsInZone(SpawnZone zone)
        {
            if (activeSpawnsByZone.ContainsKey(zone))
            {
                return new List<GameObject>(activeSpawnsByZone[zone]);
            }
            return new List<GameObject>();
        }

        /// <summary>
        /// Clear all spawns
        /// </summary>
        public void ClearAllSpawns()
        {
            foreach (var zoneSpawns in activeSpawnsByZone)
            {
                foreach (var spawn in zoneSpawns.Value)
                {
                    if (spawn != null)
                    {
                        ReturnToPool(spawn);
                    }
                }
                zoneSpawns.Value.Clear();
            }

            totalActiveSpawns = 0;
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            // Update debug information
        }

        private void DrawSpawnGizmos()
        {
            foreach (var zone in spawnZones)
            {
                if (zone.spawnArea == null) continue;

                // Draw zone boundaries
                Gizmos.color = IsZoneActiveForSpawning(zone) ? Color.green : Color.red;
                Gizmos.DrawWireSphere(zone.spawnArea.position, zone.spawnRadius);
                
                // Draw activation range
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(zone.spawnArea.position, zone.activationDistance);

                // Draw spawned objects
                if (activeSpawnsByZone.ContainsKey(zone))
                {
                    Gizmos.color = Color.blue;
                    foreach (var spawn in activeSpawnsByZone[zone])
                    {
                        if (spawn != null)
                        {
                            Gizmos.DrawWireCube(spawn.transform.position, Vector3.one * 0.5f);
                        }
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(320, 200, 250, 200));
                
                GUILayout.Label("=== SPAWN SYSTEM DEBUG ===");
                GUILayout.Label($"Total Active: {totalActiveSpawns}/{maxGlobalSpawns}");
                GUILayout.Label($"Active Zones: {spawnZones.Count(z => IsZoneActiveForSpawning(z))}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== SPAWN RATES ===");
                GUILayout.Label($"Base Rate: {baseSpawnRate:F2}/s");
                GUILayout.Label($"Collectible: {collectibleSpawnChance:F2}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== ZONES ===");
                foreach (var zone in spawnZones)
                {
                    bool active = IsZoneActiveForSpawning(zone);
                    int spawns = activeSpawnsByZone.ContainsKey(zone) ? activeSpawnsByZone[zone].Count : 0;
                    GUILayout.Label($"{zone.zoneName}: {(active ? "ACTIVE" : "inactive")} ({spawns})");
                }
                
                if (GUILayout.Button("Clear All Spawns"))
                {
                    ClearAllSpawns();
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion

        #region Helper Components

        /// <summary>
        /// Component for automatic despawning of spawned objects
        /// </summary>
        public class DespawnComponent : MonoBehaviour
        {
            private SpawnSystem spawnSystem;
            private SpawnZone spawnZone;
            private float lifetime;
            private float spawnTime;

            public void Initialize(SpawnSystem system, SpawnZone zone, float life)
            {
                spawnSystem = system;
                spawnZone = zone;
                lifetime = life;
                spawnTime = Time.time;
            }

            private void Update()
            {
                if (Time.time - spawnTime >= lifetime)
                {
                    spawnSystem?.DespawnObject(gameObject, spawnZone);
                }
            }
        }

        /// <summary>
        /// Component for collectible items
        /// </summary>
        public class CollectibleItem : MonoBehaviour
        {
            private string collectibleType;
            private int amount;
            private bool collected = false;

            public void SetCollectible(string type, int value)
            {
                collectibleType = type;
                amount = value;
            }

            private void OnTriggerEnter(Collider other)
            {
                if (collected) return;

                var player = other.GetComponent<WhiskerKing.Player.PlayerController>();
                if (player != null)
                {
                    CollectItem();
                }
            }

            private void CollectItem()
            {
                collected = true;

                // Notify level manager
                var levelManager = FindObjectOfType<LevelManager>();
                levelManager?.CollectItem(collectibleType, amount);

                // Destroy or return to pool
                gameObject.SetActive(false);
            }
        }

        #endregion
    }
}
