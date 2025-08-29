# Git Configuration for Whisker King Unity Project

This guide covers the complete Git setup for the Whisker King Unity project, including version control best practices, Git LFS configuration, and team collaboration workflows.

## Overview

The Git configuration is designed to:
- **Handle Unity's binary assets** efficiently with Git LFS
- **Maintain consistent line endings** across platforms
- **Exclude Unity-generated files** that shouldn't be tracked
- **Support team collaboration** with proper merge strategies
- **Optimize repository size** for large game assets

## Repository Configuration

### Git LFS (Large File Storage)

Git LFS is **essential** for Unity projects to handle large binary assets. The `.gitattributes` file automatically configures LFS for:

#### 3D Assets
- Models: `.fbx`, `.obj`, `.blend`, `.max`
- Meshes and geometric data
- Animation files

#### Textures and Images  
- All image formats: `.png`, `.jpg`, `.tga`, `.psd`
- HDR textures: `.exr`, `.hdr`
- Compressed textures for mobile

#### Audio Assets
- Music: `.mp3`, `.ogg`, `.wav`
- Sound effects and voice files
- Compressed audio for streaming

#### Video Assets
- Cutscenes and promotional videos
- All video formats: `.mp4`, `.mov`, `.avi`

### File Exclusions (.gitignore)

The `.gitignore` file excludes Unity-specific temporary files:

#### Unity Generated
- `Library/` - Unity's cache and temporary files
- `Temp/` - Build temporary files  
- `Obj/` - Compiled object files
- `UserSettings/` - Personal Unity editor settings

#### Build Artifacts
- `Builds/` - All platform build outputs
- `*.apk`, `*.ipa` - Mobile build files (unless using LFS)
- Compressed build files

#### IDE and OS
- Visual Studio cache and user files
- macOS `.DS_Store` files
- Windows `Thumbs.db` files

## Repository Initialization

### 1. Initialize Repository (if not done)
```bash
git init
```

### 2. Set up Git LFS
```bash
# Install Git LFS (one-time setup per machine)
git lfs install

# Track large files (already configured in .gitattributes)
git lfs track "*.fbx"
git lfs track "*.png" 
git lfs track "*.jpg"
git lfs track "*.ogg"
git lfs track "*.mp3"
# ... (all patterns in .gitattributes)
```

### 3. Configure User Settings
```bash
# Set your identity
git config user.name "Your Name"
git config user.email "your.email@example.com"

# Unity-specific settings
git config core.autocrlf false        # Preserve line endings
git config core.filemode false       # Ignore file mode changes
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'/path/to/UnityYAMLMerge.exe' merge -p %O %B %A %A"
```

### 4. Initial Commit
```bash
# Add all files
git add .

# Create initial commit
git commit -m "feat: initial Unity project setup" \
          -m "- Created Whisker King Unity project with URP" \
          -m "- Configured build settings for WebGL/Android/iOS" \
          -m "- Set up project structure and documentation" \
          -m "- Added Git LFS configuration for binary assets"
```

## Team Collaboration Workflow

### Branch Strategy

#### Main Branches
- **`main`** - Production-ready code
- **`develop`** - Integration branch for features
- **`release/v1.0.0`** - Prepare releases

#### Feature Branches
- **`feature/player-movement`** - New features
- **`fix/camera-jitter`** - Bug fixes
- **`chore/asset-optimization`** - Non-feature work

### Commit Message Convention

#### Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

#### Types
- **feat:** New features
- **fix:** Bug fixes
- **docs:** Documentation changes
- **style:** Code formatting changes
- **refactor:** Code refactoring
- **perf:** Performance improvements
- **test:** Adding or fixing tests
- **chore:** Build process or auxiliary tool changes

#### Examples
```bash
git commit -m "feat(player): add double jump mechanic" \
          -m "- Implements variable height double jump (2.5m)" \
          -m "- Adds input buffering for responsive controls" \
          -m "- Includes unit tests for jump validation" \
          -m "Closes #15"

git commit -m "fix(camera): resolve jitter during fast movement" \
          -m "- Improved damping calculations" \
          -m "- Fixed look-ahead prediction accuracy" \
          -m "Related to task 3.2 in PRD"
```

### Merge Strategy

#### Unity Scene Merging
Configure Unity Smart Merge for `.unity` and `.prefab` files:

1. **Install Unity Smart Merge** (included with Unity)
2. **Configure Git merge tool:**
   ```bash
   git config merge.tool unityyamlmerge
   git config mergetool.unityyamlmerge.cmd '/path/to/UnityYAMLMerge.exe merge -p "$BASE" "$LOCAL" "$REMOTE" "$MERGED"'
   git config mergetool.unityyamlmerge.trustexitcode false
   ```

#### Binary Asset Conflicts
For binary assets (textures, models, audio):
```bash
# Accept theirs for binary conflicts
git checkout --theirs path/to/asset.fbx
git add path/to/asset.fbx

# Or accept ours
git checkout --ours path/to/asset.fbx  
git add path/to/asset.fbx
```

## Repository Optimization

### Git LFS Optimization

#### Check LFS Status
```bash
# See which files are tracked by LFS
git lfs ls-files

# Check LFS bandwidth usage
git lfs bandwidth

# Verify LFS configuration
git lfs env
```

#### Clean Up LFS
```bash
# Remove old LFS objects (after major asset cleanup)
git lfs prune --dry-run
git lfs prune
```

### Repository Size Management

#### Asset Cleanup
```bash
# Find large files in repository
git rev-list --objects --all | git cat-file --batch-check='%(objecttype) %(objectname) %(objectsize) %(rest)' | grep blob | sort -k3nr | head -20

# Remove large files from history (dangerous - coordinate with team)
git filter-branch --force --index-filter 'git rm --cached --ignore-unmatch path/to/large-file.fbx' --prune-empty --tag-name-filter cat -- --all
```

#### Shallow Clones
For new team members (faster initial clone):
```bash
# Clone with limited history
git clone --depth 1 <repository-url>

# Later, get full history if needed
git fetch --unshallow
```

## Development Workflows

### Daily Development

#### Starting Work
```bash
# Update local repository
git pull origin develop

# Create feature branch
git checkout -b feature/player-combat

# Work on specific task
# ... implement code ...

# Stage and commit changes  
git add .
git commit -m "feat(combat): implement tail whip hit detection"
```

#### Finishing Work
```bash
# Push feature branch
git push origin feature/player-combat

# Create pull request through GitHub/GitLab
# ... code review process ...

# Merge to develop after approval
```

### Release Workflow

#### Creating Release
```bash
# Create release branch from develop
git checkout develop
git pull origin develop
git checkout -b release/v1.0.0

# Update version numbers
# ... bump version in files ...

# Final testing and bug fixes
# ... test and fix ...

# Merge to main
git checkout main
git merge --no-ff release/v1.0.0
git tag v1.0.0

# Merge back to develop
git checkout develop
git merge --no-ff release/v1.0.0

# Clean up
git branch -d release/v1.0.0
```

## Unity-Specific Considerations

### Scene Conflicts

#### Prevention
- **Scene ownership:** One person edits scene at a time
- **Prefab workflow:** Break scenes into prefabs for parallel work
- **Additive scenes:** Use additive loading for team features

#### Resolution
```bash
# When scene conflicts occur
git status
# Shows both modified: Assets/Scenes/MainLevel.unity

# Use Unity Smart Merge
git mergetool Assets/Scenes/MainLevel.unity

# Or manually resolve
# 1. Open Unity
# 2. Let Unity detect and fix references
# 3. Save scene
# 4. Commit resolved version
```

### Asset Dependencies

#### Maintaining References
- **Keep .meta files:** Essential for asset GUID consistency
- **Commit together:** Asset and its .meta file should be committed together
- **Force text serialization:** Use "Force Text" serialization mode

#### Missing Asset Recovery
```bash
# When assets show as missing in Unity
# 1. Check if .meta file exists
# 2. Ensure asset is in Git LFS
# 3. Pull LFS objects
git lfs pull

# 4. Refresh Unity Asset Database
# In Unity: Assets → Refresh
```

## Performance and Security

### Credential Management
```bash
# Use credential helper (Windows)
git config credential.helper manager-core

# Use credential helper (macOS)  
git config credential.helper osxkeychain

# Use credential helper (Linux)
git config credential.helper store
```

### Large Repository Optimization
```bash
# Partial clone (saves bandwidth)
git clone --filter=blob:limit=100m <repository-url>

# Fetch specific LFS files on demand
git lfs fetch --include="Assets/Textures/*.png"
```

## Backup and Recovery

### Repository Backup
```bash
# Create complete backup including LFS
git clone --mirror <repository-url> backup.git
cd backup.git
git lfs fetch --all
```

### Disaster Recovery
```bash
# Restore from backup
git clone backup.git whisker-king-restored
cd whisker-king-restored
git lfs pull
```

## Team Guidelines

### Pre-commit Checklist
- [ ] Code compiles without errors
- [ ] All tests pass locally
- [ ] Large assets are properly tracked by LFS
- [ ] Commit message follows convention
- [ ] No temporary or debug files included

### Code Review Requirements
- **Automated checks:** CI/CD pipeline validates builds
- **Manual review:** Focus on gameplay logic and performance
- **Asset review:** Verify large assets meet performance budgets
- **Documentation:** Update relevant documentation files

### Branch Protection Rules
Configure repository settings:
- **Protect `main` branch:** Require pull requests
- **Require reviews:** At least 1 approval for main
- **Status checks:** CI/CD must pass
- **Up-to-date branches:** Require current before merge

This Git configuration ensures efficient version control for the Whisker King Unity project while supporting team collaboration and maintaining repository performance.
