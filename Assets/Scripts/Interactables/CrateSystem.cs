using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using WhiskerKing.Combat;
using WhiskerKing.Core;

namespace WhiskerKing.Interactables
{
    /// <summary>
    /// Comprehensive crate system for Whisker King
    /// Implements 6 crate types: Standard, Yarn, Spring, Metal, Boom, Mystery
    /// Handles destruction, rewards, special effects, and chain reactions
    /// </summary>
    public class CrateSystem : MonoBehaviour, IDamageable
    {
        public enum CrateType
        {
            Standard,
            Yarn,
            Spring,
            Metal,
            Boom,
            Mystery
        }

        [System.Serializable]
        public class CrateReward
        {
            public string rewardType;
            public int amount;
            public GameObject rewardPrefab;
        }

        [Header("Crate Configuration")]
        [SerializeField] private CrateType crateType = CrateType.Standard;
        [SerializeField] private int health = 1;
        [SerializeField] private bool debugMode = false;

        [Header("Standard Crate Settings")]
        [SerializeField] private CrateReward standardReward = new CrateReward { rewardType = "FishTreats", amount = 10 };

        [Header("Yarn Crate Settings")]
        [SerializeField] private CrateReward yarnReward = new CrateReward { rewardType = "Yarn", amount = 20 };

        [Header("Spring Crate Settings")]
        [SerializeField] private float launchHeight = 6.0f;
        [SerializeField] private float launchRadius = 2.0f;

        [Header("Metal Crate Settings")]
        [SerializeField] private int metalHealth = 3;
        [SerializeField] private float metalDefense = 0.5f;

        [Header("Boom Crate Settings")]
        [SerializeField] private float fuseTime = 2.0f;
        [SerializeField] private float chainReactionRadius = 3.0f;
        [SerializeField] private float explosionDamage = 50f;
        [SerializeField] private bool isArmed = false;

        [Header("Mystery Crate Settings")]
        [SerializeField] private float positiveChance = 0.7f;
        [SerializeField] private float negativeChance = 0.3f;
        [SerializeField] private List<CrateReward> mysteryRewards = new List<CrateReward>();

        [Header("Visual Settings")]
        [SerializeField] private GameObject destructionEffect;
        [SerializeField] private GameObject springEffect;
        [SerializeField] private GameObject explosionEffect;
        [SerializeField] private Material damagedMaterial;
        [SerializeField] private Color fuseGlowColor = Color.red;

        // Components
        private Rigidbody rb;
        private Collider col;
        private MeshRenderer meshRenderer;
        private Material originalMaterial;

        // State
        private int currentHealth;
        private bool isDestroyed = false;
        private bool isExploding = false;
        private float fuseStartTime;
        private bool fuseStarted = false;

        // Configuration cache
        private LevelDesignData levelConfig;

        // Events
        public System.Action<CrateType, Vector3> OnCrateDestroyed;
        public System.Action<CrateReward> OnRewardGiven;
        public System.Action<Vector3, float> OnExplosion;

        #region Unity Lifecycle

        private void Awake()
        {
            // Get components
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer != null)
            {
                originalMaterial = meshRenderer.material;
            }
        }

        private void Start()
        {
            LoadConfiguration();
            InitializeCrate();
        }

        private void Update()
        {
            UpdateCrateState();
            
            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode)
            {
                DrawCrateGizmos();
            }
        }

        #endregion

        #region Configuration

        private void LoadConfiguration()
        {
            if (GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                levelConfig = GameConfiguration.Instance.Config.levelDesign;
                ApplyConfiguration();
                Debug.Log($"CrateSystem: Configuration loaded for {crateType}");
            }
        }

        private void ApplyConfiguration()
        {
            if (levelConfig?.crates == null) return;

            switch (crateType)
            {
                case CrateType.Standard:
                    if (levelConfig.crates.standard != null)
                    {
                        health = levelConfig.crates.standard.health;
                        standardReward.amount = levelConfig.crates.standard.reward;
                    }
                    break;

                case CrateType.Yarn:
                    if (levelConfig.crates.yarn != null)
                    {
                        health = levelConfig.crates.yarn.health;
                        yarnReward.amount = levelConfig.crates.yarn.reward;
                    }
                    break;

                case CrateType.Spring:
                    if (levelConfig.crates.spring != null)
                    {
                        health = levelConfig.crates.spring.health;
                        launchHeight = levelConfig.crates.spring.launchHeight;
                    }
                    break;

                case CrateType.Boom:
                    if (levelConfig.crates.boom != null)
                    {
                        fuseTime = levelConfig.crates.boom.fuseTimeSeconds;
                        chainReactionRadius = levelConfig.crates.boom.chainReactionRadius;
                        explosionDamage = levelConfig.crates.boom.damage;
                    }
                    break;

                case CrateType.Mystery:
                    if (levelConfig.crates.mystery != null)
                    {
                        positiveChance = levelConfig.crates.mystery.positiveChance;
                        negativeChance = levelConfig.crates.mystery.negativeChance;
                    }
                    break;
            }
        }

        #endregion

        #region Initialization

        private void InitializeCrate()
        {
            // Set initial health based on type
            currentHealth = crateType == CrateType.Metal ? metalHealth : health;
            
            // Configure physics based on type
            ConfigureCratePhysics();
            
            // Set up visual state
            UpdateVisualState();

            if (debugMode)
            {
                Debug.Log($"Initialized {crateType} crate - Health: {currentHealth}");
            }
        }

        private void ConfigureCratePhysics()
        {
            if (rb != null)
            {
                switch (crateType)
                {
                    case CrateType.Metal:
                        rb.mass = 5f; // Heavier than normal
                        break;
                    case CrateType.Boom:
                        rb.mass = 2f; // Slightly heavier due to explosives
                        break;
                    default:
                        rb.mass = 1f; // Standard mass
                        break;
                }
            }
        }

        #endregion

        #region Crate State Management

        private void UpdateCrateState()
        {
            switch (crateType)
            {
                case CrateType.Boom:
                    UpdateBoomCrate();
                    break;
                case CrateType.Mystery:
                    UpdateMysteryVisuals();
                    break;
            }
        }

        private void UpdateBoomCrate()
        {
            if (!fuseStarted || isDestroyed) return;

            float elapsed = Time.time - fuseStartTime;
            float progress = elapsed / fuseTime;

            if (progress >= 1f)
            {
                // Fuse expired, explode
                ExplodeBoomCrate();
            }
            else
            {
                // Update fuse visual effects
                UpdateFuseEffects(progress);
            }
        }

        private void UpdateMysteryVisuals()
        {
            // Add subtle glow or pulse effect for mystery crates
            if (meshRenderer != null)
            {
                float pulse = Mathf.Sin(Time.time * 2f) * 0.2f + 0.8f;
                // Apply pulse to emission if material supports it
            }
        }

        private void UpdateFuseEffects(float fuseProgress)
        {
            if (meshRenderer != null)
            {
                // Increase glow intensity as fuse progresses
                Color glowColor = fuseGlowColor * fuseProgress;
                // Apply glow effect if material supports it
            }
        }

        #endregion

        #region Damage System (IDamageable Implementation)

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, float stunDuration = 0f)
        {
            if (isDestroyed) return;

            // Apply defense modifier for metal crates
            if (crateType == CrateType.Metal)
            {
                damage *= (1f - metalDefense);
            }

            // Convert damage to health reduction (assuming 25 damage = 1 health for standard crates)
            int healthDamage = Mathf.CeilToInt(damage / 25f);
            currentHealth -= healthDamage;

            // Apply knockback
            if (rb != null && hitDirection.magnitude > 0.1f)
            {
                float knockbackForce = damage * 0.5f;
                rb.AddForce(hitDirection * knockbackForce, ForceMode.Impulse);
            }

            // Handle special crate behaviors
            HandleSpecialDamageEffects(hitPoint, hitDirection);

            // Check for destruction
            if (currentHealth <= 0)
            {
                DestroyCrate(hitPoint, hitDirection);
            }
            else
            {
                // Update visual state for damaged crate
                UpdateVisualState();
            }

            if (debugMode)
            {
                Debug.Log($"{crateType} crate took {healthDamage} damage - Health: {currentHealth}/{health}");
            }
        }

        private void HandleSpecialDamageEffects(Vector3 hitPoint, Vector3 hitDirection)
        {
            switch (crateType)
            {
                case CrateType.Boom:
                    if (!fuseStarted && !isArmed)
                    {
                        StartFuse();
                    }
                    break;

                case CrateType.Spring:
                    // Spring crates might bounce more when hit
                    if (rb != null)
                    {
                        rb.AddForce(Vector3.up * launchHeight * 0.5f, ForceMode.Impulse);
                    }
                    break;
            }
        }

        #endregion

        #region Crate Destruction

        private void DestroyCrate(Vector3 hitPoint, Vector3 hitDirection)
        {
            if (isDestroyed) return;

            isDestroyed = true;

            // Handle type-specific destruction
            switch (crateType)
            {
                case CrateType.Standard:
                    DestroyStandardCrate(hitPoint);
                    break;
                case CrateType.Yarn:
                    DestroyYarnCrate(hitPoint);
                    break;
                case CrateType.Spring:
                    DestroySpringCrate(hitPoint);
                    break;
                case CrateType.Metal:
                    DestroyMetalCrate(hitPoint);
                    break;
                case CrateType.Boom:
                    if (!isExploding)
                        ExplodeBoomCrate();
                    return; // Don't call base destruction
                case CrateType.Mystery:
                    DestroyMysteCrate(hitPoint);
                    break;
            }

            // Common destruction effects
            SpawnDestructionEffects(hitPoint);
            
            // Trigger events
            OnCrateDestroyed?.Invoke(crateType, transform.position);

            // Remove the crate
            StartCoroutine(DestroyCrateDelayed());
        }

        private void DestroyStandardCrate(Vector3 hitPoint)
        {
            GiveReward(standardReward);
        }

        private void DestroyYarnCrate(Vector3 hitPoint)
        {
            GiveReward(yarnReward);
        }

        private void DestroySpringCrate(Vector3 hitPoint)
        {
            LaunchNearbyObjects();
            
            // Spring crates give a small reward
            var springReward = new CrateReward { rewardType = "FishTreats", amount = 5 };
            GiveReward(springReward);
        }

        private void DestroyMetalCrate(Vector3 hitPoint)
        {
            // Metal crates give better rewards due to higher difficulty
            var metalReward = new CrateReward { rewardType = "FishTreats", amount = 20 };
            GiveReward(metalReward);
        }

        private void DestroyMysteCrate(Vector3 hitPoint)
        {
            GenerateMysteryReward();
        }

        #endregion

        #region Spring Crate Mechanics

        private void LaunchNearbyObjects()
        {
            // Find all objects within launch radius
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, launchRadius);
            
            foreach (var obj in nearbyObjects)
            {
                // Skip self
                if (obj.transform == transform) continue;
                
                // Launch players
                var playerController = obj.GetComponent<WhiskerKing.Player.PlayerController>();
                if (playerController != null)
                {
                    LaunchPlayer(playerController);
                    continue;
                }
                
                // Launch rigidbodies
                var objRb = obj.GetComponent<Rigidbody>();
                if (objRb != null)
                {
                    LaunchRigidbody(objRb);
                }
            }

            // Spawn spring effect
            if (springEffect != null)
            {
                Instantiate(springEffect, transform.position, Quaternion.identity);
            }

            if (debugMode)
            {
                Debug.Log($"Spring crate launched objects within {launchRadius}m radius - Height: {launchHeight}m");
            }
        }

        private void LaunchPlayer(WhiskerKing.Player.PlayerController player)
        {
            // Calculate launch velocity to reach target height
            float launchVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * launchHeight);
            Vector3 launchForce = Vector3.up * launchVelocity;
            
            player.AddForce(launchForce);
            
            if (debugMode)
            {
                Debug.Log($"Launched player with velocity: {launchVelocity:F2} m/s to reach {launchHeight}m");
            }
        }

        private void LaunchRigidbody(Rigidbody rb)
        {
            float launchVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * launchHeight);
            Vector3 launchForce = Vector3.up * launchVelocity;
            
            // Add some horizontal spread for interesting effect
            Vector3 horizontalSpread = new Vector3(
                Random.Range(-2f, 2f),
                0f,
                Random.Range(-2f, 2f)
            );
            
            rb.AddForce(launchForce + horizontalSpread, ForceMode.VelocityChange);
        }

        #endregion

        #region Boom Crate Mechanics

        private void StartFuse()
        {
            fuseStarted = true;
            fuseStartTime = Time.time;
            isArmed = true;
            
            if (debugMode)
            {
                Debug.Log($"Boom crate fuse started - Will explode in {fuseTime}s");
            }
        }

        private void ExplodeBoomCrate()
        {
            if (isExploding) return;
            
            isExploding = true;
            isDestroyed = true;

            // Spawn explosion effect
            if (explosionEffect != null)
            {
                Instantiate(explosionEffect, transform.position, Quaternion.identity);
            }

            // Damage nearby objects
            DamageNearbyObjects();
            
            // Trigger chain reactions
            TriggerChainReaction();
            
            // Trigger explosion event
            OnExplosion?.Invoke(transform.position, chainReactionRadius);

            if (debugMode)
            {
                Debug.Log($"Boom crate exploded - Damage: {explosionDamage}, Radius: {chainReactionRadius}m");
            }

            // Remove the crate immediately
            StartCoroutine(DestroyCrateDelayed(0.1f));
        }

        private void DamageNearbyObjects()
        {
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, chainReactionRadius);
            
            foreach (var obj in nearbyObjects)
            {
                if (obj.transform == transform) continue;
                
                // Calculate distance-based damage
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                float damageFalloff = 1f - (distance / chainReactionRadius);
                float actualDamage = explosionDamage * damageFalloff;
                
                // Damage damageable objects
                var damageable = obj.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Vector3 direction = (obj.transform.position - transform.position).normalized;
                    damageable.TakeDamage(actualDamage, obj.transform.position, direction);
                }

                // Apply explosion force to rigidbodies
                var rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddExplosionForce(explosionDamage * 10f, transform.position, chainReactionRadius);
                }
            }
        }

        private void TriggerChainReaction()
        {
            // Find other boom crates in range
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, chainReactionRadius);
            
            foreach (var obj in nearbyObjects)
            {
                var otherBoomCrate = obj.GetComponent<CrateSystem>();
                if (otherBoomCrate != null && 
                    otherBoomCrate != this && 
                    otherBoomCrate.crateType == CrateType.Boom &&
                    !otherBoomCrate.isExploding)
                {
                    // Start fuse on nearby boom crates if not already started
                    if (!otherBoomCrate.fuseStarted)
                    {
                        otherBoomCrate.StartFuse();
                    }
                }
            }
        }

        #endregion

        #region Mystery Crate Mechanics

        private void GenerateMysteryReward()
        {
            float randomValue = Random.Range(0f, 1f);
            
            if (randomValue <= positiveChance)
            {
                GeneratePositiveReward();
            }
            else
            {
                GenerateNegativeEffect();
            }
        }

        private void GeneratePositiveReward()
        {
            // Generate positive rewards (70% chance as per PRD)
            var possibleRewards = new List<CrateReward>
            {
                new CrateReward { rewardType = "FishTreats", amount = 25 },
                new CrateReward { rewardType = "Yarn", amount = 50 },
                new CrateReward { rewardType = "GoldenMouseToken", amount = 1 },
                new CrateReward { rewardType = "ExtraLife", amount = 1 }
            };

            var selectedReward = possibleRewards[Random.Range(0, possibleRewards.Count)];
            GiveReward(selectedReward);

            if (debugMode)
            {
                Debug.Log($"Mystery crate positive reward: {selectedReward.rewardType} x{selectedReward.amount}");
            }
        }

        private void GenerateNegativeEffect()
        {
            // Generate negative effects (30% chance as per PRD)
            var negativeEffects = new string[] { "SpawnEnemies", "TemporarySpeedReduction", "MinorDamage" };
            var selectedEffect = negativeEffects[Random.Range(0, negativeEffects.Length)];
            
            ApplyNegativeEffect(selectedEffect);

            if (debugMode)
            {
                Debug.Log($"Mystery crate negative effect: {selectedEffect}");
            }
        }

        private void ApplyNegativeEffect(string effectType)
        {
            switch (effectType)
            {
                case "SpawnEnemies":
                    // Spawn small enemies near the crate
                    SpawnMinorEnemies();
                    break;
                    
                case "TemporarySpeedReduction":
                    // Apply temporary speed reduction to nearby players
                    ApplySpeedReduction();
                    break;
                    
                case "MinorDamage":
                    // Deal minor damage to nearby players
                    ApplyMinorDamage();
                    break;
            }
        }

        private void SpawnMinorEnemies()
        {
            // Placeholder for enemy spawning
            if (debugMode)
            {
                Debug.Log("Mystery crate spawned minor enemies");
            }
        }

        private void ApplySpeedReduction()
        {
            // Find nearby players and apply temporary speed reduction
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, 3f);
            
            foreach (var obj in nearbyObjects)
            {
                var player = obj.GetComponent<WhiskerKing.Player.PlayerController>();
                if (player != null)
                {
                    // Apply temporary debuff (implementation would depend on buff/debuff system)
                    if (debugMode)
                    {
                        Debug.Log("Applied speed reduction to player");
                    }
                }
            }
        }

        private void ApplyMinorDamage()
        {
            // Deal minor damage to nearby players
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, 2f);
            
            foreach (var obj in nearbyObjects)
            {
                var damageable = obj.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Vector3 direction = (obj.transform.position - transform.position).normalized;
                    damageable.TakeDamage(10f, obj.transform.position, direction);
                }
            }
        }

        #endregion

        #region Reward System

        private void GiveReward(CrateReward reward)
        {
            // Spawn reward pickup if prefab is assigned
            if (reward.rewardPrefab != null)
            {
                GameObject rewardInstance = Instantiate(reward.rewardPrefab, transform.position + Vector3.up, Quaternion.identity);
                
                // Add upward force for visual appeal
                var rewardRb = rewardInstance.GetComponent<Rigidbody>();
                if (rewardRb != null)
                {
                    Vector3 randomDirection = new Vector3(
                        Random.Range(-1f, 1f),
                        Random.Range(1f, 2f),
                        Random.Range(-1f, 1f)
                    ).normalized;
                    
                    rewardRb.AddForce(randomDirection * 5f, ForceMode.Impulse);
                }
            }

            // Trigger reward event
            OnRewardGiven?.Invoke(reward);

            if (debugMode)
            {
                Debug.Log($"Gave reward: {reward.rewardType} x{reward.amount}");
            }
        }

        #endregion

        #region Visual Effects

        private void UpdateVisualState()
        {
            if (meshRenderer == null) return;

            // Update material based on health
            float healthPercentage = (float)currentHealth / health;
            
            if (healthPercentage < 0.5f && damagedMaterial != null)
            {
                meshRenderer.material = damagedMaterial;
            }
            else
            {
                meshRenderer.material = originalMaterial;
            }
        }

        private void SpawnDestructionEffects(Vector3 position)
        {
            if (destructionEffect != null)
            {
                Instantiate(destructionEffect, position, Quaternion.identity);
            }
        }

        #endregion

        #region Utility Methods

        private IEnumerator DestroyCrateDelayed(float delay = 0.5f)
        {
            yield return new WaitForSeconds(delay);
            
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current crate type
        /// </summary>
        public CrateType GetCrateType()
        {
            return crateType;
        }

        /// <summary>
        /// Get current health
        /// </summary>
        public int GetCurrentHealth()
        {
            return currentHealth;
        }

        /// <summary>
        /// Get max health
        /// </summary>
        public int GetMaxHealth()
        {
            return crateType == CrateType.Metal ? metalHealth : health;
        }

        /// <summary>
        /// Check if crate is destroyed
        /// </summary>
        public bool IsDestroyed()
        {
            return isDestroyed;
        }

        /// <summary>
        /// Manually trigger crate (for switches, etc.)
        /// </summary>
        public void Trigger()
        {
            Vector3 triggerPoint = transform.position;
            Vector3 triggerDirection = Vector3.up;
            
            switch (crateType)
            {
                case CrateType.Spring:
                    LaunchNearbyObjects();
                    break;
                case CrateType.Boom:
                    if (!fuseStarted)
                    {
                        StartFuse();
                    }
                    break;
                default:
                    TakeDamage(25f, triggerPoint, triggerDirection);
                    break;
            }
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            // Update debug information
        }

        private void DrawCrateGizmos()
        {
            // Draw crate-specific gizmos
            switch (crateType)
            {
                case CrateType.Spring:
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(transform.position, launchRadius);
                    Gizmos.DrawRay(transform.position, Vector3.up * launchHeight);
                    break;
                    
                case CrateType.Boom:
                    Gizmos.color = fuseStarted ? Color.red : Color.orange;
                    Gizmos.DrawWireSphere(transform.position, chainReactionRadius);
                    break;
            }
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                Vector3 screenPos = UnityEngine.Camera.main?.WorldToScreenPoint(transform.position) ?? Vector3.zero;
                
                if (screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width && 
                    screenPos.y >= 0 && screenPos.y <= Screen.height)
                {
                    GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y, 100, 40), 
                             $"{crateType}\nHP: {currentHealth}/{GetMaxHealth()}");
                }
            }
        }

        #endregion
    }
}
