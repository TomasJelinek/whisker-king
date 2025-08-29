# Whisker King - Quality Settings Guide

This guide covers the complete quality management system for Whisker King, including automatic device detection, quality presets, and performance optimization strategies aligned with PRD specifications.

## Overview

The quality system is designed to provide optimal performance across a wide range of devices while maintaining visual fidelity appropriate for each device tier. The system automatically detects device capabilities and applies appropriate quality settings to meet the PRD performance targets.

## Performance Targets (from PRD)

### Device Categories

#### Low Quality - Minimum Spec Devices
- **Target Devices:** Snapdragon 660+, A10 Fusion+, 3GB RAM
- **Performance Target:** 30 FPS minimum, ≤512MB memory
- **Graphics:** OpenGL ES 3.1, basic lighting
- **Use Case:** Entry-level smartphones, older devices

#### Medium Quality - Recommended Spec Devices  
- **Target Devices:** Snapdragon 855+, A12 Bionic+, 4GB RAM
- **Performance Target:** 60 FPS, ≤768MB memory
- **Graphics:** Vulkan/Metal preferred, enhanced effects
- **Use Case:** Mid-range to high-end smartphones

#### High Quality - High-End Devices
- **Target Devices:** Snapdragon 8 Gen 1+, A14 Bionic+, 6GB+ RAM
- **Performance Target:** 60 FPS, ≤1GB memory
- **Graphics:** Full feature set, advanced effects
- **Use Case:** Flagship smartphones, tablets, desktop

## Quality Settings Architecture

### System Components

#### 1. QualityLevelConfiguration
- **ScriptableObject-based** configuration system
- **Three quality presets** with detailed parameters
- **Performance budgets** aligned with PRD targets
- **Platform-specific optimizations**

#### 2. QualityManager (Runtime)
- **Automatic device detection** based on hardware specs
- **Dynamic quality adjustment** based on performance
- **Real-time performance monitoring** with 30-frame sampling
- **Graceful quality downgrading** when performance drops

#### 3. QualitySettingsEditor (Editor Tools)
- **Unity menu integration** for easy access
- **Validation tools** for performance compliance
- **Testing utilities** for each quality level
- **Performance estimation** tools

### File Structure
```
Assets/
├── Scripts/Core/
│   ├── QualityLevelConfiguration.cs    # Quality presets configuration
│   └── QualityManager.cs               # Runtime quality management
├── Scripts/Editor/
│   └── QualitySettingsEditor.cs        # Editor tools and validation
├── Settings/
│   ├── DefaultQualityConfig.asset      # Default quality configuration
│   └── URP/                           # Quality-specific URP assets
│       ├── URP-LowQuality.asset
│       ├── URP-MediumQuality.asset
│       └── URP-HighQuality.asset
```

## Quality Level Specifications

### Low Quality Settings

#### Target Specifications
- **Frame Rate:** 30 FPS target (minimum acceptable)
- **Memory Budget:** 512MB total, 256MB textures
- **Target Devices:** Snapdragon 660, Adreno 512, 3GB RAM

#### Rendering Configuration
```csharp
renderScale: 0.8f                    // 80% resolution for performance
enableHDR: false                     // Disable HDR to save memory
antiAliasing: None                   // No MSAA for performance
anisotropicFiltering: 0              // Disabled
shadowQuality: Disabled              // No shadows
textureQuality: Quarter Resolution   // 1/4 texture size
lodBias: 0.7f                       // More aggressive LOD
pixelLightCount: 1                   // Single directional light
particleRaycastBudget: 256          // Reduced particle budget
vSyncCount: 0                       // Disabled for max performance
```

#### URP Settings (Low Quality)
- **MSAA:** Disabled (1x)
- **Render Scale:** 0.8x
- **Shadow Distance:** 25 meters
- **Shadow Cascades:** 1
- **Additional Lights:** 0
- **Post-Processing:** Disabled

### Medium Quality Settings

#### Target Specifications  
- **Frame Rate:** 60 FPS target
- **Memory Budget:** 768MB total, 384MB textures
- **Target Devices:** Snapdragon 855, Adreno 640, 4GB RAM

#### Rendering Configuration
```csharp
renderScale: 1.0f                    // Native resolution
enableHDR: true                      // HDR enabled
antiAliasing: MSAA2x                 // 2x MSAA
anisotropicFiltering: 2              // 2x anisotropic
shadowQuality: All                   // Shadows enabled
shadowResolution: Medium             // 1024x1024 shadows
textureQuality: Half Resolution      // 1/2 texture size
lodBias: 1.0f                       // Standard LOD
pixelLightCount: 4                   // Multiple lights
particleRaycastBudget: 1024         // Standard particle budget
vSyncCount: 1                       // VSync enabled
```

#### URP Settings (Medium Quality)
- **MSAA:** 2x
- **Render Scale:** 1.0x
- **Shadow Distance:** 50 meters
- **Shadow Cascades:** 2
- **Additional Lights:** 4 per object
- **Post-Processing:** Basic (Vignette only)

### High Quality Settings

#### Target Specifications
- **Frame Rate:** 60 FPS target  
- **Memory Budget:** 1024MB total, 512MB textures
- **Target Devices:** Snapdragon 8 Gen 1, Adreno 730, 6GB+ RAM

#### Rendering Configuration
```csharp
renderScale: 1.0f                    // Native resolution
enableHDR: true                      // HDR enabled
antiAliasing: MSAA4x                 // 4x MSAA
anisotropicFiltering: 4              // 4x anisotropic
shadowQuality: All                   // High quality shadows
shadowResolution: High               // 2048x2048 shadows
textureQuality: Full Resolution      // Full texture size
lodBias: 1.2f                       // Enhanced LOD detail
pixelLightCount: 8                   // Multiple dynamic lights
particleRaycastBudget: 2048         // High particle budget
vSyncCount: 1                       // VSync enabled
```

#### URP Settings (High Quality)
- **MSAA:** 4x
- **Render Scale:** 1.0x
- **Shadow Distance:** 100 meters
- **Shadow Cascades:** 4
- **Additional Lights:** 8 per object
- **Post-Processing:** Full (Bloom, Vignette)

## Device Detection Algorithm

### Hardware Assessment
The system analyzes device specifications to determine appropriate quality:

#### Memory Evaluation
```csharp
// High-end: 4GB+ RAM, 6+ CPU cores
if (memorySize >= 4096 && processorCount >= 6)
    
// Medium: 3GB+ RAM, 4+ CPU cores  
if (memorySize >= 3072 && processorCount >= 4)
    
// Low-end: 2GB+ RAM, 4+ CPU cores (minimum spec)
if (memorySize >= 2048 && processorCount >= 4)
```

#### GPU Detection (Android)
```csharp
// High-end Adreno GPUs
"adreno 730", "adreno 740", "adreno 650", "adreno 660"

// Medium Adreno GPUs  
"adreno 630", "adreno 640", "adreno 530", "adreno 540"

// Low-end Adreno GPUs
"adreno 510", "adreno 512", "adreno 506", "adreno 508"
```

#### GPU Detection (iOS)
```csharp
// High-end Apple GPUs
"apple a14", "apple a15", "apple a16", "apple a17", "apple m1", "apple m2"

// Medium Apple GPUs
"apple a12", "apple a13"

// Low-end Apple GPUs  
"apple a10", "apple a11"
```

### Fallback Strategy
- **Unknown devices:** Default to Low Quality
- **Below minimum spec:** Force Low Quality with warning
- **Performance monitoring:** Automatic downgrade if targets not met

## Runtime Performance Management

### Automatic Quality Adjustment

#### Performance Monitoring
```csharp
// Sample frame times over 30 frames
frameTimeBuffer = new float[30];

// Check performance every 2 seconds
frameRateCheckInterval = 2.0f;

// Quality adjustment thresholds
lowQualityThreshold = 45f;    // Downgrade to Low if below 45 FPS
mediumQualityThreshold = 55f;  // Downgrade to Medium if below 55 FPS
highQualityThreshold = 65f;    // Upgrade to High if above 65 FPS
```

#### Dynamic Adjustment Logic
1. **Monitor average FPS** over 2-second intervals
2. **Compare against thresholds** for current quality level
3. **Downgrade quality** if performance is poor
4. **Upgrade quality** if performance is excellent (with hysteresis)
5. **Apply new settings** and log changes

### Memory Management
- **Texture streaming** based on quality level
- **Asset LOD selection** based on performance budget
- **Garbage collection** optimization for mobile
- **Object pooling** for frequently spawned objects

## Usage Guide

### Setup and Configuration

#### 1. Initial Setup
```csharp
// Access via Unity menu
WhiskerKing → Quality → Setup Default Quality Levels

// Or programmatically
QualityLevelConfiguration.SetupDefaultQualityLevelsMenuItem();
```

#### 2. Testing Quality Levels
```csharp
// Test individual quality levels
WhiskerKing → Quality → Test Low Quality
WhiskerKing → Quality → Test Medium Quality  
WhiskerKing → Quality → Test High Quality

// Performance testing
WhiskerKing → Quality → Performance Test
```

#### 3. Validation
```csharp
// Validate current settings against PRD targets
WhiskerKing → Quality → Validate Current Settings

// Open quality settings inspector
WhiskerKing → Quality → Open Quality Settings Window
```

### Runtime Usage

#### Automatic Quality Management
```csharp
// Add QualityManager to scene
var qualityManager = gameObject.AddComponent<QualityManager>();

// Configure performance thresholds
qualityManager.targetFrameRate = 60f;
qualityManager.lowQualityThreshold = 45f;
qualityManager.enableAutoQualityAdjustment = true;
```

#### Manual Quality Control
```csharp
// Get quality manager instance
var qualityManager = FindObjectOfType<QualityManager>();

// Set specific quality level
qualityManager.SetLowQuality();
qualityManager.SetMediumQuality();
qualityManager.SetHighQuality();

// Toggle automatic adjustment
qualityManager.SetAutoQualityAdjustment(false);
```

## Performance Optimization Strategies

### Texture Management
- **Quality-based resolution:** Full/Half/Quarter resolution textures
- **Compression:** ASTC for mobile, DXT for desktop
- **Streaming:** Load textures based on distance and importance
- **Mipmapping:** Automatic mipmap generation with quality-based bias

### Shadow Optimization
```csharp
// Low Quality: Shadows disabled
shadowQuality = ShadowQuality.Disable;

// Medium Quality: Basic shadows
shadowResolution = ShadowResolution.Medium; // 1024x1024
shadowCascades = 2;
shadowDistance = 50f;

// High Quality: Enhanced shadows
shadowResolution = ShadowResolution.High;   // 2048x2048  
shadowCascades = 4;
shadowDistance = 100f;
```

### Lighting Strategy
- **Low Quality:** Single directional light, no real-time lighting
- **Medium Quality:** Directional + 4 additional lights per object
- **High Quality:** Directional + 8 additional lights per object
- **Reflection Probes:** Disabled on Low, enabled on Medium/High

### Post-Processing Pipeline
- **Low Quality:** All post-processing disabled
- **Medium Quality:** Essential effects only (Vignette)
- **High Quality:** Full post-processing stack (Bloom, Vignette)
- **Motion Blur:** Disabled across all levels (motion sickness concerns)

## Platform-Specific Considerations

### WebGL Optimizations
- **Memory constraints:** More aggressive texture compression
- **JavaScript limitations:** Simplified shader variants
- **Browser compatibility:** Fallback for older browsers
- **Loading performance:** Progressive asset loading

### Mobile Optimizations
- **Thermal throttling:** Automatic quality reduction when overheating
- **Battery usage:** Lower quality settings for extended gameplay
- **App backgrounding:** Pause quality monitoring when inactive
- **Platform-specific APIs:** Use Vulkan/Metal when available

### Desktop Considerations
- **Higher performance baseline:** Start with High quality
- **Resolution scaling:** Support for high-DPI displays
- **Graphics driver optimization:** Leverage desktop GPU features
- **Multi-monitor support:** Quality settings per display

## Debugging and Profiling

### Quality Debug Information
```csharp
// Enable debug logging
Debug.Log($"Current Quality: {qualityManager.GetCurrentQuality()}");
Debug.Log($"Device Detected: {DetermineDeviceQuality()}");
Debug.Log($"Performance: {averageFrameRate:F1} FPS");
Debug.Log($"Memory Usage: {Profiler.GetTotalAllocatedMemory()} bytes");
```

### Performance Metrics
- **Frame rate monitoring:** Real-time FPS display
- **Memory tracking:** Heap usage and GC pressure
- **Draw call counting:** Rendering performance metrics
- **Triangle counting:** Geometry complexity measurement

### Validation Tools
- **Budget compliance:** Verify settings meet performance targets
- **Device testing:** Test on actual target devices
- **Automated testing:** CI/CD integration for performance validation
- **A/B testing:** Compare quality settings impact on user retention

## Troubleshooting

### Common Issues

#### Performance Below Targets
1. **Check device specifications** - May be below minimum requirements
2. **Verify quality settings** - Ensure appropriate level is selected
3. **Profile memory usage** - Check for memory leaks or excessive allocations
4. **Analyze rendering** - Look for excessive draw calls or triangles

#### Visual Quality Issues
1. **Texture compression artifacts** - Adjust compression settings
2. **Shadow artifacts** - Tune shadow bias and normal bias
3. **LOD popping** - Adjust LOD bias and transition distances
4. **Aliasing issues** - Increase MSAA or use post-process AA

#### Memory Issues
1. **Texture memory exceeded** - Reduce texture quality or resolution
2. **Frequent garbage collection** - Optimize object allocation patterns
3. **Memory leaks** - Check for unreleased resources
4. **Asset loading spikes** - Implement progressive loading

### Performance Testing Checklist
- [ ] Test all three quality levels on target devices
- [ ] Verify frame rate meets targets for each quality level
- [ ] Confirm memory usage stays within budgets
- [ ] Test automatic quality adjustment under stress
- [ ] Validate loading times for different quality settings
- [ ] Check visual quality is appropriate for each level

## Integration with Other Systems

### Build System Integration
- Quality settings are automatically applied during builds
- Platform-specific optimizations are included
- Asset variants are created for each quality level

### Save System Integration
- Player quality preferences are saved
- Automatic quality adjustments are remembered
- Quality overrides for specific levels are supported

### Analytics Integration
- Quality level usage is tracked
- Performance metrics are collected
- User satisfaction with quality settings is monitored

This quality settings system ensures Whisker King delivers optimal performance across all target devices while maintaining the visual standards required for an engaging gaming experience.
