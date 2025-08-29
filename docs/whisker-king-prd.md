# Product Requirements Document (PRD)

## Project: Whisker King

**Date:** August 28, 2025
**Owner:** Game Design
**Version:** 2.0 (Enhanced)
**Target Audience:** Development Team (Junior to Senior Developers)

---

## 1. Executive Summary

**Pitch:** A bite-sized, linear 3D platformer where a nimble cat sprints, jumps, slides, and tail-whips through corridor-style levels, smashing boxes, collecting goodies, and outrunning hazards. Tonally light, slapstick, and satisfying to replay for better times.

**Core Value Proposition:** Accessible 3D platforming with high replayability through time trials and collectible hunting.

**Success Metrics:**

- 60 FPS on mid-range mobile devices (Snapdragon 665+ / A12+)
- <10 second initial load time
- <5 second level load time
- Player retention: 70% complete first world, 40% complete game
- Average session length: 25-45 minutes

---

## 2. Technical Architecture & Requirements

### 2.1 Development Environment

- **Engine:** Unity 2022.3 LTS (minimum) / Unity 6.2
- **Target Platform:** OpenGL
- **Build Pipeline:** Unity Cloud Build or local IL2CPP builds
- **Version Control:** Git with Unity-specific .gitignore
- **Package Manager:** Unity Package Manager (UPM) for dependencies

### 2.2 System Requirements

**Development Machine:**

- CPU: Intel i5-8400 / AMD Ryzen 5 2600 or better
- RAM: 16GB minimum, 32GB recommended
- GPU: GTX 1060 / RX 580 or better
- Storage: 50GB free space for builds and assets

**Target Device Specifications:**

- **Minimum (Low Quality):**
  - Android: Snapdragon 660, 3GB RAM, OpenGL ES 3.1
  - iOS: A10 Fusion, 2GB RAM, Metal API
  - Browser: Chrome 80+, Firefox 75+, Safari 13+
- **Recommended (Medium Quality):**
  - Android: Snapdragon 855, 4GB RAM, Vulkan 1.1
  - iOS: A12 Bionic, 3GB RAM, Metal API
- **High Quality:**
  - Android: Snapdragon 8 Gen 1, 6GB+ RAM
  - iOS: A14 Bionic, 4GB+ RAM

### 2.3 Performance Targets

**Frame Rate:**

- Target: 60 FPS on recommended devices
- Minimum: 30 FPS on minimum devices
- Fallback: Dynamic resolution scaling to maintain frame rate

**Memory Budget:**

- Total heap: ≤512MB (Low), ≤768MB (Medium), ≤1GB (High)
- Texture memory: ≤256MB (Low), ≤384MB (Medium), ≤512MB (High)
- Audio memory: ≤64MB (Low), ≤96MB (Medium), ≤128MB (High)

**Load Times:**

- Initial load (hub): ≤10 seconds on 50 Mbps connection
- Level load: ≤5 seconds on 50 Mbps connection
- Asset streaming: Progressive loading with priority system

---

## 3. Core Gameplay Systems

### 3.1 Player Movement System

**Input Handling:**

```csharp
// Input buffer system
public class InputBuffer
{
    private const float BUFFER_TIME = 0.12f; // 120ms
    private Queue<InputCommand> bufferedInputs;

    public void BufferInput(InputCommand command)
    {
        command.timestamp = Time.time;
        bufferedInputs.Enqueue(command);
    }

    public InputCommand GetBufferedInput()
    {
        // Return input if within buffer time
        while (bufferedInputs.Count > 0)
        {
            var input = bufferedInputs.Peek();
            if (Time.time - input.timestamp <= BUFFER_TIME)
                return bufferedInputs.Dequeue();
            bufferedInputs.Dequeue();
        }
        return null;
    }
}
```

**Movement Parameters:**

- **Run Speed:** 8.0 m/s (base), 10.0 m/s (slide)
- **Jump Height:** 3.0m (tap), 4.5m (hold)
- **Double Jump Height:** 2.5m (75% of single jump)
- **Slide Duration:** 0.6s base, cancelable after 0.25s
- **Coyote Time:** 0.12s (120ms) for edge forgiveness
- **Jump Buffer:** 0.12s (120ms) for responsive feel

**Physics Settings:**

- **Gravity:** -25.0 m/s² (base), -45.0 m/s² (pounce)
- **Air Control:** 0.8x ground control
- **Friction:** 0.85 (ground), 0.95 (air)
- **Bounce Damping:** 0.6x velocity retention

### 3.2 Combat & Interaction System

**Tail Whip Mechanics:**

```csharp
public class TailWhip : MonoBehaviour
{
    [Header("Timing")]
    public float windupTime = 0.1f;      // 100ms
    public float activeTime = 0.18f;     // 180ms
    public float recoveryTime = 0.12f;   // 120ms

    [Header("Combat")]
    public float damage = 25f;
    public float stunDuration = 1.5f;
    public LayerMask targetLayers;

    [Header("Hit Detection")]
    public float range = 2.5f;
    public float angle = 270f; // degrees
    public Transform attackOrigin;
}
```

**Crate Destruction System:**

- **Standard Crate:** 1 hit, 10 Fish Treats
- **Yarn Crate:** 1 hit, 15-25 Yarn
- **Spring Crate:** 1 hit, launches player 6m upward
- **Metal Crate:** Unbreakable, used as platform
- **Boom Crate:** 2 second fuse, chain reaction radius 3m
- **Mystery Crate:** Random reward (70% positive, 30% hazard)

### 3.3 Camera System

**Camera Parameters:**

```csharp
[System.Serializable]
public class CameraSettings
{
    [Header("Follow")]
    public float followDistance = 8f;
    public float followHeight = 3f;
    public float followDamping = 5f;

    [Header("Look")]
    public float lookAheadDistance = 4f;
    public float lookAheadHeight = 2f;
    public float lookAheadDamping = 3f;

    [Header("Chase Mode")]
    public float chaseFOV = 85f;
    public float chaseDistance = 6f;
    public float chaseHeight = 2.5f;
}
```

**Camera Behaviors:**

- **Follow Mode:** Smooth third-person follow with lane centering
- **Chase Mode:** Reverse camera facing player during chase sequences
- **Cinematic Mode:** Scripted camera movements for set pieces
- **Comfort Options:** Motion blur toggle, shake intensity slider (0-100%)

---

## 4. Level Design Specifications

### 4.1 Level Structure Template

**Standard Level Layout:**

```
Start Area (10-15s) →
Mechanic Introduction (20-30s) →
Checkpoint 1 →
Mechanic Combination (30-45s) →
Checkpoint 2 →
Final Challenge (20-30s) →
Finish Line
```

**Level Dimensions:**

- **Width:** 15-20 meters (3-4 lanes)
- **Length:** 150-300 meters (2.5-5 minutes gameplay)
- **Height:** 8-12 meters (vertical gameplay space)
- **Checkpoint Spacing:** Every 25-40 seconds of progress

### 4.2 World-Specific Mechanics

**Seaside Docks:**

- Rolling barrels (speed: 3-5 m/s, damage: 20)
- Gull enemies (patrol radius: 8m, attack range: 2m)
- Water hazards (instant death, respawn at checkpoint)
- Wooden platforms (breakable after 3 hits)

**Night City Rooftops:**

- Moving fans (rotation speed: 90°/s, damage: 15)
- Alley bots (patrol pattern: back-and-forth, 6m range)
- Billboard platforms (rise/fall cycle: 4 seconds)
- Neon hazards (pulsing damage zones, 2 second intervals)

**Temple Gardens:**

- Rolling statues (speed: 4-6 m/s, damage: 25)
- Beetle enemies (burrow/unburrow cycle: 3 seconds)
- Bamboo bridges (sway amplitude: 0.5m, period: 2s)
- Leaf slides (friction: 0.3, speed boost: 1.5x)

### 4.3 Collectible Placement Rules

**Fish Treats:**

- Minimum per level: 50
- Distribution: 60% main path, 30% side routes, 10% hidden
- Spacing: 2-4 meters between clusters

**Golden Mouse Tokens:**

- 3 per level, always accessible
- Placement: One early (first 30%), one middle (40-70%), one late (70-100%)
- Challenge level: Requires exploration or timing

**Yarn:**

- Primary source: Yarn Crates (15-25 per crate)
- Secondary source: Fish Treat conversion (10:1 ratio)
- Total per level: 200-400 Yarn

---

## 5. Asset Pipeline & Technical Art

### 5.1 3D Asset Specifications

**Character Model (Capo):**

- **Polygon Count:** ≤8,000 triangles (LOD0), ≤4,000 (LOD1), ≤2,000 (LOD2)
- **Texture Resolution:** 1024x1024 (diffuse), 512x512 (normal), 256x256 (emissive)
- **Rig:** Humanoid with 18 bones, optimized for mobile
- **Animation Clips:** 15-20 clips, ≤60 frames each

**Environment Assets:**

- **Props:** ≤2,000 triangles each, LOD system required
- **Terrain:** Heightmap-based, 256x256 resolution max
- **Buildings:** Modular pieces, ≤5,000 triangles per section
- **Vegetation:** Billboard system for distant objects

**Crate System:**

```csharp
[System.Serializable]
public class CrateData
{
    public CrateType type;
    public GameObject prefab;
    public int health;
    public float respawnTime;
    public AudioClip breakSound;
    public GameObject breakVFX;
    public int rewardAmount;
    public RewardType rewardType;
}

public enum CrateType
{
    Standard,   // Fish Treats
    Yarn,       // Yarn currency
    Spring,     // Bounce effect
    Metal,      // Unbreakable
    Boom,       // Explosive
    Mystery     // Random reward
}
```

### 5.2 Texture & Material Pipeline

**Texture Compression:**

- **WebGL:** KTX2/Basis Universal for all textures
- **Mobile:** ASTC 6x6 for high-end, ETC2 for compatibility
- **Format:** Power-of-two dimensions, max 2048x2048
- **Mipmaps:** Generated for all textures

**Material System:**

```csharp
[System.Serializable]
public class MaterialSettings
{
    [Header("Rendering")]
    public RenderQueue renderQueue = RenderQueue.Geometry;
    public bool receiveShadows = true;
    public bool castShadows = true;

    [Header("Shaders")]
    public Shader diffuseShader;
    public Shader normalMappedShader;
    public Shader emissiveShader;

    [Header("Performance")]
    public bool useLOD = true;
    public int maxLODLevel = 2;
}
```

**Shader Requirements:**

- **Base Shader:** Unlit with color tinting
- **Normal Mapped:** Standard PBR workflow
- **Emissive:** HDR bloom support
- **Mobile Optimized:** ≤8 texture samples, ≤4 math operations

### 5.3 Audio Pipeline

**Audio Specifications:**

- **Format:** OGG Vorbis (WebGL), MP3 (mobile fallback)
- **Sample Rate:** 44.1 kHz
- **Bit Depth:** 16-bit
- **Compression:** Target 128 kbps for music, 96 kbps for SFX

**Audio Categories:**

```csharp
public enum AudioCategory
{
    Music,          // Background music, looping
    SFX_Player,     // Player actions, movement
    SFX_World,      // Environment, crates, hazards
    SFX_UI,         // Menu sounds, notifications
    Voice           // Character vocalizations
}

[System.Serializable]
public class AudioSettings
{
    [Header("Volumes")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Performance")]
    public int maxSimultaneousSounds = 16;
    public float audioFadeTime = 0.5f;
    public bool enable3DAudio = true;
}
```

---

## 6. Performance & Optimization

### 6.1 Rendering Pipeline

**URP Settings:**

```csharp
[System.Serializable]
public class RenderSettings
{
    [Header("Quality Presets")]
    public QualityLevel currentQuality = QualityLevel.Medium;

    [Header("Lighting")]
    public bool enableRealtimeShadows = false;
    public int shadowCascades = 2;
    public float shadowDistance = 50f;

    [Header("Post-Processing")]
    public bool enableBloom = false;
    public bool enableVignette = true;
    public bool enableMotionBlur = false;

    [Header("Mobile Optimizations")]
    public bool enableDynamicResolution = true;
    public float targetFrameRate = 60f;
    public int maxTextureSize = 1024;
}
```

**Performance Budgets:**

- **Draw Calls:** ≤200 per frame (Low), ≤300 (Medium), ≤500 (High)
- **Triangles:** ≤50,000 per frame (Low), ≤100,000 (Medium), ≤200,000 (High)
- **Textures:** ≤50MB loaded simultaneously
- **Audio Sources:** ≤16 active sources

### 6.2 Memory Management

**Object Pooling System:**

```csharp
public class ObjectPool : MonoBehaviour
{
    [System.Serializable]
    public class PooledObject
    {
        public GameObject prefab;
        public int poolSize;
        public bool expandable;
    }

    public List<PooledObject> objectsToPool;
    private Dictionary<string, Queue<GameObject>> objectPools;

    public GameObject GetPooledObject(string tag)
    {
        if (objectPools.ContainsKey(tag) && objectPools[tag].Count > 0)
        {
            GameObject obj = objectPools[tag].Dequeue();
            obj.SetActive(true);
            return obj;
        }
        return null;
    }
}
```

**Asset Streaming:**

- **Priority System:** Critical (immediate), High (within 5s), Low (background)
- **Cache Strategy:** LRU (Least Recently Used) with 100MB limit
- **Preload:** Next level assets during current level gameplay
- **Cleanup:** Unused assets after 30 seconds of inactivity

---

## 7. Testing & Quality Assurance

### 7.1 Automated Testing

**Unit Tests:**

```csharp
[TestFixture]
public class PlayerMovementTests
{
    private PlayerController player;
    private TestEnvironment env;

    [SetUp]
    public void Setup()
    {
        env = new TestEnvironment();
        player = env.CreatePlayer();
    }

    [Test]
    public void Jump_WhenGrounded_ShouldLaunchPlayer()
    {
        // Arrange
        player.transform.position = Vector3.zero;
        player.isGrounded = true;

        // Act
        player.Jump();

        // Assert
        Assert.Greater(player.velocity.y, 0f);
        Assert.IsFalse(player.isGrounded);
    }

    [Test]
    public void DoubleJump_WhenInAir_ShouldLaunchPlayer()
    {
        // Arrange
        player.isGrounded = false;
        player.canDoubleJump = true;

        // Act
        player.DoubleJump();

        // Assert
        Assert.Greater(player.velocity.y, 0f);
        Assert.IsFalse(player.canDoubleJump);
    }
}
```

**Integration Tests:**

- Level completion flow
- Save/load system integrity
- Input system responsiveness
- Performance benchmarks

### 7.2 Performance Testing

**Benchmark Scenarios:**

```csharp
public class PerformanceBenchmark : MonoBehaviour
{
    [Header("Test Scenarios")]
    public bool testEmptyLevel = true;
    public bool testFullLevel = true;
    public bool testStressTest = true;

    [Header("Metrics")]
    public float targetFrameRate = 60f;
    public float maxFrameTime = 16.67f; // 60 FPS
    public float maxMemoryUsage = 512f; // MB

    public IEnumerator RunBenchmark()
    {
        // Test empty level performance
        if (testEmptyLevel)
            yield return TestEmptyLevel();

        // Test full level with all systems
        if (testFullLevel)
            yield return TestFullLevel();

        // Test stress conditions
        if (testStressTest)
            yield return TestStressConditions();
    }
}
```

**Performance Targets:**

- **Frame Time:** ≤16.67ms (60 FPS) on target devices
- **Memory:** ≤512MB heap usage, ≤1GB total
- **Load Time:** ≤10s initial, ≤5s level
- **Input Latency:** ≤120ms end-to-end

### 7.3 User Experience Testing

**Playtest Scenarios:**

- First-time user experience (15 minutes)
- Core gameplay loop (30 minutes)
- Time trial engagement (20 minutes)
- Accessibility features validation

**Success Criteria:**

- 90% of testers complete tutorial without help
- 80% attempt time trials after first level
- 70% achieve bronze medal on first attempt
- 95% can navigate menus with keyboard only

---

## 8. Implementation Phases

### 8.1 Phase 1: Core Systems (Weeks 1-6)

**Deliverables:**

- Player movement system with input buffering
- Basic camera system (follow mode only)
- Simple level with crates and collectibles
- Basic UI framework
- Save system foundation

**Acceptance Criteria:**

- Movement feels responsive (≤120ms input latency)
- Camera maintains player in frame at all times
- Level can be completed in 3-5 minutes
- Save/load works without data corruption

### 8.2 Phase 2: Gameplay Features (Weeks 7-12)

**Deliverables:**

- All crate types implemented
- Enemy AI and hazard systems
- Checkpoint and respawn system
- Power-up system (Nine Lives, Magnet Fish)
- Time trial system with medals

**Acceptance Criteria:**

- All crate types function correctly
- Enemies patrol and attack as designed
- Checkpoints save progress accurately
- Time trial medals unlock at correct thresholds

### 8.3 Phase 3: Content Creation (Weeks 13-20)

**Deliverables:**

- 12 complete levels across 3 worlds
- All art assets integrated
- Audio system with music and SFX
- Cosmetic system with Yarn economy
- Accessibility features implemented

**Acceptance Criteria:**

- All levels are completable
- Art assets meet performance budgets
- Audio plays without stuttering
- Cosmetics can be purchased and equipped

### 8.4 Phase 4: Polish & Optimization (Weeks 21-26)

**Deliverables:**

- Performance optimization complete
- Bug fixes and gameplay balancing
- Final art and audio polish
- Platform-specific optimizations
- QA testing and user feedback integration

**Acceptance Criteria:**

- 60 FPS on target devices
- All critical bugs resolved
- Gameplay feels balanced and fun
- Ready for platform submission

---

## 9. Risk Assessment & Mitigation

### 9.1 Technical Risks

**High Risk:**

- **WebGL Performance:** Target devices may not achieve 60 FPS

  - _Mitigation:_ Implement dynamic resolution scaling, aggressive LOD system
  - _Fallback:_ 30 FPS mode with enhanced visual quality

- **Memory Management:** Asset streaming may cause memory spikes
  - _Mitigation:_ Implement object pooling, aggressive cleanup
  - _Fallback:_ Reduce texture quality, limit simultaneous assets

**Medium Risk:**

- **Input Latency:** Touch controls may feel unresponsive

  - _Mitigation:_ Input buffering, predictive touch detection
  - _Fallback:_ Simplified control scheme, visual feedback

- **Browser Compatibility:** WebGL 2.0 support varies
  - _Mitigation:_ Graceful degradation to WebGL 1.0
  - _Fallback:_ Reduced visual effects, simplified rendering

### 9.2 Development Risks

**High Risk:**

- **Scope Creep:** Adding features beyond core gameplay

  - _Mitigation:_ Strict feature freeze after Phase 2
  - _Fallback:_ Defer non-essential features to post-launch

- **Performance Optimization:** May require significant refactoring
  - _Mitigation:_ Performance testing from Phase 1
  - _Fallback:_ Extended optimization phase, reduced visual quality

**Medium Risk:**

- **Asset Pipeline:** 3D assets may exceed performance budgets
  - _Mitigation:_ Regular asset reviews, LOD system
  - _Fallback:_ Simplified models, reduced texture resolution

---

## 10. Success Metrics & KPIs

### 10.1 Technical Performance

- **Frame Rate:** 95% of gameplay at 60 FPS on target devices
- **Load Times:** 90% of users experience load times within targets
- **Memory Usage:** 95% of sessions stay within memory budget
- **Input Latency:** 90% of inputs processed within 120ms

### 10.2 User Engagement

- **Completion Rate:** 70% complete first world, 40% complete game
- **Retention:** 50% return within 7 days of first session
- **Session Length:** Average 25-45 minutes per session
- **Time Trials:** 60% of players attempt at least one time trial

### 10.3 Quality Metrics

- **Bug Density:** ≤5 critical bugs per 1000 lines of code
- **Performance Stability:** ≤5% frame rate variance during gameplay
- **Asset Quality:** 100% of assets meet performance budgets
- **Accessibility:** 100% of accessibility features function correctly

---

## 11. Post-Launch Considerations

### 11.1 Content Updates

- **Seasonal Events:** Limited-time challenges and rewards
- **New Levels:** Additional worlds or level packs
- **Cosmetic DLC:** Premium cosmetic items and themes
- **Community Challenges:** User-generated content support

### 11.2 Technical Improvements

- **Performance Updates:** Ongoing optimization and bug fixes
- **Platform Expansion:** Additional platforms (Steam, consoles)
- **Cross-Platform:** Cloud save and progression sync
- **Mod Support:** Level editor and sharing tools

---

## 12. Appendices

### 12.1 Technical Reference

- Unity version compatibility matrix
- Asset import settings
- Build configuration files
- Performance profiling tools

### 12.2 Design Documents

- Level design templates
- Art style guide
- Audio design brief
- UI/UX wireframes

### 12.3 Testing Documentation

- Test case templates
- Performance benchmark procedures
- User testing protocols
- Bug reporting guidelines

---

**Document Status:** Ready for Development
**Next Review:** After Phase 1 completion
**Contact:** Game Design Team
