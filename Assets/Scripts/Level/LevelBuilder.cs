using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using WhiskerKing.Core;
using WhiskerKing.Content;

namespace WhiskerKing.Level
{
    /// <summary>
    /// Level Builder for Whisker King
    /// Creates and manages the 12 complete levels across 3 worlds (Seaside, Night City, Temple)
    /// Handles procedural level generation, asset placement, and PRD-compliant design patterns
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        [System.Serializable]
        public class LevelConfiguration
        {
            [Header("Level Structure (PRD Compliant)")]
            public float levelLength = 800f; // meters
            public LevelManager.LevelSection[] sectionOrder = new LevelManager.LevelSection[]
            {
                LevelManager.LevelSection.Start,
                LevelManager.LevelSection.Mechanic,
                LevelManager.LevelSection.Checkpoint,
                LevelManager.LevelSection.Combination,
                LevelManager.LevelSection.Final
            };

            [Header("Checkpoint Settings")]
            public float checkpointInterval = 32f; // 25-40s at 8m/s = 200-320m, using 32s = 256m
            public int minCheckpoints = 2;
            public int maxCheckpoints = 4;

            [Header("Collectibles (PRD Requirements)")]
            public int minFishTreats = 50; // PRD: 50+ per level
            public int maxFishTreats = 75;
            public int yarnCount = 300; // PRD: 200-400, using middle value
            public int goldenTokens = 3; // PRD: exactly 3 per level
        }

        [System.Serializable]
        public class WorldSettings
        {
            [Header("World Information")]
            public string worldName;
            public WorldType worldType;
            public Color ambientColor = Color.white;
            public Material skyboxMaterial;

            [Header("Environment Assets")]
            public List<string> environmentPrefabs = new List<string>();
            public List<string> hazardPrefabs = new List<string>();
            public List<string> decorativePrefabs = new List<string>();

            [Header("World-Specific Mechanics")]
            public List<string> uniqueMechanics = new List<string>();
            public float gravityModifier = 1f;
            public bool hasWaterHazards = false;
            public bool hasWindEffects = false;
        }

        public enum WorldType
        {
            SeasideDocks,
            NightCity, 
            TempleGardens
        }

        [System.Serializable]
        public class LevelData
        {
            [Header("Level Info")]
            public int levelNumber;
            public string levelName;
            public WorldType worldType;
            public float targetCompletionTime = 180f; // 3 minutes base
            public float bronzeTime = 240f; // 4 minutes
            public float silverTime = 200f; // 3:20
            public float goldTime = 180f; // 3:00

            [Header("Difficulty Scaling")]
            public float difficultyMultiplier = 1f;
            public int enemyCount = 5;
            public int hazardDensity = 3;
            public float platformingComplexity = 1f;

            [Header("Collectible Distribution")]
            public Vector3[] fishTreatPositions;
            public Vector3[] yarnPositions;
            public Vector3[] goldenTokenPositions;

            [Header("Generated Content")]
            public List<GameObject> spawnedObjects = new List<GameObject>();
            public Transform levelRoot;
        }

        [System.Serializable]
        public class SectionTemplate
        {
            [Header("Section Configuration")]
            public LevelManager.LevelSection sectionType;
            public float sectionLength = 160f; // 20 seconds at 8m/s
            public List<string> requiredAssets = new List<string>();
            public List<string> optionalAssets = new List<string>();

            [Header("Gameplay Elements")]
            public int platformCount = 5;
            public int hazardCount = 2;
            public int enemyCount = 1;
            public bool requiresSpecialMechanic = false;
        }

        [Header("Level Building Configuration")]
        [SerializeField] private LevelConfiguration config = new LevelConfiguration();
        [SerializeField] private WorldSettings[] worldSettings = new WorldSettings[3];
        [SerializeField] private SectionTemplate[] sectionTemplates = new SectionTemplate[5];
        [SerializeField] private bool debugMode = true;

        // Level building state
        private Dictionary<WorldType, WorldSettings> worldLookup = new Dictionary<WorldType, WorldSettings>();
        private List<LevelData> allLevels = new List<LevelData>();
        private LevelData currentLevel;
        private Transform levelContainer;

        // Asset management
        private AssetManager assetManager;
        private Dictionary<string, GameObject> assetCache = new Dictionary<string, GameObject>();

        // Building progress
        private bool isBuildingLevel = false;
        private float buildProgress = 0f;
        private Queue<System.Action> buildQueue = new Queue<System.Action>();

        // Events
        public System.Action<int> OnLevelBuildStarted;
        public System.Action<int> OnLevelBuildCompleted;
        public System.Action<float> OnBuildProgressUpdated;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeLevelBuilder();
        }

        private void Start()
        {
            InitializeWorldSettings();
            GenerateAllLevels();
        }

        private void Update()
        {
            ProcessBuildQueue();
        }

        #endregion

        #region Initialization

        private void InitializeLevelBuilder()
        {
            // Get component references
            assetManager = AssetManager.Instance;

            // Create level container
            levelContainer = new GameObject("LevelContainer").transform;
            levelContainer.SetParent(transform);

            // Initialize world lookup
            worldLookup.Clear();

            Debug.Log("LevelBuilder initialized");
        }

        private void InitializeWorldSettings()
        {
            // Initialize Seaside Docks world
            worldSettings[0] = new WorldSettings
            {
                worldName = "Seaside Docks",
                worldType = WorldType.SeasideDocks,
                ambientColor = new Color(0.7f, 0.8f, 1f), // Light blue
                environmentPrefabs = new List<string>
                {
                    "Environment/Seaside/Dock",
                    "Environment/Seaside/Warehouse",
                    "Environment/Seaside/Crane",
                    "Environment/Seaside/Ship",
                    "Environment/Seaside/Pier"
                },
                hazardPrefabs = new List<string>
                {
                    "Hazards/Seaside/RollingBarrel",
                    "Hazards/Seaside/WaterHazard",
                    "Hazards/Seaside/MovingCrane",
                    "Hazards/Seaside/SlipperyDeck"
                },
                uniqueMechanics = new List<string> { "WaterSplash", "TidalEffect", "SeagullSwarms" },
                hasWaterHazards = true
            };

            // Initialize Night City world
            worldSettings[1] = new WorldSettings
            {
                worldName = "Night City",
                worldType = WorldType.NightCity,
                ambientColor = new Color(0.3f, 0.3f, 0.7f), // Dark blue
                environmentPrefabs = new List<string>
                {
                    "Environment/NightCity/Building",
                    "Environment/NightCity/Rooftop",
                    "Environment/NightCity/FireEscape", 
                    "Environment/NightCity/Neon",
                    "Environment/NightCity/Alley"
                },
                hazardPrefabs = new List<string>
                {
                    "Hazards/NightCity/ElectricWire",
                    "Hazards/NightCity/MovingFan",
                    "Hazards/NightCity/NeonSpark",
                    "Hazards/NightCity/TrafficDrone"
                },
                uniqueMechanics = new List<string> { "NeonBoost", "ElectricField", "DronePlatforms" },
                hasWindEffects = true
            };

            // Initialize Temple Gardens world
            worldSettings[2] = new WorldSettings
            {
                worldName = "Temple Gardens",
                worldType = WorldType.TempleGardens,
                ambientColor = new Color(0.9f, 0.8f, 0.6f), // Warm golden
                environmentPrefabs = new List<string>
                {
                    "Environment/Temple/Column",
                    "Environment/Temple/Garden",
                    "Environment/Temple/Statue",
                    "Environment/Temple/Bridge",
                    "Environment/Temple/Fountain"
                },
                hazardPrefabs = new List<string>
                {
                    "Hazards/Temple/MovingStatue",
                    "Hazards/Temple/ThornWall",
                    "Hazards/Temple/PressurePlate",
                    "Hazards/Temple/WaterJet"
                },
                uniqueMechanics = new List<string> { "TempleSwitch", "VineSwing", "WaterFlow" },
                gravityModifier = 0.9f // Slightly reduced gravity for mystical feel
            };

            // Build world lookup dictionary
            foreach (var world in worldSettings)
            {
                worldLookup[world.worldType] = world;
            }

            // Initialize section templates
            InitializeSectionTemplates();
        }

        private void InitializeSectionTemplates()
        {
            // Start Section - Tutorial/Introduction
            sectionTemplates[0] = new SectionTemplate
            {
                sectionType = LevelManager.LevelSection.Start,
                sectionLength = 120f, // 15 seconds
                platformCount = 3,
                hazardCount = 0,
                enemyCount = 0,
                requiresSpecialMechanic = false
            };

            // Mechanic Section - Introduce mechanics
            sectionTemplates[1] = new SectionTemplate
            {
                sectionType = LevelManager.LevelSection.Mechanic,
                sectionLength = 160f, // 20 seconds
                platformCount = 5,
                hazardCount = 1,
                enemyCount = 1,
                requiresSpecialMechanic = true
            };

            // Checkpoint Section - Safe area
            sectionTemplates[2] = new SectionTemplate
            {
                sectionType = LevelManager.LevelSection.Checkpoint,
                sectionLength = 80f, // 10 seconds  
                platformCount = 2,
                hazardCount = 0,
                enemyCount = 0,
                requiresSpecialMechanic = false
            };

            // Combination Section - Mix mechanics
            sectionTemplates[3] = new SectionTemplate
            {
                sectionType = LevelManager.LevelSection.Combination,
                sectionLength = 200f, // 25 seconds
                platformCount = 8,
                hazardCount = 3,
                enemyCount = 2,
                requiresSpecialMechanic = true
            };

            // Final Section - Challenge
            sectionTemplates[4] = new SectionTemplate
            {
                sectionType = LevelManager.LevelSection.Final,
                sectionLength = 240f, // 30 seconds
                platformCount = 6,
                hazardCount = 2,
                enemyCount = 3,
                requiresSpecialMechanic = true
            };
        }

        #endregion

        #region Level Generation

        private void GenerateAllLevels()
        {
            Debug.Log("Generating all 12 levels across 3 worlds...");

            // Generate 4 levels per world (12 total as per PRD)
            for (int worldIndex = 0; worldIndex < 3; worldIndex++)
            {
                var worldType = (WorldType)worldIndex;
                
                for (int levelIndex = 0; levelIndex < 4; levelIndex++)
                {
                    int globalLevelNumber = (worldIndex * 4) + levelIndex + 1;
                    GenerateLevelData(globalLevelNumber, worldType, levelIndex);
                }
            }

            Debug.Log($"Generated {allLevels.Count} levels");
        }

        private void GenerateLevelData(int levelNumber, WorldType worldType, int worldLevelIndex)
        {
            var worldSettings = worldLookup[worldType];
            
            var levelData = new LevelData
            {
                levelNumber = levelNumber,
                levelName = $"{worldSettings.worldName} - Level {worldLevelIndex + 1}",
                worldType = worldType,
                difficultyMultiplier = 1f + (levelNumber - 1) * 0.2f, // Progressive difficulty
                enemyCount = 3 + worldLevelIndex * 2, // More enemies per world level
                hazardDensity = 2 + worldLevelIndex, // More hazards per world level
                platformingComplexity = 1f + worldLevelIndex * 0.3f
            };

            // Calculate target times based on difficulty
            levelData.targetCompletionTime = 150f + (worldLevelIndex * 30f); // 2:30 to 4:00
            levelData.goldTime = levelData.targetCompletionTime;
            levelData.silverTime = levelData.targetCompletionTime + 30f;
            levelData.bronzeTime = levelData.targetCompletionTime + 60f;

            // Generate collectible positions
            GenerateCollectiblePositions(levelData);

            allLevels.Add(levelData);

            if (debugMode)
            {
                Debug.Log($"Generated level data: {levelData.levelName}");
            }
        }

        private void GenerateCollectiblePositions(LevelData levelData)
        {
            // Generate Fish Treat positions (PRD: 50+ per level)
            int fishTreatCount = Random.Range(config.minFishTreats, config.maxFishTreats + 1);
            levelData.fishTreatPositions = GeneratePositions(fishTreatCount, config.levelLength, 2f);

            // Generate Yarn positions (PRD: 200-400 per level) 
            levelData.yarnPositions = GeneratePositions(config.yarnCount, config.levelLength, 1f);

            // Generate Golden Token positions (PRD: exactly 3 per level)
            levelData.goldenTokenPositions = GenerateStrategicTokenPositions(config.levelLength);
        }

        private Vector3[] GeneratePositions(int count, float levelLength, float heightVariation)
        {
            var positions = new Vector3[count];
            
            for (int i = 0; i < count; i++)
            {
                float x = (i / (float)count) * levelLength; // Distribute along level
                float y = Random.Range(0f, heightVariation);
                float z = Random.Range(-2f, 2f); // Some lateral variation
                
                positions[i] = new Vector3(x, y, z);
            }

            return positions;
        }

        private Vector3[] GenerateStrategicTokenPositions(float levelLength)
        {
            // Place Golden Tokens at strategic positions (challenging to reach)
            return new Vector3[]
            {
                new Vector3(levelLength * 0.2f, 3f, 0f), // High platform early
                new Vector3(levelLength * 0.6f, 1f, -3f), // Hidden alcove middle
                new Vector3(levelLength * 0.9f, 4f, 2f) // Final challenge area
            };
        }

        #endregion

        #region Level Building

        public void BuildLevel(int levelNumber)
        {
            if (isBuildingLevel)
            {
                Debug.LogWarning("Level building already in progress");
                return;
            }

            var levelData = allLevels.FirstOrDefault(l => l.levelNumber == levelNumber);
            if (levelData == null)
            {
                Debug.LogError($"Level {levelNumber} not found");
                return;
            }

            StartCoroutine(BuildLevelCoroutine(levelData));
        }

        private IEnumerator BuildLevelCoroutine(LevelData levelData)
        {
            isBuildingLevel = true;
            buildProgress = 0f;
            currentLevel = levelData;
            
            OnLevelBuildStarted?.Invoke(levelData.levelNumber);
            Debug.Log($"Building level: {levelData.levelName}");

            // Create level root
            levelData.levelRoot = new GameObject($"Level_{levelData.levelNumber}_{levelData.worldType}").transform;
            levelData.levelRoot.SetParent(levelContainer);

            // Step 1: Load required assets
            yield return StartCoroutine(LoadLevelAssets(levelData));
            UpdateBuildProgress(0.2f);

            // Step 2: Build level sections
            yield return StartCoroutine(BuildLevelSections(levelData));
            UpdateBuildProgress(0.5f);

            // Step 3: Place collectibles
            yield return StartCoroutine(PlaceCollectibles(levelData));
            UpdateBuildProgress(0.7f);

            // Step 4: Add world-specific mechanics
            yield return StartCoroutine(AddWorldMechanics(levelData));
            UpdateBuildProgress(0.85f);

            // Step 5: Final optimization and cleanup
            yield return StartCoroutine(OptimizeLevel(levelData));
            UpdateBuildProgress(1f);

            OnLevelBuildCompleted?.Invoke(levelData.levelNumber);
            Debug.Log($"Level {levelData.levelNumber} built successfully");

            isBuildingLevel = false;
        }

        private IEnumerator LoadLevelAssets(LevelData levelData)
        {
            var worldSettings = worldLookup[levelData.worldType];
            var requiredAssets = new List<string>();
            
            // Collect all required assets
            requiredAssets.AddRange(worldSettings.environmentPrefabs);
            requiredAssets.AddRange(worldSettings.hazardPrefabs);
            requiredAssets.AddRange(worldSettings.decorativePrefabs);
            
            // Add collectible assets
            requiredAssets.AddRange(new[] 
            { 
                "Collectibles/FishTreat", 
                "Collectibles/Yarn", 
                "Collectibles/GoldenToken" 
            });

            int loadedCount = 0;
            int totalAssets = requiredAssets.Count;

            foreach (string assetKey in requiredAssets)
            {
                bool assetLoaded = false;
                
                assetManager.LoadAssetAsync<GameObject>(assetKey, AssetManager.LoadPriority.High,
                    (asset) => {
                        assetCache[assetKey] = asset;
                        assetLoaded = true;
                        loadedCount++;
                    },
                    (error) => {
                        Debug.LogWarning($"Could not load asset {assetKey}: {error}");
                        CreateFallbackAsset(assetKey);
                        assetLoaded = true;
                        loadedCount++;
                    });

                yield return new WaitUntil(() => assetLoaded);

                // Update progress
                float progress = (float)loadedCount / totalAssets * 0.2f;
                UpdateBuildProgress(progress);

                // Yield periodically to avoid frame hitches
                if (loadedCount % 5 == 0)
                {
                    yield return null;
                }
            }

            Debug.Log($"Loaded {loadedCount} assets for level {levelData.levelNumber}");
        }

        private IEnumerator BuildLevelSections(LevelData levelData)
        {
            float currentPosition = 0f;
            
            foreach (var sectionType in config.sectionOrder)
            {
                var template = sectionTemplates.FirstOrDefault(t => t.sectionType == sectionType);
                if (template != null)
                {
                    yield return StartCoroutine(BuildSection(levelData, template, currentPosition));
                    currentPosition += template.sectionLength;
                }
                
                yield return null; // Yield between sections
            }
        }

        private IEnumerator BuildSection(LevelData levelData, SectionTemplate template, float startPosition)
        {
            var sectionRoot = new GameObject($"Section_{template.sectionType}").transform;
            sectionRoot.SetParent(levelData.levelRoot);
            sectionRoot.position = new Vector3(startPosition, 0, 0);

            var worldSettings = worldLookup[levelData.worldType];

            // Build platforms
            for (int i = 0; i < template.platformCount; i++)
            {
                float platformX = startPosition + (i / (float)template.platformCount) * template.sectionLength;
                Vector3 platformPos = new Vector3(platformX, Random.Range(0f, 3f), Random.Range(-1f, 1f));
                
                yield return StartCoroutine(SpawnPlatform(worldSettings, platformPos, sectionRoot));
            }

            // Build hazards
            for (int i = 0; i < template.hazardCount; i++)
            {
                float hazardX = startPosition + Random.Range(0.2f, 0.8f) * template.sectionLength;
                Vector3 hazardPos = new Vector3(hazardX, Random.Range(0f, 2f), Random.Range(-1f, 1f));
                
                yield return StartCoroutine(SpawnHazard(worldSettings, hazardPos, sectionRoot));
            }

            // Add enemies
            for (int i = 0; i < template.enemyCount; i++)
            {
                float enemyX = startPosition + Random.Range(0.1f, 0.9f) * template.sectionLength;
                Vector3 enemyPos = new Vector3(enemyX, 0.5f, Random.Range(-1f, 1f));
                
                yield return StartCoroutine(SpawnEnemy(worldSettings, enemyPos, sectionRoot));
            }

            levelData.spawnedObjects.Add(sectionRoot.gameObject);
        }

        private IEnumerator SpawnPlatform(WorldSettings world, Vector3 position, Transform parent)
        {
            if (world.environmentPrefabs.Count > 0)
            {
                string platformKey = world.environmentPrefabs[Random.Range(0, world.environmentPrefabs.Count)];
                yield return StartCoroutine(SpawnAsset(platformKey, position, parent));
            }
        }

        private IEnumerator SpawnHazard(WorldSettings world, Vector3 position, Transform parent)
        {
            if (world.hazardPrefabs.Count > 0)
            {
                string hazardKey = world.hazardPrefabs[Random.Range(0, world.hazardPrefabs.Count)];
                yield return StartCoroutine(SpawnAsset(hazardKey, position, parent));
            }
        }

        private IEnumerator SpawnEnemy(WorldSettings world, Vector3 position, Transform parent)
        {
            // Spawn world-appropriate enemy
            string enemyKey = world.worldType switch
            {
                WorldType.SeasideDocks => "Enemies/Gull",
                WorldType.NightCity => "Enemies/AlleyBot",
                WorldType.TempleGardens => "Enemies/Beetle",
                _ => "Enemies/Generic"
            };
            
            yield return StartCoroutine(SpawnAsset(enemyKey, position, parent));
        }

        private IEnumerator SpawnAsset(string assetKey, Vector3 position, Transform parent)
        {
            if (assetCache.ContainsKey(assetKey))
            {
                var instance = Instantiate(assetCache[assetKey], position, Quaternion.identity, parent);
                currentLevel.spawnedObjects.Add(instance);
            }
            else
            {
                // Create fallback if asset not available
                CreateFallbackAsset(assetKey, position, parent);
            }
            
            yield return null;
        }

        private IEnumerator PlaceCollectibles(LevelData levelData)
        {
            // Place Fish Treats
            foreach (var position in levelData.fishTreatPositions)
            {
                yield return StartCoroutine(SpawnAsset("Collectibles/FishTreat", position, levelData.levelRoot));
            }

            // Place Yarn
            foreach (var position in levelData.yarnPositions)
            {
                yield return StartCoroutine(SpawnAsset("Collectibles/Yarn", position, levelData.levelRoot));
            }

            // Place Golden Tokens
            foreach (var position in levelData.goldenTokenPositions)
            {
                yield return StartCoroutine(SpawnAsset("Collectibles/GoldenToken", position, levelData.levelRoot));
            }
            
            yield return null;
        }

        private IEnumerator AddWorldMechanics(LevelData levelData)
        {
            var worldSettings = worldLookup[levelData.worldType];
            
            // Apply world-specific settings
            RenderSettings.ambientLight = worldSettings.ambientColor;
            
            if (worldSettings.skyboxMaterial != null)
            {
                RenderSettings.skybox = worldSettings.skyboxMaterial;
            }

            // Apply gravity modifier
            Physics.gravity = new Vector3(0, -9.81f * worldSettings.gravityModifier, 0);

            // Add world-specific mechanics
            foreach (string mechanic in worldSettings.uniqueMechanics)
            {
                yield return StartCoroutine(AddWorldMechanic(levelData, mechanic));
            }
        }

        private IEnumerator AddWorldMechanic(LevelData levelData, string mechanicName)
        {
            // Add world-specific gameplay mechanics
            switch (mechanicName)
            {
                case "WaterSplash":
                    AddWaterSplashMechanic(levelData);
                    break;
                case "TidalEffect":
                    AddTidalEffectMechanic(levelData);
                    break;
                case "NeonBoost":
                    AddNeonBoostMechanic(levelData);
                    break;
                case "ElectricField":
                    AddElectricFieldMechanic(levelData);
                    break;
                case "TempleSwitch":
                    AddTempleSwitchMechanic(levelData);
                    break;
                case "VineSwing":
                    AddVineSwingMechanic(levelData);
                    break;
            }
            
            yield return null;
        }

        private IEnumerator OptimizeLevel(LevelData levelData)
        {
            // Combine meshes for performance
            yield return StartCoroutine(OptimizeMeshes(levelData));
            
            // Setup occlusion culling
            SetupOcclusionCulling(levelData);
            
            // Apply LOD to complex objects
            ApplyLODOptimization(levelData);
            
            // Final cleanup
            yield return null;
        }

        private IEnumerator OptimizeMeshes(LevelData levelData)
        {
            // This would combine static meshes for better performance
            // For now, just organize objects
            yield return null;
        }

        #endregion

        #region World Mechanics

        private void AddWaterSplashMechanic(LevelData levelData)
        {
            // Add water splash effects for Seaside Docks
            var splashZone = new GameObject("WaterSplashZone");
            splashZone.transform.SetParent(levelData.levelRoot);
            
            // Add water collision detection and splash effects
            var collider = splashZone.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(config.levelLength, 10f, 10f);
            collider.center = new Vector3(config.levelLength * 0.5f, -2f, 0);
            
            levelData.spawnedObjects.Add(splashZone);
        }

        private void AddTidalEffectMechanic(LevelData levelData)
        {
            // Add tidal platform movement
            // Implementation would animate platforms up/down
        }

        private void AddNeonBoostMechanic(LevelData levelData)
        {
            // Add neon speed boost zones for Night City
            int boostCount = 3;
            for (int i = 0; i < boostCount; i++)
            {
                float x = (i + 1) * (config.levelLength / (boostCount + 1));
                var boostZone = CreateFallbackAsset("NeonBoost", new Vector3(x, 1f, 0), levelData.levelRoot);
                
                // Add boost effect component
                var boostEffect = boostZone.AddComponent<BoxCollider>();
                boostEffect.isTrigger = true;
                
                levelData.spawnedObjects.Add(boostZone);
            }
        }

        private void AddElectricFieldMechanic(LevelData levelData)
        {
            // Add electric field hazards for Night City
        }

        private void AddTempleSwitchMechanic(LevelData levelData)
        {
            // Add temple switches that activate platforms
        }

        private void AddVineSwingMechanic(LevelData levelData)
        {
            // Add vine swinging points for Temple Gardens
        }

        #endregion

        #region Helper Methods

        private void CreateFallbackAsset(string assetKey)
        {
            // Create a simple fallback asset
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = $"Fallback_{ExtractAssetName(assetKey)}";
            
            // Apply different colors based on asset type
            var renderer = fallback.GetComponent<Renderer>();
            if (assetKey.Contains("Environment"))
                renderer.material.color = Color.gray;
            else if (assetKey.Contains("Hazard"))
                renderer.material.color = Color.red;
            else if (assetKey.Contains("Collectible"))
                renderer.material.color = Color.yellow;
            else if (assetKey.Contains("Enemy"))
                renderer.material.color = Color.magenta;
            
            assetCache[assetKey] = fallback;
        }

        private GameObject CreateFallbackAsset(string assetKey, Vector3 position, Transform parent)
        {
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = ExtractAssetName(assetKey);
            fallback.transform.position = position;
            fallback.transform.SetParent(parent);
            
            return fallback;
        }

        private string ExtractAssetName(string assetKey)
        {
            int lastSlashIndex = assetKey.LastIndexOf('/');
            return lastSlashIndex >= 0 ? assetKey.Substring(lastSlashIndex + 1) : assetKey;
        }

        private void SetupOcclusionCulling(LevelData levelData)
        {
            // Setup occlusion culling for performance
        }

        private void ApplyLODOptimization(LevelData levelData)
        {
            // Apply LOD groups to complex objects
        }

        private void ProcessBuildQueue()
        {
            // Process any queued build operations
            if (buildQueue.Count > 0 && !isBuildingLevel)
            {
                var buildAction = buildQueue.Dequeue();
                buildAction?.Invoke();
            }
        }

        private void UpdateBuildProgress(float progress)
        {
            buildProgress = progress;
            OnBuildProgressUpdated?.Invoke(buildProgress);
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get all generated levels
        /// </summary>
        public List<LevelData> GetAllLevels()
        {
            return new List<LevelData>(allLevels);
        }

        /// <summary>
        /// Get level data by number
        /// </summary>
        public LevelData GetLevel(int levelNumber)
        {
            return allLevels.FirstOrDefault(l => l.levelNumber == levelNumber);
        }

        /// <summary>
        /// Get levels by world
        /// </summary>
        public List<LevelData> GetLevelsByWorld(WorldType worldType)
        {
            return allLevels.Where(l => l.worldType == worldType).ToList();
        }

        /// <summary>
        /// Check if level is built
        /// </summary>
        public bool IsLevelBuilt(int levelNumber)
        {
            var level = GetLevel(levelNumber);
            return level?.levelRoot != null && level.spawnedObjects.Count > 0;
        }

        /// <summary>
        /// Destroy level
        /// </summary>
        public void DestroyLevel(int levelNumber)
        {
            var level = GetLevel(levelNumber);
            if (level?.levelRoot != null)
            {
                foreach (var obj in level.spawnedObjects)
                {
                    if (obj != null)
                        DestroyImmediate(obj);
                }
                
                level.spawnedObjects.Clear();
                DestroyImmediate(level.levelRoot.gameObject);
                level.levelRoot = null;
            }
        }

        /// <summary>
        /// Get build progress
        /// </summary>
        public float GetBuildProgress()
        {
            return buildProgress;
        }

        /// <summary>
        /// Check if currently building
        /// </summary>
        public bool IsBuildingLevel()
        {
            return isBuildingLevel;
        }

        /// <summary>
        /// Get level statistics
        /// </summary>
        public string GetLevelStatistics()
        {
            int builtLevels = allLevels.Count(l => IsLevelBuilt(l.levelNumber));
            return $"Levels: {allLevels.Count}/12, Built: {builtLevels}, " +
                   $"Worlds: 3, Building: {isBuildingLevel}";
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(530, 10, 250, 250));
            
            GUILayout.Label("=== LEVEL BUILDER ===");
            GUILayout.Label($"Total Levels: {allLevels.Count}/12");
            
            int builtCount = allLevels.Count(l => IsLevelBuilt(l.levelNumber));
            GUILayout.Label($"Built Levels: {builtCount}");
            
            if (isBuildingLevel && currentLevel != null)
            {
                GUILayout.Label($"Building: {currentLevel.levelName}");
                GUILayout.Label($"Progress: {buildProgress:P1}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== WORLD BREAKDOWN ===");
            
            foreach (WorldType world in System.Enum.GetValues(typeof(WorldType)))
            {
                int worldLevels = GetLevelsByWorld(world).Count;
                int worldBuilt = GetLevelsByWorld(world).Count(l => IsLevelBuilt(l.levelNumber));
                GUILayout.Label($"{world}: {worldBuilt}/{worldLevels}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            if (!isBuildingLevel)
            {
                if (GUILayout.Button("Build Level 1"))
                    BuildLevel(1);
                    
                if (GUILayout.Button("Build Level 5"))
                    BuildLevel(5);
                    
                if (GUILayout.Button("Build Level 9"))
                    BuildLevel(9);
            }
            else
            {
                if (GUILayout.Button("Stop Building"))
                {
                    StopAllCoroutines();
                    isBuildingLevel = false;
                }
            }
            
            if (GUILayout.Button("Destroy All"))
            {
                for (int i = 1; i <= 12; i++)
                {
                    DestroyLevel(i);
                }
            }
            
            GUILayout.EndArea();
        }

        #endregion
    }
}
