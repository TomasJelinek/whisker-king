using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using WhiskerKing.Core;
using WhiskerKing.Content;

namespace WhiskerKing.Animation
{
    /// <summary>
    /// Advanced Animation System for Whisker King
    /// Handles character animations with optimization, blending, and PRD-compliant frame limits
    /// Manages 15-20 animation clips with ≤60 frames each as per PRD specifications
    /// </summary>
    public class AnimationSystem : MonoBehaviour
    {
        [System.Serializable]
        public class AnimationConfiguration
        {
            [Header("Performance Settings")]
            public int maxFramesPerAnimation = 60;  // PRD requirement
            public int maxSimultaneousAnimations = 15; // PRD: 15-20 clips
            public bool enableAnimationCulling = true;
            public float cullingDistance = 50f;

            [Header("Blending Settings")]
            public float defaultBlendTime = 0.25f;
            public float combatBlendTime = 0.1f;   // Fast blending for combat
            public float movementBlendTime = 0.15f; // Smooth movement transitions
            public bool enableAdvancedBlending = true;

            [Header("Optimization")]
            public bool enableLODAnimations = true;
            public bool enableCompressionOptimization = true;
            public AnimationCompressionLevel compressionLevel = AnimationCompressionLevel.Medium;
            public bool enableAnimationEvents = true;
        }

        public enum AnimationCompressionLevel
        {
            None,
            Low,
            Medium, 
            High
        }

        [System.Serializable]
        public class AnimationClipData
        {
            [Header("Clip Information")]
            public string clipName;
            public AnimationClip clip;
            public AnimationClipCategory category;
            public int frameCount;
            public float duration;
            public bool isLooping;

            [Header("Performance")]
            public AnimationCompressionLevel compression = AnimationCompressionLevel.Medium;
            public bool enabledInLOD0 = true; // High detail
            public bool enabledInLOD1 = true; // Medium detail
            public bool enabledInLOD2 = false; // Low detail (simplified)

            [Header("Blending")]
            public float customBlendTime = -1f; // -1 uses default
            public bool canInterrupt = true;
            public int priority = 0; // Higher priority can interrupt lower
        }

        public enum AnimationClipCategory
        {
            Movement,
            Combat,
            Interaction,
            Idle,
            Special,
            UI,
            Cinematic
        }

        [System.Serializable]
        public class AnimationLayer
        {
            public string layerName;
            public float weight = 1f;
            public bool additive = false;
            public List<AnimationClipData> layerClips = new List<AnimationClipData>();
            public AvatarMask mask;
        }

        [System.Serializable]
        public class AnimationEvent
        {
            public string eventName;
            public float normalizedTime; // 0-1
            public string stringParameter;
            public float floatParameter;
            public int intParameter;
            public System.Action<AnimationEvent> callback;
        }

        [Header("Animation Configuration")]
        [SerializeField] private AnimationConfiguration config = new AnimationConfiguration();
        [SerializeField] private List<AnimationClipData> animationClips = new List<AnimationClipData>();
        [SerializeField] private List<AnimationLayer> animationLayers = new List<AnimationLayer>();
        [SerializeField] private bool debugMode = true;

        // Core components
        private Animator animator;
        private RuntimeAnimatorController runtimeController;
        private Dictionary<string, AnimationClipData> clipLookup = new Dictionary<string, AnimationClipData>();

        // Animation state
        private string currentAnimation = "";
        private float currentAnimationTime = 0f;
        private Queue<AnimationRequest> animationQueue = new Queue<AnimationRequest>();
        private Dictionary<string, float> layerWeights = new Dictionary<string, float>();

        // Performance tracking
        private int activeAnimationCount = 0;
        private float lastOptimizationUpdate = 0f;
        private bool animationCulled = false;
        private Camera mainCamera;

        // Animation events
        private Dictionary<string, List<AnimationEvent>> animationEvents = new Dictionary<string, List<AnimationEvent>>();

        // Component references
        private AssetManager assetManager;

        private struct AnimationRequest
        {
            public string clipName;
            public float blendTime;
            public bool interrupt;
            public int priority;
            public float requestTime;
        }

        // Events
        public System.Action<string> OnAnimationStarted;
        public System.Action<string> OnAnimationCompleted;
        public System.Action<AnimationEvent> OnAnimationEvent;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeAnimationSystem();
        }

        private void Start()
        {
            LoadAnimationAssets();
            SetupAnimationController();
            InitializeAnimationEvents();
        }

        private void Update()
        {
            ProcessAnimationQueue();
            UpdateAnimationOptimization();
            UpdateAnimationEvents();
            
            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        #endregion

        #region Initialization

        private void InitializeAnimationSystem()
        {
            // Get required components
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<Animator>();
            }

            // Get references
            assetManager = AssetManager.Instance;
            mainCamera = Camera.main ?? FindObjectOfType<Camera>();

            // Initialize collections
            clipLookup.Clear();
            animationEvents.Clear();

            Debug.Log("AnimationSystem initialized");
        }

        private void LoadAnimationAssets()
        {
            // Load animation clips from AssetManager
            var animationKeys = new string[]
            {
                // Movement animations (PRD compliant)
                "Animations/Capo/Idle",        // ≤60 frames
                "Animations/Capo/Run",         // ≤60 frames
                "Animations/Capo/Slide",       // ≤60 frames (0.6s duration)
                "Animations/Capo/Jump",        // ≤60 frames
                "Animations/Capo/Fall",        // ≤60 frames
                "Animations/Capo/Land",        // ≤60 frames
                
                // Combat animations (PRD timing compliant)
                "Animations/Capo/TailWhipWindup",  // 6 frames (0.1s)
                "Animations/Capo/TailWhipActive",  // 11 frames (0.18s)
                "Animations/Capo/TailWhipRecovery", // 7 frames (0.12s)
                "Animations/Capo/HitReaction",     // ≤60 frames
                "Animations/Capo/Stunned",         // ≤60 frames (1.5s loop)

                // Special animations
                "Animations/Capo/Celebration",     // ≤60 frames
                "Animations/Capo/Death",           // ≤60 frames
                "Animations/Capo/Respawn",         // ≤60 frames
                "Animations/Capo/Collect",         // ≤60 frames
                "Animations/Capo/Pause",           // ≤60 frames

                // Interaction animations
                "Animations/Capo/Bounce",          // ≤60 frames
                "Animations/Capo/Launch"           // ≤60 frames (Spring Crate)
            };

            StartCoroutine(LoadAnimationClipsCoroutine(animationKeys));
        }

        private IEnumerator LoadAnimationClipsCoroutine(string[] animationKeys)
        {
            int loadedCount = 0;
            
            foreach (string key in animationKeys)
            {
                bool clipLoaded = false;
                
                assetManager.LoadAssetAsync<AnimationClip>(key, AssetManager.LoadPriority.High,
                    (clip) => {
                        RegisterAnimationClip(clip, key);
                        clipLoaded = true;
                        loadedCount++;
                    },
                    (error) => {
                        Debug.LogWarning($"Could not load animation clip {key}: {error}");
                        CreateFallbackAnimation(key);
                        clipLoaded = true;
                        loadedCount++;
                    });
                
                // Wait for this clip to load before continuing
                yield return new WaitUntil(() => clipLoaded);
                
                // Yield periodically to avoid frame hitches
                if (loadedCount % 3 == 0)
                {
                    yield return null;
                }
            }
            
            Debug.Log($"Loaded {loadedCount} animation clips");
            ValidateAnimationCompliance();
        }

        private void RegisterAnimationClip(AnimationClip clip, string key)
        {
            if (clip == null) return;

            // Validate PRD compliance
            int frameCount = Mathf.RoundToInt(clip.length * clip.frameRate);
            if (frameCount > config.maxFramesPerAnimation)
            {
                Debug.LogWarning($"Animation {key} exceeds PRD frame limit: {frameCount} > {config.maxFramesPerAnimation}");
            }

            var clipData = new AnimationClipData
            {
                clipName = ExtractClipName(key),
                clip = clip,
                category = DetermineClipCategory(key),
                frameCount = frameCount,
                duration = clip.length,
                isLooping = clip.isLooping,
                compression = config.compressionLevel
            };

            // Apply PRD-specific settings
            ApplyPRDSettings(clipData, key);

            animationClips.Add(clipData);
            clipLookup[clipData.clipName] = clipData;

            if (debugMode)
            {
                Debug.Log($"Registered animation: {clipData.clipName} ({frameCount} frames, {clip.length:F2}s)");
            }
        }

        private void ApplyPRDSettings(AnimationClipData clipData, string key)
        {
            // Apply PRD-compliant blend times based on animation type
            if (key.Contains("TailWhip"))
            {
                clipData.customBlendTime = 0.05f; // Fast combat blending
                clipData.priority = 100; // High priority for combat
            }
            else if (key.Contains("Run") || key.Contains("Slide") || key.Contains("Jump"))
            {
                clipData.customBlendTime = config.movementBlendTime;
                clipData.priority = 50; // Medium priority for movement
            }
            else if (key.Contains("Idle"))
            {
                clipData.customBlendTime = 0.5f; // Slow blend to idle
                clipData.priority = 1; // Low priority, easily interrupted
            }

            // LOD settings based on animation importance
            if (key.Contains("TailWhip") || key.Contains("Jump") || key.Contains("Run"))
            {
                // Keep important animations at all LOD levels
                clipData.enabledInLOD0 = true;
                clipData.enabledInLOD1 = true;
                clipData.enabledInLOD2 = true;
            }
            else if (key.Contains("Celebration") || key.Contains("Death"))
            {
                // Disable complex animations at low LOD
                clipData.enabledInLOD0 = true;
                clipData.enabledInLOD1 = true;
                clipData.enabledInLOD2 = false;
            }
        }

        private void CreateFallbackAnimation(string key)
        {
            // Create a simple fallback animation for missing clips
            var fallbackClip = new AnimationClip();
            fallbackClip.name = ExtractClipName(key) + "_Fallback";
            fallbackClip.frameRate = 30f;
            
            // Create a simple keyframe animation (e.g., slight rotation)
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0f);
            curve.AddKey(1f, 5f); // Small rotation
            curve.AddKey(2f, 0f);
            
            fallbackClip.SetCurve("", typeof(Transform), "localEulerAngles.y", curve);
            fallbackClip.legacy = false;

            RegisterAnimationClip(fallbackClip, key);
        }

        private void ValidateAnimationCompliance()
        {
            int totalClips = animationClips.Count;
            int compliantClips = animationClips.Count(clip => clip.frameCount <= config.maxFramesPerAnimation);
            
            Debug.Log($"Animation PRD Compliance: {compliantClips}/{totalClips} clips compliant");
            
            if (totalClips > 20)
            {
                Debug.LogWarning($"Total animation count ({totalClips}) exceeds PRD recommendation of 15-20 clips");
            }

            foreach (var clip in animationClips)
            {
                if (clip.frameCount > config.maxFramesPerAnimation)
                {
                    Debug.LogWarning($"Animation '{clip.clipName}' exceeds PRD frame limit: {clip.frameCount} frames");
                }
            }
        }

        #endregion

        #region Animation Controller Setup

        private void SetupAnimationController()
        {
            if (animator == null) return;

            // Create runtime animator controller
            CreateRuntimeAnimatorController();
            
            // Setup animation layers
            SetupAnimationLayers();
            
            // Initialize layer weights
            InitializeLayerWeights();
        }

        private void CreateRuntimeAnimatorController()
        {
            // In a real project, this would load a pre-made Animator Controller
            // For this implementation, we'll create a basic one programmatically
            
            var controller = new UnityEditor.Animations.AnimatorController();
            controller.name = "CapoAnimatorController";
            
            // Create states for each animation
            foreach (var clipData in animationClips)
            {
                if (clipData.clip != null)
                {
                    var state = controller.layers[0].stateMachine.AddState(clipData.clipName);
                    state.motion = clipData.clip;
                }
            }
            
            animator.runtimeAnimatorController = controller;
            runtimeController = controller;
        }

        private void SetupAnimationLayers()
        {
            // Create default layers if none exist
            if (animationLayers.Count == 0)
            {
                CreateDefaultLayers();
            }

            // Setup layer weights in animator
            for (int i = 0; i < animationLayers.Count && i < animator.layerCount; i++)
            {
                var layer = animationLayers[i];
                animator.SetLayerWeight(i, layer.weight);
                layerWeights[layer.layerName] = layer.weight;
            }
        }

        private void CreateDefaultLayers()
        {
            // Base Layer - Core movement and idle animations
            var baseLayer = new AnimationLayer
            {
                layerName = "Base Layer",
                weight = 1f,
                additive = false
            };

            // Combat Layer - Combat animations with partial body override
            var combatLayer = new AnimationLayer
            {
                layerName = "Combat Layer",
                weight = 0f,
                additive = true
            };

            // Special Layer - Celebration, death, and other special animations
            var specialLayer = new AnimationLayer
            {
                layerName = "Special Layer", 
                weight = 0f,
                additive = false
            };

            animationLayers.AddRange(new[] { baseLayer, combatLayer, specialLayer });
        }

        private void InitializeLayerWeights()
        {
            layerWeights.Clear();
            
            foreach (var layer in animationLayers)
            {
                layerWeights[layer.layerName] = layer.weight;
            }
        }

        #endregion

        #region Animation Playback

        public void PlayAnimation(string clipName, float blendTime = -1f, bool interrupt = false)
        {
            if (string.IsNullOrEmpty(clipName)) return;

            var request = new AnimationRequest
            {
                clipName = clipName,
                blendTime = blendTime,
                interrupt = interrupt,
                priority = GetClipPriority(clipName),
                requestTime = Time.time
            };

            animationQueue.Enqueue(request);
        }

        public void PlayAnimationImmediate(string clipName, float blendTime = -1f)
        {
            if (!clipLookup.ContainsKey(clipName))
            {
                Debug.LogWarning($"Animation clip '{clipName}' not found");
                return;
            }

            var clipData = clipLookup[clipName];
            
            // Check if animation should be culled
            if (animationCulled && !IsAnimationEssential(clipName))
            {
                return;
            }

            // Use custom blend time if specified, otherwise use clip default or config default
            float actualBlendTime = blendTime >= 0 ? blendTime :
                                  clipData.customBlendTime >= 0 ? clipData.customBlendTime :
                                  config.defaultBlendTime;

            // Play animation with crossfade
            if (animator != null && animator.enabled)
            {
                animator.CrossFade(clipName, actualBlendTime);
                currentAnimation = clipName;
                currentAnimationTime = 0f;
                
                OnAnimationStarted?.Invoke(clipName);
                
                if (debugMode)
                {
                    Debug.Log($"Playing animation: {clipName} (blend: {actualBlendTime:F2}s)");
                }
            }
        }

        private void ProcessAnimationQueue()
        {
            if (animationQueue.Count == 0) return;

            var request = animationQueue.Dequeue();
            
            // Check if current animation can be interrupted
            if (!string.IsNullOrEmpty(currentAnimation) && !request.interrupt)
            {
                var currentClipData = clipLookup.GetValueOrDefault(currentAnimation);
                if (currentClipData != null && !currentClipData.canInterrupt)
                {
                    // Check priority
                    if (request.priority <= currentClipData.priority)
                    {
                        return; // Don't interrupt higher priority animation
                    }
                }
            }

            PlayAnimationImmediate(request.clipName, request.blendTime);
        }

        public void StopAnimation(string clipName)
        {
            if (animator != null && currentAnimation == clipName)
            {
                // Blend to idle or another appropriate animation
                PlayAnimationImmediate("Idle", 0.25f);
            }
        }

        public void SetAnimationSpeed(float speed)
        {
            if (animator != null)
            {
                animator.speed = speed;
            }
        }

        #endregion

        #region Animation Events

        private void InitializeAnimationEvents()
        {
            // Setup animation events for specific clips
            SetupCombatAnimationEvents();
            SetupMovementAnimationEvents();
            SetupSpecialAnimationEvents();
        }

        private void SetupCombatAnimationEvents()
        {
            // TailWhip events (PRD timing)
            AddAnimationEvent("TailWhipWindup", new AnimationEvent
            {
                eventName = "WindupComplete",
                normalizedTime = 1.0f, // End of windup (0.1s)
                stringParameter = "TailWhipActive"
            });

            AddAnimationEvent("TailWhipActive", new AnimationEvent
            {
                eventName = "HitDetection",
                normalizedTime = 0.5f, // Middle of active phase
                stringParameter = "DealDamage"
            });

            AddAnimationEvent("TailWhipActive", new AnimationEvent
            {
                eventName = "ActiveComplete", 
                normalizedTime = 1.0f, // End of active (0.18s)
                stringParameter = "TailWhipRecovery"
            });
        }

        private void SetupMovementAnimationEvents()
        {
            // Jump events
            AddAnimationEvent("Jump", new AnimationEvent
            {
                eventName = "JumpApex",
                normalizedTime = 0.5f,
                stringParameter = "Peak"
            });

            // Landing events
            AddAnimationEvent("Land", new AnimationEvent
            {
                eventName = "LandComplete",
                normalizedTime = 0.8f,
                stringParameter = "TransitionToIdle"
            });
        }

        private void SetupSpecialAnimationEvents()
        {
            // Collection events
            AddAnimationEvent("Collect", new AnimationEvent
            {
                eventName = "CollectItem",
                normalizedTime = 0.6f,
                stringParameter = "PickupComplete"
            });
        }

        private void AddAnimationEvent(string clipName, AnimationEvent animEvent)
        {
            if (!animationEvents.ContainsKey(clipName))
            {
                animationEvents[clipName] = new List<AnimationEvent>();
            }
            
            animationEvents[clipName].Add(animEvent);
        }

        private void UpdateAnimationEvents()
        {
            if (animator == null || string.IsNullOrEmpty(currentAnimation)) return;

            // Check if current animation has events
            if (animationEvents.ContainsKey(currentAnimation))
            {
                var events = animationEvents[currentAnimation];
                var currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
                
                foreach (var animEvent in events)
                {
                    // Check if event should trigger based on normalized time
                    if (currentStateInfo.normalizedTime >= animEvent.normalizedTime && 
                        currentAnimationTime < animEvent.normalizedTime)
                    {
                        TriggerAnimationEvent(animEvent);
                    }
                }
                
                currentAnimationTime = currentStateInfo.normalizedTime;
            }
        }

        private void TriggerAnimationEvent(AnimationEvent animEvent)
        {
            OnAnimationEvent?.Invoke(animEvent);
            animEvent.callback?.Invoke(animEvent);
            
            if (debugMode)
            {
                Debug.Log($"Animation event triggered: {animEvent.eventName} at {animEvent.normalizedTime:F2}");
            }
        }

        #endregion

        #region Optimization

        private void UpdateAnimationOptimization()
        {
            if (Time.time - lastOptimizationUpdate < 1f) return; // Update every second
            lastOptimizationUpdate = Time.time;

            UpdateCullingOptimization();
            UpdateLODOptimization();
            UpdatePerformanceOptimization();
        }

        private void UpdateCullingOptimization()
        {
            if (!config.enableAnimationCulling || mainCamera == null) return;

            float distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);
            bool shouldCull = distanceToCamera > config.cullingDistance;
            
            if (shouldCull != animationCulled)
            {
                animationCulled = shouldCull;
                
                if (animator != null)
                {
                    if (animationCulled)
                    {
                        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    }
                    else
                    {
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animator.updateMode = AnimatorUpdateMode.Normal;
                    }
                }
            }
        }

        private void UpdateLODOptimization()
        {
            if (!config.enableLODAnimations) return;

            // Get current LOD level from character system or LOD manager
            int currentLOD = GetCurrentLODLevel();
            
            // Enable/disable animations based on LOD level
            foreach (var clipData in animationClips)
            {
                bool shouldEnable = currentLOD switch
                {
                    0 => clipData.enabledInLOD0,
                    1 => clipData.enabledInLOD1,
                    2 => clipData.enabledInLOD2,
                    _ => false
                };

                // This would require custom animator controller logic to enable/disable clips
                // For now, we just track which animations should be active
            }
        }

        private void UpdatePerformanceOptimization()
        {
            // Count active animations
            activeAnimationCount = 0;
            if (animator != null)
            {
                for (int i = 0; i < animator.layerCount; i++)
                {
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(i);
                    if (stateInfo.length > 0 && stateInfo.normalizedTime < 1f)
                    {
                        activeAnimationCount++;
                    }
                }
            }

            // Limit simultaneous animations based on performance budget
            if (activeAnimationCount > config.maxSimultaneousAnimations)
            {
                // Reduce quality or disable less important animations
                OptimizeActiveAnimations();
            }
        }

        private void OptimizeActiveAnimations()
        {
            // Reduce layer weights for less important animations
            foreach (var kvp in layerWeights.ToArray())
            {
                if (kvp.Key != "Base Layer") // Never reduce base layer
                {
                    float reducedWeight = kvp.Value * 0.5f;
                    SetLayerWeight(kvp.Key, reducedWeight);
                }
            }
        }

        #endregion

        #region Helper Methods

        private string ExtractClipName(string key)
        {
            int lastSlashIndex = key.LastIndexOf('/');
            return lastSlashIndex >= 0 ? key.Substring(lastSlashIndex + 1) : key;
        }

        private AnimationClipCategory DetermineClipCategory(string key)
        {
            if (key.Contains("TailWhip") || key.Contains("Hit") || key.Contains("Stun"))
                return AnimationClipCategory.Combat;
            if (key.Contains("Run") || key.Contains("Jump") || key.Contains("Slide") || key.Contains("Fall"))
                return AnimationClipCategory.Movement;
            if (key.Contains("Idle"))
                return AnimationClipCategory.Idle;
            if (key.Contains("Collect") || key.Contains("Bounce"))
                return AnimationClipCategory.Interaction;
            if (key.Contains("Death") || key.Contains("Celebration"))
                return AnimationClipCategory.Special;
            
            return AnimationClipCategory.Movement;
        }

        private int GetClipPriority(string clipName)
        {
            if (clipLookup.ContainsKey(clipName))
            {
                return clipLookup[clipName].priority;
            }
            return 10; // Default priority
        }

        private bool IsAnimationEssential(string clipName)
        {
            // Essential animations that should never be culled
            return clipName.Contains("TailWhip") || 
                   clipName.Contains("Jump") || 
                   clipName.Contains("Run") ||
                   clipName.Contains("Idle");
        }

        private int GetCurrentLODLevel()
        {
            // Try to get LOD level from character system
            var characterSystem = GetComponent<Character.CharacterSystem>();
            return characterSystem?.GetCurrentLODLevel() ?? 0;
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Check if animation is currently playing
        /// </summary>
        public bool IsAnimationPlaying(string clipName)
        {
            return currentAnimation == clipName;
        }

        /// <summary>
        /// Get current animation name
        /// </summary>
        public string GetCurrentAnimation()
        {
            return currentAnimation;
        }

        /// <summary>
        /// Set layer weight
        /// </summary>
        public void SetLayerWeight(string layerName, float weight)
        {
            if (animator != null && layerWeights.ContainsKey(layerName))
            {
                for (int i = 0; i < animationLayers.Count; i++)
                {
                    if (animationLayers[i].layerName == layerName && i < animator.layerCount)
                    {
                        animator.SetLayerWeight(i, weight);
                        layerWeights[layerName] = weight;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Get layer weight
        /// </summary>
        public float GetLayerWeight(string layerName)
        {
            return layerWeights.GetValueOrDefault(layerName, 0f);
        }

        /// <summary>
        /// Get animation clip count
        /// </summary>
        public int GetAnimationClipCount()
        {
            return animationClips.Count;
        }

        /// <summary>
        /// Get PRD compliance status
        /// </summary>
        public bool IsPRDCompliant()
        {
            return animationClips.Count <= 20 && 
                   animationClips.All(clip => clip.frameCount <= config.maxFramesPerAnimation);
        }

        /// <summary>
        /// Get animation statistics
        /// </summary>
        public string GetAnimationStatistics()
        {
            int compliantClips = animationClips.Count(clip => clip.frameCount <= config.maxFramesPerAnimation);
            return $"Animation Clips: {animationClips.Count}/20, " +
                   $"PRD Compliant: {compliantClips}/{animationClips.Count}, " +
                   $"Active: {activeAnimationCount}, " +
                   $"Culled: {animationCulled}";
        }

        #endregion

        #region Debug Interface

        private void UpdateDebugInfo()
        {
            // Update debug information
        }

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(270, 10, 250, 200));
            
            GUILayout.Label("=== ANIMATION SYSTEM ===");
            GUILayout.Label($"Current: {currentAnimation}");
            GUILayout.Label($"Clips: {animationClips.Count}/20");
            GUILayout.Label($"Active: {activeAnimationCount}/{config.maxSimultaneousAnimations}");
            GUILayout.Label($"Culled: {animationCulled}");
            
            int compliantClips = animationClips.Count(clip => clip.frameCount <= config.maxFramesPerAnimation);
            GUILayout.Label($"PRD Compliant: {compliantClips}/{animationClips.Count}");
            
            GUILayout.Space(10);
            GUILayout.Label("=== LAYER WEIGHTS ===");
            
            foreach (var kvp in layerWeights.Take(3))
            {
                GUILayout.Label($"{kvp.Key}: {kvp.Value:F2}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            if (GUILayout.Button("Play Idle"))
                PlayAnimation("Idle");
                
            if (GUILayout.Button("Play Run"))
                PlayAnimation("Run");
                
            if (GUILayout.Button("Play TailWhip"))
                PlayAnimation("TailWhipWindup");
            
            config.enableAnimationCulling = GUILayout.Toggle(config.enableAnimationCulling, "Enable Culling");
            
            GUILayout.EndArea();
        }

        #endregion
    }
}
