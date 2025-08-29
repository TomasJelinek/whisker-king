using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using WhiskerKing.Core;
using WhiskerKing.Content;
using WhiskerKing.Performance;
using WhiskerKing.Audio;

namespace WhiskerKing.VFX
{
    /// <summary>
    /// Visual Effects System for Whisker King
    /// Manages particle effects, visual polish, and gameplay action feedback
    /// Optimized for mobile platforms with performance budgeting
    /// </summary>
    public class VisualEffectsSystem : MonoBehaviour
    {
        [System.Serializable]
        public class VFXConfiguration
        {
            [Header("Performance Settings")]
            public int maxActiveEffects = 20;
            public int maxParticlesPerEffect = 50;
            public float maxEffectLifetime = 10f;
            public bool enableParticleCulling = true;
            public float cullingDistance = 30f;

            [Header("Quality Settings")]
            public VFXQuality defaultQuality = VFXQuality.Medium;
            public bool adaptQualityToPerformance = true;
            public bool enableMobileOptimizations = true;

            [Header("Effect Pooling")]
            public bool enableEffectPooling = true;
            public int poolInitialSize = 10;
            public int poolMaxSize = 50;
            public bool allowPoolExpansion = true;
        }

        public enum VFXQuality
        {
            Low,    // Minimal particles, simple effects
            Medium, // Standard particles, moderate complexity
            High    // Full particle systems, complex effects
        }

        [System.Serializable]
        public class EffectTemplate
        {
            [Header("Effect Information")]
            public string effectName;
            public EffectType type;
            public GameObject effectPrefab;
            public bool isLooping = false;
            public float duration = 2f;

            [Header("Performance")]
            public int baseParticleCount = 25;
            public int lowQualityParticles = 10;
            public int mediumQualityParticles = 25;
            public int highQualityParticles = 50;

            [Header("Audio Integration")]
            public AudioClip soundEffect;
            public AudioManager.AudioCategory audioCategory = AudioManager.AudioCategory.SFX_World;
            public bool playAudioWithEffect = true;

            [Header("Gameplay Integration")]
            public bool attachToPlayer = false;
            public bool followTarget = false;
            public float cameraShakeStrength = 0f;
            public bool pauseGameBriefly = false;
        }

        public enum EffectType
        {
            // Movement Effects
            Jump,
            Land,
            Slide,
            Run,
            Bounce,
            
            // Combat Effects
            TailWhip,
            Hit,
            Block,
            Stun,
            
            // Collection Effects
            FishTreat,
            Yarn,
            GoldenToken,
            
            // Environmental Effects
            Splash,
            Dust,
            Explosion,
            Sparkle,
            
            // UI Effects
            LevelComplete,
            MenuTransition,
            
            // Special Effects
            PowerUp,
            Hazard,
            Portal
        }

        [System.Serializable]
        public class EffectInstance
        {
            public string effectId;
            public EffectType type;
            public GameObject effectObject;
            public ParticleSystem[] particleSystems;
            public Transform target;
            public float spawnTime;
            public float lifetime;
            public bool isActive;
            public bool isPaused;
        }

        [Header("VFX Configuration")]
        [SerializeField] private VFXConfiguration config = new VFXConfiguration();
        [SerializeField] private EffectTemplate[] effectTemplates = new EffectTemplate[20];
        [SerializeField] private bool debugMode = true;

        // Effect management
        private Dictionary<EffectType, EffectTemplate> templateLookup = new Dictionary<EffectType, EffectTemplate>();
        private List<EffectInstance> activeEffects = new List<EffectInstance>();
        private Dictionary<EffectType, ObjectPool<ParticleSystem>> effectPools = new Dictionary<EffectType, ObjectPool<ParticleSystem>>();

        // Quality management
        private VFXQuality currentQuality = VFXQuality.Medium;
        private int particleBudget = 1000;
        private int currentParticleCount = 0;

        // Performance tracking
        private Dictionary<EffectType, int> effectUsageCount = new Dictionary<EffectType, int>();
        private float lastCleanupTime = 0f;
        private const float CLEANUP_INTERVAL = 5f;

        // Component references
        private AssetManager assetManager;
        private AudioManager audioManager;
        private PerformanceManager performanceManager;
        private Camera mainCamera;

        // Events
        public System.Action<EffectType, Vector3> OnEffectTriggered;
        public System.Action<EffectInstance> OnEffectCompleted;
        public System.Action<VFXQuality> OnQualityChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeVFXSystem();
        }

        private void Start()
        {
            LoadEffectTemplates();
            InitializeEffectPools();
            SetQualityBasedOnPlatform();
        }

        private void Update()
        {
            UpdateActiveEffects();
            UpdatePerformanceOptimizations();
            CleanupExpiredEffects();
        }

        private void LateUpdate()
        {
            UpdateParticleCount();
        }

        #endregion

        #region Initialization

        private void InitializeVFXSystem()
        {
            // Get component references
            assetManager = AssetManager.Instance;
            audioManager = AudioManager.Instance;
            performanceManager = PerformanceManager.Instance;
            mainCamera = Camera.main ?? FindObjectOfType<Camera>();

            // Initialize collections
            templateLookup.Clear();
            activeEffects.Clear();
            effectPools.Clear();
            effectUsageCount.Clear();

            // Set initial particle budget based on platform
            SetParticleBudgetForPlatform();

            Debug.Log("VisualEffectsSystem initialized");
        }

        private void LoadEffectTemplates()
        {
            // Initialize effect templates for all gameplay actions
            InitializeMovementEffects();
            InitializeCombatEffects();
            InitializeCollectionEffects();
            InitializeEnvironmentalEffects();
            InitializeUIEffects();

            // Build template lookup dictionary
            for (int i = 0; i < effectTemplates.Length; i++)
            {
                var template = effectTemplates[i];
                if (template != null && !string.IsNullOrEmpty(template.effectName))
                {
                    templateLookup[template.type] = template;
                    effectUsageCount[template.type] = 0;
                    
                    if (debugMode)
                    {
                        Debug.Log($"Loaded effect template: {template.effectName} ({template.type})");
                    }
                }
            }

            Debug.Log($"Loaded {templateLookup.Count} effect templates");
        }

        private void InitializeMovementEffects()
        {
            // Jump Effect
            effectTemplates[0] = new EffectTemplate
            {
                effectName = "Player_Jump",
                type = EffectType.Jump,
                duration = 0.5f,
                baseParticleCount = 15,
                cameraShakeStrength = 0.1f,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_Player
            };

            // Landing Effect
            effectTemplates[1] = new EffectTemplate
            {
                effectName = "Player_Land",
                type = EffectType.Land,
                duration = 0.3f,
                baseParticleCount = 20,
                cameraShakeStrength = 0.2f,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_Player
            };

            // Slide Effect
            effectTemplates[2] = new EffectTemplate
            {
                effectName = "Player_Slide",
                type = EffectType.Slide,
                isLooping = true,
                duration = 0.6f, // PRD: slide duration
                baseParticleCount = 10,
                attachToPlayer = true,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_Player
            };

            // Bounce Effect
            effectTemplates[3] = new EffectTemplate
            {
                effectName = "Player_Bounce",
                type = EffectType.Bounce,
                duration = 0.4f,
                baseParticleCount = 25,
                cameraShakeStrength = 0.15f,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_Player
            };
        }

        private void InitializeCombatEffects()
        {
            // Tail Whip Effect
            effectTemplates[4] = new EffectTemplate
            {
                effectName = "TailWhip_Impact",
                type = EffectType.TailWhip,
                duration = 0.3f, // PRD: total combat duration 0.3s
                baseParticleCount = 30,
                cameraShakeStrength = 0.3f,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_Player,
                pauseGameBriefly = false // Keep game responsive
            };

            // Hit Effect
            effectTemplates[5] = new EffectTemplate
            {
                effectName = "Combat_Hit",
                type = EffectType.Hit,
                duration = 0.25f,
                baseParticleCount = 20,
                cameraShakeStrength = 0.2f,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_World
            };

            // Stun Effect
            effectTemplates[6] = new EffectTemplate
            {
                effectName = "Enemy_Stun",
                type = EffectType.Stun,
                isLooping = true,
                duration = 1.5f, // PRD: stun duration
                baseParticleCount = 15,
                followTarget = true,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_World
            };
        }

        private void InitializeCollectionEffects()
        {
            // Fish Treat Collection
            effectTemplates[7] = new EffectTemplate
            {
                effectName = "Collect_FishTreat",
                type = EffectType.FishTreat,
                duration = 1f,
                baseParticleCount = 12,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_UI
            };

            // Yarn Collection
            effectTemplates[8] = new EffectTemplate
            {
                effectName = "Collect_Yarn",
                type = EffectType.Yarn,
                duration = 0.8f,
                baseParticleCount = 8,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_UI
            };

            // Golden Token Collection
            effectTemplates[9] = new EffectTemplate
            {
                effectName = "Collect_GoldenToken",
                type = EffectType.GoldenToken,
                duration = 1.5f,
                baseParticleCount = 40,
                cameraShakeStrength = 0.1f,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_UI,
                pauseGameBriefly = false // Keep responsive
            };
        }

        private void InitializeEnvironmentalEffects()
        {
            // Water Splash (Seaside Docks)
            effectTemplates[10] = new EffectTemplate
            {
                effectName = "Environment_Splash",
                type = EffectType.Splash,
                duration = 1.2f,
                baseParticleCount = 35,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_World
            };

            // Dust Cloud
            effectTemplates[11] = new EffectTemplate
            {
                effectName = "Environment_Dust",
                type = EffectType.Dust,
                duration = 2f,
                baseParticleCount = 25,
                playAudioWithEffect = false
            };

            // Explosion (Boom Crate)
            effectTemplates[12] = new EffectTemplate
            {
                effectName = "Crate_Explosion",
                type = EffectType.Explosion,
                duration = 1f,
                baseParticleCount = 50,
                cameraShakeStrength = 0.4f,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_World
            };

            // Sparkle (Mystery effects)
            effectTemplates[13] = new EffectTemplate
            {
                effectName = "Magic_Sparkle",
                type = EffectType.Sparkle,
                duration = 1.5f,
                baseParticleCount = 30,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_World
            };
        }

        private void InitializeUIEffects()
        {
            // Level Complete
            effectTemplates[14] = new EffectTemplate
            {
                effectName = "UI_LevelComplete",
                type = EffectType.LevelComplete,
                duration = 3f,
                baseParticleCount = 60,
                playAudioWithEffect = true,
                audioCategory = AudioManager.AudioCategory.SFX_UI
            };

            // Menu Transition
            effectTemplates[15] = new EffectTemplate
            {
                effectName = "UI_MenuTransition",
                type = EffectType.MenuTransition,
                duration = 0.5f,
                baseParticleCount = 20,
                playAudioWithEffect = false
            };
        }

        private void InitializeEffectPools()
        {
            if (!config.enableEffectPooling) return;

            foreach (var template in templateLookup.Values)
            {
                CreateEffectPool(template.type, template);
            }

            Debug.Log($"Initialized {effectPools.Count} effect pools");
        }

        private void CreateEffectPool(EffectType effectType, EffectTemplate template)
        {
            // Create a pool for this effect type
            var pool = new ObjectPool<ParticleSystem>(
                () => CreateParticleSystemForTemplate(template),
                (ps) => ps.gameObject.SetActive(true),
                (ps) => {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.gameObject.SetActive(false);
                },
                (ps) => DestroyImmediate(ps.gameObject),
                config.poolMaxSize
            );

            // Pre-populate pool
            for (int i = 0; i < config.poolInitialSize; i++)
            {
                var ps = pool.Get();
                pool.ReturnToPool(ps);
            }

            effectPools[effectType] = pool;
        }

        private ParticleSystem CreateParticleSystemForTemplate(EffectTemplate template)
        {
            var go = new GameObject($"VFX_{template.effectName}");
            var particleSystem = go.AddComponent<ParticleSystem>();

            // Configure particle system based on template
            ConfigureParticleSystem(particleSystem, template);

            return particleSystem;
        }

        private void ConfigureParticleSystem(ParticleSystem ps, EffectTemplate template)
        {
            var main = ps.main;
            main.duration = template.duration;
            main.loop = template.isLooping;
            main.startLifetime = template.duration * 0.5f;
            main.startSpeed = 5f;
            main.maxParticles = GetParticleCountForQuality(template, currentQuality);

            // Configure emission
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = main.maxParticles / main.duration;

            // Configure shape based on effect type
            ConfigureParticleShape(ps, template.type);

            // Configure visual appearance
            ConfigureParticleVisuals(ps, template.type);
        }

        private void ConfigureParticleShape(ParticleSystem ps, EffectType effectType)
        {
            var shape = ps.shape;
            
            switch (effectType)
            {
                case EffectType.Jump:
                case EffectType.Land:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.5f;
                    break;
                
                case EffectType.TailWhip:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 270f / 2f; // PRD: 270° angle
                    shape.radius = 2.5f; // PRD: 2.5f range
                    break;
                
                case EffectType.Explosion:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 1f;
                    break;
                
                default:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.3f;
                    break;
            }
        }

        private void ConfigureParticleVisuals(ParticleSystem ps, EffectType effectType)
        {
            var main = ps.main;
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            // Set colors based on effect type
            Color startColor = effectType switch
            {
                EffectType.Jump or EffectType.Land => Color.white,
                EffectType.TailWhip => Color.yellow,
                EffectType.Hit => Color.red,
                EffectType.FishTreat => Color.cyan,
                EffectType.Yarn => Color.magenta,
                EffectType.GoldenToken => Color.yellow,
                EffectType.Splash => Color.blue,
                EffectType.Explosion => Color.orange,
                _ => Color.white
            };

            main.startColor = startColor;

            // Configure fade out
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(startColor, 0.0f), new GradientColorKey(startColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = gradient;
        }

        private void SetQualityBasedOnPlatform()
        {
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                currentQuality = VFXQuality.Low;
                particleBudget = 500; // Conservative for mobile
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                currentQuality = VFXQuality.Medium;
                particleBudget = 750;
            }
            else
            {
                currentQuality = VFXQuality.High;
                particleBudget = 1000;
            }

            Debug.Log($"VFX quality set to {currentQuality} with particle budget {particleBudget}");
        }

        private void SetParticleBudgetForPlatform()
        {
            particleBudget = Application.platform switch
            {
                RuntimePlatform.Android => 400,
                RuntimePlatform.IPhonePlayer => 500,
                RuntimePlatform.WebGLPlayer => 750,
                _ => 1000
            };
        }

        #endregion

        #region Effect Playback

        public void PlayEffect(EffectType effectType, Vector3 position, Transform target = null)
        {
            if (!templateLookup.ContainsKey(effectType))
            {
                if (debugMode)
                    Debug.LogWarning($"Effect template not found for type: {effectType}");
                return;
            }

            // Check particle budget
            if (currentParticleCount >= particleBudget)
            {
                if (debugMode)
                    Debug.LogWarning("Particle budget exceeded, skipping effect");
                return;
            }

            // Check max active effects
            if (activeEffects.Count >= config.maxActiveEffects)
            {
                CleanupOldestEffect();
            }

            var template = templateLookup[effectType];
            var effectInstance = CreateEffectInstance(template, position, target);
            
            if (effectInstance != null)
            {
                activeEffects.Add(effectInstance);
                effectUsageCount[effectType]++;
                
                // Play audio if configured
                if (template.playAudioWithEffect && audioManager != null && template.soundEffect != null)
                {
                    audioManager.PlayOneShot(template.soundEffect, template.audioCategory);
                }

                // Apply camera shake if configured
                if (template.cameraShakeStrength > 0f)
                {
                    ApplyCameraShake(template.cameraShakeStrength);
                }

                OnEffectTriggered?.Invoke(effectType, position);

                if (debugMode)
                {
                    Debug.Log($"Playing effect: {template.effectName} at {position}");
                }
            }
        }

        public void PlayEffectAtPlayer(EffectType effectType)
        {
            var player = FindObjectOfType<Player.PlayerController>();
            if (player != null)
            {
                PlayEffect(effectType, player.transform.position, player.transform);
            }
        }

        public void StopEffect(EffectType effectType)
        {
            var effectsToStop = activeEffects.Where(e => e.type == effectType).ToList();
            
            foreach (var effect in effectsToStop)
            {
                StopEffectInstance(effect);
            }
        }

        public void StopAllEffects()
        {
            var effectsToStop = new List<EffectInstance>(activeEffects);
            
            foreach (var effect in effectsToStop)
            {
                StopEffectInstance(effect);
            }

            activeEffects.Clear();
        }

        private EffectInstance CreateEffectInstance(EffectTemplate template, Vector3 position, Transform target)
        {
            GameObject effectObject = null;
            ParticleSystem[] particleSystems = null;

            // Use pooled effect if available
            if (config.enableEffectPooling && effectPools.ContainsKey(template.type))
            {
                var pooledPS = effectPools[template.type].Get();
                if (pooledPS != null)
                {
                    effectObject = pooledPS.gameObject;
                    particleSystems = new ParticleSystem[] { pooledPS };
                }
            }

            // Create new effect if pooling failed or not enabled
            if (effectObject == null)
            {
                if (template.effectPrefab != null)
                {
                    effectObject = Instantiate(template.effectPrefab);
                    particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>();
                }
                else
                {
                    // Create basic particle system
                    var ps = CreateParticleSystemForTemplate(template);
                    effectObject = ps.gameObject;
                    particleSystems = new ParticleSystem[] { ps };
                }
            }

            if (effectObject == null)
                return null;

            // Position the effect
            effectObject.transform.position = position;

            // Configure particle count for current quality
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                main.maxParticles = GetParticleCountForQuality(template, currentQuality);
                ps.Play();
            }

            // Create effect instance
            var instance = new EffectInstance
            {
                effectId = System.Guid.NewGuid().ToString(),
                type = template.type,
                effectObject = effectObject,
                particleSystems = particleSystems,
                target = target,
                spawnTime = Time.time,
                lifetime = template.duration,
                isActive = true,
                isPaused = false
            };

            return instance;
        }

        private int GetParticleCountForQuality(EffectTemplate template, VFXQuality quality)
        {
            return quality switch
            {
                VFXQuality.Low => template.lowQualityParticles,
                VFXQuality.Medium => template.mediumQualityParticles,
                VFXQuality.High => template.highQualityParticles,
                _ => template.baseParticleCount
            };
        }

        private void StopEffectInstance(EffectInstance effect)
        {
            if (effect?.effectObject != null)
            {
                // Stop all particle systems
                foreach (var ps in effect.particleSystems)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }

                // Return to pool or destroy
                if (config.enableEffectPooling && effectPools.ContainsKey(effect.type))
                {
                    if (effect.particleSystems.Length > 0)
                    {
                        effectPools[effect.type].ReturnToPool(effect.particleSystems[0]);
                    }
                }
                else
                {
                    DestroyImmediate(effect.effectObject);
                }

                effect.isActive = false;
            }

            activeEffects.Remove(effect);
        }

        #endregion

        #region Effect Management

        private void UpdateActiveEffects()
        {
            var effectsToRemove = new List<EffectInstance>();
            float currentTime = Time.time;

            foreach (var effect in activeEffects)
            {
                if (!effect.isActive)
                {
                    effectsToRemove.Add(effect);
                    continue;
                }

                // Check lifetime
                if (currentTime - effect.spawnTime >= effect.lifetime)
                {
                    StopEffectInstance(effect);
                    effectsToRemove.Add(effect);
                    OnEffectCompleted?.Invoke(effect);
                    continue;
                }

                // Update effect position if following target
                var template = templateLookup.GetValueOrDefault(effect.type);
                if (template != null && template.followTarget && effect.target != null)
                {
                    effect.effectObject.transform.position = effect.target.position;
                }

                // Check culling distance
                if (config.enableParticleCulling && mainCamera != null)
                {
                    float distance = Vector3.Distance(mainCamera.transform.position, effect.effectObject.transform.position);
                    bool shouldCull = distance > config.cullingDistance;
                    
                    foreach (var ps in effect.particleSystems)
                    {
                        if (ps != null)
                        {
                            var emission = ps.emission;
                            emission.enabled = !shouldCull;
                        }
                    }
                }
            }

            // Remove completed effects
            foreach (var effect in effectsToRemove)
            {
                if (activeEffects.Contains(effect))
                {
                    activeEffects.Remove(effect);
                }
            }
        }

        private void UpdateParticleCount()
        {
            currentParticleCount = 0;
            
            foreach (var effect in activeEffects)
            {
                if (effect.isActive && effect.particleSystems != null)
                {
                    foreach (var ps in effect.particleSystems)
                    {
                        if (ps != null && ps.isPlaying)
                        {
                            currentParticleCount += ps.particleCount;
                        }
                    }
                }
            }
        }

        private void UpdatePerformanceOptimizations()
        {
            if (!config.adaptQualityToPerformance || performanceManager == null)
                return;

            var metrics = performanceManager.GetPerformanceMetrics();
            
            // Reduce quality if performance is poor
            if (metrics.averageFrameRate < 25f && currentQuality > VFXQuality.Low)
            {
                SetVFXQuality(VFXQuality.Low);
                Debug.LogWarning("Reduced VFX quality due to low performance");
            }
            else if (metrics.averageFrameRate > 45f && currentQuality < config.defaultQuality)
            {
                var targetQuality = (VFXQuality)Mathf.Min((int)currentQuality + 1, (int)config.defaultQuality);
                SetVFXQuality(targetQuality);
            }

            // Reduce particle budget if over budget
            if (currentParticleCount > particleBudget * 0.9f)
            {
                CleanupOldestEffect();
            }
        }

        private void CleanupExpiredEffects()
        {
            if (Time.time - lastCleanupTime < CLEANUP_INTERVAL)
                return;

            lastCleanupTime = Time.time;
            
            // Remove inactive effects
            activeEffects.RemoveAll(effect => !effect.isActive || effect.effectObject == null);
            
            // Force cleanup if too many effects
            while (activeEffects.Count > config.maxActiveEffects * 0.8f)
            {
                CleanupOldestEffect();
            }
        }

        private void CleanupOldestEffect()
        {
            if (activeEffects.Count == 0) return;

            // Find oldest non-critical effect
            var oldestEffect = activeEffects
                .Where(e => e.type != EffectType.TailWhip && e.type != EffectType.GoldenToken)
                .OrderBy(e => e.spawnTime)
                .FirstOrDefault();

            if (oldestEffect != null)
            {
                StopEffectInstance(oldestEffect);
            }
        }

        #endregion

        #region Quality Management

        public void SetVFXQuality(VFXQuality quality)
        {
            if (currentQuality == quality) return;

            var oldQuality = currentQuality;
            currentQuality = quality;

            // Update particle budgets
            UpdateParticleBudgetForQuality(quality);

            // Update active effects
            UpdateActiveEffectsForQuality(quality);

            OnQualityChanged?.Invoke(quality);
            
            if (debugMode)
            {
                Debug.Log($"VFX quality changed: {oldQuality} -> {quality}");
            }
        }

        private void UpdateParticleBudgetForQuality(VFXQuality quality)
        {
            float budgetMultiplier = quality switch
            {
                VFXQuality.Low => 0.5f,
                VFXQuality.Medium => 0.75f,
                VFXQuality.High => 1f,
                _ => 0.75f
            };

            particleBudget = Mathf.RoundToInt(particleBudget * budgetMultiplier);
        }

        private void UpdateActiveEffectsForQuality(VFXQuality quality)
        {
            foreach (var effect in activeEffects)
            {
                var template = templateLookup.GetValueOrDefault(effect.type);
                if (template != null && effect.particleSystems != null)
                {
                    int newParticleCount = GetParticleCountForQuality(template, quality);
                    
                    foreach (var ps in effect.particleSystems)
                    {
                        if (ps != null)
                        {
                            var main = ps.main;
                            main.maxParticles = newParticleCount;
                        }
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private void ApplyCameraShake(float strength)
        {
            // Apply camera shake effect
            var cameraController = FindObjectOfType<Camera.CameraController>();
            if (cameraController != null)
            {
                // This would trigger camera shake with the specified strength
                // The actual implementation would depend on the camera system
                if (debugMode)
                {
                    Debug.Log($"Applying camera shake with strength: {strength}");
                }
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current VFX quality
        /// </summary>
        public VFXQuality GetCurrentQuality()
        {
            return currentQuality;
        }

        /// <summary>
        /// Get active effect count
        /// </summary>
        public int GetActiveEffectCount()
        {
            return activeEffects.Count;
        }

        /// <summary>
        /// Get current particle count
        /// </summary>
        public int GetCurrentParticleCount()
        {
            return currentParticleCount;
        }

        /// <summary>
        /// Get particle budget
        /// </summary>
        public int GetParticleBudget()
        {
            return particleBudget;
        }

        /// <summary>
        /// Get effect usage statistics
        /// </summary>
        public Dictionary<EffectType, int> GetEffectUsage()
        {
            return new Dictionary<EffectType, int>(effectUsageCount);
        }

        /// <summary>
        /// Force cleanup all effects
        /// </summary>
        public void ForceCleanupAllEffects()
        {
            StopAllEffects();
            
            foreach (var pool in effectPools.Values)
            {
                pool.Dispose();
            }
            effectPools.Clear();

            currentParticleCount = 0;
            
            foreach (var key in effectUsageCount.Keys.ToArray())
            {
                effectUsageCount[key] = 0;
            }

            Debug.Log("Forced cleanup of all VFX effects");
        }

        /// <summary>
        /// Get VFX statistics
        /// </summary>
        public string GetVFXStatistics()
        {
            return $"Quality: {currentQuality}, " +
                   $"Active Effects: {activeEffects.Count}/{config.maxActiveEffects}, " +
                   $"Particles: {currentParticleCount}/{particleBudget}, " +
                   $"Pools: {effectPools.Count}";
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(1050, 10, 250, 350));
            
            GUILayout.Label("=== VFX SYSTEM ===");
            GUILayout.Label($"Quality: {currentQuality}");
            GUILayout.Label($"Active Effects: {activeEffects.Count}/{config.maxActiveEffects}");
            GUILayout.Label($"Particles: {currentParticleCount}/{particleBudget}");
            GUILayout.Label($"Effect Pools: {effectPools.Count}");
            
            float particleBudgetUsage = particleBudget > 0 ? (float)currentParticleCount / particleBudget : 0f;
            GUILayout.Label($"Budget Usage: {particleBudgetUsage:P1}");
            
            GUILayout.Space(10);
            GUILayout.Label("=== EFFECT USAGE ===");
            
            foreach (var kvp in effectUsageCount.Take(4))
            {
                GUILayout.Label($"{kvp.Key}: {kvp.Value}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== QUALITY CONTROL ===");
            
            if (GUILayout.Button("Low Quality"))
                SetVFXQuality(VFXQuality.Low);
            
            if (GUILayout.Button("Medium Quality"))
                SetVFXQuality(VFXQuality.Medium);
            
            if (GUILayout.Button("High Quality"))
                SetVFXQuality(VFXQuality.High);
            
            GUILayout.Space(10);
            GUILayout.Label("=== TEST EFFECTS ===");
            
            if (GUILayout.Button("Test Jump Effect"))
                PlayEffectAtPlayer(EffectType.Jump);
            
            if (GUILayout.Button("Test TailWhip Effect"))
                PlayEffectAtPlayer(EffectType.TailWhip);
            
            if (GUILayout.Button("Test Collection Effect"))
                PlayEffectAtPlayer(EffectType.GoldenToken);
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            if (GUILayout.Button("Stop All Effects"))
                StopAllEffects();
            
            if (GUILayout.Button("Force Cleanup"))
                ForceCleanupAllEffects();
            
            config.enableParticleCulling = GUILayout.Toggle(config.enableParticleCulling, "Particle Culling");
            
            GUILayout.EndArea();
        }

        #endregion
    }
}
