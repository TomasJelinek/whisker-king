using UnityEngine;
using WhiskerKing.Core;
using WhiskerKing.Player;

namespace WhiskerKing.Camera
{
    /// <summary>
    /// Advanced camera system for Whisker King
    /// Supports Follow, Chase, and Cinematic modes with smooth transitions
    /// Integrates with GameConfiguration for PRD-compliant settings
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public enum CameraMode
        {
            Follow,
            Chase,
            Cinematic
        }

        [Header("Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;
        [SerializeField] private CameraMode startingMode = CameraMode.Follow;

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindPlayer = true;

        [Header("Follow Mode Settings (Override if not using GameConfiguration)")]
        [SerializeField] private float followDistance = 8.0f;
        [SerializeField] private float followHeight = 3.0f;
        [SerializeField] private float followDamping = 5.0f;
        [SerializeField] private float lookAheadDistance = 4.0f;
        [SerializeField] private float lookAheadHeight = 2.0f;
        [SerializeField] private float lookAheadDamping = 3.0f;

        [Header("Chase Mode Settings")]
        [SerializeField] private float chaseFOV = 85.0f;
        [SerializeField] private float chaseDistance = 6.0f;
        [SerializeField] private float chaseHeight = 2.5f;

        [Header("Comfort Settings")]
        [SerializeField] private bool motionBlurEnabled = false;
        [SerializeField] [Range(0f, 1f)] private float shakeIntensity = 0.5f;

        [Header("Transition Settings")]
        [SerializeField] private float transitionDuration = 2.0f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Lane Centering")]
        [SerializeField] private bool enableLaneCentering = false;
        [SerializeField] private float laneWidth = 4.0f;
        [SerializeField] private float centeringStrength = 2.0f;

        [Header("Camera Shake")]
        [SerializeField] private bool enableCameraShake = true;
        [SerializeField] private float shakeFrequency = 10f;
        [SerializeField] private Vector3 shakeAmplitude = new Vector3(0.1f, 0.1f, 0.05f);

        // Components
        private UnityEngine.Camera cameraComponent;
        private PlayerController playerController;

        // Current camera state
        private CameraMode currentMode;
        private CameraMode targetMode;
        private bool isTransitioning = false;
        private float transitionStartTime;
        private Vector3 currentPosition;
        private Vector3 currentLookAt;
        private float currentFOV;

        // Follow mode state
        private Vector3 followTargetPosition;
        private Vector3 lookAheadTargetPosition;
        private Vector3 followVelocity;
        private Vector3 lookAheadVelocity;

        // Chase mode state
        private Vector3 chaseTargetPosition;
        private Vector3 chaseVelocity;

        // Cinematic mode state
        private Vector3 cinematicStartPosition;
        private Vector3 cinematicEndPosition;
        private Vector3 cinematicStartRotation;
        private Vector3 cinematicEndRotation;
        private float cinematicStartFOV;
        private float cinematicEndFOV;
        private float cinematicDuration;
        private float cinematicStartTime;
        private bool cinematicActive = false;

        // Configuration cache
        private CameraSettingsData cameraConfig;

        // Lane centering state
        private Vector3 laneCenter;
        private float laneOffset;

        // Camera shake state
        private Vector3 shakeOffset;
        private float shakeTime;
        private bool isShaking;

        // Comfort options
        private Vector3 comfortOffset;

        // Performance tracking
        private Vector3 debugLastTargetPosition;
        private float debugCurrentDistance;
        private float debugLookAheadMagnitude;

        #region Unity Lifecycle

        private void Awake()
        {
            // Get required components
            cameraComponent = GetComponent<UnityEngine.Camera>();
            if (cameraComponent == null)
            {
                cameraComponent = gameObject.AddComponent<UnityEngine.Camera>();
                Debug.LogWarning("CameraController: Added Camera component automatically");
            }

            // Find player target if needed
            if (autoFindPlayer && target == null)
            {
                FindPlayerTarget();
            }

            // Initialize camera state
            currentMode = startingMode;
            targetMode = startingMode;
            currentPosition = transform.position;
            currentLookAt = transform.forward;
            currentFOV = cameraComponent.fieldOfView;
        }

        private void Start()
        {
            LoadConfiguration();
            InitializeCamera();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateCameraMode();
            UpdateCameraTransitions();
            UpdateCameraPosition();
            UpdateCameraLookAt();
            UpdateCameraFOV();

            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode && Application.isPlaying && target != null)
            {
                DrawDebugGizmos();
            }
        }

        #endregion

        #region Configuration

        private void LoadConfiguration()
        {
            if (useGameConfiguration && GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                var cameraSettings = GameConfiguration.Instance.GetCameraSettings();
                
                if (cameraSettings != null)
                {
                    cameraConfig = cameraSettings;
                    ApplyConfiguration();
                    Debug.Log("CameraController: Configuration loaded from GameConfig");
                }
                else
                {
                    Debug.LogWarning("CameraController: GameConfiguration not available, using default values");
                    UseDefaultConfiguration();
                }
            }
            else
            {
                Debug.Log("CameraController: Using Inspector values (GameConfiguration disabled)");
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            // Apply follow mode settings
            if (cameraConfig.followMode != null)
            {
                followDistance = cameraConfig.followMode.distance;
                followHeight = cameraConfig.followMode.height;
                followDamping = cameraConfig.followMode.damping;
                lookAheadDistance = cameraConfig.followMode.lookAheadDistance;
                lookAheadHeight = cameraConfig.followMode.lookAheadHeight;
                lookAheadDamping = cameraConfig.followMode.lookAheadDamping;
            }

            // Apply chase mode settings
            if (cameraConfig.chaseMode != null)
            {
                chaseFOV = cameraConfig.chaseMode.fov;
                chaseDistance = cameraConfig.chaseMode.distance;
                chaseHeight = cameraConfig.chaseMode.height;
            }

            // Apply comfort settings
            if (cameraConfig.comfort != null)
            {
                motionBlurEnabled = cameraConfig.comfort.motionBlurEnabled;
                shakeIntensity = cameraConfig.comfort.shakeIntensity;
            }
        }

        private void UseDefaultConfiguration()
        {
            // Create default configuration based on PRD values
            cameraConfig = new CameraSettingsData
            {
                followMode = new FollowModeData
                {
                    distance = 8.0f,
                    height = 3.0f,
                    damping = 5.0f,
                    lookAheadDistance = 4.0f,
                    lookAheadHeight = 2.0f,
                    lookAheadDamping = 3.0f
                },
                chaseMode = new ChaseModeData
                {
                    fov = 85.0f,
                    distance = 6.0f,
                    height = 2.5f
                },
                comfort = new ComfortData
                {
                    motionBlurEnabled = false,
                    shakeIntensity = 0.5f
                }
            };

            ApplyConfiguration();
        }

        #endregion

        #region Initialization

        private void FindPlayerTarget()
        {
            // Find PlayerController in scene
            playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                target = playerController.transform;
                Debug.Log("CameraController: Found player target automatically");
            }
            else
            {
                Debug.LogWarning("CameraController: Could not find PlayerController in scene");
            }
        }

        private void InitializeCamera()
        {
            if (target == null)
            {
                Debug.LogError("CameraController: No target assigned!");
                return;
            }

            // Set initial camera position based on mode
            SetInitialCameraPosition();

            // Configure camera component
            ConfigureCameraComponent();

            // Initialize mode-specific state
            InitializeModeState();

            Debug.Log($"CameraController initialized - Mode: {currentMode}, Target: {target.name}");
        }

        private void SetInitialCameraPosition()
        {
            Vector3 targetPosition = target.position;
            
            switch (currentMode)
            {
                case CameraMode.Follow:
                    currentPosition = targetPosition + Vector3.back * followDistance + Vector3.up * followHeight;
                    break;
                case CameraMode.Chase:
                    currentPosition = targetPosition + Vector3.forward * chaseDistance + Vector3.up * chaseHeight;
                    break;
                case CameraMode.Cinematic:
                    // Keep current position for cinematic mode
                    break;
            }
            
            transform.position = currentPosition;
            transform.LookAt(targetPosition);
        }

        private void ConfigureCameraComponent()
        {
            // Set up camera for 3D platformer
            cameraComponent.fieldOfView = currentMode == CameraMode.Chase ? chaseFOV : 60f;
            cameraComponent.nearClipPlane = 0.1f;
            cameraComponent.farClipPlane = 100f;
            
            // Enable depth texture for post-processing
            cameraComponent.depthTextureMode = DepthTextureMode.Depth;
        }

        private void InitializeModeState()
        {
            Vector3 targetPos = target.position;
            
            // Initialize follow mode state
            followTargetPosition = targetPos + Vector3.back * followDistance + Vector3.up * followHeight;
            lookAheadTargetPosition = targetPos;
            
            // Initialize chase mode state  
            chaseTargetPosition = targetPos + Vector3.forward * chaseDistance + Vector3.up * chaseHeight;
            
            // Reset velocities
            followVelocity = Vector3.zero;
            lookAheadVelocity = Vector3.zero;
            chaseVelocity = Vector3.zero;
        }

        #endregion

        #region Camera Mode Management

        private void UpdateCameraMode()
        {
            // Mode switching logic will be implemented in later tasks
            // For now, maintain current mode unless explicitly changed
        }

        /// <summary>
        /// Set camera mode with smooth transition
        /// </summary>
        public void SetCameraMode(CameraMode newMode)
        {
            if (newMode == currentMode && !isTransitioning) return;

            targetMode = newMode;
            
            if (!isTransitioning)
            {
                StartModeTransition();
            }
        }

        private void StartModeTransition()
        {
            isTransitioning = true;
            transitionStartTime = Time.time;
            
            if (debugMode)
            {
                Debug.Log($"Camera transition started: {currentMode} → {targetMode}");
            }
        }

        private void UpdateCameraTransitions()
        {
            if (!isTransitioning) return;

            float elapsed = Time.time - transitionStartTime;
            float progress = elapsed / transitionDuration;
            
            if (progress >= 1.0f)
            {
                // Transition complete
                CompleteTransition();
            }
            else
            {
                // Apply transition curve
                float curvedProgress = transitionCurve.Evaluate(progress);
                ApplyTransitionProgress(curvedProgress);
            }
        }

        private void CompleteTransition()
        {
            currentMode = targetMode;
            isTransitioning = false;
            
            if (debugMode)
            {
                Debug.Log($"Camera transition completed: {currentMode}");
            }
        }

        private void ApplyTransitionProgress(float progress)
        {
            // Smooth transition between camera modes
            Vector3 sourcePosition = GetModePosition(currentMode);
            Vector3 targetPosition = GetModePosition(targetMode);
            float sourceFOV = GetModeFOV(currentMode);
            float targetFOV = GetModeFOV(targetMode);
            
            // Interpolate position and FOV
            currentPosition = Vector3.Lerp(sourcePosition, targetPosition, progress);
            currentFOV = Mathf.Lerp(sourceFOV, targetFOV, progress);
            
            // Update camera component
            cameraComponent.fieldOfView = currentFOV;
        }

        private Vector3 GetModePosition(CameraMode mode)
        {
            if (target == null) return currentPosition;
            
            Vector3 targetPos = target.position;
            
            switch (mode)
            {
                case CameraMode.Follow:
                    return targetPos + Vector3.back * followDistance + Vector3.up * followHeight;
                case CameraMode.Chase:
                    Vector3 chaseDir = target.forward;
                    if (chaseDir.magnitude < 0.1f) chaseDir = Vector3.forward;
                    return targetPos + chaseDir * chaseDistance + Vector3.up * chaseHeight;
                case CameraMode.Cinematic:
                    return currentPosition; // Maintain current position for cinematic
                default:
                    return currentPosition;
            }
        }

        private float GetModeFOV(CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Follow:
                    return 60f;
                case CameraMode.Chase:
                    return chaseFOV; // 85° as per PRD
                case CameraMode.Cinematic:
                    return currentFOV; // Maintain current FOV for cinematic
                default:
                    return 60f;
            }
        }

        #endregion

        #region Camera Updates (Placeholder for next tasks)

        private void UpdateCameraPosition()
        {
            if (target == null) return;

            switch (currentMode)
            {
                case CameraMode.Follow:
                    UpdateFollowMode();
                    break;
                case CameraMode.Chase:
                    UpdateChaseMode();
                    break;
                case CameraMode.Cinematic:
                    UpdateCinematicMode();
                    break;
            }

            // Apply post-processing effects
            ApplyLaneCentering();
            ApplyCameraShake();
            ApplyComfortOptions();
        }

        private void UpdateFollowMode()
        {
            // Calculate base follow position (8f distance, 3f height as per PRD)
            Vector3 targetPosition = target.position;
            
            // Get player movement direction for look-ahead
            Vector3 playerVelocity = GetPlayerVelocity();
            Vector3 playerDirection = GetPlayerMovementDirection();
            
            // Calculate base camera position
            Vector3 baseOffset = Vector3.back * followDistance + Vector3.up * followHeight;
            Vector3 baseFollowPosition = targetPosition + baseOffset;
            
            // Apply look-ahead offset
            Vector3 lookAheadOffset = CalculateLookAheadOffset(playerVelocity, playerDirection);
            Vector3 finalFollowPosition = baseFollowPosition + lookAheadOffset;
            
            // Smooth camera movement with damping
            followTargetPosition = finalFollowPosition;
            currentPosition = Vector3.SmoothDamp(currentPosition, followTargetPosition, ref followVelocity, 1f / followDamping);
            
            // Handle collision avoidance
            currentPosition = HandleCameraCollision(currentPosition, targetPosition);
            
            if (debugMode)
            {
                debugCurrentDistance = Vector3.Distance(currentPosition, targetPosition);
            }
        }

        private Vector3 CalculateLookAheadOffset(Vector3 playerVelocity, Vector3 playerDirection)
        {
            // Look-ahead system implementation (4f distance, 2f height as per PRD)
            if (playerVelocity.magnitude < 0.1f)
            {
                // Player not moving, gradually return to center
                Vector3 currentLookAhead = lookAheadTargetPosition - target.position;
                lookAheadTargetPosition = Vector3.SmoothDamp(lookAheadTargetPosition, target.position, ref lookAheadVelocity, 1f / lookAheadDamping);
                return Vector3.zero;
            }
            
            // Calculate look-ahead position based on player movement
            Vector3 horizontalDirection = new Vector3(playerDirection.x, 0, playerDirection.z).normalized;
            float speedFactor = Mathf.Clamp01(playerVelocity.magnitude / 10f); // Normalize to reasonable range
            
            // Apply look-ahead offset (4f distance, 2f height)
            Vector3 lookAheadHorizontal = horizontalDirection * lookAheadDistance * speedFactor;
            Vector3 lookAheadVertical = Vector3.up * lookAheadHeight * speedFactor * 0.5f; // Subtle vertical offset
            
            Vector3 targetLookAhead = target.position + lookAheadHorizontal + lookAheadVertical;
            lookAheadTargetPosition = Vector3.SmoothDamp(lookAheadTargetPosition, targetLookAhead, ref lookAheadVelocity, 1f / lookAheadDamping);
            
            // Return the offset from base position
            return (lookAheadTargetPosition - target.position);
        }

        private Vector3 HandleCameraCollision(Vector3 desiredPosition, Vector3 targetPosition)
        {
            // Perform raycast from target to desired camera position
            Vector3 direction = (desiredPosition - targetPosition).normalized;
            float desiredDistance = Vector3.Distance(desiredPosition, targetPosition);
            
            RaycastHit hit;
            if (Physics.Raycast(targetPosition, direction, out hit, desiredDistance, 
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                // Collision detected, move camera closer
                float safeDistance = hit.distance - 0.5f; // Keep 0.5f buffer from walls
                safeDistance = Mathf.Max(safeDistance, 1.0f); // Never get closer than 1f
                
                Vector3 safePosition = targetPosition + direction * safeDistance;
                
                if (debugMode)
                {
                    Debug.DrawRay(targetPosition, direction * hit.distance, Color.red, 0.1f);
                }
                
                return safePosition;
            }
            
            if (debugMode)
            {
                Debug.DrawRay(targetPosition, direction * desiredDistance, Color.green, 0.1f);
            }
            
            return desiredPosition;
        }

        private Vector3 GetPlayerVelocity()
        {
            if (playerController != null)
            {
                return playerController.GetVelocity();
            }
            
            // Fallback: calculate velocity from position changes
            Vector3 velocity = (target.position - debugLastTargetPosition) / Time.deltaTime;
            return velocity;
        }

        private Vector3 GetPlayerMovementDirection()
        {
            if (playerController != null)
            {
                return playerController.GetMovementDirection();
            }
            
            // Fallback: use velocity direction
            Vector3 velocity = GetPlayerVelocity();
            return velocity.normalized;
        }

        private void UpdateChaseMode()
        {
            // Chase Mode: Reverse camera (6f distance, 2.5f height, 85° FOV as per PRD)
            Vector3 targetPosition = target.position;
            
            // Get player movement direction
            Vector3 playerVelocity = GetPlayerVelocity();
            Vector3 playerDirection = GetPlayerMovementDirection();
            
            // Calculate chase position (camera in front of player, facing backwards)
            Vector3 chaseOffset = CalculateChaseOffset(playerDirection, playerVelocity);
            Vector3 desiredChasePosition = targetPosition + chaseOffset;
            
            // Smooth camera movement
            chaseTargetPosition = desiredChasePosition;
            currentPosition = Vector3.SmoothDamp(currentPosition, chaseTargetPosition, ref chaseVelocity, 1f / followDamping);
            
            // Handle collision avoidance for chase mode
            currentPosition = HandleChaseCollision(currentPosition, targetPosition);
            
            if (debugMode)
            {
                debugCurrentDistance = Vector3.Distance(currentPosition, targetPosition);
            }
        }

        private Vector3 CalculateChaseOffset(Vector3 playerDirection, Vector3 playerVelocity)
        {
            // Chase mode positions camera in front of player, looking back
            Vector3 forwardDirection;
            
            if (playerVelocity.magnitude > 0.5f)
            {
                // Use movement direction when player is moving
                forwardDirection = new Vector3(playerDirection.x, 0, playerDirection.z).normalized;
            }
            else
            {
                // Use current forward direction when stationary, or default forward
                forwardDirection = target.forward;
                if (forwardDirection.magnitude < 0.1f)
                {
                    forwardDirection = Vector3.forward; // Default forward direction
                }
            }
            
            // Position camera in front of player (reverse of follow mode)
            Vector3 horizontalOffset = forwardDirection * chaseDistance; // 6f distance
            Vector3 verticalOffset = Vector3.up * chaseHeight; // 2.5f height
            
            // Add slight side offset for better view angle
            Vector3 rightDirection = Vector3.Cross(Vector3.up, forwardDirection).normalized;
            Vector3 sideOffset = rightDirection * (chaseDistance * 0.2f); // 20% side offset
            
            return horizontalOffset + verticalOffset + sideOffset;
        }

        private Vector3 HandleChaseCollision(Vector3 desiredPosition, Vector3 targetPosition)
        {
            // Chase mode collision handling - ensure camera doesn't clip through walls
            Vector3 direction = (desiredPosition - targetPosition).normalized;
            float desiredDistance = Vector3.Distance(desiredPosition, targetPosition);
            
            // Perform sphere cast for better collision detection
            RaycastHit hit;
            float sphereRadius = 0.3f;
            
            if (Physics.SphereCast(targetPosition, sphereRadius, direction, out hit, desiredDistance, 
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                // Collision detected, adjust camera position
                float safeDistance = hit.distance - sphereRadius - 0.2f; // Additional safety buffer
                safeDistance = Mathf.Max(safeDistance, 1.5f); // Never get closer than 1.5f in chase mode
                
                Vector3 safePosition = targetPosition + direction * safeDistance;
                
                // Try alternative positions if primary position is blocked
                if (safeDistance < chaseDistance * 0.7f)
                {
                    safePosition = FindAlternativeChasePosition(targetPosition, direction, safeDistance);
                }
                
                if (debugMode)
                {
                    Debug.DrawRay(targetPosition, direction * hit.distance, Color.red, 0.1f);
                }
                
                return safePosition;
            }
            
            if (debugMode)
            {
                Debug.DrawRay(targetPosition, direction * desiredDistance, Color.blue, 0.1f);
            }
            
            return desiredPosition;
        }

        private Vector3 FindAlternativeChasePosition(Vector3 targetPosition, Vector3 primaryDirection, float blockedDistance)
        {
            // Try alternative camera positions when primary position is blocked
            Vector3 bestPosition = targetPosition + primaryDirection * blockedDistance;
            float bestScore = 0f;
            
            // Test positions at different angles
            float[] testAngles = { -30f, 30f, -60f, 60f, -90f, 90f };
            
            foreach (float angle in testAngles)
            {
                Vector3 rotatedDirection = Quaternion.AngleAxis(angle, Vector3.up) * primaryDirection;
                Vector3 testPosition = targetPosition + rotatedDirection * chaseDistance + Vector3.up * chaseHeight;
                
                // Check if this position is clear
                if (!Physics.CheckSphere(testPosition, 0.3f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    float score = CalculateChasePositionScore(testPosition, targetPosition, primaryDirection);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPosition = testPosition;
                    }
                }
            }
            
            return bestPosition;
        }

        private float CalculateChasePositionScore(Vector3 position, Vector3 targetPosition, Vector3 preferredDirection)
        {
            // Score camera positions based on distance and angle to preferred direction
            float distance = Vector3.Distance(position, targetPosition);
            Vector3 actualDirection = (position - targetPosition).normalized;
            float angleDot = Vector3.Dot(actualDirection, preferredDirection);
            
            // Prefer positions close to ideal distance and direction
            float distanceScore = 1f - Mathf.Abs(distance - chaseDistance) / chaseDistance;
            float angleScore = (angleDot + 1f) * 0.5f; // Convert from [-1,1] to [0,1]
            
            return (distanceScore + angleScore) * 0.5f;
        }

        private void UpdateCinematicMode()
        {
            // Cinematic Mode: Scripted camera movements with smooth interpolation
            if (!cinematicActive) return;
            
            float elapsed = Time.time - cinematicStartTime;
            float progress = Mathf.Clamp01(elapsed / cinematicDuration);
            
            if (progress >= 1.0f)
            {
                // Cinematic completed
                CompleteCinematic();
                return;
            }
            
            // Apply easing curve for smooth cinematic movement
            float easedProgress = ApplyCinematicEasing(progress);
            
            // Interpolate position
            currentPosition = Vector3.Lerp(cinematicStartPosition, cinematicEndPosition, easedProgress);
            
            // Interpolate rotation
            Vector3 currentEulerAngles = Vector3.Lerp(cinematicStartRotation, cinematicEndRotation, easedProgress);
            transform.rotation = Quaternion.Euler(currentEulerAngles);
            
            // Interpolate FOV
            currentFOV = Mathf.Lerp(cinematicStartFOV, cinematicEndFOV, easedProgress);
            cameraComponent.fieldOfView = currentFOV;
        }

        private float ApplyCinematicEasing(float progress)
        {
            // Smooth ease-in-out for cinematic movements
            return progress * progress * (3f - 2f * progress);
        }

        private void CompleteCinematic()
        {
            cinematicActive = false;
            
            if (debugMode)
            {
                Debug.Log("Cinematic sequence completed");
            }
            
            // Optionally trigger callback or event when cinematic ends
            OnCinematicCompleted();
        }

        private void OnCinematicCompleted()
        {
            // Hook for cinematic completion events
            // This can be used by other systems to know when cinematics end
        }

        /// <summary>
        /// Start a cinematic camera movement from current position to target
        /// </summary>
        public void StartCinematic(Vector3 targetPosition, Vector3 targetRotation, float targetFOV, float duration)
        {
            cinematicStartPosition = transform.position;
            cinematicEndPosition = targetPosition;
            cinematicStartRotation = transform.eulerAngles;
            cinematicEndRotation = targetRotation;
            cinematicStartFOV = cameraComponent.fieldOfView;
            cinematicEndFOV = targetFOV;
            cinematicDuration = duration;
            cinematicStartTime = Time.time;
            cinematicActive = true;
            
            // Switch to cinematic mode
            SetCameraMode(CameraMode.Cinematic);
            
            if (debugMode)
            {
                Debug.Log($"Cinematic started - Duration: {duration}s, Target: {targetPosition}");
            }
        }

        /// <summary>
        /// Stop current cinematic and return to previous mode
        /// </summary>
        public void StopCinematic()
        {
            if (cinematicActive)
            {
                cinematicActive = false;
                
                // Return to follow mode by default
                SetCameraMode(CameraMode.Follow);
                
                if (debugMode)
                {
                    Debug.Log("Cinematic stopped manually");
                }
            }
        }

        private void UpdateCameraLookAt()
        {
            if (target == null) return;
            
            Vector3 lookTarget = target.position;
            currentLookAt = Vector3.Lerp(currentLookAt, lookTarget, followDamping * Time.deltaTime);
            
            transform.position = currentPosition;
            transform.LookAt(lookTarget);
        }

        private void UpdateCameraFOV()
        {
            float targetFOV = currentMode == CameraMode.Chase ? chaseFOV : 60f;
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, 2f * Time.deltaTime);
            cameraComponent.fieldOfView = currentFOV;
        }

        #endregion

        #region Camera Effects

        private void ApplyLaneCentering()
        {
            if (!enableLaneCentering || target == null) return;

            // Lane centering for corridor-style levels
            Vector3 targetPosition = target.position;
            
            // Calculate lane center (can be customized based on level geometry)
            laneCenter = CalculateLaneCenter(targetPosition);
            
            // Calculate offset from lane center
            Vector3 targetToCenter = laneCenter - targetPosition;
            targetToCenter.y = 0; // Only apply horizontal centering
            
            laneOffset = targetToCenter.magnitude;
            
            if (laneOffset > laneWidth * 0.5f)
            {
                // Player is outside lane, apply centering force
                Vector3 centeringForce = targetToCenter.normalized * centeringStrength * Time.deltaTime;
                centeringForce = Vector3.ClampMagnitude(centeringForce, laneOffset);
                
                // Apply centering to camera position
                currentPosition += centeringForce * 0.5f; // Gentle centering
            }
        }

        private Vector3 CalculateLaneCenter(Vector3 targetPosition)
        {
            // For basic implementation, use the Z-axis as the lane direction
            // In a more advanced system, this could use level geometry or splines
            return new Vector3(0, targetPosition.y, targetPosition.z);
        }

        private void ApplyCameraShake()
        {
            if (!enableCameraShake || shakeIntensity <= 0.001f) return;

            UpdateCameraShake();
            
            // Apply shake offset to camera position
            currentPosition += shakeOffset * shakeIntensity;
        }

        private void UpdateCameraShake()
        {
            // Check for shake triggers
            bool shouldShake = ShouldApplyShake();
            
            if (shouldShake && !isShaking)
            {
                StartCameraShake();
            }
            else if (!shouldShake && isShaking)
            {
                StopCameraShake();
            }

            if (isShaking)
            {
                shakeTime += Time.deltaTime;
                
                // Generate procedural shake using Perlin noise
                float shakeX = (Mathf.PerlinNoise(shakeTime * shakeFrequency, 0) - 0.5f) * 2f * shakeAmplitude.x;
                float shakeY = (Mathf.PerlinNoise(0, shakeTime * shakeFrequency) - 0.5f) * 2f * shakeAmplitude.y;
                float shakeZ = (Mathf.PerlinNoise(shakeTime * shakeFrequency * 0.5f, shakeTime * shakeFrequency * 0.5f) - 0.5f) * 2f * shakeAmplitude.z;
                
                shakeOffset = new Vector3(shakeX, shakeY, shakeZ);
                
                // Apply decay over time for natural shake falloff
                float shakeDecay = Mathf.Exp(-shakeTime * 2f);
                shakeOffset *= shakeDecay;
            }
            else
            {
                shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, 5f * Time.deltaTime);
            }
        }

        private bool ShouldApplyShake()
        {
            // Check various conditions that should trigger camera shake
            if (playerController == null) return false;

            // Shake when landing with high velocity
            if (playerController.BouncedRecently() && playerController.GetLastBounceIntensity() > 0.3f)
                return true;

            // Shake when sliding
            if (playerController.IsSliding())
                return true;

            // Shake based on movement speed
            float speed = playerController.GetHorizontalSpeed();
            if (speed > 8f) // Above run speed
                return true;

            return false;
        }

        private void StartCameraShake()
        {
            isShaking = true;
            shakeTime = 0f;
        }

        private void StopCameraShake()
        {
            isShaking = false;
        }

        private void ApplyComfortOptions()
        {
            // Apply comfort settings to reduce motion sickness
            ApplyComfortDamping();
            ApplyMotionBlurSettings();
        }

        private void ApplyComfortDamping()
        {
            // Reduce rapid camera movements based on comfort settings
            if (shakeIntensity < 0.5f) // If player prefers less shake
            {
                // Apply additional damping to reduce sudden movements
                Vector3 velocityDamping = Vector3.Lerp(Vector3.zero, currentPosition - transform.position, 0.8f);
                comfortOffset = velocityDamping * (0.5f - shakeIntensity);
                currentPosition += comfortOffset;
            }
        }

        private void ApplyMotionBlurSettings()
        {
            // Motion blur is typically handled by post-processing
            // This method provides a hook for motion blur configuration
            if (!motionBlurEnabled)
            {
                // Disable motion blur in post-processing if available
                // This would integrate with Unity's Post Processing Stack
            }
        }

        /// <summary>
        /// Manually trigger camera shake with custom parameters
        /// </summary>
        public void TriggerCameraShake(float intensity = 1f, float duration = 0.5f)
        {
            if (enableCameraShake && shakeIntensity > 0.001f)
            {
                StartCameraShake();
                
                // Apply custom intensity
                shakeAmplitude *= intensity;
                
                // Auto-stop after duration
                Invoke(nameof(StopCameraShake), duration);
                
                if (debugMode)
                {
                    Debug.Log($"Camera shake triggered - Intensity: {intensity}, Duration: {duration}s");
                }
            }
        }

        /// <summary>
        /// Set lane centering target for corridor levels
        /// </summary>
        public void SetLaneCenter(Vector3 centerPosition, float width = 4f)
        {
            laneCenter = centerPosition;
            laneWidth = width;
            enableLaneCentering = true;
        }

        /// <summary>
        /// Disable lane centering for open areas
        /// </summary>
        public void DisableLaneCentering()
        {
            enableLaneCentering = false;
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current camera mode
        /// </summary>
        public CameraMode GetCurrentMode()
        {
            return currentMode;
        }

        /// <summary>
        /// Check if camera is transitioning between modes
        /// </summary>
        public bool IsTransitioning()
        {
            return isTransitioning;
        }

        /// <summary>
        /// Set camera target
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            
            // Try to get PlayerController if target has one
            if (target != null)
            {
                playerController = target.GetComponent<PlayerController>();
            }
        }

        /// <summary>
        /// Get current camera distance from target
        /// </summary>
        public float GetCurrentDistance()
        {
            if (target == null) return 0f;
            return Vector3.Distance(transform.position, target.position);
        }

        /// <summary>
        /// Get camera configuration
        /// </summary>
        public CameraSettingsData GetConfiguration()
        {
            return cameraConfig;
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            if (target != null)
            {
                debugLastTargetPosition = target.position;
                debugCurrentDistance = GetCurrentDistance();
                
                if (playerController != null)
                {
                    debugLookAheadMagnitude = playerController.GetMovementDirection().magnitude;
                }
            }
        }

        private void DrawDebugGizmos()
        {
            // Draw current camera target
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.5f);
            
            // Draw camera position
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            
            // Draw look direction
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * 5f);
            
            // Draw distance circle
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, debugCurrentDistance);
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(370, 10, 300, 200));
                
                GUILayout.Label("=== CAMERA DEBUG ===");
                GUILayout.Label($"Mode: {currentMode}");
                if (isTransitioning)
                {
                    float progress = (Time.time - transitionStartTime) / transitionDuration;
                    GUILayout.Label($"Transitioning to {targetMode}: {progress * 100:F0}%");
                }
                
                GUILayout.Space(5);
                GUILayout.Label("=== POSITION ===");
                GUILayout.Label($"Camera Pos: {transform.position}");
                GUILayout.Label($"Target Pos: {debugLastTargetPosition}");
                GUILayout.Label($"Distance: {debugCurrentDistance:F2}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== SETTINGS ===");
                GUILayout.Label($"FOV: {currentFOV:F1}°");
                GUILayout.Label($"Follow Distance: {followDistance:F1}");
                GUILayout.Label($"Follow Height: {followHeight:F1}");
                GUILayout.Label($"Look-Ahead: {debugLookAheadMagnitude:F2}");
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }
}
