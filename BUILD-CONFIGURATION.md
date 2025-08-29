# Whisker King - Build Configuration Guide

This guide covers the complete build setup for Whisker King across all target platforms: WebGL, Android, and iOS. The build system is designed to meet the PRD performance targets while maintaining cross-platform compatibility.

## Overview

The Whisker King build system includes:
- **Automated build scripts** via Unity Editor menu
- **Platform-specific optimizations** for each target
- **Performance validation** and quality assurance
- **Configurable build options** for development and release

## Target Platforms & Specifications

### WebGL (Primary Platform)
- **Target Devices:** Desktop browsers, mobile browsers
- **Performance:** 60 FPS on mid-range devices
- **Memory Budget:** ≤512MB
- **Load Time:** ≤10 seconds initial, ≤5 seconds level
- **Compression:** Brotli for optimal file size
- **Graphics API:** WebGL 2.0 with WebGL 1.0 fallback

### Android (Mobile Platform)
- **Minimum Specs:** Snapdragon 660, 3GB RAM, OpenGL ES 3.1
- **Recommended Specs:** Snapdragon 855, 4GB RAM, Vulkan 1.1
- **Target SDK:** Android API 33 (Android 13)
- **Minimum SDK:** Android API 21 (Android 5.0)
- **Architecture:** ARM64 primary, ARM32 fallback
- **Graphics API:** Vulkan preferred, OpenGL ES 3.0 fallback

### iOS (Mobile Platform)
- **Minimum Specs:** A10 Fusion, 2GB RAM, iOS 12.0+
- **Recommended Specs:** A12 Bionic, 3GB RAM, iOS 14.0+
- **Graphics API:** Metal (exclusive)
- **Architecture:** ARM64 only
- **Deployment:** App Store and TestFlight

## Quick Start

### 1. Access Build Menu
In Unity Editor: **WhiskerKing → Build**

### 2. Available Build Options
- **WebGL Development** - Debug build for testing
- **WebGL Release** - Optimized build for production
- **Android Development** - Debug APK for testing
- **Android Release** - Store-ready APK/AAB
- **iOS Development** - Debug Xcode project
- **iOS Release** - App Store ready Xcode project
- **All Platforms Development** - Build all platforms for testing

### 3. Validation
Before building: **WhiskerKing → Validate → Check Build Settings**

## Platform-Specific Configuration

### WebGL Configuration

#### Player Settings
```csharp
// Compression and Memory
PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
PlayerSettings.WebGL.memorySize = 512; // MB
PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

// Performance
PlayerSettings.WebGL.threadsSupport = true;
PlayerSettings.WebGL.wasmStreaming = true;
PlayerSettings.colorSpace = ColorSpace.Linear;
```

#### Graphics APIs
1. **OpenGL ES 3.0** (Primary)
2. **OpenGL ES 2.0** (Fallback)

#### WebGL Template
Use **PROJECT:Better2020** template for:
- Better loading screens
- Mobile-optimized controls
- Memory management
- Error handling

#### Build Sizes (Target)
- **Development Build:** ~50-80MB
- **Release Build:** ~30-50MB (with Brotli compression)

### Android Configuration

#### Player Settings
```csharp
// SDK Configuration
PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel21;
PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

// Scripting
PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
```

#### Graphics APIs
1. **Vulkan** (High-end devices)
2. **OpenGL ES 3.0** (Mid-range devices)
3. **OpenGL ES 2.0** (Legacy fallback)

#### APK vs AAB (Android App Bundle)
- **Development:** APK for easy testing
- **Release:** AAB for Google Play Store
- **Benefits:** 20-30% smaller download size, dynamic delivery

#### Gradle Build Configuration
```gradle
android {
    compileSdkVersion 33
    buildToolsVersion "30.0.3"
    
    defaultConfig {
        minSdkVersion 21
        targetSdkVersion 33
        versionCode 1
        versionName "1.0.0"
    }
    
    buildTypes {
        release {
            minifyEnabled true
            proguardFiles getDefaultProguardFile('proguard-android.txt'), 'proguard-rules.pro'
        }
    }
}
```

### iOS Configuration

#### Player Settings
```csharp
// iOS Target Configuration  
PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
PlayerSettings.iOS.targetOSVersionString = "12.0";

// Scripting
PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
```

#### Graphics APIs
- **Metal** (Exclusive - Apple's modern graphics API)

#### Xcode Project Settings
After Unity build, configure in Xcode:
```xml
<!-- Info.plist additions -->
<key>NSCameraUsageDescription</key>
<string>This app does not use camera</string>
<key>NSMicrophoneUsageDescription</key>
<string>This app does not use microphone</string>
```

#### Code Signing
1. **Development:** Use development certificates
2. **Release:** Use distribution certificates
3. **Provisioning:** Configure team ID in PlayerSettings

## Build Automation

### Using the Build System Script

#### Manual Build (Editor)
```csharp
// Access via Unity menu
WhiskerKing → Build → [Platform] [Type]
```

#### Programmatic Build
```csharp
// In custom editor scripts
BuildSystem.BuildWebGLRelease();
BuildSystem.BuildAndroidDevelopment();
BuildSystem.BuildiOSRelease();
```

### Command Line Builds

#### WebGL
```bash
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildSystem.BuildWebGLRelease -logFile build.log
```

#### Android
```bash
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildSystem.BuildAndroidRelease -logFile build.log
```

#### iOS
```bash
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildSystem.BuildiOSRelease -logFile build.log
```

### CI/CD Integration

#### GitHub Actions Example
```yaml
name: Build Whisker King
on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - uses: game-ci/unity-builder@v2
      with:
        projectPath: .
        targetPlatform: WebGL
        buildMethod: BuildSystem.BuildWebGLRelease
```

## Performance Optimization

### Build Size Optimization

#### WebGL
- **Code Stripping:** Remove unused engine code
- **Texture Compression:** Use ASTC/DXT compression
- **Audio Compression:** Use OGG Vorbis, 96kbps for SFX
- **Asset Bundles:** Load assets on demand

#### Android
- **ProGuard:** Enable for release builds
- **Asset Compression:** Use LZ4HC compression
- **Split APKs:** By architecture if needed
- **Dynamic Delivery:** Use Play Asset Delivery for large assets

#### iOS  
- **Bitcode:** Disabled (deprecated by Apple)
- **Asset Slicing:** Use App Store asset slicing
- **On Demand Resources:** For large assets
- **Symbol Stripping:** Remove debug symbols for release

### Runtime Performance Settings

#### Quality Level Selection
```csharp
// Automatic quality detection based on device
if (SystemInfo.systemMemorySize >= 4096)
    QualitySettings.SetQualityLevel(2); // High
else if (SystemInfo.systemMemorySize >= 3072)
    QualitySettings.SetQualityLevel(1); // Medium
else
    QualitySettings.SetQualityLevel(0); // Low
```

#### Frame Rate Management
```csharp
// Target frame rates by platform
#if UNITY_WEBGL
    Application.targetFrameRate = 60;
#elif UNITY_ANDROID || UNITY_IOS
    Application.targetFrameRate = Screen.currentResolution.refreshRate > 60 ? 60 : 30;
#endif
```

## Testing & Validation

### Pre-Build Checklist
- [ ] All scenes exist and are valid
- [ ] Product name, company, and version are set
- [ ] Bundle identifier is configured for mobile
- [ ] Graphics APIs are correctly configured
- [ ] Quality levels are set up (Low/Medium/High)
- [ ] Performance budgets are validated

### Build Validation Script
```csharp
[MenuItem("WhiskerKing/Validate/Full Build Check")]
public static void FullBuildValidation()
{
    ValidateScenes();
    ValidatePlayerSettings(); 
    ValidateQualitySettings();
    ValidatePerformanceBudgets();
    ValidatePlatformSettings();
}
```

### Performance Testing
After each build:
1. **Frame Rate:** Maintain 60 FPS on target devices
2. **Memory Usage:** Stay within platform budgets
3. **Load Times:** Meet PRD targets (≤10s initial, ≤5s level)
4. **File Size:** Verify compression effectiveness

## Deployment

### WebGL Deployment
1. **Build Location:** `Builds/WebGL/`
2. **Upload to:** Web server with HTTPS
3. **Required Headers:** 
   ```
   Cross-Origin-Embedder-Policy: require-corp
   Cross-Origin-Opener-Policy: same-origin
   ```
4. **CDN Setup:** Use CDN for global distribution

### Android Deployment  
1. **Development:** Install APK directly or via ADB
2. **Testing:** Upload to Google Play Console (Internal Testing)
3. **Release:** Publish AAB to Google Play Store
4. **Requirements:** API level compliance, 64-bit support

### iOS Deployment
1. **Development:** Install via Xcode to device
2. **Testing:** Upload to TestFlight
3. **Release:** Submit to App Store via Xcode
4. **Requirements:** Valid certificates, privacy compliance

## Troubleshooting

### Common WebGL Issues

#### Memory Issues
```
Out of memory error
```
**Solution:** Reduce `PlayerSettings.WebGL.memorySize` or optimize assets

#### Loading Failures
```
WebAssembly instantiation failed
```
**Solution:** Enable WASM streaming, check server MIME types

### Common Android Issues

#### Build Failures
```
CommandInvokationFailure: Unable to convert classes into dex format
```
**Solution:** Enable ProGuard, reduce method count, use multidex

#### Performance Issues
```
Low frame rate on mid-range devices
```
**Solution:** Force OpenGL ES 3.0, disable Vulkan for testing

### Common iOS Issues

#### Provisioning Problems
```
No profiles for team 'TeamID' were found
```
**Solution:** Configure team ID in PlayerSettings, update certificates

#### Metal Validation Errors
```
Metal API Validation Enabled
```
**Solution:** Disable for release builds, fix shader issues

## Build Output Structure

### WebGL Build
```
Builds/WebGL/
├── Build/
│   ├── WhiskerKing.data.gz
│   ├── WhiskerKing.framework.js.gz  
│   ├── WhiskerKing.loader.js
│   └── WhiskerKing.wasm.gz
├── StreamingAssets/
├── TemplateData/
└── index.html
```

### Android Build  
```
Builds/Android/
├── WhiskerKing.apk (Development)
├── WhiskerKing.aab (Release) 
└── symbols.zip (Release)
```

### iOS Build
```
Builds/iOS/
├── Unity-iPhone.xcodeproj/
├── Classes/
├── Libraries/
├── Data/
└── Info.plist
```

## Performance Monitoring

### Analytics Integration
Track key metrics in builds:
- Load times per platform
- Frame rate distribution  
- Memory usage patterns
- Crash rates by device

### Profiler Integration
```csharp
#if DEVELOPMENT_BUILD
    Profiler.enabled = true;
    Profiler.BeginSample("GameLoop");
#endif
```

This build configuration ensures Whisker King meets all PRD performance targets while maintaining high quality across all target platforms.
