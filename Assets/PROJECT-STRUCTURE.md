# Whisker King - Project Structure

This document outlines the complete folder organization for the Whisker King Unity project. Each folder serves a specific purpose and follows Unity best practices for mobile game development.

## Directory Structure

```
Assets/
├── Scripts/                  # All C# scripts organized by system
│   ├── Player/              # Player movement, input, and behaviors
│   ├── Camera/              # Camera controllers and systems
│   ├── Combat/              # Combat mechanics (tail whip, damage)
│   ├── Interactables/       # Crates, collectibles, interactive objects
│   ├── Level/               # Level management, checkpoints, progression
│   ├── Audio/               # Audio management and effects
│   ├── UI/                  # User interface and menu systems
│   ├── Core/                # Core game systems (GameManager, QualityManager)
│   └── Performance/         # Performance optimization (ObjectPool, benchmarks)
├── Prefabs/                 # Reusable game objects
│   ├── Player/              # Player character prefabs
│   ├── Enemies/             # Enemy and hazard prefabs
│   ├── Interactables/       # Crate and collectible prefabs
│   └── UI/                  # UI element prefabs
├── Materials/               # Shader materials for 3D objects
├── Textures/                # 2D textures and sprites
├── Models/                  # 3D models and meshes
├── Audio/                   # Audio assets organized by type
│   ├── Music/               # Background music tracks
│   ├── SFX/                 # Sound effects and ambient audio
│   └── Voice/               # Character voice lines and narration
├── Animations/              # Animation clips and controllers
├── Scenes/                  # Unity scene files
│   ├── MainMenu/            # Main menu and UI scenes
│   ├── Levels/              # Gameplay level scenes
│   └── TestScenes/          # Development and testing scenes
└── Settings/                # Configuration assets
    ├── URP/                 # Universal Render Pipeline settings
    └── Input/               # Input system configurations
```

## Folder Guidelines

### Scripts Organization
- **One class per file** with matching filename
- **Test files** alongside their corresponding scripts (e.g., `PlayerController.cs` + `PlayerController.Tests.cs`)
- **Namespace convention:** `WhiskerKing.[FolderName]` (e.g., `WhiskerKing.Player`)
- **Interface files** prefixed with 'I' (e.g., `IPlayerController.cs`)

### Prefabs Naming Convention
- **Player prefabs:** `Player_[Variant]` (e.g., `Player_Capo`, `Player_Debug`)
- **Enemy prefabs:** `Enemy_[Type]` (e.g., `Enemy_Gull`, `Enemy_Bot`)
- **Crate prefabs:** `Crate_[Type]` (e.g., `Crate_Standard`, `Crate_Boom`)
- **UI prefabs:** `UI_[Element]` (e.g., `UI_MainMenu`, `UI_HUD`)

### Asset Naming Standards
- **Textures:** `T_[Object]_[Type]` (e.g., `T_Capo_Diffuse`, `T_Crate_Normal`)
- **Materials:** `M_[Object]` (e.g., `M_Capo`, `M_Water`)
- **Models:** `SM_[Object]` for Static Mesh (e.g., `SM_Crate_Standard`)
- **Animations:** `A_[Character]_[Action]` (e.g., `A_Capo_Jump`, `A_Capo_TailWhip`)

### Scene Organization
- **Level naming:** `Level_[World]_[Number]` (e.g., `Level_Seaside_01`)
- **Menu scenes:** `Menu_[Type]` (e.g., `Menu_Main`, `Menu_Settings`)
- **Test scenes:** `Test_[System]` (e.g., `Test_Movement`, `Test_Combat`)

### Audio Organization
- **Music files:** `BGM_[Level/Menu]` (e.g., `BGM_Seaside`, `BGM_MainMenu`)
- **SFX files:** `SFX_[Category]_[Action]` (e.g., `SFX_Player_Jump`, `SFX_Crate_Break`)
- **Voice files:** `VO_[Character]_[Line]` (e.g., `VO_Capo_Meow`, `VO_Narrator_Intro`)

## Version Control Notes

- All folders contain `.gitkeep` files to ensure empty directories are tracked
- Large asset files should use **Git LFS** (Large File Storage)
- **Binary assets** (textures, audio, models) automatically handled by `.gitignore`
- **Meta files** are included for proper Unity asset referencing

## Performance Considerations

### Asset Limits per Folder
- **Textures:** Max 1024x1024 for mobile, use compressed formats
- **Audio:** OGG Vorbis for music, compressed for SFX
- **Models:** ≤8,000 triangles for characters, ≤2,000 for props
- **Scenes:** Keep draw calls ≤300 per scene for 60 FPS

### Organization Benefits
- **Faster builds:** Unity can process organized assets more efficiently
- **Team collaboration:** Clear structure prevents merge conflicts
- **Asset streaming:** Folders map to addressable groups for memory management
- **Platform builds:** Easy to exclude desktop-only assets from mobile builds

## Development Workflow

### Daily Operations
1. **Scripts:** Work in appropriate system folder
2. **Testing:** Use TestScenes folder for experimental features
3. **Prefabs:** Create in correct category, test in TestScenes first
4. **Assets:** Import to proper folder, apply naming convention immediately

### Build Pipeline
1. **Development builds:** Include all TestScenes and debug assets
2. **Release builds:** Exclude TestScenes folder and debug prefabs
3. **Platform builds:** Use folder structure to filter platform-specific assets
4. **Asset bundles:** Map folders to addressable groups for streaming

## Maintenance Tasks

### Weekly
- [ ] Remove unused assets from folders
- [ ] Verify naming conventions are followed
- [ ] Check for duplicate assets across folders
- [ ] Update .gitkeep files if folders are restructured

### Before Releases
- [ ] Validate all prefab references are within correct folders
- [ ] Ensure no TestScenes assets are referenced by release scenes
- [ ] Verify asset compression settings match performance targets
- [ ] Confirm folder structure matches build configuration

This structure supports the game's performance targets (60 FPS, <512MB memory) while maintaining clean organization for a team development environment.
