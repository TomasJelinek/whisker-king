# Whisker King - Package Management Guide

This guide covers the complete package management strategy for Whisker King, including required dependencies, installation procedures, validation tools, and maintenance workflows.

## Overview

The Whisker King project uses Unity's Package Manager (UPM) to manage dependencies and ensure consistent development environments across team members. The package configuration is optimized for the PRD performance targets and mobile-first development approach.

## Package Categories

### Required Packages (Core Functionality)

These packages are essential for the game to function and meet PRD requirements:

#### Rendering & Graphics
- **Universal Render Pipeline (URP)** `com.unity.render-pipelines.universal@14.0.8`
  - Mobile-optimized rendering pipeline
  - Enables quality scaling system
  - Required for PRD performance targets

#### Input & Controls  
- **Input System** `com.unity.inputsystem@1.7.0`
  - Modern input handling with 120ms buffer support
  - Touch and gamepad support for mobile
  - Essential for responsive controls per PRD

#### Performance & Memory
- **Addressables** `com.unity.addressables@1.21.14`
  - Asset streaming and memory management
  - Critical for mobile memory budgets (≤512MB)
  - Enables progressive loading system

- **Burst Compiler** `com.unity.burst@1.8.4`
  - High-performance compiled C# code
  - Required for 60 FPS target on mobile
  - Optimizes physics and math operations

- **Mathematics** `com.unity.mathematics@1.2.6`
  - SIMD-optimized math operations
  - Required by Burst and physics systems
  - Essential for performance-critical code

- **Collections** `com.unity.collections@1.4.0`
  - High-performance native collections
  - Memory-efficient data structures
  - Required for job system integration

- **Job System** `com.unity.jobs@0.51.0`
  - Multithreading support for performance
  - Background processing for asset loading
  - Required for smooth 60 FPS gameplay

#### Audio & Visual
- **TextMeshPro** `com.unity.textmeshpro@3.0.6`
  - Enhanced text rendering for UI
  - Better mobile performance than Unity Text
  - Supports localization requirements

- **Timeline** `com.unity.timeline@1.7.4`
  - Cutscenes and scripted sequences
  - Animation system integration
  - Required for narrative elements

#### Testing & Analytics
- **Test Framework** `com.unity.test-framework@1.1.33`
  - Unit testing and integration tests
  - Quality assurance automation
  - Required for PRD testing requirements

- **Analytics** `com.unity.analytics@3.8.1`
  - Game metrics and player behavior tracking
  - Performance monitoring
  - Required for PRD success metrics

### Enhanced Functionality Packages

These packages provide advanced features and improved workflows:

#### Camera System
- **Cinemachine** `com.unity.cinemachine@2.9.7`
  - Advanced camera control system
  - Smooth follow and chase camera modes
  - Required for PRD camera specifications

#### Development Tools
- **Performance Profile Analyzer** `com.unity.performance.profile-analyzer@1.2.2`
  - Performance analysis and optimization
  - Memory usage tracking
  - Critical for meeting PRD performance targets

- **Recorder** `com.unity.recorder@4.0.1`
  - Video and image capture for marketing
  - Gameplay recording for testing
  - Optional but recommended for development

#### IDE Integration
- **Visual Studio Integration** `com.unity.ide.visualstudio@2.0.18`
- **VS Code Integration** `com.unity.ide.vscode@1.2.5`  
- **JetBrains Rider Integration** `com.unity.ide.rider@3.0.24`
- **Unity Collaborate** `com.unity.collab-proxy@2.0.5`

## Installation & Setup

### Automatic Installation

#### Install Required Packages
```csharp
// Via Unity menu
WhiskerKing → Packages → Install Required Packages

// Installs all packages necessary for core functionality
// Based on PRD requirements and performance targets
```

#### Install Optional Packages  
```csharp
// Via Unity menu
WhiskerKing → Packages → Install Optional Packages

// Installs development tools and IDE integrations
// Improves development workflow but not required for builds
```

### Manual Installation

#### Using Unity Package Manager UI
1. Open **Window → Package Manager**
2. Select **In Project** from dropdown
3. Click **+ → Add package by name**
4. Enter package name (e.g., `com.unity.addressables`)
5. Click **Add**

#### Using Packages/manifest.json
```json
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "14.0.8",
    "com.unity.inputsystem": "1.7.0",
    "com.unity.addressables": "1.21.14"
  }
}
```

#### Using Git URLs (Custom Packages)
```json
{
  "dependencies": {
    "com.custom.package": "https://github.com/user/repo.git#version"
  }
}
```

## Package Validation & Management

### Validation Tools

#### Validate Package Dependencies
```csharp
// Via Unity menu  
WhiskerKing → Packages → Validate Package Dependencies

// Checks:
// - All required packages are installed
// - Package versions match expected versions
// - No conflicting package versions
// - Missing dependencies
```

#### Generate Package Report
```csharp
// Via Unity menu
WhiskerKing → Packages → Generate Package Report

// Creates detailed report:
// - All installed packages with versions
// - Required vs optional package status
// - Compatibility information
// - Export to JSON for documentation
```

### Package Versions & Compatibility

#### Version Strategy
- **Required Packages:** Use exact versions for consistency
- **Optional Packages:** Allow minor version updates
- **Development Tools:** Use latest stable versions
- **Lock file:** `packages-lock.json` ensures reproducible builds

#### Unity Version Compatibility
| Unity Version | URP Version | Input System | Addressables |
|---------------|-------------|--------------|--------------|
| 2022.3 LTS    | 14.0.8      | 1.7.0        | 1.21.14      |
| 2023.1        | 15.0.6      | 1.7.0        | 1.21.17      |
| 2023.2        | 15.0.7      | 1.7.0        | 1.21.19      |

### Update Management

#### Update All Packages
```csharp
// Via Unity menu
WhiskerKing → Packages → Update All Packages

// WARNING: May introduce breaking changes
// Always test thoroughly after updates
// Recommend updating during development phases only
```

#### Package Update Strategy
1. **Major Updates:** Only during development phases
2. **Minor Updates:** Allowed for bug fixes
3. **Patch Updates:** Generally safe to apply
4. **Lock Versions:** Before release candidates

## Package-Specific Configuration

### Universal Render Pipeline (URP)

#### Post-Installation Setup
```csharp
// Configure URP assets for quality levels
Assets/Settings/URP/URP-LowQuality.asset
Assets/Settings/URP/URP-MediumQuality.asset  
Assets/Settings/URP/URP-HighQuality.asset

// Apply via QualityManager system
var qualityManager = FindObjectOfType<QualityManager>();
qualityManager.SetQuality(QualityLevel.Medium);
```

#### Mobile Optimizations
- Forward rendering path for mobile GPUs
- Limited shadow cascades (1-4 based on quality)
- Conservative post-processing effects
- Texture compression enabled

### Input System

#### Configuration Files
```
Assets/Settings/Input/WhiskerKingInputActions.inputactions
```

#### Setup Requirements
```csharp
// Enable new Input System in Player Settings
PlayerSettings.defaultIsNativeInputSystemEnabled = false;

// Configure input buffer for responsive controls
inputBuffer.bufferTime = 0.12f; // 120ms as per PRD
```

### Addressables

#### Group Configuration
- **Default Local Group:** Core assets, always loaded
- **Remote Content:** Level-specific assets, streamed
- **Localized Content:** Language-specific assets

#### Memory Management
```csharp
// Configure memory budgets per quality level
Low Quality: 256MB texture budget
Medium Quality: 384MB texture budget  
High Quality: 512MB texture budget
```

### Cinemachine

#### Camera Setup
```csharp
// Configure cameras for PRD specifications
Follow Camera: 8f distance, 3f height, 5f damping
Chase Camera: 6f distance, 2.5f height, 85° FOV
Look-Ahead: 4f distance, 2f height, 3f damping
```

## Development Workflow

### Daily Development

#### Package Validation Checklist
```bash
# Before starting work
1. Validate package dependencies
2. Check for package updates
3. Verify no package conflicts
4. Confirm all required packages present
```

#### Package-Related Issues

##### Missing Package Dependencies
```
Error: Assembly 'Unity.InputSystem' not found
Solution: Install Input System package
Command: WhiskerKing → Packages → Install Required Packages
```

##### Package Version Conflicts  
```
Error: Package version mismatch
Solution: Validate and update packages
Command: WhiskerKing → Packages → Validate Package Dependencies
```

##### Compilation Errors After Package Updates
```
Error: API changes in updated package
Solution: Update code to use new API
Process: 1) Check package changelog, 2) Update affected scripts
```

### Team Collaboration

#### Package Synchronization
- **packages-lock.json** is committed to ensure identical package versions
- **manifest.json** defines the package requirements
- Team members should validate packages after pulling changes

#### Onboarding New Developers
```bash
1. Clone repository
2. Open project in Unity (will install packages automatically)
3. Run: WhiskerKing → Packages → Validate Package Dependencies
4. Fix any missing packages if needed
```

### Continuous Integration

#### CI/CD Package Validation
```yaml
# Example GitHub Actions step
- name: Validate Unity Packages
  run: |
    unity -batchmode -quit -logFile -
    -projectPath . -executeMethod PackageManager.ValidatePackages
```

## Performance Considerations

### Package Impact on Build Size

#### Build Size by Platform
- **WebGL:** ~30-50MB (with package optimizations)
- **Android:** ~25-40MB (AAB with asset packs)
- **iOS:** ~35-55MB (App Store optimization)

#### Package Contribution to Build Size
| Package | WebGL Impact | Mobile Impact | Notes |
|---------|--------------|---------------|--------|
| URP | ~5-8MB | ~3-5MB | Essential for mobile performance |
| Input System | ~1-2MB | ~1MB | Replaces legacy input |
| Addressables | ~2-3MB | ~2MB | Reduces runtime memory |
| Cinemachine | ~3-4MB | ~2-3MB | Advanced camera features |
| Analytics | ~1MB | ~500KB | Runtime telemetry |

### Runtime Memory Usage

#### Package Memory Footprint
- **URP:** ~50-100MB (depends on quality settings)
- **Input System:** ~5-10MB (input buffers and mapping)
- **Addressables:** ~10-20MB (management overhead)
- **Cinemachine:** ~5-15MB (camera state and calculations)

#### Memory Optimization
```csharp
// Configure addressable memory limits
AddressableAssetSettings.maxConcurrentWebRequests = 3;
AddressableAssetSettings.catalogRequestsTimeout = 30;
```

## Troubleshooting

### Common Package Issues

#### Package Installation Fails
```
Error: Unable to add package
Causes: Network issues, Unity Hub not logged in, package not found
Solutions:
1. Check internet connection
2. Login to Unity Hub
3. Verify package name and version
4. Try manual installation via Package Manager UI
```

#### Package Compilation Errors
```
Error: Assembly definition conflicts
Causes: Package dependencies not resolved
Solutions:
1. Refresh packages (Assets → Reimport All)
2. Clear Library folder and restart Unity
3. Check for conflicting Assembly Definition files
```

#### Package Update Breaking Changes
```
Error: API methods not found after update
Causes: Breaking changes in package update
Solutions:
1. Check package changelog and migration guide
2. Update affected code to use new APIs
3. Consider downgrading to previous version if needed
```

### Performance Issues

#### High Memory Usage After Package Updates
- Monitor memory usage with Profiler
- Check for memory leaks in package integration
- Adjust quality settings if needed

#### Frame Rate Drops
- Profile rendering performance
- Check if packages introduced expensive operations
- Optimize package-related settings

#### Build Time Increases
- Check if packages added unnecessary build steps
- Optimize package import settings
- Consider excluding unused package features

## Package Development Guidelines

### Custom Package Creation

#### When to Create Custom Packages
- Reusable code across multiple projects
- Large features that could be modular
- Third-party integrations
- Tools and utilities

#### Package Structure
```
com.whiskerking.utilities/
├── package.json
├── README.md
├── CHANGELOG.md
├── Runtime/
│   ├── Scripts/
│   └── WhiskerKing.Utilities.asmdef
├── Editor/
│   ├── Scripts/
│   └── WhiskerKing.Utilities.Editor.asmdef
└── Tests/
    ├── Runtime/
    └── Editor/
```

#### Package Manifest (package.json)
```json
{
  "name": "com.whiskerking.utilities",
  "version": "1.0.0",
  "displayName": "Whisker King Utilities",
  "description": "Common utilities for Whisker King development",
  "unity": "2022.3",
  "dependencies": {
    "com.unity.mathematics": "1.2.6"
  },
  "keywords": ["whisker king", "utilities", "mobile"],
  "author": {
    "name": "Whisker King Development Team"
  }
}
```

## Maintenance & Updates

### Regular Maintenance Tasks

#### Weekly
- [ ] Check for package security updates
- [ ] Monitor package changelog for relevant updates
- [ ] Validate package dependencies haven't changed

#### Monthly  
- [ ] Review package usage and remove unused packages
- [ ] Update non-breaking package versions
- [ ] Generate package report for documentation

#### Before Releases
- [ ] Lock all package versions in manifest
- [ ] Generate final package report
- [ ] Test builds with current package versions
- [ ] Document any package-related configuration for deployment

### Long-term Package Strategy

#### Unity LTS Alignment
- Align package versions with Unity LTS releases
- Plan package updates around Unity version updates
- Maintain compatibility with target Unity version

#### Package Deprecation
- Monitor Unity's package deprecation notices
- Plan migration paths for deprecated packages
- Update development workflows when packages change

This package management system ensures Whisker King maintains consistent, high-performance dependencies across all development environments while supporting the mobile-first development approach outlined in the PRD.
