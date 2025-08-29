using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Collections;
using WhiskerKing.Core;
using WhiskerKing.Content;
using WhiskerKing.Performance;

namespace WhiskerKing.Character
{
    /// <summary>
    /// Character System for Whisker King
    /// Manages the 3D character model (Capo) with LOD optimization, animation integration,
    /// and cosmetic customization system
    /// </summary>
    public class CharacterSystem : MonoBehaviour
    {
        [System.Serializable]
        public class CharacterConfiguration
        {
            [Header("Model Settings")]
            public string characterModelKey = "Characters/Capo";
            public bool enableLODOptimization = true;
            public bool enableCullingOptimization = true;
            public float maxRenderDistance = 100f;

            [Header("LOD Settings")]
            public float lodDistance0 = 15f;  // High detail
            public float lodDistance1 = 35f;  // Medium detail  
            public float lodDistance2 = 75f;  // Low detail
            public float cullDistance = 150f; // Cull completely

            [Header("Animation Settings")]
            public bool enableAnimationOptimization = true;
            public int maxAnimationLayers = 4;
            public float animationCullingDistance = 50f;
            public bool enableRootMotion = false;

            [Header("Customization")]
            public bool enableCosmetics = true;
            public int maxCosmeticSlots = 8;
            public bool enableColorCustomization = true;
            public bool enableAccessorySystem = true;
        }

        [System.Serializable]
        public class CharacterLOD
        {
            public Mesh lodMesh;
            public Material[] lodMaterials;
            public int triangleCount;
            public float screenRelativeTransitionHeight;
            public bool castShadows = true;
            public bool receiveShadows = true;
        }

        [System.Serializable]
        public class CosmeticItem
        {
            public string itemId;
            public string itemName;
            public CosmeticType type;
            public GameObject itemPrefab;
            public Transform attachPoint;
            public bool isUnlocked;
            public int yarnCost;
            public Sprite icon;
        }

        public enum CosmeticType
        {
            Hat,
            Collar,
            Bandana,
            Tail,
            Paws,
            Eyes,
            Fur,
            Special
        }

        [System.Serializable]
        public class AnimationSet
        {
            [Header("Movement Animations")]
            public AnimationClip idleAnimation;
            public AnimationClip runAnimation;
            public AnimationClip slideAnimation;
            public AnimationClip jumpAnimation;
            public AnimationClip fallAnimation;
            public AnimationClip landAnimation;

            [Header("Combat Animations")]
            public AnimationClip tailWhipWindup;
            public AnimationClip tailWhipActive;
            public AnimationClip tailWhipRecovery;
            public AnimationClip hitReaction;
            public AnimationClip stunned;

            [Header("Special Animations")]
            public AnimationClip celebrationAnimation;
            public AnimationClip deathAnimation;
            public AnimationClip respawnAnimation;
            public AnimationClip collectAnimation;
            public AnimationClip pauseAnimation;
        }

        // Character state
        public enum CharacterState
        {
            Idle,
            Running,
            Sliding,
            Jumping,
            Falling,
            Landing,
            Attacking,
            Stunned,
            Celebrating,
            Dead
        }

        [Header("Character Configuration")]
        [SerializeField] private CharacterConfiguration config = new CharacterConfiguration();
        [SerializeField] private AnimationSet animations = new AnimationSet();
        [SerializeField] private bool debugMode = true;

        // Character components
        private GameObject characterModel;
        private Animator characterAnimator;
        private SkinnedMeshRenderer mainRenderer;
        private LODGroup lodGroup;
        
        // LOD system
        private CharacterLOD[] characterLODs = new CharacterLOD[4];
        private int currentLODLevel = 0;
        private float distanceToCamera = 0f;

        // Animation system
        private CharacterState currentState = CharacterState.Idle;
        private CharacterState previousState = CharacterState.Idle;
        private float stateChangeTime = 0f;
        private Dictionary<CharacterState, int> animationHashes = new Dictionary<CharacterState, int>();

        // Cosmetic system
        private Dictionary<CosmeticType, CosmeticItem> equippedCosmetics = new Dictionary<CosmeticType, CosmeticItem>();
        private List<CosmeticItem> availableCosmetics = new List<CosmeticItem>();
        private Dictionary<CosmeticType, Transform> cosmeticSlots = new Dictionary<CosmeticType, Transform>();

        // Performance tracking
        private Camera mainCamera;
        private float lastLODUpdateTime = 0f;
        private bool isVisible = true;
        private Bounds characterBounds;

        // Component references
        private AssetManager assetManager;
        private LODManager lodManager;

        // Events
        public System.Action<CharacterState> OnStateChanged;
        public System.Action<CosmeticItem> OnCosmeticEquipped;
        public System.Action<int> OnLODChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeCharacterSystem();
        }

        private void Start()
        {
            StartCoroutine(LoadCharacterModelCoroutine());
            InitializeAnimationHashes();
            LoadAvailableCosmetics();
        }

        private void Update()
        {
            UpdateLODSystem();
            UpdateAnimationSystem();
            UpdateVisibilityOptimizations();
        }

        private void LateUpdate()
        {
            UpdateCosmeticAttachments();
        }

        #endregion

        #region Initialization

        private void InitializeCharacterSystem()
        {
            // Get component references
            assetManager = AssetManager.Instance;
            lodManager = LODManager.Instance;
            mainCamera = Camera.main ?? FindObjectOfType<Camera>();

            // Initialize cosmetic slots
            InitializeCosmeticSlots();

            Debug.Log("CharacterSystem initialized");
        }

        private void InitializeCosmeticSlots()
        {
            cosmeticSlots.Clear();
            
            // Initialize attachment points for cosmetics
            foreach (CosmeticType type in System.Enum.GetValues(typeof(CosmeticType)))
            {
                cosmeticSlots[type] = null; // Will be set when character model loads
            }
        }

        private void InitializeAnimationHashes()
        {
            // Pre-compute animation parameter hashes for performance
            animationHashes[CharacterState.Idle] = Animator.StringToHash("Idle");
            animationHashes[CharacterState.Running] = Animator.StringToHash("Running");
            animationHashes[CharacterState.Sliding] = Animator.StringToHash("Sliding");
            animationHashes[CharacterState.Jumping] = Animator.StringToHash("Jumping");
            animationHashes[CharacterState.Falling] = Animator.StringToHash("Falling");
            animationHashes[CharacterState.Landing] = Animator.StringToHash("Landing");
            animationHashes[CharacterState.Attacking] = Animator.StringToHash("Attacking");
            animationHashes[CharacterState.Stunned] = Animator.StringToHash("Stunned");
            animationHashes[CharacterState.Celebrating] = Animator.StringToHash("Celebrating");
            animationHashes[CharacterState.Dead] = Animator.StringToHash("Dead");
        }

        #endregion

        #region Character Model Loading

        private IEnumerator LoadCharacterModelCoroutine()
        {
            Debug.Log("Loading character model...");

            // Load character model through AssetManager
            bool modelLoaded = false;
            string errorMessage = "";

            assetManager.LoadAssetAsync<GameObject>(config.characterModelKey, 
                AssetManager.LoadPriority.Critical,
                (model) => {
                    characterModel = Instantiate(model, transform);
                    SetupCharacterComponents();
                    SetupLODSystem();
                    SetupCosmeticAttachmentPoints();
                    modelLoaded = true;
                },
                (error) => {
                    errorMessage = error;
                    modelLoaded = true;
                });

            // Wait for model to load
            yield return new WaitUntil(() => modelLoaded);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Debug.LogError($"Failed to load character model: {errorMessage}");
                CreateFallbackCharacterModel();
            }
            else
            {
                Debug.Log("Character model loaded successfully");
            }
        }

        private void SetupCharacterComponents()
        {
            if (characterModel == null) return;

            // Get core components
            characterAnimator = characterModel.GetComponent<Animator>();
            mainRenderer = characterModel.GetComponentInChildren<SkinnedMeshRenderer>();
            
            if (characterAnimator == null)
            {
                characterAnimator = characterModel.AddComponent<Animator>();
                LoadAnimatorController();
            }

            if (mainRenderer == null)
            {
                Debug.LogWarning("No SkinnedMeshRenderer found on character model");
            }

            // Setup bounds for culling
            if (mainRenderer != null)
            {
                characterBounds = mainRenderer.bounds;
            }
        }

        private void LoadAnimatorController()
        {
            // Load the Animator Controller for Capo
            assetManager.LoadAssetAsync<RuntimeAnimatorController>("Animations/CapoController",
                AssetManager.LoadPriority.High,
                (controller) => {
                    if (characterAnimator != null)
                    {
                        characterAnimator.runtimeAnimatorController = controller;
                    }
                },
                (error) => {
                    Debug.LogWarning($"Could not load animator controller: {error}");
                    CreateDefaultAnimatorController();
                });
        }

        private void CreateDefaultAnimatorController()
        {
            // Create a simple animator controller with basic states
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.name = "CapoController_Default";
            
            // This would normally be done in the editor, but we can create a basic one at runtime
            if (characterAnimator != null)
            {
                characterAnimator.runtimeAnimatorController = controller;
            }
        }

        private void CreateFallbackCharacterModel()
        {
            // Create a simple fallback model using Unity primitives
            characterModel = new GameObject("Capo_Fallback");
            characterModel.transform.SetParent(transform);
            
            // Create body (capsule)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(characterModel.transform);
            body.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            
            // Create head (sphere)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(characterModel.transform);
            head.transform.localPosition = new Vector3(0, 1.5f, 0);
            head.transform.localScale = Vector3.one * 0.7f;

            // Create tail (cylinder)
            var tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tail.name = "Tail";
            tail.transform.SetParent(characterModel.transform);
            tail.transform.localPosition = new Vector3(0, 0.5f, -1f);
            tail.transform.localRotation = Quaternion.Euler(90, 0, 0);
            tail.transform.localScale = new Vector3(0.2f, 0.8f, 0.2f);

            // Add basic components
            characterAnimator = characterModel.AddComponent<Animator>();
            mainRenderer = body.GetComponent<MeshRenderer>() as SkinnedMeshRenderer;

            SetupBasicLOD();
        }

        #endregion

        #region LOD System

        private void SetupLODSystem()
        {
            if (characterModel == null) return;

            // Create LOD group
            lodGroup = characterModel.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = characterModel.AddComponent<LODGroup>();
            }

            // Setup LOD levels according to PRD specifications
            SetupCharacterLODs();
            ApplyLODGroup();

            // Register with LODManager
            if (lodManager != null)
            {
                lodManager.AddLODObject(characterModel);
            }
        }

        private void SetupCharacterLODs()
        {
            // LOD 0: High detail (full model)
            characterLODs[0] = new CharacterLOD
            {
                lodMesh = mainRenderer?.sharedMesh,
                lodMaterials = mainRenderer?.sharedMaterials,
                triangleCount = mainRenderer?.sharedMesh?.triangles.Length / 3 ?? 1000,
                screenRelativeTransitionHeight = 0.6f,
                castShadows = true,
                receiveShadows = true
            };

            // LOD 1: Medium detail (reduced polygons)
            characterLODs[1] = new CharacterLOD
            {
                lodMesh = CreateReducedMesh(mainRenderer?.sharedMesh, 0.7f),
                lodMaterials = mainRenderer?.sharedMaterials,
                triangleCount = (int)(characterLODs[0].triangleCount * 0.7f),
                screenRelativeTransitionHeight = 0.3f,
                castShadows = true,
                receiveShadows = false
            };

            // LOD 2: Low detail (heavily reduced)
            characterLODs[2] = new CharacterLOD
            {
                lodMesh = CreateReducedMesh(mainRenderer?.sharedMesh, 0.4f),
                lodMaterials = CreateSimplifiedMaterials(mainRenderer?.sharedMaterials),
                triangleCount = (int)(characterLODs[0].triangleCount * 0.4f),
                screenRelativeTransitionHeight = 0.1f,
                castShadows = false,
                receiveShadows = false
            };

            // LOD 3: Impostor/Billboard (very distant)
            characterLODs[3] = new CharacterLOD
            {
                lodMesh = CreateBillboardMesh(),
                lodMaterials = new Material[] { CreateBillboardMaterial() },
                triangleCount = 2, // Just 2 triangles for billboard
                screenRelativeTransitionHeight = 0.01f,
                castShadows = false,
                receiveShadows = false
            };
        }

        private void ApplyLODGroup()
        {
            if (lodGroup == null) return;

            var lods = new LOD[characterLODs.Length];
            
            for (int i = 0; i < characterLODs.Length; i++)
            {
                var renderers = new Renderer[] { mainRenderer };
                lods[i] = new LOD(characterLODs[i].screenRelativeTransitionHeight, renderers);
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        private void SetupBasicLOD()
        {
            // Simple LOD setup for fallback model
            if (characterModel != null)
            {
                lodGroup = characterModel.AddComponent<LODGroup>();
                var renderers = characterModel.GetComponentsInChildren<Renderer>();
                
                var lods = new LOD[]
                {
                    new LOD(0.6f, renderers), // High detail
                    new LOD(0.3f, renderers), // Medium detail  
                    new LOD(0.1f, renderers), // Low detail
                    new LOD(0.01f, new Renderer[0]) // Culled
                };
                
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }

        private void UpdateLODSystem()
        {
            if (mainCamera == null || characterModel == null) return;

            // Update distance to camera
            distanceToCamera = Vector3.Distance(mainCamera.transform.position, transform.position);

            // Determine LOD level based on distance and PRD requirements
            int newLODLevel = CalculateLODLevel(distanceToCamera);
            
            if (newLODLevel != currentLODLevel)
            {
                currentLODLevel = newLODLevel;
                ApplyLODLevel(currentLODLevel);
                OnLODChanged?.Invoke(currentLODLevel);
            }

            // Update animation optimization based on distance
            if (config.enableAnimationOptimization)
            {
                UpdateAnimationOptimization();
            }
        }

        private int CalculateLODLevel(float distance)
        {
            if (distance <= config.lodDistance0) return 0; // High detail
            if (distance <= config.lodDistance1) return 1; // Medium detail
            if (distance <= config.lodDistance2) return 2; // Low detail
            if (distance <= config.cullDistance) return 3; // Billboard
            return 4; // Culled
        }

        private void ApplyLODLevel(int lodLevel)
        {
            if (lodLevel >= 4 || mainRenderer == null)
            {
                // Cull character
                mainRenderer.enabled = false;
                if (characterAnimator != null)
                {
                    characterAnimator.enabled = false;
                }
                return;
            }

            mainRenderer.enabled = true;
            
            if (lodLevel < characterLODs.Length)
            {
                var lod = characterLODs[lodLevel];
                
                // Apply LOD mesh and materials
                if (lod.lodMesh != null && mainRenderer is SkinnedMeshRenderer smr)
                {
                    smr.sharedMesh = lod.lodMesh;
                }
                
                if (lod.lodMaterials != null)
                {
                    mainRenderer.sharedMaterials = lod.lodMaterials;
                }

                // Apply shadow settings
                mainRenderer.shadowCastingMode = lod.castShadows ? 
                    ShadowCastingMode.On : ShadowCastingMode.Off;
                mainRenderer.receiveShadows = lod.receiveShadows;
            }
        }

        #endregion

        #region Animation System

        private void UpdateAnimationSystem()
        {
            if (characterAnimator == null || !characterAnimator.enabled) return;

            // Update animator parameters based on current state
            foreach (var kvp in animationHashes)
            {
                bool isActive = kvp.Key == currentState;
                characterAnimator.SetBool(kvp.Value, isActive);
            }

            // Handle state transitions
            if (currentState != previousState)
            {
                HandleStateTransition(previousState, currentState);
                previousState = currentState;
                stateChangeTime = Time.time;
            }
        }

        private void UpdateAnimationOptimization()
        {
            if (characterAnimator == null) return;

            bool shouldAnimateAtFullRate = distanceToCamera <= config.animationCullingDistance;
            
            if (shouldAnimateAtFullRate)
            {
                characterAnimator.updateMode = AnimatorUpdateMode.Normal;
                characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else
            {
                characterAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                characterAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        private void HandleStateTransition(CharacterState from, CharacterState to)
        {
            OnStateChanged?.Invoke(to);
            
            if (debugMode)
            {
                Debug.Log($"Character state changed: {from} -> {to}");
            }

            // Handle specific transition logic
            switch (to)
            {
                case CharacterState.Jumping:
                    PlayAnimationClip(animations.jumpAnimation);
                    break;
                case CharacterState.Landing:
                    PlayAnimationClip(animations.landAnimation);
                    break;
                case CharacterState.Attacking:
                    StartCoroutine(PlayAttackSequence());
                    break;
                case CharacterState.Celebrating:
                    PlayAnimationClip(animations.celebrationAnimation);
                    break;
            }
        }

        private IEnumerator PlayAttackSequence()
        {
            // Play attack animation sequence
            PlayAnimationClip(animations.tailWhipWindup);
            yield return new WaitForSeconds(0.1f); // PRD: 0.1s windup

            PlayAnimationClip(animations.tailWhipActive);
            yield return new WaitForSeconds(0.18f); // PRD: 0.18s active

            PlayAnimationClip(animations.tailWhipRecovery);
            yield return new WaitForSeconds(0.12f); // PRD: 0.12s recovery

            // Return to previous state
            SetCharacterState(CharacterState.Idle);
        }

        private void PlayAnimationClip(AnimationClip clip)
        {
            if (clip != null && characterAnimator != null)
            {
                // Play clip with crossfade
                characterAnimator.CrossFade(clip.name, 0.1f);
            }
        }

        #endregion

        #region Cosmetic System

        private void LoadAvailableCosmetics()
        {
            if (!config.enableCosmetics) return;

            // Load cosmetic items from AssetManager
            var cosmeticKeys = new string[]
            {
                "Cosmetics/Hats/PirateHat",
                "Cosmetics/Collars/GoldCollar", 
                "Cosmetics/Bandanas/RedBandana",
                "Cosmetics/Tails/RainbowTail",
                "Cosmetics/Paws/SpeedPaws",
                "Cosmetics/Eyes/GoldenEyes"
            };

            foreach (string cosmeticKey in cosmeticKeys)
            {
                assetManager.LoadAssetAsync<GameObject>(cosmeticKey, 
                    AssetManager.LoadPriority.Low,
                    (cosmetic) => AddAvailableCosmetic(cosmetic, cosmeticKey),
                    (error) => Debug.LogWarning($"Could not load cosmetic {cosmeticKey}: {error}"));
            }
        }

        private void AddAvailableCosmetic(GameObject cosmeticPrefab, string cosmeticKey)
        {
            var cosmeticType = DetermineCosmeticType(cosmeticKey);
            
            var cosmeticItem = new CosmeticItem
            {
                itemId = cosmeticKey,
                itemName = ExtractCosmeticName(cosmeticKey),
                type = cosmeticType,
                itemPrefab = cosmeticPrefab,
                isUnlocked = false,
                yarnCost = CalculateCosmeticCost(cosmeticType)
            };

            availableCosmetics.Add(cosmeticItem);
        }

        private void SetupCosmeticAttachmentPoints()
        {
            if (characterModel == null) return;

            // Find attachment points in the character model
            Transform[] bones = characterModel.GetComponentsInChildren<Transform>();
            
            foreach (Transform bone in bones)
            {
                switch (bone.name.ToLower())
                {
                    case "head":
                        cosmeticSlots[CosmeticType.Hat] = bone;
                        cosmeticSlots[CosmeticType.Eyes] = bone;
                        break;
                    case "neck":
                        cosmeticSlots[CosmeticType.Collar] = bone;
                        cosmeticSlots[CosmeticType.Bandana] = bone;
                        break;
                    case "tail":
                        cosmeticSlots[CosmeticType.Tail] = bone;
                        break;
                    case "leftpaw":
                    case "rightpaw":
                        cosmeticSlots[CosmeticType.Paws] = bone;
                        break;
                }
            }
        }

        private void UpdateCosmeticAttachments()
        {
            foreach (var kvp in equippedCosmetics)
            {
                var cosmetic = kvp.Value;
                if (cosmetic.attachPoint != null && cosmeticSlots.ContainsKey(cosmetic.type))
                {
                    var attachPoint = cosmeticSlots[cosmetic.type];
                    if (attachPoint != null)
                    {
                        cosmetic.attachPoint.position = attachPoint.position;
                        cosmetic.attachPoint.rotation = attachPoint.rotation;
                    }
                }
            }
        }

        public void EquipCosmetic(string cosmeticId)
        {
            var cosmetic = availableCosmetics.FirstOrDefault(c => c.itemId == cosmeticId);
            if (cosmetic == null || !cosmetic.isUnlocked)
            {
                Debug.LogWarning($"Cannot equip cosmetic {cosmeticId}: not available or locked");
                return;
            }

            // Unequip existing cosmetic of the same type
            if (equippedCosmetics.ContainsKey(cosmetic.type))
            {
                UnequipCosmetic(cosmetic.type);
            }

            // Instantiate and attach cosmetic
            if (cosmeticSlots.ContainsKey(cosmetic.type) && cosmeticSlots[cosmetic.type] != null)
            {
                var cosmeticInstance = Instantiate(cosmetic.itemPrefab, cosmeticSlots[cosmetic.type]);
                cosmetic.attachPoint = cosmeticInstance.transform;
                
                equippedCosmetics[cosmetic.type] = cosmetic;
                OnCosmeticEquipped?.Invoke(cosmetic);
                
                Debug.Log($"Equipped cosmetic: {cosmetic.itemName}");
            }
        }

        public void UnequipCosmetic(CosmeticType type)
        {
            if (equippedCosmetics.ContainsKey(type))
            {
                var cosmetic = equippedCosmetics[type];
                if (cosmetic.attachPoint != null)
                {
                    Destroy(cosmetic.attachPoint.gameObject);
                }
                
                equippedCosmetics.Remove(type);
                Debug.Log($"Unequipped cosmetic: {cosmetic.itemName}");
            }
        }

        public bool UnlockCosmetic(string cosmeticId, int availableYarn)
        {
            var cosmetic = availableCosmetics.FirstOrDefault(c => c.itemId == cosmeticId);
            if (cosmetic == null)
            {
                Debug.LogWarning($"Cosmetic {cosmeticId} not found");
                return false;
            }

            if (cosmetic.isUnlocked)
            {
                Debug.LogWarning($"Cosmetic {cosmeticId} already unlocked");
                return false;
            }

            if (availableYarn < cosmetic.yarnCost)
            {
                Debug.LogWarning($"Not enough yarn to unlock {cosmeticId}: need {cosmetic.yarnCost}, have {availableYarn}");
                return false;
            }

            cosmetic.isUnlocked = true;
            Debug.Log($"Unlocked cosmetic: {cosmetic.itemName} for {cosmetic.yarnCost} yarn");
            return true;
        }

        #endregion

        #region Helper Methods

        private Mesh CreateReducedMesh(Mesh originalMesh, float reductionFactor)
        {
            if (originalMesh == null) return null;

            // This is a simplified mesh reduction
            // In a real implementation, you'd use a proper mesh decimation algorithm
            var reducedMesh = new Mesh();
            reducedMesh.name = $"{originalMesh.name}_LOD{reductionFactor}";
            
            // Copy vertices (could implement actual reduction here)
            reducedMesh.vertices = originalMesh.vertices;
            reducedMesh.triangles = originalMesh.triangles;
            reducedMesh.normals = originalMesh.normals;
            reducedMesh.uv = originalMesh.uv;
            
            return reducedMesh;
        }

        private Material[] CreateSimplifiedMaterials(Material[] originalMaterials)
        {
            if (originalMaterials == null) return null;

            var simplifiedMaterials = new Material[originalMaterials.Length];
            
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                if (originalMaterials[i] != null)
                {
                    // Create simplified version with unlit shader
                    var simplified = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    simplified.name = $"{originalMaterials[i].name}_Simplified";
                    simplified.color = originalMaterials[i].color;
                    simplified.mainTexture = originalMaterials[i].mainTexture;
                    simplifiedMaterials[i] = simplified;
                }
            }
            
            return simplifiedMaterials;
        }

        private Mesh CreateBillboardMesh()
        {
            var mesh = new Mesh();
            mesh.name = "BillboardMesh";
            
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0, 0),
                new Vector3(0.5f, 0, 0),
                new Vector3(-0.5f, 2, 0),
                new Vector3(0.5f, 2, 0)
            };
            
            mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0), 
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            
            mesh.RecalculateNormals();
            return mesh;
        }

        private Material CreateBillboardMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.name = "BillboardMaterial";
            material.color = Color.white;
            
            // This would normally use a pre-rendered texture of the character
            return material;
        }

        private CosmeticType DetermineCosmeticType(string cosmeticKey)
        {
            if (cosmeticKey.Contains("Hat")) return CosmeticType.Hat;
            if (cosmeticKey.Contains("Collar")) return CosmeticType.Collar;
            if (cosmeticKey.Contains("Bandana")) return CosmeticType.Bandana;
            if (cosmeticKey.Contains("Tail")) return CosmeticType.Tail;
            if (cosmeticKey.Contains("Paws")) return CosmeticType.Paws;
            if (cosmeticKey.Contains("Eyes")) return CosmeticType.Eyes;
            if (cosmeticKey.Contains("Fur")) return CosmeticType.Fur;
            return CosmeticType.Special;
        }

        private string ExtractCosmeticName(string cosmeticKey)
        {
            int lastSlashIndex = cosmeticKey.LastIndexOf('/');
            return lastSlashIndex >= 0 ? cosmeticKey.Substring(lastSlashIndex + 1) : cosmeticKey;
        }

        private int CalculateCosmeticCost(CosmeticType type)
        {
            return type switch
            {
                CosmeticType.Hat => 50,
                CosmeticType.Collar => 30,
                CosmeticType.Bandana => 25,
                CosmeticType.Tail => 40,
                CosmeticType.Paws => 35,
                CosmeticType.Eyes => 60,
                CosmeticType.Fur => 100,
                CosmeticType.Special => 150,
                _ => 25
            };
        }

        private void UpdateVisibilityOptimizations()
        {
            if (mainCamera == null) return;

            // Simple frustum culling check
            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, characterBounds);

            // Apply visibility optimizations
            if (mainRenderer != null)
            {
                mainRenderer.enabled = isVisible && (distanceToCamera <= config.maxRenderDistance);
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Set character animation state
        /// </summary>
        public void SetCharacterState(CharacterState newState)
        {
            if (newState != currentState)
            {
                previousState = currentState;
                currentState = newState;
            }
        }

        /// <summary>
        /// Get current character state
        /// </summary>
        public CharacterState GetCharacterState()
        {
            return currentState;
        }

        /// <summary>
        /// Get current LOD level
        /// </summary>
        public int GetCurrentLODLevel()
        {
            return currentLODLevel;
        }

        /// <summary>
        /// Get available cosmetics
        /// </summary>
        public List<CosmeticItem> GetAvailableCosmetics()
        {
            return new List<CosmeticItem>(availableCosmetics);
        }

        /// <summary>
        /// Get equipped cosmetics
        /// </summary>
        public Dictionary<CosmeticType, CosmeticItem> GetEquippedCosmetics()
        {
            return new Dictionary<CosmeticType, CosmeticItem>(equippedCosmetics);
        }

        /// <summary>
        /// Check if character is visible
        /// </summary>
        public bool IsVisible()
        {
            return isVisible;
        }

        /// <summary>
        /// Get distance to camera
        /// </summary>
        public float GetDistanceToCamera()
        {
            return distanceToCamera;
        }

        /// <summary>
        /// Force LOD update
        /// </summary>
        public void ForceUpdateLOD()
        {
            UpdateLODSystem();
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(10, 10, 250, 200));
            
            GUILayout.Label("=== CHARACTER SYSTEM ===");
            GUILayout.Label($"State: {currentState}");
            GUILayout.Label($"LOD Level: {currentLODLevel}");
            GUILayout.Label($"Distance: {distanceToCamera:F1}m");
            GUILayout.Label($"Visible: {isVisible}");
            
            if (characterLODs[currentLODLevel] != null)
            {
                GUILayout.Label($"Triangles: {characterLODs[currentLODLevel].triangleCount}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== COSMETICS ===");
            GUILayout.Label($"Available: {availableCosmetics.Count}");
            GUILayout.Label($"Equipped: {equippedCosmetics.Count}");
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            if (GUILayout.Button("Toggle Animation"))
            {
                if (characterAnimator != null)
                    characterAnimator.enabled = !characterAnimator.enabled;
            }
            
            if (GUILayout.Button("Force LOD Update"))
            {
                ForceUpdateLOD();
            }
            
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugMode) return;

            // Draw LOD distance spheres
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, config.lodDistance0);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, config.lodDistance1);
            
            Gizmos.color = Color.orange;
            Gizmos.DrawWireSphere(transform.position, config.lodDistance2);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, config.cullDistance);
        }

        #endregion
    }
}
