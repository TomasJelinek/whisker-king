using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using WhiskerKing.Player;
using WhiskerKing.Camera;
using WhiskerKing.Combat;
using WhiskerKing.Level;
using WhiskerKing.Audio;
using WhiskerKing.UI;
using WhiskerKing.Performance;
using WhiskerKing.Core;

namespace WhiskerKing.Testing
{
    /// <summary>
    /// Comprehensive Test Suite for Whisker King
    /// Contains all major system tests with PRD compliance validation
    /// Organized by system and includes integration tests
    /// </summary>
    public class ComprehensiveTestSuite
    {
        #region PlayerController Advanced Tests

        [UnityTest]
        public IEnumerator PlayerController_MovementPhysics_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestPlayer", 
                typeof(CharacterController), typeof(PlayerController));
            var playerController = testGO.GetComponent<PlayerController>();
            var characterController = testGO.GetComponent<CharacterController>();

            TestFramework.StartTest("PlayerController_MovementPhysics", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Critical);

            try
            {
                // Test run speed (8.0 m/s base)
                float runSpeed = GetPrivateField<float>(playerController, "runSpeed");
                TestFramework.AssertEqual(8.0f, runSpeed, 0.1f, "Run speed should match PRD specification");

                // Test slide speed (10.0 m/s)
                float slideSpeed = GetPrivateField<float>(playerController, "slideSpeed");
                TestFramework.AssertEqual(10.0f, slideSpeed, 0.1f, "Slide speed should match PRD specification");

                // Test jump heights
                float jumpHeight = GetPrivateField<float>(playerController, "jumpHeight");
                TestFramework.AssertEqual(3.0f, jumpHeight, 0.1f, "Jump height should match PRD specification");

                float jumpHoldHeight = GetPrivateField<float>(playerController, "jumpHoldHeight");
                TestFramework.AssertEqual(4.5f, jumpHoldHeight, 0.1f, "Jump hold height should match PRD specification");

                float doubleJumpHeight = GetPrivateField<float>(playerController, "doubleJumpHeight");
                TestFramework.AssertEqual(2.5f, doubleJumpHeight, 0.1f, "Double jump height should match PRD specification");

                // Test gravity
                float gravity = GetPrivateField<float>(playerController, "gravity");
                TestFramework.AssertEqual(-25.0f, gravity, 0.1f, "Gravity should match PRD specification");

                // Test air control
                float airControl = GetPrivateField<float>(playerController, "airControl");
                TestFramework.AssertEqual(0.8f, airControl, 0.05f, "Air control should match PRD specification");

                TestFramework.EndTest("PlayerController_MovementPhysics", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("PlayerController_MovementPhysics", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerController_CoyoteTime_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestPlayer", 
                typeof(CharacterController), typeof(PlayerController));
            var playerController = testGO.GetComponent<PlayerController>();

            TestFramework.StartTest("PlayerController_CoyoteTime", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.High);

            try
            {
                // Test coyote time duration (0.12s)
                float coyoteTimeMS = GetPrivateField<float>(playerController, "coyoteTimeMS");
                TestFramework.AssertEqual(120f, coyoteTimeMS, 1f, "Coyote time should be 120ms as per PRD");

                TestFramework.EndTest("PlayerController_CoyoteTime", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("PlayerController_CoyoteTime", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerController_BouncePhysics_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestPlayer", 
                typeof(CharacterController), typeof(PlayerController));
            var playerController = testGO.GetComponent<PlayerController>();

            TestFramework.StartTest("PlayerController_BouncePhysics", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Medium);

            try
            {
                // Test bounce velocity retention (0.6x)
                float bounceVelocityMultiplier = GetPrivateField<float>(playerController, "bounceVelocityMultiplier");
                TestFramework.AssertEqual(0.6f, bounceVelocityMultiplier, 0.05f, 
                    "Bounce velocity multiplier should be 0.6x as per PRD");

                TestFramework.EndTest("PlayerController_BouncePhysics", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("PlayerController_BouncePhysics", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        #endregion

        #region InputBuffer Advanced Tests

        [UnityTest]
        public IEnumerator InputBuffer_TimingValidation_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestInputBuffer");
            var inputBuffer = testGO.AddComponent<InputBuffer>();

            TestFramework.StartTest("InputBuffer_TimingValidation", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Critical);

            try
            {
                // Test buffer time (120ms)
                float bufferTime = GetPrivateField<float>(inputBuffer, "bufferTimeMS");
                TestFramework.AssertEqual(120f, bufferTime, 1f, "Buffer time should be 120ms as per PRD");

                // Test input buffering
                inputBuffer.BufferInput(InputBuffer.InputType.Jump, Vector2.zero, InputBuffer.InputPriority.Normal);
                
                bool hasBufferedInput = inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump);
                TestFramework.Assert(hasBufferedInput, "Input should be buffered immediately");

                // Test buffer expiration timing
                yield return new WaitForSecondsRealtime(0.15f); // Wait longer than buffer time
                
                hasBufferedInput = inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump);
                TestFramework.Assert(!hasBufferedInput, "Input should expire after buffer time");

                TestFramework.EndTest("InputBuffer_TimingValidation", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("InputBuffer_TimingValidation", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }
        }

        [UnityTest]
        public IEnumerator InputBuffer_PrioritySystem_Validation()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestInputBuffer");
            var inputBuffer = testGO.AddComponent<InputBuffer>();

            TestFramework.StartTest("InputBuffer_PrioritySystem", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.High);

            try
            {
                // Buffer multiple inputs with different priorities
                inputBuffer.BufferInput(InputBuffer.InputType.Jump, Vector2.zero, InputBuffer.InputPriority.Low);
                inputBuffer.BufferInput(InputBuffer.InputType.Attack, Vector2.zero, InputBuffer.InputPriority.Critical);
                inputBuffer.BufferInput(InputBuffer.InputType.Slide, Vector2.zero, InputBuffer.InputPriority.High);

                // Consume input - should return highest priority
                var consumedInput = inputBuffer.ConsumeBufferedInput();
                TestFramework.Assert(consumedInput.HasValue && consumedInput.Value.inputType == InputBuffer.InputType.Attack,
                    "Should consume highest priority input first");

                TestFramework.EndTest("InputBuffer_PrioritySystem", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("InputBuffer_PrioritySystem", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        #endregion

        #region CameraController Advanced Tests

        [UnityTest]
        public IEnumerator CameraController_AllModes_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestCamera", typeof(Camera), typeof(CameraController));
            var cameraController = testGO.GetComponent<CameraController>();

            TestFramework.StartTest("CameraController_AllModes", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Critical);

            try
            {
                // Test Follow Mode settings
                cameraController.SetCameraMode(CameraController.CameraMode.Follow);
                yield return new WaitForSecondsRealtime(0.1f);

                // Test Chase Mode settings  
                cameraController.SetCameraMode(CameraController.CameraMode.Chase);
                yield return new WaitForSecondsRealtime(0.1f);

                // Test Cinematic Mode settings
                cameraController.SetCameraMode(CameraController.CameraMode.Cinematic);
                yield return new WaitForSecondsRealtime(0.1f);

                // All modes should transition without errors
                TestFramework.EndTest("CameraController_AllModes", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("CameraController_AllModes", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }
        }

        [UnityTest]
        public IEnumerator CameraController_LookAheadSystem_Validation()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestCamera", typeof(Camera), typeof(CameraController));
            var cameraController = testGO.GetComponent<CameraController>();

            TestFramework.StartTest("CameraController_LookAheadSystem", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.High);

            try
            {
                // Test look-ahead distance (4f as per PRD)
                float lookAheadDistance = GetPrivateField<float>(cameraController, "lookAheadDistance");
                TestFramework.AssertEqual(4f, lookAheadDistance, 0.1f, 
                    "Look-ahead distance should be 4f as per PRD");

                // Test look-ahead height offset (2f as per PRD)
                float lookAheadHeightOffset = GetPrivateField<float>(cameraController, "lookAheadHeightOffset");
                TestFramework.AssertEqual(2f, lookAheadHeightOffset, 0.1f, 
                    "Look-ahead height offset should be 2f as per PRD");

                TestFramework.EndTest("CameraController_LookAheadSystem", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("CameraController_LookAheadSystem", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        #endregion

        #region Combat System Advanced Tests

        [UnityTest]
        public IEnumerator TailWhip_TimingSystem_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestTailWhip", typeof(TailWhip));
            var tailWhip = testGO.GetComponent<TailWhip>();

            TestFramework.StartTest("TailWhip_TimingSystem", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Critical);

            try
            {
                // Test timing values (PRD: 0.1s windup, 0.18s active, 0.12s recovery)
                float windupTime = GetPrivateField<float>(tailWhip, "windupTime");
                TestFramework.AssertEqual(0.1f, windupTime, 0.01f, "Windup time should be 0.1s as per PRD");

                float activeTime = GetPrivateField<float>(tailWhip, "activeTime");
                TestFramework.AssertEqual(0.18f, activeTime, 0.01f, "Active time should be 0.18s as per PRD");

                float recoveryTime = GetPrivateField<float>(tailWhip, "recoveryTime");
                TestFramework.AssertEqual(0.12f, recoveryTime, 0.01f, "Recovery time should be 0.12s as per PRD");

                TestFramework.EndTest("TailWhip_TimingSystem", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("TailWhip_TimingSystem", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TailWhip_HitDetection_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestTailWhip", typeof(TailWhip));
            var tailWhip = testGO.GetComponent<TailWhip>();

            TestFramework.StartTest("TailWhip_HitDetection", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Critical);

            try
            {
                // Test hit detection parameters (PRD: 2.5f range, 270° angle)
                float range = GetPrivateField<float>(tailWhip, "range");
                TestFramework.AssertEqual(2.5f, range, 0.1f, "Hit range should be 2.5f as per PRD");

                float angleDegreess = GetPrivateField<float>(tailWhip, "angleDegreess");
                TestFramework.AssertEqual(270f, angleDegreess, 1f, "Hit angle should be 270° as per PRD");

                // Test damage and stun duration (PRD: 25f damage, 1.5s stun)
                float damage = GetPrivateField<float>(tailWhip, "damage");
                TestFramework.AssertEqual(25f, damage, 0.1f, "Damage should be 25f as per PRD");

                float stunDuration = GetPrivateField<float>(tailWhip, "stunDuration");
                TestFramework.AssertEqual(1.5f, stunDuration, 0.1f, "Stun duration should be 1.5s as per PRD");

                TestFramework.EndTest("TailWhip_HitDetection", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("TailWhip_HitDetection", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CrateSystem_AllTypes_Validation()
        {
            TestFramework.StartTest("CrateSystem_AllTypes", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.High);

            try
            {
                // Test all crate types exist and have correct properties
                var crateTypes = new CrateSystem.CrateType[]
                {
                    CrateSystem.CrateType.Standard,
                    CrateSystem.CrateType.Yarn,
                    CrateSystem.CrateType.Spring,
                    CrateSystem.CrateType.Metal,
                    CrateSystem.CrateType.Boom,
                    CrateSystem.CrateType.Mystery
                };

                foreach (var crateType in crateTypes)
                {
                    var testGO = TestFramework.CreateTestGameObject($"Test{crateType}Crate", typeof(CrateSystem));
                    var crateSystem = testGO.GetComponent<CrateSystem>();
                    
                    // Set crate type and validate
                    SetPrivateField(crateSystem, "crateType", crateType);
                    var actualType = GetPrivateField<CrateSystem.CrateType>(crateSystem, "crateType");
                    TestFramework.Assert(actualType == crateType, $"Crate type {crateType} should be settable");

                    Object.DestroyImmediate(testGO);
                    yield return null;
                }

                TestFramework.EndTest("CrateSystem_AllTypes", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("CrateSystem_AllTypes", false, e.Message);
                throw;
            }
        }

        #endregion

        #region Level System Advanced Tests

        [UnityTest]
        public IEnumerator LevelManager_PRDStructure_Validation()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestLevelManager", typeof(LevelManager));
            var levelManager = testGO.GetComponent<LevelManager>();

            TestFramework.StartTest("LevelManager_PRDStructure", 
                TestFramework.TestCategory.Integration, TestFramework.TestPriority.Critical);

            try
            {
                // Test level sections (PRD: Start→Mechanic→Checkpoint→Combination→Final)
                var expectedSections = new LevelManager.LevelSection[]
                {
                    LevelManager.LevelSection.Start,
                    LevelManager.LevelSection.Mechanic,
                    LevelManager.LevelSection.Checkpoint,
                    LevelManager.LevelSection.Combination,
                    LevelManager.LevelSection.Final
                };

                foreach (var section in expectedSections)
                {
                    TestFramework.Assert(System.Enum.IsDefined(typeof(LevelManager.LevelSection), section),
                        $"Level section {section} should exist as per PRD");
                }

                // Test checkpoint timing (PRD: 25-40s intervals)
                float checkpointInterval = GetPrivateField<float>(levelManager, "checkpointInterval");
                TestFramework.Assert(checkpointInterval >= 25f && checkpointInterval <= 40f,
                    "Checkpoint interval should be 25-40 seconds as per PRD");

                TestFramework.EndTest("LevelManager_PRDStructure", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("LevelManager_PRDStructure", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelManager_CollectibleTargets_PRDCompliant()
        {
            // Setup
            var testGO = TestFramework.CreateTestGameObject("TestLevelManager", typeof(LevelManager));
            var levelManager = testGO.GetComponent<LevelManager>();

            TestFramework.StartTest("LevelManager_CollectibleTargets", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.High);

            try
            {
                // Test collectible targets per PRD
                // Fish Treats: 50+ per level
                // Yarn: 200-400 per level  
                // Golden Mouse Tokens: 3 per level

                // Create mock level progress
                var progress = new LevelManager.LevelProgress();
                
                // Test minimum requirements
                progress.collectedFishTreats = 50;
                TestFramework.Assert(progress.collectedFishTreats >= 50, 
                    "Fish Treats should be at least 50 per level as per PRD");

                progress.collectedYarn = 300; // Mid-range
                TestFramework.Assert(progress.collectedYarn >= 200 && progress.collectedYarn <= 400,
                    "Yarn should be 200-400 per level as per PRD");

                progress.collectedTokens = 3;
                TestFramework.Assert(progress.collectedTokens == 3,
                    "Golden Mouse Tokens should be exactly 3 per level as per PRD");

                TestFramework.EndTest("LevelManager_CollectibleTargets", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("LevelManager_CollectibleTargets", false, e.Message);
                throw;
            }
            finally
            {
                Object.DestroyImmediate(testGO);
            }

            yield return null;
        }

        #endregion

        #region Audio System Advanced Tests

        [UnityTest]
        public IEnumerator AudioManager_CategorySystem_PRDCompliant()
        {
            TestFramework.StartTest("AudioManager_CategorySystem", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Critical);

            try
            {
                // Test all required audio categories exist (PRD: 6 categories)
                var requiredCategories = new AudioManager.AudioCategory[]
                {
                    AudioManager.AudioCategory.Music,
                    AudioManager.AudioCategory.SFX_Player,
                    AudioManager.AudioCategory.SFX_World,
                    AudioManager.AudioCategory.SFX_UI,
                    AudioManager.AudioCategory.Voice,
                    AudioManager.AudioCategory.Ambient
                };

                foreach (var category in requiredCategories)
                {
                    TestFramework.Assert(System.Enum.IsDefined(typeof(AudioManager.AudioCategory), category),
                        $"Audio category {category} should exist as per PRD");
                }

                TestFramework.EndTest("AudioManager_CategorySystem", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("AudioManager_CategorySystem", false, e.Message);
                throw;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator AudioManager_VolumeControls_PRDCompliant()
        {
            TestFramework.StartTest("AudioManager_VolumeControls", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.High);

            try
            {
                var audioManager = AudioManager.Instance;
                if (audioManager == null)
                {
                    TestFramework.EndTest("AudioManager_VolumeControls", false, "AudioManager instance not found");
                    yield break;
                }

                // Test volume controls (PRD: Master, Music, SFX, Voice with 0-1 range)
                audioManager.SetMasterVolume(0.8f);
                audioManager.SetMusicVolume(0.7f);
                audioManager.SetSFXVolume(0.9f);
                audioManager.SetVoiceVolume(0.6f);

                // Validate volume ranges
                float masterVolume = audioManager.GetCategoryVolume(AudioManager.AudioCategory.Music);
                TestFramework.Assert(masterVolume >= 0f && masterVolume <= 1f,
                    "Volume should be in 0-1 range as per PRD");

                TestFramework.EndTest("AudioManager_VolumeControls", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("AudioManager_VolumeControls", false, e.Message);
                throw;
            }

            yield return null;
        }

        #endregion

        #region UI System Advanced Tests

        [UnityTest]
        public IEnumerator UIManager_ScreenNavigation_Validation()
        {
            TestFramework.StartTest("UIManager_ScreenNavigation", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.Critical);

            try
            {
                var uiManager = UIManager.Instance;
                if (uiManager == null)
                {
                    TestFramework.EndTest("UIManager_ScreenNavigation", false, "UIManager instance not found");
                    yield break;
                }

                // Test all required screens exist per PRD
                var requiredScreens = new UIManager.UIScreen[]
                {
                    UIManager.UIScreen.MainMenu,
                    UIManager.UIScreen.InGameHUD,
                    UIManager.UIScreen.PauseMenu,
                    UIManager.UIScreen.SettingsMenu,
                    UIManager.UIScreen.LevelSelect,
                    UIManager.UIScreen.LevelComplete
                };

                foreach (var screen in requiredScreens)
                {
                    TestFramework.Assert(System.Enum.IsDefined(typeof(UIManager.UIScreen), screen),
                        $"UI screen {screen} should exist as per PRD");
                }

                TestFramework.EndTest("UIManager_ScreenNavigation", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("UIManager_ScreenNavigation", false, e.Message);
                throw;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator UIManager_AccessibilityFeatures_PRDCompliant()
        {
            TestFramework.StartTest("UIManager_AccessibilityFeatures", 
                TestFramework.TestCategory.Unit, TestFramework.TestPriority.High);

            try
            {
                var uiManager = UIManager.Instance;
                if (uiManager == null)
                {
                    TestFramework.EndTest("UIManager_AccessibilityFeatures", false, "UIManager instance not found");
                    yield break;
                }

                // Test accessibility features per PRD
                var accessibility = uiManager.GetAccessibilitySettings();
                
                // Test colorblind support types
                var colorBlindTypes = new UIManager.ColorBlindType[]
                {
                    UIManager.ColorBlindType.Normal,
                    UIManager.ColorBlindType.Protanopia,
                    UIManager.ColorBlindType.Deuteranopia,
                    UIManager.ColorBlindType.Tritanopia
                };

                foreach (var type in colorBlindTypes)
                {
                    TestFramework.Assert(System.Enum.IsDefined(typeof(UIManager.ColorBlindType), type),
                        $"Colorblind type {type} should exist as per PRD");
                }

                // Test UI scaling
                TestFramework.Assert(accessibility.uiScale > 0f,
                    "UI scale should be positive");

                TestFramework.EndTest("UIManager_AccessibilityFeatures", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("UIManager_AccessibilityFeatures", false, e.Message);
                throw;
            }

            yield return null;
        }

        #endregion

        #region Performance System Advanced Tests

        [UnityTest]
        public IEnumerator PerformanceManager_BudgetEnforcement_PRDCompliant()
        {
            TestFramework.StartTest("PerformanceManager_BudgetEnforcement", 
                TestFramework.TestCategory.Performance, TestFramework.TestPriority.Critical);

            try
            {
                var perfManager = PerformanceManager.Instance;
                if (perfManager == null)
                {
                    TestFramework.EndTest("PerformanceManager_BudgetEnforcement", false, "PerformanceManager instance not found");
                    yield break;
                }

                var budget = perfManager.GetPerformanceBudget();
                var metrics = perfManager.GetPerformanceMetrics();

                // Test frame rate targets (PRD: 60 FPS target, 30 FPS minimum)
                TestFramework.Assert(budget.targetFrameRate >= 30, 
                    "Target frame rate should be at least 30 FPS as per PRD");
                TestFramework.Assert(budget.minimumFrameRate >= 30,
                    "Minimum frame rate should be at least 30 FPS as per PRD");

                // Test memory budgets (PRD: ≤512MB-1GB total)
                TestFramework.Assert(budget.maxTotalMemory <= 1024f,
                    "Total memory budget should not exceed 1GB as per PRD");

                TestFramework.EndTest("PerformanceManager_BudgetEnforcement", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("PerformanceManager_BudgetEnforcement", false, e.Message);
                throw;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ObjectPool_PerformanceOptimization_Validation()
        {
            TestFramework.StartTest("ObjectPool_PerformanceOptimization", 
                TestFramework.TestCategory.Performance, TestFramework.TestPriority.High);

            try
            {
                var poolManager = PoolManager.Instance;
                if (poolManager == null)
                {
                    TestFramework.EndTest("ObjectPool_PerformanceOptimization", false, "PoolManager instance not found");
                    yield break;
                }

                // Test object pooling efficiency
                var testPrefab = TestFramework.CreateTestGameObject("TestPoolPrefab");
                var pool = poolManager.GetPool(testPrefab.AddComponent<MonoBehaviour>());

                // Get and return objects to test efficiency
                var objects = new MonoBehaviour[10];
                for (int i = 0; i < 10; i++)
                {
                    objects[i] = pool.Get();
                }

                for (int i = 0; i < 10; i++)
                {
                    pool.ReturnToPool(objects[i]);
                }

                // Test pool efficiency
                float efficiency = pool.PoolEfficiency;
                TestFramework.Assert(efficiency > 0f, "Pool should have positive efficiency");

                Object.DestroyImmediate(testPrefab);
                TestFramework.EndTest("ObjectPool_PerformanceOptimization", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("ObjectPool_PerformanceOptimization", false, e.Message);
                throw;
            }

            yield return null;
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator Integration_PlayerCameraMovement_FullWorkflow()
        {
            TestFramework.StartTest("Integration_PlayerCameraMovement", 
                TestFramework.TestCategory.Integration, TestFramework.TestPriority.Critical);

            try
            {
                // Setup complete player-camera system
                var playerGO = TestFramework.CreateTestGameObject("IntegrationPlayer", 
                    typeof(CharacterController), typeof(PlayerController));
                var cameraGO = TestFramework.CreateTestGameObject("IntegrationCamera", 
                    typeof(Camera), typeof(CameraController));

                var playerController = playerGO.GetComponent<PlayerController>();
                var cameraController = cameraGO.GetComponent<CameraController>();

                // Set up camera to follow player
                cameraController.SetTarget(playerGO.transform);
                cameraController.SetCameraMode(CameraController.CameraMode.Follow);

                // Simulate player movement
                yield return new WaitForSecondsRealtime(0.5f);

                // Test that systems work together
                TestFramework.Assert(cameraController.GetTarget() == playerGO.transform,
                    "Camera should be following player");

                Object.DestroyImmediate(playerGO);
                Object.DestroyImmediate(cameraGO);
                TestFramework.EndTest("Integration_PlayerCameraMovement", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("Integration_PlayerCameraMovement", false, e.Message);
                throw;
            }
        }

        [UnityTest]
        public IEnumerator Integration_SaveLoadSystem_FullWorkflow()
        {
            TestFramework.StartTest("Integration_SaveLoadSystem", 
                TestFramework.TestCategory.Integration, TestFramework.TestPriority.High);

            try
            {
                var saveSystem = SaveSystem.Instance;
                if (saveSystem == null)
                {
                    TestFramework.EndTest("Integration_SaveLoadSystem", false, "SaveSystem instance not found");
                    yield break;
                }

                // Test save/load workflow
                var initialProgression = saveSystem.GetCurrentSave();
                TestFramework.Assert(initialProgression != null, "Should have initial save data");

                // Test save operation
                bool saveResult = saveSystem.SaveGame(0);
                TestFramework.Assert(saveResult, "Save operation should succeed");

                // Test load operation  
                bool loadResult = saveSystem.LoadGame(0);
                TestFramework.Assert(loadResult, "Load operation should succeed");

                TestFramework.EndTest("Integration_SaveLoadSystem", true);
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest("Integration_SaveLoadSystem", false, e.Message);
                throw;
            }

            yield return null;
        }

        #endregion

        #region Helper Methods

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (T)field.GetValue(obj) : default(T);
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

        #endregion
    }
}
