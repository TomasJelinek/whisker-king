# Whisker King - Unity Project Setup Guide

## Prerequisites

- **Unity Hub** installed
- **Unity 2022.3 LTS** or **Unity 6.2** (minimum Unity 2022.3 LTS recommended)
- **Git** for version control
- **Visual Studio** or **Visual Studio Code** for code editing

## Step 1: Create Unity Project

1. **Open Unity Hub**
2. **Click "New project"**
3. **Select "3D (URP)" template** - This will set up Universal Render Pipeline by default
4. **Set Project Name:** "Whisker King"
5. **Set Location:** Choose your desired directory (this repo should be inside the project folder)
6. **Unity Version:** Select Unity 2022.3 LTS or Unity 6.2
7. **Click "Create project"**

## Step 2: Initial Project Configuration

### Configure Project Settings
1. Go to **Edit > Project Settings**
2. **Player Settings:**
   - Set Company Name: "Your Studio Name"
   - Set Product Name: "Whisker King"
   - Set Version: "1.0.0"
   - Set Bundle Identifier (for mobile): `com.yourstudio.whiskerking`

### Configure Build Settings
1. Go to **File > Build Settings**
2. **Add Target Platforms:**
   - **WebGL** (primary platform)
   - **Android** (for mobile deployment)
   - **iOS** (for mobile deployment)

### Configure Quality Settings
1. Go to **Edit > Project Settings > Quality**
2. **Create Quality Levels:**
   - **Low Quality** (for minimum devices)
   - **Medium Quality** (for recommended devices) 
   - **High Quality** (for high-end devices)

## Step 3: Project Structure

After creating the project, organize the Assets folder with this structure:

```
Assets/
├── Scripts/
│   ├── Player/
│   ├── Camera/
│   ├── Combat/
│   ├── Interactables/
│   ├── Level/
│   ├── Audio/
│   ├── UI/
│   ├── Core/
│   └── Performance/
├── Prefabs/
│   ├── Player/
│   ├── Enemies/
│   ├── Interactables/
│   └── UI/
├── Materials/
├── Textures/
├── Models/
├── Audio/
│   ├── Music/
│   ├── SFX/
│   └── Voice/
├── Animations/
├── Scenes/
│   ├── MainMenu/
│   ├── Levels/
│   └── TestScenes/
└── Settings/
    ├── URP/
    └── Input/
```

## Step 4: Package Dependencies

The following packages should be installed via **Window > Package Manager**:

### Required Packages:
- **Universal RP** (usually included with 3D URP template)
- **Input System** (for modern input handling)
- **Test Framework** (for unit testing)
- **Unity Analytics** (for game metrics)
- **Addressable Assets** (for memory management)

### Installation Commands:
You can also add these to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "14.0.8",
    "com.unity.inputsystem": "1.7.0",
    "com.unity.test-framework": "1.1.33",
    "com.unity.analytics": "3.8.1",
    "com.unity.addressables": "1.21.14"
  }
}
```

## Step 5: Version Control Setup

1. **Initialize Git repository** in project root:
   ```bash
   git init
   git add .
   git commit -m "Initial Unity project setup"
   ```

2. **Connect to remote repository** (if applicable):
   ```bash
   git remote add origin [your-repo-url]
   git push -u origin main
   ```

## Step 6: Performance Configuration

### URP Asset Configuration
1. Navigate to **Assets/Settings/URP**
2. Select the **UniversalRP-HighQuality** asset
3. Configure mobile-optimized settings:
   - **Rendering Path:** Forward
   - **Depth Texture:** Disabled (for mobile)
   - **Opaque Texture:** Disabled (for mobile)
   - **Max Additional Lights:** 2
   - **Shadow Resolution:** 1024

## Next Steps

After completing this setup:
1. Verify the project opens without errors
2. Create a simple test scene to confirm URP is working
3. Test build settings for target platforms
4. Proceed to implement the core game systems as outlined in the task list

## Performance Targets Reminder

- **Target FPS:** 60 on recommended devices, 30 minimum
- **Memory Budget:** ≤512MB (Low), ≤768MB (Medium), ≤1GB (High)  
- **Load Times:** ≤10s initial, ≤5s level loads
- **Supported Devices:** Snapdragon 665+, A12+
