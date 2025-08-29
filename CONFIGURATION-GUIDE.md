# Whisker King - Configuration Management Guide

This guide covers the complete configuration management system for Whisker King, including the GameConfig.json structure, runtime configuration management, and environment-specific overrides.

## Overview

The Whisker King configuration system provides centralized management of all game settings, performance targets, and platform-specific configurations. The system is designed to:

- **Centralize settings** - All configuration in one JSON file
- **Support environment overrides** - Different settings for development/testing/production
- **Enable runtime modification** - Configuration can be updated without rebuilding
- **Validate settings** - Automatic validation against PRD requirements
- **Cross-platform compatibility** - Works on all target platforms

## Configuration Architecture

### File Structure
```
Assets/StreamingAssets/Config/
└── GameConfig.json                 # Main configuration file

Assets/Scripts/Core/
└── GameConfiguration.cs            # Runtime configuration manager
```

### Key Components

#### 1. GameConfig.json
- **Location:** `Assets/StreamingAssets/Config/GameConfig.json`
- **Format:** JSON with comprehensive game settings
- **Size:** ~15KB (compact for mobile loading)
- **Encoding:** UTF-8 with validation

#### 2. GameConfiguration.cs
- **Singleton pattern** for global access
- **Async loading** for WebGL compatibility
- **Validation system** with error recovery
- **Environment override** support

## Configuration Structure

### Game Information
```json
{
  "gameInfo": {
    "name": "Whisker King",
    "version": "1.0.0",
    "buildNumber": 1,
    "configVersion": "1.0",
    "lastUpdated": "2024-01-15T10:00:00Z"
  }
}
```

**Purpose:** Basic game metadata and versioning information.
**Usage:** Build validation, analytics, and debugging.

### Performance Targets

#### Frame Rate Configuration
```json
{
  "performanceTargets": {
    "frameRate": {
      "target": 60,
      "minimum": 30,
      "maxFrameTime": 16.67,
      "vSyncEnabled": true
    }
  }
}
```

**PRD Alignment:**
- Target: 60 FPS on recommended devices
- Minimum: 30 FPS on minimum spec devices
- Frame time budget: 16.67ms for 60 FPS

#### Memory Budgets
```json
{
  "memoryBudgets": {
    "lowQuality": {
      "totalHeapMB": 512,
      "textureMB": 256,
      "audioMB": 64,
      "scriptsMB": 128,
      "otherMB": 64
    },
    "mediumQuality": {
      "totalHeapMB": 768,
      "textureMB": 384,
      "audioMB": 96,
      "scriptsMB": 192,
      "otherMB": 96
    },
    "highQuality": {
      "totalHeapMB": 1024,
      "textureMB": 512,
      "audioMB": 128,
      "scriptsMB": 256,
      "otherMB": 128
    }
  }
}
```

**PRD Alignment:**
- Low Quality: ≤512MB total (minimum spec devices)
- Medium Quality: ≤768MB total (recommended devices)
- High Quality: ≤1GB total (high-end devices)

#### Load Time Targets
```json
{
  "loadTimes": {
    "initialLoadMaxSeconds": 10.0,
    "levelLoadMaxSeconds": 5.0,
    "assetStreamingMaxSeconds": 2.0
  }
}
```

**PRD Alignment:**
- Initial load: ≤10 seconds (hub scene)
- Level load: ≤5 seconds (gameplay transition)
- Asset streaming: ≤2 seconds (progressive loading)

### Device Specifications

#### Minimum Specifications
```json
{
  "deviceSpecifications": {
    "minimumSpecs": {
      "android": {
        "chipset": "Snapdragon 660",
        "gpu": "Adreno 512",
        "ramMB": 3072,
        "apiLevel": 21,
        "openGLVersion": "3.1"
      },
      "ios": {
        "chipset": "A10 Fusion",
        "ramMB": 2048,
        "minimumVersion": "12.0",
        "metalSupport": true
      },
      "webgl": {
        "webglVersion": "2.0",
        "webgl1Fallback": true,
        "memoryMB": 512,
        "requiresHTTPS": true
      }
    }
  }
}
```

**Usage:** Automatic quality detection and device compatibility validation.

### Quality Settings

#### Auto Quality Adjustment
```json
{
  "qualitySettings": {
    "autoQualityAdjustment": {
      "enabled": true,
      "checkIntervalSeconds": 2.0,
      "sampleFrames": 30,
      "thresholds": {
        "lowQualityFPS": 45.0,
        "mediumQualityFPS": 55.0,
        "highQualityFPS": 65.0
      }
    }
  }
}
```

**Integration:** Works with QualityManager.cs for dynamic quality adjustment.

### Player Movement Configuration

#### Physics Settings
```json
{
  "playerMovement": {
    "physics": {
      "runSpeed": 8.0,
      "slideSpeed": 10.0,
      "jumpHeight": 3.0,
      "jumpHeightHold": 4.5,
      "doubleJumpHeight": 2.5,
      "gravity": -25.0,
      "pounceGravity": -45.0,
      "airControl": 0.8,
      "groundFriction": 0.85,
      "airFriction": 0.95,
      "bounceDamping": 0.6
    }
  }
}
```

**PRD Alignment:** Exact values from PRD Section 3.1 Player Movement System.

#### Input Configuration
```json
{
  "input": {
    "bufferTimeMS": 120,
    "coyoteTimeMS": 120,
    "slideDurationMS": 600,
    "slideMinCancelTimeMS": 250
  }
}
```

**PRD Alignment:** 
- Input buffer: 120ms for responsive controls
- Coyote time: 120ms for edge forgiveness
- Slide mechanics: 600ms duration, 250ms cancel window

### Combat System Configuration

#### Tail Whip Settings
```json
{
  "combat": {
    "tailWhip": {
      "windupTimeMS": 100,
      "activeTimeMS": 180,
      "recoveryTimeMS": 120,
      "damage": 25.0,
      "stunDuration": 1.5,
      "range": 2.5,
      "angleDegreess": 270.0
    }
  }
}
```

**PRD Alignment:** Exact values from PRD Section 3.2 Combat & Interaction System.

### Camera Configuration

#### Follow Mode Settings
```json
{
  "cameraSettings": {
    "followMode": {
      "distance": 8.0,
      "height": 3.0,
      "damping": 5.0,
      "lookAheadDistance": 4.0,
      "lookAheadHeight": 2.0,
      "lookAheadDamping": 3.0
    },
    "chaseMode": {
      "fov": 85.0,
      "distance": 6.0,
      "height": 2.5
    }
  }
}
```

**PRD Alignment:** Values from PRD Section 3.3 Camera System.

### Level Design Configuration

#### Collectible Settings
```json
{
  "levelDesign": {
    "collectibles": {
      "fishTreats": {
        "minimumPerLevel": 50,
        "mainPathPercentage": 60,
        "sideRoutesPercentage": 30,
        "hiddenPercentage": 10,
        "spacingMeters": 3.0
      },
      "goldenMouseTokens": {
        "perLevel": 3,
        "earlyPercentage": 30,
        "middlePercentage": 40,
        "latePercentage": 30
      },
      "yarn": {
        "totalPerLevel": 300,
        "conversionRatio": 10,
        "crateAmount": 20
      }
    }
  }
}
```

**PRD Alignment:** Values from PRD Section 4.2 Collectible Placement Rules.

### Audio Configuration

#### Category Settings
```json
{
  "audioSettings": {
    "categories": {
      "master": {
        "defaultVolume": 1.0,
        "maxSimultaneousSounds": 32
      },
      "music": {
        "defaultVolume": 0.8,
        "maxSimultaneousSounds": 2
      },
      "sfxPlayer": {
        "defaultVolume": 1.0,
        "maxSimultaneousSounds": 8
      }
    },
    "spatialAudio": {
      "enabled": true,
      "maxDistance": 50.0,
      "rolloffMode": "Logarithmic"
    }
  }
}
```

**Integration:** Works with AudioManager system for centralized audio control.

## Runtime Usage

### Basic Configuration Access
```csharp
// Get configuration instance
var config = GameConfiguration.Instance;

// Access performance targets
float targetFPS = config.GetTargetFrameRate();
int memoryBudget = config.GetMemoryBudget(QualityLevel.Medium);

// Access gameplay settings
var playerMovement = config.GetPlayerMovement();
var cameraSettings = config.GetCameraSettings();
```

### Configuration Loading
```csharp
public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Configuration loads automatically on Awake
        GameConfiguration.Instance.OnConfigurationLoaded += OnConfigLoaded;
        GameConfiguration.Instance.OnConfigurationError += OnConfigError;
    }

    private void OnConfigLoaded()
    {
        Debug.Log("Game configuration loaded successfully");
        InitializeGameSystems();
    }

    private void OnConfigError(string error)
    {
        Debug.LogError($"Configuration error: {error}");
        // Fallback to default settings
    }
}
```

### Integration with Game Systems

#### Quality Manager Integration
```csharp
public class QualityManager : MonoBehaviour
{
    void Start()
    {
        var config = GameConfiguration.Instance;
        
        // Configure automatic quality adjustment
        enableAutoQualityAdjustment = config.Config.qualitySettings.autoQualityAdjustment.enabled;
        frameRateCheckInterval = config.Config.qualitySettings.autoQualityAdjustment.checkIntervalSeconds;
        
        // Set performance thresholds
        lowQualityThreshold = config.Config.qualitySettings.autoQualityAdjustment.thresholds.lowQualityFPS;
        mediumQualityThreshold = config.Config.qualitySettings.autoQualityAdjustment.thresholds.mediumQualityFPS;
        highQualityThreshold = config.Config.qualitySettings.autoQualityAdjustment.thresholds.highQualityFPS;
    }
}
```

#### Player Controller Integration
```csharp
public class PlayerController : MonoBehaviour
{
    void Start()
    {
        var config = GameConfiguration.Instance;
        var movement = config.GetPlayerMovement();
        
        // Configure physics
        runSpeed = movement.physics.runSpeed;
        jumpHeight = movement.physics.jumpHeight;
        gravity = movement.physics.gravity;
        
        // Configure input
        inputBufferTime = movement.input.bufferTimeMS / 1000f;
        coyoteTime = movement.input.coyoteTimeMS / 1000f;
    }
}
```

## Environment Overrides

### Override System
The configuration supports environment-specific overrides for development, testing, and production builds.

```json
{
  "metaSettings": {
    "environmentOverrides": {
      "development": {
        "debugSettings.developmentBuild": true,
        "debugSettings.profilerEnabled": true,
        "debugSettings.cheatCodesEnabled": true
      },
      "testing": {
        "analyticsSettings.enabled": false,
        "debugSettings.performanceOverlay": true
      },
      "production": {
        "debugSettings.developmentBuild": false,
        "debugSettings.profilerEnabled": false,
        "debugSettings.cheatCodesEnabled": false
      }
    }
  }
}
```

### Environment Detection
The system automatically detects the current environment:
- **Development:** Debug builds and Unity Editor
- **Testing:** Development builds with DEVELOPMENT_BUILD define
- **Production:** Release builds without debug flags

### Custom Overrides
```csharp
// Apply custom configuration override
GameConfiguration.Instance.Config.performanceTargets.frameRate.target = 30;
```

## Configuration Management

### Validation System

#### Automatic Validation
```csharp
public class GameConfiguration : MonoBehaviour
{
    private void ValidateConfiguration()
    {
        // Frame rate validation
        if (Config.performanceTargets.frameRate.target <= 0)
        {
            Debug.LogWarning("Invalid frame rate, using default");
            Config.performanceTargets.frameRate.target = 60;
        }

        // Memory budget validation
        ValidateMemoryBudgets();
        
        // Platform compatibility validation
        ValidateDeviceSpecs();
    }
}
```

#### Manual Validation
```csharp
// In Unity Editor
GameConfiguration.Instance.ValidateConfigurationEditor();

// Runtime validation
if (!GameConfiguration.Instance.IsConfigurationValid())
{
    GameConfiguration.Instance.ReloadConfiguration();
}
```

### Configuration Updates

#### Hot Reloading (Development)
```csharp
#if UNITY_EDITOR
[MenuItem("WhiskerKing/Configuration/Reload Configuration")]
public static void ReloadConfiguration()
{
    GameConfiguration.Instance.ReloadConfiguration();
}
#endif
```

#### Runtime Modification
```csharp
// Update performance target
GameConfiguration.Instance.Config.performanceTargets.frameRate.target = newFrameRate;

// Notify systems of changes
GameConfiguration.Instance.OnConfigurationLoaded?.Invoke();
```

## Platform-Specific Considerations

### WebGL Configuration
```json
{
  "buildSettings": {
    "platforms": {
      "webgl": {
        "compressionFormat": "Brotli",
        "memorySize": 512,
        "exceptionSupport": false,
        "threading": true,
        "wasm": true
      }
    }
  }
}
```

### Mobile Configuration
```json
{
  "uiSettings": {
    "mobile": {
      "touchControlsEnabled": true,
      "gestureRecognition": true,
      "hapticFeedbackEnabled": true
    },
    "display": {
      "safeAreaEnabled": true,
      "notchSupport": true,
      "orientationLock": "Landscape"
    }
  }
}
```

### Desktop Configuration
```json
{
  "performanceTargets": {
    "frameRate": {
      "target": 60,
      "minimum": 60,
      "vSyncEnabled": false
    }
  }
}
```

## Development Tools

### Unity Editor Integration

#### Configuration Inspector
```csharp
#if UNITY_EDITOR
[CustomEditor(typeof(GameConfiguration))]
public class GameConfigurationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        if (GUILayout.Button("Reload Configuration"))
        {
            ((GameConfiguration)target).ReloadConfiguration();
        }
        
        if (GUILayout.Button("Validate Configuration"))
        {
            ((GameConfiguration)target).ValidateConfigurationEditor();
        }
        
        if (GUILayout.Button("Log Configuration Summary"))
        {
            ((GameConfiguration)target).LogConfigurationSummary();
        }
    }
}
#endif
```

#### Menu Integration
```csharp
public static class ConfigurationMenu
{
    [MenuItem("WhiskerKing/Configuration/Reload")]
    public static void ReloadConfiguration()
    {
        GameConfiguration.Instance?.ReloadConfiguration();
    }

    [MenuItem("WhiskerKing/Configuration/Validate")]
    public static void ValidateConfiguration()
    {
        GameConfiguration.Instance?.ValidateConfigurationEditor();
    }

    [MenuItem("WhiskerKing/Configuration/Export Template")]
    public static void ExportConfigurationTemplate()
    {
        // Export a template configuration file
        var template = GameConfiguration.CreateDefaultConfiguration();
        var json = JsonConvert.SerializeObject(template, Formatting.Indented);
        File.WriteAllText("GameConfig_Template.json", json);
    }
}
```

### Configuration Validation Tools

#### PRD Compliance Checker
```csharp
public static class PRDComplianceChecker
{
    public static bool ValidatePRDCompliance(GameConfigData config)
    {
        bool isCompliant = true;
        
        // Frame rate targets
        if (config.performanceTargets.frameRate.target < 60)
        {
            Debug.LogWarning("Target frame rate below PRD requirement (60 FPS)");
            isCompliant = false;
        }
        
        // Memory budgets
        if (config.performanceTargets.memoryBudgets.lowQuality.totalHeapMB > 512)
        {
            Debug.LogWarning("Low quality memory budget exceeds PRD limit (512MB)");
            isCompliant = false;
        }
        
        // Load times
        if (config.performanceTargets.loadTimes.initialLoadMaxSeconds > 10.0f)
        {
            Debug.LogWarning("Initial load time exceeds PRD requirement (10s)");
            isCompliant = false;
        }
        
        return isCompliant;
    }
}
```

## Best Practices

### Configuration Design
1. **Keep it centralized** - All settings in one place
2. **Use meaningful names** - Clear, descriptive keys
3. **Document defaults** - Comment rationale for values
4. **Version configuration** - Track changes over time
5. **Validate early** - Check configuration at startup

### Performance Considerations
1. **Load once** - Cache configuration at startup
2. **Minimize parsing** - Use efficient JSON parsing
3. **Avoid frequent updates** - Configuration should be relatively static
4. **Consider memory** - Large configurations impact memory usage

### Team Collaboration
1. **Version control** - Track configuration changes
2. **Code reviews** - Review configuration changes like code
3. **Documentation** - Keep documentation up to date
4. **Testing** - Test configuration changes on all platforms

### Deployment Strategy
1. **Environment separation** - Different configs per environment
2. **Rollback capability** - Keep previous configuration versions
3. **Validation** - Automated validation in CI/CD
4. **Monitoring** - Track configuration-related issues

## Troubleshooting

### Common Issues

#### Configuration File Not Found
```
Error: Configuration file not found: [path]
Solution: Ensure GameConfig.json exists in StreamingAssets/Config/
```

#### JSON Parsing Errors
```
Error: JSON parsing failed
Solution: Validate JSON syntax using online JSON validator
```

#### Platform Loading Issues
```
Error: Configuration loading fails on WebGL
Solution: Check CORS settings and HTTPS requirements
```

#### Performance Issues
```
Error: Configuration causes performance drops
Solution: Review memory budgets and rendering settings
```

### Debugging Tools

#### Configuration Logging
```csharp
// Enable detailed configuration logging
GameConfiguration.Instance.Config.metaSettings.configValidation.logMissingKeys = true;

// Log configuration summary
GameConfiguration.Instance.LogConfigurationSummary();
```

#### Validation Debugging
```csharp
// Check specific configuration values
Debug.Log($"Target FPS: {GameConfiguration.Instance.GetTargetFrameRate()}");
Debug.Log($"Memory Budget: {GameConfiguration.Instance.GetMemoryBudget(QualityLevel.Medium)}");
```

This configuration system ensures Whisker King maintains optimal performance across all target platforms while providing flexible, maintainable settings management aligned with the PRD requirements.
