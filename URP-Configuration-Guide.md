# Universal Render Pipeline (URP) Configuration Guide

## Overview

This guide explains the URP configuration for Whisker King, optimized for mobile performance while maintaining visual quality. The setup includes three quality presets that automatically adapt based on device capabilities.

## Quality Presets

### Low Quality (Target: Snapdragon 660, A10 Fusion)
- **Render Scale:** 0.8x (reduces resolution by 20%)
- **MSAA:** Disabled (1x)
- **HDR:** Disabled
- **Shadows:** Disabled
- **Additional Lights:** 0
- **Shadow Distance:** 25 meters
- **Shadow Cascades:** 1
- **Target Frame Rate:** 30 FPS
- **Memory Target:** ≤512MB

**Use Case:** Entry-level devices, ensures stable performance

### Medium Quality (Target: Snapdragon 855, A12 Bionic)  
- **Render Scale:** 1.0x (native resolution)
- **MSAA:** 2x
- **HDR:** Enabled
- **Shadows:** Enabled (1024 resolution)
- **Additional Lights:** 4 per object
- **Shadow Distance:** 50 meters
- **Shadow Cascades:** 2
- **Target Frame Rate:** 60 FPS
- **Memory Target:** ≤768MB

**Use Case:** Mid-range devices, balanced quality/performance

### High Quality (Target: Snapdragon 8 Gen 1, A14 Bionic+)
- **Render Scale:** 1.0x (native resolution)
- **MSAA:** 4x  
- **HDR:** Enabled with higher precision
- **Shadows:** Enabled (2048 resolution)
- **Additional Lights:** 8 per object
- **Shadow Distance:** 100 meters
- **Shadow Cascades:** 4
- **Target Frame Rate:** 60 FPS
- **Memory Target:** ≤1GB

**Use Case:** High-end devices, maximum visual fidelity

## Key Features

### Automatic Quality Adjustment
The `QualityManager` script monitors real-time performance and automatically adjusts quality settings:

- **Frame Rate Monitoring:** Samples 30 frames every 2 seconds
- **Dynamic Switching:** Changes quality based on performance thresholds
- **Device Detection:** Analyzes GPU, RAM, and CPU to set initial quality

### Performance Thresholds
- **High Quality:** Requires >65 FPS sustained
- **Medium Quality:** Requires >55 FPS sustained  
- **Low Quality:** Used when FPS <45

### Mobile Optimizations

#### Rendering Pipeline
- **Forward Rendering:** Optimized for mobile GPUs
- **Depth Texture:** Disabled for low/medium quality
- **Opaque Texture:** Disabled to save memory
- **Reflection Probes:** Limited blending on lower settings

#### Shadow Configuration
- **Soft Shadows:** Only on high quality
- **Shadow Bias:** Optimized to prevent shadow acne
- **Cascade Borders:** Minimized for smooth transitions
- **Shadow Near Plane Offset:** Set to 3 for stability

#### Texture Settings
- **Blue Noise:** Used for dithering and temporal effects
- **Bayer Matrix:** Used for screen-space techniques
- **Format:** Compressed textures with mipmaps
- **Filtering:** Bilinear filtering for performance

## Implementation Steps

### 1. Import URP Assets
Copy the three URP asset files to `Assets/Settings/URP/`:
- `URP-LowQuality.asset`
- `URP-MediumQuality.asset` 
- `URP-HighQuality.asset`

### 2. Configure Quality Settings
1. Open **Edit > Project Settings > Quality**
2. Create three quality levels matching the URP assets
3. Assign each URP asset to its corresponding quality level
4. Set **Medium Quality** as default

### 3. Add Quality Manager
1. Create an empty GameObject named "QualityManager"
2. Add the `QualityManager` script component
3. Assign the three URP assets to the script fields
4. Configure performance thresholds as needed

### 4. Configure Project Settings
1. **Graphics Settings:**
   - Set **Scriptable Render Pipeline Settings** to Medium Quality URP asset
   - Enable **SRP Batcher** for draw call batching
   - Set **Shader Stripping** to remove unused variants

2. **Player Settings:**
   - Set **Color Space** to Linear (if supported) or Gamma
   - Enable **Multithreaded Rendering**
   - Set **Graphics API** to OpenGL ES 3.1+ or Vulkan

### 5. Platform-Specific Settings

#### WebGL
- Disable **Depth Texture** and **Opaque Texture**
- Use **ASTC** or **DXT** texture compression
- Limit texture resolution to 1024x1024
- Enable **Crunch Compression** for additional savings

#### Android
- Use **ASTC 6x6** compression for textures
- Enable **Vulkan API** on supported devices
- Set **Texture Streaming** for large textures
- Configure **APK Expansion** for asset delivery

#### iOS
- Use **ASTC 6x6** compression
- Enable **Metal API**
- Configure **On Demand Resources** for streaming
- Set appropriate **Target Device Family**

## Performance Budgets

### Draw Calls per Frame
- **Low Quality:** ≤200 draw calls
- **Medium Quality:** ≤300 draw calls  
- **High Quality:** ≤500 draw calls

### Triangle Budget
- **Low Quality:** ≤50,000 triangles
- **Medium Quality:** ≤100,000 triangles
- **High Quality:** ≤200,000 triangles

### Texture Memory
- **Low Quality:** ≤256MB
- **Medium Quality:** ≤384MB
- **High Quality:** ≤512MB

## Monitoring and Debugging

### Built-in Profiler
Use Unity's Profiler to monitor:
- **Rendering:** Draw calls, triangles, texture memory
- **Memory:** Heap usage, GC allocations
- **CPU:** Script execution time, rendering time

### Custom Performance Metrics
The QualityManager provides:
- Real-time frame rate monitoring
- Automatic quality adjustment logging
- Device capability detection
- Memory usage tracking

### Debug Console Commands
```csharp
// Manual quality control
QualityManager.SetLowQuality();
QualityManager.SetMediumQuality(); 
QualityManager.SetHighQuality();

// Toggle auto-adjustment
QualityManager.SetAutoQualityAdjustment(false);
```

## Troubleshooting

### Common Issues

1. **Low Frame Rate on High-End Devices**
   - Check if thermal throttling is occurring
   - Verify texture compression is enabled
   - Ensure SRP Batcher is working (check Frame Debugger)

2. **Memory Warnings on Mobile**
   - Reduce texture quality in Quality Manager
   - Enable more aggressive mipmap bias
   - Implement texture streaming for large assets

3. **Visual Artifacts**
   - Check shadow bias settings for shadow acne
   - Verify normal bias for Peter Panning
   - Adjust cascade splits for smooth shadow transitions

### Performance Tips

1. **Use SRP Batcher:** Ensure materials are compatible
2. **GPU Instancing:** For repeated objects (crates, collectibles)
3. **Culling:** Implement frustum and occlusion culling
4. **LOD System:** Use appropriate LOD bias for distance
5. **Texture Streaming:** Load textures based on distance/importance

## Testing Checklist

- [ ] All three quality levels load without errors
- [ ] Automatic quality adjustment works on target devices
- [ ] Frame rate stays within target ranges
- [ ] Memory usage stays within budgets
- [ ] Shadow quality is appropriate for each level
- [ ] Texture quality scales properly
- [ ] Build sizes are within limits for each platform

## Platform-Specific Notes

### WebGL Performance
- Expect 20-30% performance decrease compared to native mobile
- Use **WebGL 2.0** with fallback to **WebGL 1.0**
- Implement progressive loading for large scenes
- Use **KTX2/Basis Universal** textures for best compression

### Mobile Considerations
- Test on actual devices, not just simulators
- Monitor thermal throttling during extended play sessions
- Implement pause/resume handling for quality adjustment
- Consider battery usage impact of different quality levels
