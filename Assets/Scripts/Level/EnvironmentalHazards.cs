using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using WhiskerKing.Core;
using WhiskerKing.Player;
using WhiskerKing.Combat;

namespace WhiskerKing.Level
{
    /// <summary>
    /// Environmental Hazards system for Whisker King
    /// Implements spikes, pits, moving platforms, crushing obstacles, and dynamic environmental threats
    /// Integrates with level progression and provides engaging challenges
    /// </summary>
    public class EnvironmentalHazards : MonoBehaviour
    {
        public enum HazardType
        {
            Spike,
            Pit,
            MovingPlatform,
            CrushingWall,
            LaserBeam,
            FireJet,
            ElectricField,
            MovingBlade
        }

        public enum HazardState
        {
            Inactive,
            Warning,
            Active,
            Recovering,
            Disabled
        }

        [System.Serializable]
        public class HazardData
        {
            public HazardType type;
            public float damage = 25f;
            public float activationDelay = 1f;
            public float activeDuration = 2f;
            public float cooldownTime = 3f;
            public bool repeating = true;
            public bool playerTriggered = false;
            public float triggerRange = 5f;
        }

        [Header("Hazard Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;

        [Header("Hazard Settings")]
        [SerializeField] private HazardType hazardType = HazardType.Spike;
        [SerializeField] private HazardData hazardConfig = new HazardData();

        [Header("Movement Settings (for moving hazards)")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float movementSpeed = 5f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private bool reverseDirection = false;

        [Header("Visual Settings")]
        [SerializeField] private GameObject warningEffect;
        [SerializeField] private GameObject activeEffect;
        [SerializeField] private GameObject damageEffect;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color activeColor = Color.red;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip warningSound;
        [SerializeField] private AudioClip activationSound;
        [SerializeField] private AudioClip damageSound;

        // Components
        private Collider hazardCollider;
        private MeshRenderer meshRenderer;
        private PlayerController nearbyPlayer;
        
        // Hazard state
        private HazardState currentState = HazardState.Inactive;
        private float stateStartTime;
        private bool isPlayerInRange = false;
        private List<GameObject> affectedObjects = new List<GameObject>();

        // Movement state
        private int currentWaypointIndex = 0;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float movementStartTime;
        private bool movingForward = true;

        // Configuration cache
        private LevelDesignData levelConfig;

        // Effects
        private GameObject currentWarningEffect;
        private GameObject currentActiveEffect;
        private Material originalMaterial;

        // Events
        public System.Action<HazardType, Vector3> OnHazardActivated;
        public System.Action<GameObject, float> OnObjectDamaged;
        public System.Action<HazardState> OnStateChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            // Get components
            hazardCollider = GetComponent<Collider>();
            meshRenderer = GetComponent<MeshRenderer>();
            audioSource = GetComponent<AudioSource>();

            if (meshRenderer != null)
            {
                originalMaterial = meshRenderer.material;
            }

            // Initialize audio source if not assigned
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f; // 3D sound
            }
        }

        private void Start()
        {
            LoadConfiguration();
            InitializeHazard();
        }

        private void Update()
        {
            UpdateHazardState();
            UpdateMovement();
            CheckPlayerProximity();
            
            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleTriggerExit(other);
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode)
            {
                DrawHazardGizmos();
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
                Debug.Log($"EnvironmentalHazards: Configuration loaded for {hazardType}");
            }
            else
            {
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            if (levelConfig?.hazards == null) return;

            // Apply hazard-specific configuration
            switch (hazardType)
            {
                case HazardType.Spike:
                    if (levelConfig.hazards.spikes != null)
                    {
                        hazardConfig.damage = levelConfig.hazards.spikes.damage;
                        hazardConfig.activationDelay = levelConfig.hazards.spikes.activationDelay;
                        hazardConfig.activeDuration = levelConfig.hazards.spikes.activeDuration;
                    }
                    break;

                case HazardType.MovingPlatform:
                    if (levelConfig.hazards.movingPlatforms != null)
                    {
                        movementSpeed = levelConfig.hazards.movingPlatforms.speed;
                    }
                    break;

                case HazardType.LaserBeam:
                    if (levelConfig.hazards.lasers != null)
                    {
                        hazardConfig.damage = levelConfig.hazards.lasers.damage;
                        hazardConfig.activeDuration = levelConfig.hazards.lasers.duration;
                    }
                    break;
            }
        }

        private void UseDefaultConfiguration()
        {
            // Use PRD-compliant defaults
            hazardConfig.damage = 25f;
            hazardConfig.activationDelay = 1f;
            hazardConfig.activeDuration = 2f;
            hazardConfig.cooldownTime = 3f;
            hazardConfig.repeating = true;
        }

        #endregion

        #region Initialization

        private void InitializeHazard()
        {
            // Set initial state
            currentState = HazardState.Inactive;
            stateStartTime = Time.time;

            // Initialize movement system
            if (waypoints != null && waypoints.Length > 0)
            {
                startPosition = transform.position;
                SetMovementTarget();
            }

            // Configure hazard based on type
            ConfigureHazardType();

            Debug.Log($"Environmental hazard initialized: {hazardType}");
        }

        private void ConfigureHazardType()
        {
            switch (hazardType)
            {
                case HazardType.Spike:
                    ConfigureSpikes();
                    break;
                case HazardType.Pit:
                    ConfigurePit();
                    break;
                case HazardType.MovingPlatform:
                    ConfigureMovingPlatform();
                    break;
                case HazardType.CrushingWall:
                    ConfigureCrushingWall();
                    break;
                case HazardType.LaserBeam:
                    ConfigureLaserBeam();
                    break;
                case HazardType.FireJet:
                    ConfigureFireJet();
                    break;
                case HazardType.ElectricField:
                    ConfigureElectricField();
                    break;
                case HazardType.MovingBlade:
                    ConfigureMovingBlade();
                    break;
            }
        }

        #endregion

        #region Hazard Type Configuration

        private void ConfigureSpikes()
        {
            // Spikes activate when player approaches
            hazardConfig.playerTriggered = true;
            hazardConfig.triggerRange = 3f;
            hazardConfig.activationDelay = 0.5f;
            hazardConfig.activeDuration = 1f;
            hazardConfig.cooldownTime = 2f;
        }

        private void ConfigurePit()
        {
            // Pits are always active death zones
            hazardConfig.damage = 100f; // Instant death
            hazardConfig.playerTriggered = false;
            hazardConfig.repeating = false;
        }

        private void ConfigureMovingPlatform()
        {
            // Moving platforms don't damage, just transport
            hazardConfig.damage = 0f;
            hazardConfig.playerTriggered = false;
            hazardConfig.repeating = true;
        }

        private void ConfigureCrushingWall()
        {
            // Crushing walls have long activation delay for fairness
            hazardConfig.damage = 50f;
            hazardConfig.activationDelay = 2f;
            hazardConfig.activeDuration = 1f;
            hazardConfig.cooldownTime = 5f;
        }

        private void ConfigureLaserBeam()
        {
            // Laser beams have predictable patterns
            hazardConfig.damage = 30f;
            hazardConfig.activationDelay = 1f;
            hazardConfig.activeDuration = 3f;
            hazardConfig.cooldownTime = 2f;
        }

        private void ConfigureFireJet()
        {
            // Fire jets have moderate damage and fast activation
            hazardConfig.damage = 20f;
            hazardConfig.activationDelay = 0.3f;
            hazardConfig.activeDuration = 1.5f;
            hazardConfig.cooldownTime = 3f;
        }

        private void ConfigureElectricField()
        {
            // Electric fields have continuous low damage
            hazardConfig.damage = 15f;
            hazardConfig.activationDelay = 0f;
            hazardConfig.activeDuration = 2f;
            hazardConfig.cooldownTime = 1f;
        }

        private void ConfigureMovingBlade()
        {
            // Moving blades are predictable but deadly
            hazardConfig.damage = 40f;
            hazardConfig.playerTriggered = false;
            hazardConfig.repeating = true;
        }

        #endregion

        #region State Management

        private void UpdateHazardState()
        {
            float stateElapsed = Time.time - stateStartTime;

            switch (currentState)
            {
                case HazardState.Inactive:
                    if (ShouldActivate())
                    {
                        StartWarningPhase();
                    }
                    break;

                case HazardState.Warning:
                    if (stateElapsed >= hazardConfig.activationDelay)
                    {
                        ActivateHazard();
                    }
                    break;

                case HazardState.Active:
                    if (stateElapsed >= hazardConfig.activeDuration)
                    {
                        StartRecoveryPhase();
                    }
                    break;

                case HazardState.Recovering:
                    if (stateElapsed >= hazardConfig.cooldownTime)
                    {
                        if (hazardConfig.repeating)
                        {
                            SetHazardState(HazardState.Inactive);
                        }
                        else
                        {
                            SetHazardState(HazardState.Disabled);
                        }
                    }
                    break;
            }
        }

        private bool ShouldActivate()
        {
            if (hazardConfig.playerTriggered)
            {
                return isPlayerInRange;
            }
            else
            {
                return hazardConfig.repeating; // Auto-activate for repeating hazards
            }
        }

        private void StartWarningPhase()
        {
            SetHazardState(HazardState.Warning);
            
            // Show warning effects
            ShowWarningEffects();
            PlayWarningSound();

            if (debugMode)
            {
                Debug.Log($"{hazardType} hazard warning started");
            }
        }

        private void ActivateHazard()
        {
            SetHazardState(HazardState.Active);
            
            // Show active effects
            ShowActiveEffects();
            PlayActivationSound();
            
            // Enable collision damage
            EnableHazardCollision();

            OnHazardActivated?.Invoke(hazardType, transform.position);

            if (debugMode)
            {
                Debug.Log($"{hazardType} hazard activated");
            }
        }

        private void StartRecoveryPhase()
        {
            SetHazardState(HazardState.Recovering);
            
            // Hide effects
            HideAllEffects();
            
            // Disable collision damage
            DisableHazardCollision();

            if (debugMode)
            {
                Debug.Log($"{hazardType} hazard recovering");
            }
        }

        private void SetHazardState(HazardState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            stateStartTime = Time.time;
            
            OnStateChanged?.Invoke(newState);
            
            // Update visual appearance
            UpdateVisualState();
        }

        #endregion

        #region Movement System

        private void UpdateMovement()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            // Move towards current target
            MoveTowardsTarget();
            
            // Check if reached target
            if (HasReachedTarget())
            {
                SetNextMovementTarget();
            }
        }

        private void MoveTowardsTarget()
        {
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            float journeyTime = journeyLength / movementSpeed;
            float elapsedTime = Time.time - movementStartTime;
            float progress = Mathf.Clamp01(elapsedTime / journeyTime);
            
            // Apply movement curve
            float easedProgress = movementCurve.Evaluate(progress);
            
            // Update position
            transform.position = Vector3.Lerp(startPosition, targetPosition, easedProgress);
        }

        private bool HasReachedTarget()
        {
            return Vector3.Distance(transform.position, targetPosition) < 0.1f;
        }

        private void SetNextMovementTarget()
        {
            if (movingForward)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= waypoints.Length)
                {
                    currentWaypointIndex = waypoints.Length - 1;
                    movingForward = false;
                }
            }
            else
            {
                currentWaypointIndex--;
                if (currentWaypointIndex < 0)
                {
                    currentWaypointIndex = 0;
                    movingForward = true;
                }
            }

            SetMovementTarget();
        }

        private void SetMovementTarget()
        {
            startPosition = transform.position;
            targetPosition = waypoints[currentWaypointIndex].position;
            movementStartTime = Time.time;
        }

        #endregion

        #region Player Detection

        private void CheckPlayerProximity()
        {
            if (!hazardConfig.playerTriggered) return;

            bool playerWasInRange = isPlayerInRange;
            isPlayerInRange = false;

            // Find player within trigger range
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, hazardConfig.triggerRange);
            
            foreach (var collider in nearbyColliders)
            {
                var player = collider.GetComponent<PlayerController>();
                if (player != null)
                {
                    isPlayerInRange = true;
                    nearbyPlayer = player;
                    break;
                }
            }

            // Player entered range
            if (isPlayerInRange && !playerWasInRange)
            {
                OnPlayerEnterRange();
            }
            // Player left range
            else if (!isPlayerInRange && playerWasInRange)
            {
                OnPlayerExitRange();
            }
        }

        private void OnPlayerEnterRange()
        {
            if (debugMode)
            {
                Debug.Log($"Player entered {hazardType} trigger range");
            }
        }

        private void OnPlayerExitRange()
        {
            nearbyPlayer = null;
            
            if (debugMode)
            {
                Debug.Log($"Player exited {hazardType} trigger range");
            }
        }

        #endregion

        #region Collision Handling

        private void HandleTriggerEnter(Collider other)
        {
            if (currentState != HazardState.Active) return;

            // Check if object can be damaged
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null && !affectedObjects.Contains(other.gameObject))
            {
                ApplyHazardDamage(other.gameObject, damageable);
                affectedObjects.Add(other.gameObject);
            }

            // Handle special cases
            HandleSpecialCollision(other);
        }

        private void HandleTriggerExit(Collider other)
        {
            // Remove from affected objects when leaving hazard area
            if (affectedObjects.Contains(other.gameObject))
            {
                affectedObjects.Remove(other.gameObject);
            }
        }

        private void ApplyHazardDamage(GameObject target, IDamageable damageable)
        {
            Vector3 hitPoint = target.transform.position;
            Vector3 hitDirection = (target.transform.position - transform.position).normalized;
            
            damageable.TakeDamage(hazardConfig.damage, hitPoint, hitDirection);
            
            // Show damage effects
            ShowDamageEffects(hitPoint);
            PlayDamageSound();
            
            OnObjectDamaged?.Invoke(target, hazardConfig.damage);

            if (debugMode)
            {
                Debug.Log($"{hazardType} damaged {target.name} for {hazardConfig.damage} damage");
            }
        }

        private void HandleSpecialCollision(Collider other)
        {
            switch (hazardType)
            {
                case HazardType.MovingPlatform:
                    // Make object follow platform movement
                    var player = other.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        // Platform carrying logic would go here
                    }
                    break;

                case HazardType.Pit:
                    // Instant death for pits
                    var playerInPit = other.GetComponent<PlayerController>();
                    if (playerInPit != null)
                    {
                        // Trigger player death/respawn
                        var levelManager = FindObjectOfType<LevelManager>();
                        levelManager?.LoadLastCheckpoint();
                    }
                    break;
            }
        }

        #endregion

        #region Visual Effects

        private void ShowWarningEffects()
        {
            if (warningEffect != null && currentWarningEffect == null)
            {
                currentWarningEffect = Instantiate(warningEffect, transform.position, transform.rotation);
            }

            // Change material color for warning
            if (meshRenderer != null)
            {
                var material = meshRenderer.material;
                material.color = Color.Lerp(originalMaterial.color, warningColor, 0.7f);
            }
        }

        private void ShowActiveEffects()
        {
            // Hide warning effects
            if (currentWarningEffect != null)
            {
                Destroy(currentWarningEffect);
                currentWarningEffect = null;
            }

            // Show active effects
            if (activeEffect != null && currentActiveEffect == null)
            {
                currentActiveEffect = Instantiate(activeEffect, transform.position, transform.rotation);
            }

            // Change material color for active state
            if (meshRenderer != null)
            {
                var material = meshRenderer.material;
                material.color = activeColor;
            }
        }

        private void ShowDamageEffects(Vector3 position)
        {
            if (damageEffect != null)
            {
                Instantiate(damageEffect, position, Quaternion.identity);
            }
        }

        private void HideAllEffects()
        {
            if (currentWarningEffect != null)
            {
                Destroy(currentWarningEffect);
                currentWarningEffect = null;
            }

            if (currentActiveEffect != null)
            {
                Destroy(currentActiveEffect);
                currentActiveEffect = null;
            }

            // Restore original material
            if (meshRenderer != null && originalMaterial != null)
            {
                meshRenderer.material = originalMaterial;
            }
        }

        private void UpdateVisualState()
        {
            // Update visual state based on current hazard state
            switch (currentState)
            {
                case HazardState.Inactive:
                    HideAllEffects();
                    break;
                case HazardState.Warning:
                    // Warning effects already shown in StartWarningPhase
                    break;
                case HazardState.Active:
                    // Active effects already shown in ActivateHazard
                    break;
                case HazardState.Recovering:
                    HideAllEffects();
                    break;
            }
        }

        #endregion

        #region Audio System

        private void PlayWarningSound()
        {
            if (warningSound != null && audioSource != null)
            {
                audioSource.clip = warningSound;
                audioSource.Play();
            }
        }

        private void PlayActivationSound()
        {
            if (activationSound != null && audioSource != null)
            {
                audioSource.clip = activationSound;
                audioSource.Play();
            }
        }

        private void PlayDamageSound()
        {
            if (damageSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(damageSound);
            }
        }

        #endregion

        #region Hazard Control

        private void EnableHazardCollision()
        {
            if (hazardCollider != null)
            {
                hazardCollider.isTrigger = true;
                hazardCollider.enabled = true;
            }
            
            affectedObjects.Clear();
        }

        private void DisableHazardCollision()
        {
            if (hazardCollider != null)
            {
                hazardCollider.enabled = false;
            }
            
            affectedObjects.Clear();
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Force activate the hazard
        /// </summary>
        public void ForceActivate()
        {
            if (currentState == HazardState.Inactive)
            {
                StartWarningPhase();
            }
        }

        /// <summary>
        /// Disable the hazard permanently
        /// </summary>
        public void DisableHazard()
        {
            SetHazardState(HazardState.Disabled);
            HideAllEffects();
            DisableHazardCollision();
        }

        /// <summary>
        /// Reset hazard to initial state
        /// </summary>
        public void ResetHazard()
        {
            SetHazardState(HazardState.Inactive);
            affectedObjects.Clear();
            HideAllEffects();
            DisableHazardCollision();
        }

        /// <summary>
        /// Get current hazard state
        /// </summary>
        public HazardState GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// Get hazard configuration
        /// </summary>
        public HazardData GetConfiguration()
        {
            return hazardConfig;
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            // Update debug information
        }

        private void DrawHazardGizmos()
        {
            // Draw hazard area
            Gizmos.color = currentState switch
            {
                HazardState.Warning => Color.yellow,
                HazardState.Active => Color.red,
                _ => Color.white
            };
            
            Gizmos.DrawWireCube(transform.position, transform.localScale);
            
            // Draw trigger range for player-triggered hazards
            if (hazardConfig.playerTriggered)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, hazardConfig.triggerRange);
            }
            
            // Draw waypoint path for moving hazards
            if (waypoints != null && waypoints.Length > 1)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < waypoints.Length - 1; i++)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
                
                // Draw current target
                if (currentWaypointIndex < waypoints.Length)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, waypoints[currentWaypointIndex].position);
                }
            }
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                Vector3 screenPos = Camera.main?.WorldToScreenPoint(transform.position) ?? Vector3.zero;
                
                if (screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width && 
                    screenPos.y >= 0 && screenPos.y <= Screen.height)
                {
                    GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y, 100, 60), 
                             $"{hazardType}\n{currentState}\nDmg: {hazardConfig.damage}");
                }
            }
        }

        #endregion
    }
}
