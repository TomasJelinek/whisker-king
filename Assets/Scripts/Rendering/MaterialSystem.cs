using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Collections;
using WhiskerKing.Core;
using WhiskerKing.Content;
using WhiskerKing.Performance;

namespace WhiskerKing.Rendering
{
    /// <summary>
    /// Material System for Whisker King
    /// Manages mobile-optimized materials, shaders, and texture compression
    /// Handles LOD materials and platform-specific optimizations
    /// </summary>
    public class MaterialSystem : MonoBehaviour
    {
        [System.Serializable]
        public class MaterialConfiguration
        {
            [Header("Shader Settings")]
            public bool useMobileOptimizedShaders = true;
            public bool enableShaderVariantStripping = true;
            public int maxShaderLOD = 300; // Mobile-friendly limit
            
            [Header("Texture Settings")]
            public int maxTextureSize = 2048; // PRD compliance
            public TextureImporterFormat mobileTextureFormat = TextureImporterFormat.ASTC_4x4;
            public TextureImporterFormat webGLTextureFormat = TextureImporterFormat.DXT5;
            public bool generateMipmaps = true;
            public bool enableTextureStreaming = true;

            [Header("Material Optimization")]
            public bool enableMaterialSharing = true;
            public bool enableBatching = true;
            public int maxMaterialInstances = 100;
            public bool enableMaterialLOD = true;
            
            [Header("Quality Settings")]
            public MaterialQuality defaultQuality = MaterialQuality.Medium;
            public bool adaptQualityToPlatform = true;
        }

        public enum MaterialQuality
        {
            Low,    // Simplified materials, reduced features
            Medium, // Standard materials with basic features
            High    // Full-featured materials with all effects
        }

        [System.Serializable]
        public class MaterialTemplate
        {
            [Header("Template Information")]
            public string templateName;
            public MaterialType type;
            public Shader[] shaderVariants = new Shader[3]; // Low, Medium, High quality
            public Texture2D defaultAlbedo;
            public Texture2D defaultNormal;
            public Texture2D defaultMask;

            [Header("Properties")]
            public Color baseColor = Color.white;
            public float metallic = 0f;
            public float smoothness = 0.5f;
            public float normalStrength = 1f;
            public float emissionStrength = 0f;

            [Header("Mobile Optimization")]
            public bool supportsMobileOptimization = true;
            public int mobileTriangleReduction = 50; // Percentage
            public bool disableNormalsOnMobile = false;
        }

        public enum MaterialType
        {
            Character,
            Environment,
            UI,
            Effect,
            Transparent,
            Emissive,
            Water,
            Foliage
        }

        [System.Serializable]
        public class TextureSettings
        {
            [Header("Platform Settings")]
            public TextureImporterFormat androidFormat = TextureImporterFormat.ASTC_4x4;
            public TextureImporterFormat iOSFormat = TextureImporterFormat.ASTC_4x4;
            public TextureImporterFormat webGLFormat = TextureImporterFormat.DXT5;
            public TextureImporterFormat standaloneFormat = TextureImporterFormat.DXT5;

            [Header("Size Limits")]
            public int maxSizeMobile = 1024;
            public int maxSizeWebGL = 2048;
            public int maxSizeStandalone = 2048;

            [Header("Compression")]
            public bool enableCompression = true;
            public TextureCompressionQuality compressionQuality = TextureCompressionQuality.Normal;
            public bool crunchedCompression = true;
        }

        [Header("Material Configuration")]
        [SerializeField] private MaterialConfiguration config = new MaterialConfiguration();
        [SerializeField] private MaterialTemplate[] materialTemplates = new MaterialTemplate[8];
        [SerializeField] private TextureSettings textureSettings = new TextureSettings();
        [SerializeField] private bool debugMode = true;

        // Material management
        private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private Dictionary<MaterialType, MaterialTemplate> templateLookup = new Dictionary<MaterialType, MaterialTemplate>();
        private MaterialQuality currentQuality = MaterialQuality.Medium;

        // Shader management
        private Dictionary<string, Shader> shaderCache = new Dictionary<string, Shader>();
        private Dictionary<MaterialType, Shader[]> shaderVariants = new Dictionary<MaterialType, Shader[]>();

        // Texture management
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private Queue<TextureLoadRequest> textureLoadQueue = new Queue<TextureLoadRequest>();

        // Performance tracking
        private int activeMaterialCount = 0;
        private long totalTextureMemory = 0;
        private Dictionary<MaterialType, int> materialUsage = new Dictionary<MaterialType, int>();

        // Component references
        private AssetManager assetManager;
        private PerformanceManager performanceManager;

        private struct TextureLoadRequest
        {
            public string texturePath;
            public System.Action<Texture2D> onLoaded;
            public System.Action<string> onFailed;
        }

        // Events
        public System.Action<MaterialQuality> OnQualityChanged;
        public System.Action<Material> OnMaterialCreated;
        public System.Action<string> OnTextureLoaded;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeMaterialSystem();
        }

        private void Start()
        {
            LoadMaterialTemplates();
            SetupShaderVariants();
            InitializePlatformOptimizations();
            StartCoroutine(ProcessTextureLoadQueue());
        }

        private void Update()
        {
            UpdatePerformanceOptimizations();
        }

        #endregion

        #region Initialization

        private void InitializeMaterialSystem()
        {
            // Get component references
            assetManager = AssetManager.Instance;
            performanceManager = PerformanceManager.Instance;

            // Initialize collections
            materialCache.Clear();
            templateLookup.Clear();
            shaderCache.Clear();
            textureCache.Clear();

            // Set platform-appropriate quality
            SetQualityBasedOnPlatform();

            Debug.Log("MaterialSystem initialized");
        }

        private void LoadMaterialTemplates()
        {
            // Initialize default material templates
            InitializeCharacterMaterials();
            InitializeEnvironmentMaterials();
            InitializeEffectMaterials();
            InitializeUIMaterials();

            // Build template lookup
            foreach (var template in materialTemplates)
            {
                if (template != null)
                {
                    templateLookup[template.type] = template;
                    
                    // Initialize material usage tracking
                    materialUsage[template.type] = 0;
                }
            }

            Debug.Log($"Loaded {materialTemplates.Length} material templates");
        }

        private void InitializeCharacterMaterials()
        {
            materialTemplates[0] = new MaterialTemplate
            {
                templateName = "Character_Standard",
                type = MaterialType.Character,
                shaderVariants = new Shader[]
                {
                    Shader.Find("Universal Render Pipeline/Simple Lit"), // Low
                    Shader.Find("Universal Render Pipeline/Lit"),        // Medium
                    Shader.Find("Universal Render Pipeline/Lit")         // High
                },
                baseColor = new Color(1f, 0.8f, 0.6f), // Cat-like fur color
                metallic = 0f,
                smoothness = 0.3f,
                normalStrength = 0.8f,
                supportsMobileOptimization = true
            };
        }

        private void InitializeEnvironmentMaterials()
        {
            materialTemplates[1] = new MaterialTemplate
            {
                templateName = "Environment_Standard",
                type = MaterialType.Environment,
                shaderVariants = new Shader[]
                {
                    Shader.Find("Universal Render Pipeline/Simple Lit"),
                    Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("Universal Render Pipeline/Lit")
                },
                baseColor = Color.gray,
                metallic = 0.2f,
                smoothness = 0.4f,
                normalStrength = 1f,
                supportsMobileOptimization = true
            };

            materialTemplates[2] = new MaterialTemplate
            {
                templateName = "Water_Standard",
                type = MaterialType.Water,
                shaderVariants = new Shader[]
                {
                    Shader.Find("Universal Render Pipeline/Unlit"),
                    Shader.Find("Shader Graphs/Water_Medium"),
                    Shader.Find("Shader Graphs/Water_High")
                },
                baseColor = new Color(0.1f, 0.3f, 0.8f, 0.7f),
                metallic = 0f,
                smoothness = 0.9f,
                supportsMobileOptimization = true
            };
        }

        private void InitializeEffectMaterials()
        {
            materialTemplates[3] = new MaterialTemplate
            {
                templateName = "Effect_Standard",
                type = MaterialType.Effect,
                shaderVariants = new Shader[]
                {
                    Shader.Find("Universal Render Pipeline/Unlit"),
                    Shader.Find("Universal Render Pipeline/Unlit"),
                    Shader.Find("Shader Graphs/Effect_High")
                },
                baseColor = Color.white,
                emissionStrength = 1f,
                supportsMobileOptimization = true
            };

            materialTemplates[4] = new MaterialTemplate
            {
                templateName = "Transparent_Standard",
                type = MaterialType.Transparent,
                shaderVariants = new Shader[]
                {
                    Shader.Find("Universal Render Pipeline/Unlit"),
                    Shader.Find("Universal Render Pipeline/Unlit"),
                    Shader.Find("Universal Render Pipeline/Unlit")
                },
                baseColor = new Color(1f, 1f, 1f, 0.5f),
                supportsMobileOptimization = true
            };
        }

        private void InitializeUIMaterials()
        {
            materialTemplates[5] = new MaterialTemplate
            {
                templateName = "UI_Standard",
                type = MaterialType.UI,
                shaderVariants = new Shader[]
                {
                    Shader.Find("UI/Default"),
                    Shader.Find("UI/Default"),
                    Shader.Find("UI/Default")
                },
                baseColor = Color.white,
                supportsMobileOptimization = true
            };
        }

        private void SetupShaderVariants()
        {
            // Cache shader variants for each material type
            foreach (var template in materialTemplates)
            {
                if (template != null)
                {
                    shaderVariants[template.type] = template.shaderVariants;
                    
                    // Cache individual shaders
                    for (int i = 0; i < template.shaderVariants.Length; i++)
                    {
                        var shader = template.shaderVariants[i];
                        if (shader != null)
                        {
                            string key = $"{template.type}_{(MaterialQuality)i}";
                            shaderCache[key] = shader;
                        }
                    }
                }
            }
        }

        private void SetQualityBasedOnPlatform()
        {
            if (!config.adaptQualityToPlatform)
            {
                currentQuality = config.defaultQuality;
                return;
            }

            // Set quality based on platform capabilities
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    currentQuality = MaterialQuality.Low;
                    break;
                case RuntimePlatform.WebGLPlayer:
                    currentQuality = MaterialQuality.Medium;
                    break;
                default:
                    currentQuality = MaterialQuality.High;
                    break;
            }

            Debug.Log($"Material quality set to {currentQuality} for platform {Application.platform}");
        }

        private void InitializePlatformOptimizations()
        {
            // Apply platform-specific optimizations
            ApplyMobileOptimizations();
            ApplyWebGLOptimizations();
            ApplyTextureSettings();
        }

        private void ApplyMobileOptimizations()
        {
            if (Application.platform == RuntimePlatform.Android || 
                Application.platform == RuntimePlatform.IPhonePlayer)
            {
                // Reduce shader LOD for mobile
                Shader.globalMaximumLOD = config.maxShaderLOD;
                
                // Disable expensive features
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                
                Debug.Log("Applied mobile material optimizations");
            }
        }

        private void ApplyWebGLOptimizations()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // WebGL-specific optimizations
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                
                Debug.Log("Applied WebGL material optimizations");
            }
        }

        private void ApplyTextureSettings()
        {
            // This would normally be done through TextureImporter settings
            // Here we set runtime texture parameters
            Texture.SetGlobalAnisotropicFilteringLimits(1, 4);
        }

        #endregion

        #region Material Creation

        public Material CreateMaterial(MaterialType type, string materialName = null)
        {
            if (!templateLookup.ContainsKey(type))
            {
                Debug.LogError($"Material template for type {type} not found");
                return CreateFallbackMaterial(type);
            }

            var template = templateLookup[type];
            string cacheKey = materialName ?? $"{type}_{currentQuality}";

            // Check cache first
            if (config.enableMaterialSharing && materialCache.ContainsKey(cacheKey))
            {
                materialUsage[type]++;
                return materialCache[cacheKey];
            }

            // Create new material
            var material = CreateMaterialFromTemplate(template, cacheKey);
            
            // Cache the material
            if (config.enableMaterialSharing)
            {
                materialCache[cacheKey] = material;
            }

            materialUsage[type]++;
            activeMaterialCount++;
            OnMaterialCreated?.Invoke(material);

            if (debugMode)
            {
                Debug.Log($"Created material: {cacheKey} ({type})");
            }

            return material;
        }

        private Material CreateMaterialFromTemplate(MaterialTemplate template, string materialName)
        {
            // Get appropriate shader for current quality
            var shader = GetShaderForQuality(template.type, currentQuality);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit"); // Fallback
            }

            var material = new Material(shader);
            material.name = materialName;

            // Apply template properties
            ApplyTemplateProperties(material, template);

            // Apply quality-specific optimizations
            ApplyQualityOptimizations(material, template, currentQuality);

            return material;
        }

        private void ApplyTemplateProperties(Material material, MaterialTemplate template)
        {
            // Standard properties
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", template.baseColor);
            
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", template.baseColor);

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", template.metallic);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", template.smoothness);

            if (material.HasProperty("_BumpScale"))
                material.SetFloat("_BumpScale", template.normalStrength);

            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", Color.white * template.emissionStrength);

            // Apply default textures
            ApplyDefaultTextures(material, template);
        }

        private void ApplyDefaultTextures(Material material, MaterialTemplate template)
        {
            if (template.defaultAlbedo != null && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", template.defaultAlbedo);
            }

            if (template.defaultNormal != null && material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", template.defaultNormal);
            }

            if (template.defaultMask != null && material.HasProperty("_MetallicGlossMap"))
            {
                material.SetTexture("_MetallicGlossMap", template.defaultMask);
            }
        }

        private void ApplyQualityOptimizations(Material material, MaterialTemplate template, MaterialQuality quality)
        {
            switch (quality)
            {
                case MaterialQuality.Low:
                    ApplyLowQualityOptimizations(material, template);
                    break;
                case MaterialQuality.Medium:
                    ApplyMediumQualityOptimizations(material, template);
                    break;
                case MaterialQuality.High:
                    ApplyHighQualityOptimizations(material, template);
                    break;
            }
        }

        private void ApplyLowQualityOptimizations(Material material, MaterialTemplate template)
        {
            // Disable expensive features for low quality
            if (material.HasProperty("_BumpMap") && template.disableNormalsOnMobile)
            {
                material.SetTexture("_BumpMap", null);
            }

            // Reduce metallic/smoothness for simpler lighting
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", template.metallic * 0.5f);
        }

        private void ApplyMediumQualityOptimizations(Material material, MaterialTemplate template)
        {
            // Standard properties with minor optimizations
        }

        private void ApplyHighQualityOptimizations(Material material, MaterialTemplate template)
        {
            // Full quality with all features enabled
            if (material.HasProperty("_DetailAlbedoMap"))
            {
                // Enable detail textures if available
            }
        }

        private Material CreateFallbackMaterial(MaterialType type)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader);
            material.name = $"Fallback_{type}";
            
            // Set color based on type for identification
            Color fallbackColor = type switch
            {
                MaterialType.Character => Color.magenta,
                MaterialType.Environment => Color.gray,
                MaterialType.Effect => Color.cyan,
                MaterialType.UI => Color.white,
                MaterialType.Water => Color.blue,
                _ => Color.red
            };
            
            material.SetColor("_BaseColor", fallbackColor);
            return material;
        }

        #endregion

        #region Texture Management

        public void LoadTextureAsync(string texturePath, System.Action<Texture2D> onLoaded, 
                                   System.Action<string> onFailed = null)
        {
            // Check cache first
            if (textureCache.ContainsKey(texturePath))
            {
                onLoaded?.Invoke(textureCache[texturePath]);
                return;
            }

            // Add to load queue
            var request = new TextureLoadRequest
            {
                texturePath = texturePath,
                onLoaded = onLoaded,
                onFailed = onFailed
            };

            textureLoadQueue.Enqueue(request);
        }

        private IEnumerator ProcessTextureLoadQueue()
        {
            while (true)
            {
                if (textureLoadQueue.Count > 0)
                {
                    var request = textureLoadQueue.Dequeue();
                    yield return StartCoroutine(LoadTextureCoroutine(request));
                }
                else
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        private IEnumerator LoadTextureCoroutine(TextureLoadRequest request)
        {
            bool textureLoaded = false;
            Texture2D loadedTexture = null;
            string errorMessage = "";

            // Use AssetManager to load texture
            assetManager.LoadAssetAsync<Texture2D>(request.texturePath, AssetManager.LoadPriority.Medium,
                (texture) => {
                    loadedTexture = texture;
                    ApplyTextureOptimizations(loadedTexture);
                    textureCache[request.texturePath] = loadedTexture;
                    textureLoaded = true;
                },
                (error) => {
                    errorMessage = error;
                    textureLoaded = true;
                });

            yield return new WaitUntil(() => textureLoaded);

            if (loadedTexture != null)
            {
                request.onLoaded?.Invoke(loadedTexture);
                OnTextureLoaded?.Invoke(request.texturePath);
                UpdateTextureMemoryUsage(loadedTexture, true);
            }
            else
            {
                request.onFailed?.Invoke(errorMessage);
            }
        }

        private void ApplyTextureOptimizations(Texture2D texture)
        {
            if (texture == null) return;

            // Apply runtime optimizations
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = currentQuality == MaterialQuality.High ? 4 : 1;
        }

        private void UpdateTextureMemoryUsage(Texture2D texture, bool adding)
        {
            if (texture == null) return;

            long textureMemory = EstimateTextureMemorySize(texture);
            
            if (adding)
                totalTextureMemory += textureMemory;
            else
                totalTextureMemory -= textureMemory;
        }

        private long EstimateTextureMemorySize(Texture2D texture)
        {
            if (texture == null) return 0;

            int bytesPerPixel = GetBytesPerPixelForFormat(texture.format);
            return texture.width * texture.height * bytesPerPixel;
        }

        private int GetBytesPerPixelForFormat(TextureFormat format)
        {
            return format switch
            {
                TextureFormat.RGBA32 => 4,
                TextureFormat.RGB24 => 3,
                TextureFormat.RGBA4444 => 2,
                TextureFormat.RGB565 => 2,
                TextureFormat.Alpha8 => 1,
                TextureFormat.DXT1 => 1,
                TextureFormat.DXT5 => 1,
                TextureFormat.ASTC_4x4 => 1,
                _ => 4
            };
        }

        #endregion

        #region Quality Management

        public void SetMaterialQuality(MaterialQuality quality)
        {
            if (currentQuality == quality) return;

            var oldQuality = currentQuality;
            currentQuality = quality;

            // Rebuild all cached materials with new quality
            StartCoroutine(RefreshMaterialsForQuality(oldQuality, quality));

            OnQualityChanged?.Invoke(quality);
            Debug.Log($"Material quality changed: {oldQuality} -> {quality}");
        }

        private IEnumerator RefreshMaterialsForQuality(MaterialQuality oldQuality, MaterialQuality newQuality)
        {
            var materialsToRefresh = new List<string>(materialCache.Keys);
            
            foreach (string key in materialsToRefresh)
            {
                var material = materialCache[key];
                if (material != null)
                {
                    // Update shader for new quality
                    var materialType = GetMaterialTypeFromKey(key);
                    var newShader = GetShaderForQuality(materialType, newQuality);
                    
                    if (newShader != null)
                    {
                        material.shader = newShader;
                        
                        // Re-apply quality optimizations
                        var template = templateLookup.GetValueOrDefault(materialType);
                        if (template != null)
                        {
                            ApplyQualityOptimizations(material, template, newQuality);
                        }
                    }
                }

                // Yield periodically to avoid frame drops
                yield return null;
            }

            Debug.Log($"Refreshed {materialsToRefresh.Count} materials for quality level {newQuality}");
        }

        private MaterialType GetMaterialTypeFromKey(string key)
        {
            // Extract material type from cache key
            foreach (MaterialType type in System.Enum.GetValues(typeof(MaterialType)))
            {
                if (key.Contains(type.ToString()))
                {
                    return type;
                }
            }
            return MaterialType.Environment; // Fallback
        }

        private Shader GetShaderForQuality(MaterialType type, MaterialQuality quality)
        {
            if (shaderVariants.ContainsKey(type))
            {
                var variants = shaderVariants[type];
                int qualityIndex = (int)quality;
                
                if (qualityIndex < variants.Length && variants[qualityIndex] != null)
                {
                    return variants[qualityIndex];
                }
            }

            // Fallback to unlit shader
            return Shader.Find("Universal Render Pipeline/Unlit");
        }

        #endregion

        #region Performance Optimization

        private void UpdatePerformanceOptimizations()
        {
            // Check if we need to reduce material quality due to performance
            if (performanceManager != null)
            {
                var metrics = performanceManager.GetPerformanceMetrics();
                
                if (metrics.averageFrameRate < 30f && currentQuality > MaterialQuality.Low)
                {
                    // Automatically reduce quality if performance is poor
                    SetMaterialQuality(MaterialQuality.Low);
                    Debug.LogWarning("Automatically reduced material quality due to low FPS");
                }
                else if (metrics.averageFrameRate > 50f && currentQuality < MaterialQuality.High)
                {
                    // Increase quality if performance allows
                    var targetQuality = config.defaultQuality;
                    if (currentQuality < targetQuality)
                    {
                        SetMaterialQuality((MaterialQuality)Mathf.Min((int)currentQuality + 1, (int)targetQuality));
                    }
                }
            }

            // Clean up unused materials
            if (activeMaterialCount > config.maxMaterialInstances)
            {
                CleanupUnusedMaterials();
            }
        }

        private void CleanupUnusedMaterials()
        {
            var materialsToRemove = new List<string>();
            
            foreach (var kvp in materialCache)
            {
                if (kvp.Value == null)
                {
                    materialsToRemove.Add(kvp.Key);
                }
            }

            foreach (string key in materialsToRemove)
            {
                materialCache.Remove(key);
                activeMaterialCount--;
            }

            if (materialsToRemove.Count > 0)
            {
                Debug.Log($"Cleaned up {materialsToRemove.Count} unused materials");
            }
        }

        #endregion

        #region Helper Methods

        private string ExtractAssetName(string path)
        {
            int lastSlashIndex = path.LastIndexOf('/');
            return lastSlashIndex >= 0 ? path.Substring(lastSlashIndex + 1) : path;
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current material quality
        /// </summary>
        public MaterialQuality GetCurrentQuality()
        {
            return currentQuality;
        }

        /// <summary>
        /// Get material by cache key
        /// </summary>
        public Material GetMaterial(string cacheKey)
        {
            return materialCache.GetValueOrDefault(cacheKey);
        }

        /// <summary>
        /// Get material usage statistics
        /// </summary>
        public Dictionary<MaterialType, int> GetMaterialUsage()
        {
            return new Dictionary<MaterialType, int>(materialUsage);
        }

        /// <summary>
        /// Get total texture memory usage
        /// </summary>
        public long GetTextureMemoryUsage()
        {
            return totalTextureMemory;
        }

        /// <summary>
        /// Force cleanup of all materials
        /// </summary>
        public void ForceCleanup()
        {
            materialCache.Clear();
            textureCache.Clear();
            activeMaterialCount = 0;
            totalTextureMemory = 0;

            foreach (var key in materialUsage.Keys.ToArray())
            {
                materialUsage[key] = 0;
            }

            Resources.UnloadUnusedAssets();
            Debug.Log("Forced cleanup of all materials and textures");
        }

        /// <summary>
        /// Get material system statistics
        /// </summary>
        public string GetMaterialStatistics()
        {
            return $"Quality: {currentQuality}, " +
                   $"Materials: {activeMaterialCount}/{config.maxMaterialInstances}, " +
                   $"Textures: {textureCache.Count}, " +
                   $"Memory: {totalTextureMemory / (1024 * 1024):F1}MB";
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(790, 10, 250, 300));
            
            GUILayout.Label("=== MATERIAL SYSTEM ===");
            GUILayout.Label($"Quality: {currentQuality}");
            GUILayout.Label($"Materials: {activeMaterialCount}/{config.maxMaterialInstances}");
            GUILayout.Label($"Textures: {textureCache.Count}");
            GUILayout.Label($"Memory: {totalTextureMemory / (1024 * 1024):F1}MB");
            
            GUILayout.Space(10);
            GUILayout.Label("=== MATERIAL USAGE ===");
            
            foreach (var kvp in materialUsage.Take(4))
            {
                GUILayout.Label($"{kvp.Key}: {kvp.Value}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== QUALITY CONTROL ===");
            
            if (GUILayout.Button("Low Quality"))
                SetMaterialQuality(MaterialQuality.Low);
            
            if (GUILayout.Button("Medium Quality"))
                SetMaterialQuality(MaterialQuality.Medium);
            
            if (GUILayout.Button("High Quality"))
                SetMaterialQuality(MaterialQuality.High);
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            if (GUILayout.Button("Force Cleanup"))
                ForceCleanup();
            
            if (GUILayout.Button("Create Test Materials"))
            {
                CreateMaterial(MaterialType.Character, "TestCharacter");
                CreateMaterial(MaterialType.Environment, "TestEnvironment");
                CreateMaterial(MaterialType.Effect, "TestEffect");
            }
            
            config.enableMaterialSharing = GUILayout.Toggle(config.enableMaterialSharing, "Material Sharing");
            
            GUILayout.EndArea();
        }

        #endregion
    }
}
