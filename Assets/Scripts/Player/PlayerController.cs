using UnityEngine;
using UnityEngine.InputSystem;
using WhiskerKing.Core;

namespace WhiskerKing.Player
{
    /// <summary>
    /// Main player controller for Whisker King
    /// Handles movement, jumping, sliding, and physics based on PRD specifications
    /// Uses Unity's CharacterController for robust collision detection and movement
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;

        [Header("Movement Settings (Override if not using GameConfiguration)")]
        [SerializeField] private float runSpeed = 8.0f;
        [SerializeField] private float slideSpeed = 10.0f;
        [SerializeField] private float jumpHeight = 3.0f;
        [SerializeField] private float jumpHeightHold = 4.5f;
        [SerializeField] private float doubleJumpHeight = 2.5f;

        [Header("Physics Settings")]
        [SerializeField] private float gravity = -25.0f;
        [SerializeField] private float pounceGravity = -45.0f;
        [SerializeField] private float airControl = 0.8f;
        [SerializeField] private float groundFriction = 0.85f;
        [SerializeField] private float airFriction = 0.95f;
        [SerializeField] private float bounceDamping = 0.6f;

        [Header("Input Settings")]
        [SerializeField] private float bufferTimeMS = 120f;
        [SerializeField] private float coyoteTimeMS = 120f;
        [SerializeField] private float slideDurationMS = 600f;
        [SerializeField] private float slideMinCancelTimeMS = 250f;

        // Components
        private CharacterController characterController;
        private InputBuffer inputBuffer;

        // Movement state
        private Vector3 velocity;
        private Vector3 moveDirection;
        private bool isGrounded;
        private bool wasGroundedLastFrame;
        private float lastGroundedTime;
        private bool canDoubleJump;
        private bool hasUsedDoubleJump;

        // Slide state
        private bool isSliding;
        private float slideStartTime;
        private bool canCancelSlide;

        // Input state
        private Vector2 movementInput;
        private bool jumpPressed;
        private bool jumpHeld;
        private bool slidePressed;

        // Configuration cache
        private PhysicsData physicsConfig;
        private InputData inputConfig;
        private CombatData combatConfig;

        // Debug info
        private Vector3 debugLastVelocity;
        private float debugCurrentSpeed;

        #region Unity Lifecycle

        private void Awake()
        {
            // Get required components
            characterController = GetComponent<CharacterController>();
            inputBuffer = GetComponent<InputBuffer>() ?? gameObject.AddComponent<InputBuffer>();

            // Validate CharacterController
            if (characterController == null)
            {
                Debug.LogError("PlayerController requires a CharacterController component!", this);
                enabled = false;
                return;
            }

            // Initialize input buffer
            inputBuffer.Initialize(bufferTimeMS / 1000f);
        }

        private void Start()
        {
            LoadConfiguration();
            InitializePlayer();
        }

        private void Update()
        {
            UpdateGroundedState();
            UpdateCoyoteTime();
            UpdateSlideState();
            HandleMovement();
            HandleJumping();
            HandleSliding();
            ApplyGravity();
            ApplyMovement();
            
            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode && Application.isPlaying)
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
                var playerMovement = GameConfiguration.Instance.GetPlayerMovement();
                
                if (playerMovement != null)
                {
                    physicsConfig = playerMovement.physics;
                    inputConfig = playerMovement.input;
                    combatConfig = playerMovement.combat;

                    ApplyConfiguration();
                    Debug.Log("PlayerController: Configuration loaded from GameConfig");
                }
                else
                {
                    Debug.LogWarning("PlayerController: GameConfiguration not available, using default values");
                    UseDefaultConfiguration();
                }
            }
            else
            {
                Debug.Log("PlayerController: Using Inspector values (GameConfiguration disabled)");
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            // Apply physics settings
            runSpeed = physicsConfig.runSpeed;
            slideSpeed = physicsConfig.slideSpeed;
            jumpHeight = physicsConfig.jumpHeight;
            jumpHeightHold = physicsConfig.jumpHeightHold;
            doubleJumpHeight = physicsConfig.doubleJumpHeight;
            gravity = physicsConfig.gravity;
            pounceGravity = physicsConfig.pounceGravity;
            airControl = physicsConfig.airControl;
            groundFriction = physicsConfig.groundFriction;
            airFriction = physicsConfig.airFriction;
            bounceDamping = physicsConfig.bounceDamping;

            // Apply input settings
            bufferTimeMS = inputConfig.bufferTimeMS;
            coyoteTimeMS = inputConfig.coyoteTimeMS;
            slideDurationMS = inputConfig.slideDurationMS;
            slideMinCancelTimeMS = inputConfig.slideMinCancelTimeMS;

            // Update input buffer
            if (inputBuffer != null)
            {
                inputBuffer.SetBufferTime(bufferTimeMS / 1000f);
            }
        }

        private void UseDefaultConfiguration()
        {
            // Create default configuration based on PRD values
            physicsConfig = new PhysicsData
            {
                runSpeed = 8.0f,
                slideSpeed = 10.0f,
                jumpHeight = 3.0f,
                jumpHeightHold = 4.5f,
                doubleJumpHeight = 2.5f,
                gravity = -25.0f,
                pounceGravity = -45.0f,
                airControl = 0.8f,
                groundFriction = 0.85f,
                airFriction = 0.95f,
                bounceDamping = 0.6f
            };

            inputConfig = new InputData
            {
                bufferTimeMS = 120,
                coyoteTimeMS = 120,
                slideDurationMS = 600,
                slideMinCancelTimeMS = 250
            };

            ApplyConfiguration();
        }

        #endregion

        #region Initialization

        private void InitializePlayer()
        {
            // Initialize movement state
            velocity = Vector3.zero;
            moveDirection = Vector3.zero;
            isGrounded = false;
            wasGroundedLastFrame = false;
            lastGroundedTime = 0f;
            canDoubleJump = false;
            hasUsedDoubleJump = false;

            // Initialize slide state
            isSliding = false;
            slideStartTime = 0f;
            canCancelSlide = false;

            // Configure CharacterController
            ConfigureCharacterController();

            Debug.Log($"PlayerController initialized - Speed: {runSpeed}m/s, Jump: {jumpHeight}m, Gravity: {gravity}m/s²");
        }

        private void ConfigureCharacterController()
        {
            // Set CharacterController properties for optimal platformer physics
            characterController.minMoveDistance = 0.001f; // Very small value for responsive movement
            characterController.skinWidth = 0.08f; // Slightly larger for stable ground detection
            characterController.stepOffset = 0.3f; // Allow stepping up small obstacles
            characterController.slopeLimit = 45f; // Allow walking up moderate slopes
            characterController.center = new Vector3(0, 1f, 0); // Center at character middle
            characterController.radius = 0.5f; // Reasonable collision radius
            characterController.height = 2f; // Standard character height
        }

        #endregion

        #region Ground Detection

        private void UpdateGroundedState()
        {
            wasGroundedLastFrame = isGrounded;
            
            // Use CharacterController's built-in ground detection
            isGrounded = characterController.isGrounded;

            // Additional ground check for more reliable detection
            if (!isGrounded)
            {
                isGrounded = PerformGroundCheck();
            }

            // Update grounded time tracking
            if (isGrounded)
            {
                lastGroundedTime = Time.time;
                
                // Reset double jump when landing
                if (!wasGroundedLastFrame)
                {
                    OnLanded();
                }
            }
        }

        private bool PerformGroundCheck()
        {
            // Perform additional raycast for more precise ground detection
            float checkDistance = 0.1f;
            Vector3 rayStart = transform.position;
            
            return Physics.Raycast(rayStart, Vector3.down, checkDistance + characterController.skinWidth,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
        }

        private void UpdateCoyoteTime()
        {
            // Coyote time allows jumping briefly after leaving ground
            float coyoteTimeSeconds = coyoteTimeMS / 1000f;
            
            if (!isGrounded && wasGroundedLastFrame)
            {
                // Just left ground, start coyote time
                lastGroundedTime = Time.time;
            }
        }

        private bool CanUseCoyoteTime()
        {
            float coyoteTimeSeconds = coyoteTimeMS / 1000f;
            return !isGrounded && Time.time - lastGroundedTime <= coyoteTimeSeconds;
        }

        private void OnLanded()
        {
            // Store landing velocity for bounce calculation
            float landingVelocity = velocity.y;
            
            // Reset double jump capability
            canDoubleJump = true;
            hasUsedDoubleJump = false;
            
            // Apply bounce mechanics with 0.6x velocity retention as per PRD
            ApplyBouncePhysics(landingVelocity);

            Debug.Log($"Player landed - Velocity: {landingVelocity:F2} → {velocity.y:F2} m/s");
        }

        private void ApplyBouncePhysics(float impactVelocity)
        {
            // Only apply bounce if landing with significant downward velocity
            float bounceThreshold = -3.0f; // Minimum velocity to trigger bounce
            
            if (impactVelocity < bounceThreshold)
            {
                // Calculate bounce velocity using bounceDamping (0.6x as per PRD)
                float bounceVelocity = -impactVelocity * bounceDamping;
                
                // Apply minimum bounce velocity to prevent tiny bounces
                if (bounceVelocity > 1.0f)
                {
                    velocity.y = bounceVelocity;
                    
                    // Trigger bounce event for effects
                    OnBounceOccurred(impactVelocity, bounceVelocity);
                    
                    if (debugMode)
                    {
                        Debug.Log($"Bounce applied - Impact: {impactVelocity:F2} → Bounce: {bounceVelocity:F2} m/s (damping: {bounceDamping:F1}x)");
                    }
                }
                else
                {
                    // Too small to bounce, just absorb impact
                    velocity.y = 0f;
                    
                    if (debugMode)
                    {
                        Debug.Log($"Impact absorbed - Velocity too small for bounce: {bounceVelocity:F2} m/s");
                    }
                }
            }
            else
            {
                // Soft landing, no bounce
                velocity.y = 0f;
            }
        }

        private void OnBounceOccurred(float impactVelocity, float bounceVelocity)
        {
            // Hook for bounce effects (particles, sound, screen shake, etc.)
            // This will be expanded later when integrating audio and visual effects
            
            // Calculate bounce intensity for effects scaling
            float bounceIntensity = Mathf.Abs(impactVelocity) / 20f; // Normalized to reasonable range
            bounceIntensity = Mathf.Clamp01(bounceIntensity);
            
            if (debugMode)
            {
                Debug.Log($"Bounce effects triggered - Intensity: {bounceIntensity:F2}");
            }
            
            // Store bounce info for other systems to query
            lastBounceIntensity = bounceIntensity;
            lastBounceTime = Time.time;
        }

        // Bounce tracking for external systems
        private float lastBounceIntensity = 0f;
        private float lastBounceTime = -1f;

        /// <summary>
        /// Get the intensity of the last bounce (0-1 scale)
        /// </summary>
        public float GetLastBounceIntensity()
        {
            return lastBounceIntensity;
        }

        /// <summary>
        /// Get time since last bounce
        /// </summary>
        public float GetTimeSinceLastBounce()
        {
            if (lastBounceTime < 0) return float.MaxValue;
            return Time.time - lastBounceTime;
        }

        /// <summary>
        /// Check if player bounced recently (within last 0.5 seconds)
        /// </summary>
        public bool BouncedRecently()
        {
            return GetTimeSinceLastBounce() < 0.5f;
        }

        #endregion

        #region Movement

        private void HandleMovement()
        {
            // Get horizontal movement input
            Vector3 inputDirection = CalculateMovementDirection();
            
            // Calculate target speed based on current state
            float targetSpeed = CalculateTargetSpeed();
            
            // Apply movement modifiers
            targetSpeed = ApplyMovementModifiers(targetSpeed);
            
            // Calculate target velocity
            Vector3 targetVelocity = inputDirection * targetSpeed;
            
            // Apply friction/acceleration with proper physics
            ApplyMovementPhysics(targetVelocity);
            
            // Update movement direction for other systems
            moveDirection = inputDirection;
        }

        private Vector3 CalculateMovementDirection()
        {
            // Convert 2D input to 3D world direction
            Vector3 worldDirection = Vector3.zero;
            
            // For 3D platformer, typically X = horizontal, Z = forward/backward
            if (movementInput.magnitude > 0.1f) // Dead zone to prevent drift
            {
                worldDirection = new Vector3(movementInput.x, 0, movementInput.y).normalized;
                
                // Transform input relative to camera if needed (for later camera integration)
                // For now, assume world-space movement
                worldDirection = TransformInputToWorldSpace(worldDirection);
            }
            
            return worldDirection;
        }

        private Vector3 TransformInputToWorldSpace(Vector3 inputDirection)
        {
            // For basic movement, use world-space directions
            // This will be enhanced later when integrating with camera system
            return inputDirection;
        }

        private float CalculateTargetSpeed()
        {
            // Base speed calculation based on current state
            float baseSpeed = runSpeed; // 8.0 m/s as per PRD
            
            if (isSliding)
            {
                baseSpeed = slideSpeed; // 10.0 m/s as per PRD
            }
            
            return baseSpeed;
        }

        private float ApplyMovementModifiers(float baseSpeed)
        {
            float modifiedSpeed = baseSpeed;
            
            // Apply air control modifier if in air
            if (!isGrounded)
            {
                modifiedSpeed *= airControl; // 0.8x as per PRD
            }
            
            // Apply movement input magnitude for analog stick support
            modifiedSpeed *= movementInput.magnitude;
            
            // Clamp to reasonable limits
            modifiedSpeed = Mathf.Clamp(modifiedSpeed, 0f, slideSpeed * 1.2f);
            
            return modifiedSpeed;
        }

        private void ApplyMovementPhysics(Vector3 targetVelocity)
        {
            // Get current horizontal velocity (preserve Y for jumping/gravity)
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
            Vector3 newHorizontalVelocity;
            
            // Calculate acceleration/deceleration
            float accelerationRate = CalculateAccelerationRate(targetVelocity, horizontalVelocity);
            
            if (targetVelocity.magnitude < 0.1f)
            {
                // Player stopped inputting - apply friction/deceleration
                newHorizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, 
                    accelerationRate * Time.deltaTime);
            }
            else
            {
                // Player is inputting movement - accelerate towards target
                newHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, 
                    accelerationRate * Time.deltaTime);
            }
            
            // Apply the new horizontal velocity while preserving Y velocity
            velocity = new Vector3(newHorizontalVelocity.x, velocity.y, newHorizontalVelocity.z);
        }

        private float CalculateAccelerationRate(Vector3 targetVelocity, Vector3 currentVelocity)
        {
            // Different acceleration rates for different scenarios
            float baseAccelerationRate;
            
            if (isGrounded)
            {
                if (targetVelocity.magnitude < 0.1f)
                {
                    // Deceleration on ground
                    baseAccelerationRate = runSpeed * (1f - groundFriction) * 10f; // Quick stop
                }
                else
                {
                    // Acceleration on ground
                    baseAccelerationRate = runSpeed * 8f; // Fast ground acceleration
                }
            }
            else
            {
                // Air movement
                if (targetVelocity.magnitude < 0.1f)
                {
                    // Air deceleration (minimal)
                    baseAccelerationRate = runSpeed * (1f - airFriction) * 2f; // Slow air deceleration
                }
                else
                {
                    // Air acceleration (limited)
                    baseAccelerationRate = runSpeed * airControl * 4f; // Limited air control
                }
            }
            
            return baseAccelerationRate;
        }

        /// <summary>
        /// Check if player is currently moving horizontally
        /// </summary>
        public bool IsMoving()
        {
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
            return horizontalVelocity.magnitude > 0.1f;
        }

        /// <summary>
        /// Get current movement input magnitude (0-1)
        /// </summary>
        public float GetMovementInputMagnitude()
        {
            return movementInput.magnitude;
        }

        /// <summary>
        /// Get normalized movement direction
        /// </summary>
        public Vector3 GetMovementDirection()
        {
            return moveDirection;
        }

        /// <summary>
        /// Reset player state (for checkpoints, level restart, etc.)
        /// </summary>
        public void ResetPlayerState()
        {
            // Reset movement state
            velocity = Vector3.zero;
            isGrounded = true;
            isSliding = false;
            canDoubleJump = true;
            hasUsedDoubleJump = false;
            
            // Reset timing
            lastGroundedTime = Time.time;
            slideStartTime = 0f;
            bounceStartTime = 0f;
            
            // Reset input state
            jumpPressed = false;
            jumpHeld = false;
            slidePressed = false;
            movementInput = Vector2.zero;
            
            // Clear input buffer
            if (inputBuffer != null)
            {
                inputBuffer.ClearAllInputs();
            }
            
            if (debugMode)
            {
                Debug.Log("PlayerController state reset");
            }
        }

        /// <summary>
        /// Add force to player (for external systems like spring crates)
        /// </summary>
        public void AddForce(Vector3 force)
        {
            velocity += force;
            
            // If upward force, consider as not grounded
            if (force.y > 0)
            {
                isGrounded = false;
                lastGroundedTime = Time.time - (coyoteTimeMS / 1000f) - 0.1f;
            }
            
            if (debugMode)
            {
                Debug.Log($"External force applied: {force}");
            }
        }

        #endregion

        #region Jumping

        private void HandleJumping()
        {
            // Check for jump input from buffer (with 120ms buffer time)
            if (inputBuffer.ConsumeInput(InputBuffer.InputType.Jump) || jumpPressed)
            {
                TryInitiateJump();
                jumpPressed = false; // Reset direct input
            }
            
            // Handle variable jump height based on hold duration
            HandleVariableJumpHeight();
        }

        private void TryInitiateJump()
        {
            if (CanInitiateJump())
            {
                PerformJump(jumpHeight); // 3.0m as per PRD
                
                if (debugMode)
                {
                    Debug.Log($"Jump initiated - Height: {jumpHeight}m, Velocity: {velocity.y:F2} m/s");
                }
            }
            else if (CanInitiateDoubleJump())
            {
                PerformDoubleJump();
                
                if (debugMode)
                {
                    Debug.Log($"Double jump initiated - Height: {doubleJumpHeight}m, Velocity: {velocity.y:F2} m/s");
                }
            }
        }

        private bool CanInitiateJump()
        {
            // Can perform regular jump if:
            // 1. Currently grounded, OR
            // 2. Within coyote time window
            return isGrounded || CanUseCoyoteTime();
        }

        private bool CanInitiateDoubleJump()
        {
            // Can perform double jump if:
            // 1. Not grounded (in air), AND
            // 2. Double jump is available, AND
            // 3. Haven't used double jump yet, AND
            // 4. Not within coyote time (to prevent regular jump instead)
            return !isGrounded && 
                   canDoubleJump && 
                   !hasUsedDoubleJump && 
                   !CanUseCoyoteTime();
        }

        private void PerformDoubleJump()
        {
            // Perform double jump with 2.5m height (75% of single jump as per PRD)
            PerformJump(doubleJumpHeight);
            
            // Mark double jump as used
            hasUsedDoubleJump = true;
            canDoubleJump = false; // Prevent triple jump
            
            // Add some visual/audio feedback opportunity
            OnDoubleJumpPerformed();
        }

        private void OnDoubleJumpPerformed()
        {
            // Hook for visual/audio effects when double jump is performed
            // This will be expanded later when integrating particle effects and audio
            
            if (debugMode)
            {
                Debug.Log("Double jump performed - Effects hook called");
            }
        }

        private void PerformJump(float jumpHeightTarget)
        {
            // Calculate required velocity to reach target jump height
            // Using physics formula: v = sqrt(2 * g * h)
            // Where g is positive gravity magnitude, h is height
            float jumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * jumpHeightTarget);
            
            // Set vertical velocity for jump
            velocity.y = jumpVelocity;
            
            // Update jump state
            isGrounded = false; // Force airborne state
            lastGroundedTime = Time.time - (coyoteTimeMS / 1000f) - 0.1f; // Invalidate coyote time
            
            // Reset double jump availability
            canDoubleJump = true;
            hasUsedDoubleJump = false;
        }

        private void HandleVariableJumpHeight()
        {
            // Variable jump height implementation
            // Short tap = 3.0m, hold = 4.5m as per PRD
            
            if (velocity.y > 0 && !isGrounded) // Currently jumping upward
            {
                if (!jumpHeld)
                {
                    // Jump button released - reduce upward velocity for variable height
                    ApplyVariableJumpHeight();
                }
            }
        }

        private void ApplyVariableJumpHeight()
        {
            // Apply jump cut-off for variable height
            // This creates the difference between tap (3.0m) and hold (4.5m)
            
            float cutoffMultiplier = CalculateJumpCutoffMultiplier();
            
            if (cutoffMultiplier < 1.0f)
            {
                velocity.y *= cutoffMultiplier;
                
                if (debugMode)
                {
                    Debug.Log($"Jump cut-off applied - Multiplier: {cutoffMultiplier:F2}, New velocity: {velocity.y:F2} m/s");
                }
            }
        }

        private float CalculateJumpCutoffMultiplier()
        {
            // Calculate how much to reduce jump velocity based on release timing
            // This creates the variable height effect
            
            // The cutoff should allow reaching approximately 3.0m when released early
            // and 4.5m when held (as per PRD: 3.0m tap, 4.5m hold)
            
            // Target: 3.0m / 4.5m = 0.667 ratio
            float targetRatio = jumpHeight / jumpHeightHold; // 3.0 / 4.5 = 0.667
            
            // Apply square root because velocity reduction affects height quadratically
            float cutoffMultiplier = Mathf.Sqrt(targetRatio);
            
            // Clamp to reasonable range
            return Mathf.Clamp(cutoffMultiplier, 0.6f, 1.0f);
        }

        /// <summary>
        /// Check if player can use coyote time for jumping
        /// </summary>
        private bool CanUseCoyoteTime()
        {
            float coyoteTimeSeconds = coyoteTimeMS / 1000f;
            return !isGrounded && Time.time - lastGroundedTime <= coyoteTimeSeconds;
        }

        /// <summary>
        /// Check if player is currently jumping (moving upward)
        /// </summary>
        public bool IsJumping()
        {
            return velocity.y > 0.1f && !isGrounded;
        }

        /// <summary>
        /// Check if player is falling (moving downward in air)
        /// </summary>
        public bool IsFalling()
        {
            return velocity.y < -0.1f && !isGrounded;
        }

        /// <summary>
        /// Get current vertical velocity
        /// </summary>
        public float GetVerticalVelocity()
        {
            return velocity.y;
        }

        /// <summary>
        /// Calculate predicted jump height based on current velocity
        /// </summary>
        public float GetPredictedJumpHeight()
        {
            if (velocity.y <= 0) return 0f;
            
            // Using physics formula: h = v² / (2g)
            return (velocity.y * velocity.y) / (2 * Mathf.Abs(gravity));
        }

        /// <summary>
        /// Check if player can perform a double jump
        /// </summary>
        public bool CanDoubleJump()
        {
            return CanInitiateDoubleJump();
        }

        /// <summary>
        /// Check if player has used their double jump
        /// </summary>
        public bool HasUsedDoubleJump()
        {
            return hasUsedDoubleJump;
        }

        /// <summary>
        /// Get the height difference between regular and double jump
        /// </summary>
        public float GetDoubleJumpRatio()
        {
            return doubleJumpHeight / jumpHeight;
        }

        /// <summary>
        /// Force reset double jump availability (for power-ups, special platforms, etc.)
        /// </summary>
        public void ResetDoubleJump()
        {
            if (!isGrounded) // Only allow reset in air
            {
                canDoubleJump = true;
                hasUsedDoubleJump = false;
                
                if (debugMode)
                {
                    Debug.Log("Double jump reset (external trigger)");
                }
            }
        }

        #endregion

        #region Sliding

        private void HandleSliding()
        {
            // Check for slide input from buffer (with 120ms buffer time)
            if (inputBuffer.ConsumeInput(InputBuffer.InputType.Slide) || slidePressed)
            {
                TryInitiateSlide();
                slidePressed = false; // Reset direct input
            }
            
            // Handle slide cancellation and duration
            UpdateSlideState();
        }

        private void TryInitiateSlide()
        {
            // Can only slide if grounded and not already sliding
            if (CanInitiateSlide())
            {
                StartSlide();
                
                if (debugMode)
                {
                    Debug.Log($"Slide initiated - Duration: {slideDurationMS}ms, Speed: {slideSpeed} m/s");
                }
            }
        }

        private bool CanInitiateSlide()
        {
            // Can slide if:
            // 1. Currently grounded, AND
            // 2. Not already sliding, AND
            // 3. Has horizontal movement input (prevents accidental slides)
            return isGrounded && 
                   !isSliding && 
                   movementInput.magnitude > 0.1f;
        }

        private void StartSlide()
        {
            // Begin slide state
            isSliding = true;
            slideStartTime = Time.time;
            canCancelSlide = false; // Can't cancel immediately
            
            // Adjust CharacterController for sliding
            AdjustCharacterControllerForSlide(true);
            
            // Apply initial slide momentum if needed
            ApplySlideImpulse();
        }

        private void UpdateSlideState()
        {
            if (!isSliding) return;
            
            float slideDuration = Time.time - slideStartTime;
            float slideDurationSeconds = slideDurationMS / 1000f;
            float minCancelTimeSeconds = slideMinCancelTimeMS / 1000f;
            
            // Enable cancellation after minimum time
            if (!canCancelSlide && slideDuration >= minCancelTimeSeconds)
            {
                canCancelSlide = true;
                
                if (debugMode)
                {
                    Debug.Log($"Slide can now be canceled (after {minCancelTimeSeconds * 1000:F0}ms)");
                }
            }
            
            // Check for slide cancellation
            if (ShouldCancelSlide(slideDuration, slideDurationSeconds))
            {
                EndSlide();
                return;
            }
            
            // Auto-end slide after full duration
            if (slideDuration >= slideDurationSeconds)
            {
                EndSlide();
                
                if (debugMode)
                {
                    Debug.Log($"Slide auto-ended after {slideDurationSeconds * 1000:F0}ms");
                }
            }
        }

        private bool ShouldCancelSlide(float currentDuration, float maxDuration)
        {
            // Cancel slide if:
            // 1. Slide is cancelable (after min time), AND
            // 2. One of the following conditions:
            //    a. No movement input (player let go), OR
            //    b. Jump input received, OR
            //    c. Player is no longer grounded (fell off edge), OR
            //    d. Player changed direction significantly
            
            if (!canCancelSlide) return false;
            
            // Check for jump cancellation
            if (inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump) || jumpPressed)
            {
                return true;
            }
            
            // Check for no input cancellation
            if (movementInput.magnitude < 0.1f)
            {
                return true;
            }
            
            // Check for airborne cancellation
            if (!isGrounded)
            {
                return true;
            }
            
            // Check for direction change cancellation (optional - more advanced)
            if (HasSignificantDirectionChange())
            {
                return true;
            }
            
            return false;
        }

        private bool HasSignificantDirectionChange()
        {
            // Check if player has significantly changed direction during slide
            // This is more advanced and may not be needed for basic sliding
            
            Vector3 currentDirection = CalculateMovementDirection();
            Vector3 slideDirection = GetSlideDirection();
            
            if (currentDirection.magnitude < 0.1f || slideDirection.magnitude < 0.1f)
                return false;
            
            float directionDot = Vector3.Dot(currentDirection.normalized, slideDirection.normalized);
            return directionDot < 0.5f; // Significant direction change (>60 degrees)
        }

        private Vector3 GetSlideDirection()
        {
            // For now, use current movement direction
            // In a more advanced system, this could be the initial slide direction
            return moveDirection;
        }

        private void EndSlide()
        {
            // End slide state
            isSliding = false;
            canCancelSlide = false;
            
            // Restore CharacterController
            AdjustCharacterControllerForSlide(false);
            
            // Apply slide ending effects if needed
            OnSlideEnded();
        }

        private void AdjustCharacterControllerForSlide(bool startSliding)
        {
            if (startSliding)
            {
                // Make character controller shorter for sliding
                // Store original values first time
                if (!hasStoredOriginalControllerValues)
                {
                    originalControllerHeight = characterController.height;
                    originalControllerCenter = characterController.center;
                    hasStoredOriginalControllerValues = true;
                }
                
                // Adjust for sliding (shorter height, lower center)
                characterController.height = originalControllerHeight * 0.5f; // Half height
                characterController.center = new Vector3(
                    originalControllerCenter.x,
                    originalControllerCenter.y - (originalControllerHeight * 0.25f), // Lower center
                    originalControllerCenter.z
                );
            }
            else
            {
                // Restore original dimensions
                if (hasStoredOriginalControllerValues)
                {
                    characterController.height = originalControllerHeight;
                    characterController.center = originalControllerCenter;
                }
            }
        }

        private void ApplySlideImpulse()
        {
            // Optional: Apply some extra momentum at slide start
            // This can make sliding feel more dynamic
            
            Vector3 slideDirection = CalculateMovementDirection();
            if (slideDirection.magnitude > 0.1f)
            {
                // Small impulse in slide direction
                Vector3 impulse = slideDirection * (slideSpeed * 0.1f);
                velocity += impulse;
                
                if (debugMode)
                {
                    Debug.Log($"Slide impulse applied: {impulse.magnitude:F2} m/s");
                }
            }
        }

        private void OnSlideEnded()
        {
            // Hook for effects when slide ends
            // This will be expanded later for particle effects and audio
            
            if (debugMode)
            {
                float slideDuration = Time.time - slideStartTime;
                Debug.Log($"Slide ended - Duration: {slideDuration * 1000:F0}ms");
            }
        }

        // New fields for slide functionality
        private bool hasStoredOriginalControllerValues = false;
        private float originalControllerHeight;
        private Vector3 originalControllerCenter;

        /// <summary>
        /// Check if player is currently sliding
        /// </summary>
        public bool IsSliding()
        {
            return isSliding;
        }

        /// <summary>
        /// Get current slide duration in seconds
        /// </summary>
        public float GetSlideDuration()
        {
            if (!isSliding) return 0f;
            return Time.time - slideStartTime;
        }

        /// <summary>
        /// Get remaining slide time in seconds
        /// </summary>
        public float GetRemainingSlideTime()
        {
            if (!isSliding) return 0f;
            float maxDuration = slideDurationMS / 1000f;
            float currentDuration = GetSlideDuration();
            return Mathf.Max(0f, maxDuration - currentDuration);
        }

        /// <summary>
        /// Check if slide can be canceled
        /// </summary>
        public bool CanCancelSlide()
        {
            return isSliding && canCancelSlide;
        }

        /// <summary>
        /// Force cancel slide (for external systems)
        /// </summary>
        public void ForceEndSlide()
        {
            if (isSliding)
            {
                EndSlide();
                
                if (debugMode)
                {
                    Debug.Log("Slide force-canceled (external trigger)");
                }
            }
        }

        #endregion

        #region Physics

        private void ApplyGravity()
        {
            if (!isGrounded)
            {
                // Apply gravity
                float currentGravity = gravity; // Will add pounce gravity in future tasks
                velocity.y += currentGravity * Time.deltaTime;
                
                // Apply terminal velocity limit
                velocity.y = Mathf.Max(velocity.y, -50f);
            }
            else if (velocity.y < 0)
            {
                // Ensure we stay grounded
                velocity.y = -2f; // Small downward force to stay grounded
            }
        }

        private void ApplyMovement()
        {
            // Move the character using CharacterController
            Vector3 deltaPosition = velocity * Time.deltaTime;
            characterController.Move(deltaPosition);

            // Store debug information
            debugLastVelocity = velocity;
            debugCurrentSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;
        }

        #endregion

        #region Input Handling

        public void OnMove(InputAction.CallbackContext context)
        {
            movementInput = context.ReadValue<Vector2>();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                jumpPressed = true;
                jumpHeld = true;
                inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            }
            else if (context.canceled)
            {
                jumpHeld = false;
            }
        }

        public void OnSlide(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                slidePressed = true;
                inputBuffer.BufferInput(InputBuffer.InputType.Slide);
            }
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                inputBuffer.BufferInput(InputBuffer.InputType.Attack);
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current player velocity
        /// </summary>
        public Vector3 GetVelocity() => velocity;

        /// <summary>
        /// Get current horizontal speed
        /// </summary>
        public float GetHorizontalSpeed() => new Vector3(velocity.x, 0, velocity.z).magnitude;

        /// <summary>
        /// Check if player is currently grounded
        /// </summary>
        public bool IsGrounded() => isGrounded;

        /// <summary>
        /// Check if player is currently sliding
        /// </summary>
        public bool IsSliding() => isSliding;

        /// <summary>
        /// Force set player position (for teleporting, respawning, etc.)
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
            velocity = Vector3.zero;
        }

        /// <summary>
        /// Apply external force to player (for bounce pads, explosions, etc.)
        /// </summary>
        public void AddForce(Vector3 force)
        {
            velocity += force;
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            debugCurrentSpeed = GetHorizontalSpeed();
        }

        private void DrawDebugGizmos()
        {
            // Draw velocity vector
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, velocity);

            // Draw ground check ray
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 rayStart = transform.position;
            Gizmos.DrawRay(rayStart, Vector3.down * (0.1f + characterController.skinWidth));

            // Draw movement direction
            if (moveDirection.magnitude > 0.1f)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, moveDirection);
            }
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(10, 10, 350, 250));
                
                // Movement status
                GUILayout.Label("=== MOVEMENT DEBUG ===");
                GUILayout.Label($"Grounded: {isGrounded}");
                GUILayout.Label($"Is Moving: {IsMoving()}");
                GUILayout.Label($"Is Sliding: {isSliding}");
                
                // Speed information
                GUILayout.Space(5);
                GUILayout.Label("=== SPEED INFO ===");
                GUILayout.Label($"Current Speed: {debugCurrentSpeed:F2} m/s");
                GUILayout.Label($"Target Run Speed: {runSpeed:F1} m/s");
                GUILayout.Label($"Target Slide Speed: {slideSpeed:F1} m/s");
                
                // Velocity breakdown
                GUILayout.Space(5);
                GUILayout.Label("=== VELOCITY ===");
                GUILayout.Label($"Total: {debugLastVelocity}");
                GUILayout.Label($"Horizontal: {new Vector3(debugLastVelocity.x, 0, debugLastVelocity.z).magnitude:F2} m/s");
                GUILayout.Label($"Vertical: {debugLastVelocity.y:F2} m/s");
                
                // Input information
                GUILayout.Space(5);
                GUILayout.Label("=== INPUT ===");
                GUILayout.Label($"Movement Input: {movementInput}");
                GUILayout.Label($"Input Magnitude: {movementInput.magnitude:F2}");
                GUILayout.Label($"Movement Direction: {moveDirection}");
                
                // Jump information
                GUILayout.Space(5);
                GUILayout.Label("=== JUMPING ===");
                GUILayout.Label($"Is Jumping: {IsJumping()}");
                GUILayout.Label($"Is Falling: {IsFalling()}");
                GUILayout.Label($"Jump Height: {jumpHeight:F1}m / {jumpHeightHold:F1}m");
                GUILayout.Label($"Double Jump Height: {doubleJumpHeight:F1}m ({GetDoubleJumpRatio():P0})");
                GUILayout.Label($"Predicted Height: {GetPredictedJumpHeight():F1}m");
                GUILayout.Label($"Coyote Time Available: {CanUseCoyoteTime()}");
                
                GUILayout.Space(3);
                GUILayout.Label("=== DOUBLE JUMP ===");
                GUILayout.Label($"Can Double Jump: {CanDoubleJump()}");
                GUILayout.Label($"Has Used Double Jump: {HasUsedDoubleJump()}");
                GUILayout.Label($"Double Jump Available: {canDoubleJump}");
                
                // Sliding information
                GUILayout.Space(5);
                GUILayout.Label("=== SLIDING ===");
                GUILayout.Label($"Is Sliding: {IsSliding()}");
                if (IsSliding())
                {
                    GUILayout.Label($"Slide Duration: {GetSlideDuration() * 1000:F0}ms / {slideDurationMS}ms");
                    GUILayout.Label($"Remaining Time: {GetRemainingSlideTime() * 1000:F0}ms");
                    GUILayout.Label($"Can Cancel: {CanCancelSlide()}");
                }
                
                // Show input button states
                GUILayout.Space(3);
                GUILayout.Label("=== INPUT STATE ===");
                GUILayout.Label($"Jump Held: {jumpHeld}");
                GUILayout.Label($"Jump Pressed: {jumpPressed}");
                GUILayout.Label($"Slide Pressed: {slidePressed}");
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }
}
