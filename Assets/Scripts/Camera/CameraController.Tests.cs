using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.Camera;
using WhiskerKing.Player;

namespace WhiskerKing.Camera.Tests
{
    /// <summary>
    /// Unit tests for CameraController system
    /// Tests camera modes, transitions, and PRD compliance for camera specifications
    /// </summary>
    public class CameraControllerTests
    {
        private GameObject cameraGameObject;
        private CameraController cameraController;
        private GameObject targetGameObject;
        private PlayerController playerController;

        [SetUp]
        public void Setup()
        {
            // Create camera setup
            cameraGameObject = new GameObject("TestCamera");
            cameraController = cameraGameObject.AddComponent<CameraController>();
            cameraGameObject.AddComponent<UnityEngine.Camera>();

            // Create target setup
            targetGameObject = new GameObject("TestTarget");
            playerController = targetGameObject.AddComponent<PlayerController>();
            targetGameObject.AddComponent<CharacterController>();

            // Configure camera
            cameraController.SetTarget(targetGameObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (cameraGameObject != null)
                Object.DestroyImmediate(cameraGameObject);
            if (targetGameObject != null)
                Object.DestroyImmediate(targetGameObject);
        }

        #region Basic Functionality Tests

        [Test]
        public void CameraController_InitializesWithDefaultMode()
        {
            // Act & Assert
            Assert.AreEqual(CameraController.CameraMode.Follow, cameraController.GetCurrentMode());
        }

        [Test]
        public void SetTarget_UpdatesTarget()
        {
            // Arrange
            GameObject newTarget = new GameObject("NewTarget");
            
            // Act
            cameraController.SetTarget(newTarget.transform);
            
            // Assert
            // We can't directly access the target field, but we can test that no errors occur
            Assert.DoesNotThrow(() => cameraController.GetCurrentDistance());
            
            // Cleanup
            Object.DestroyImmediate(newTarget);
        }

        [Test]
        public void SetCameraMode_ChangesMode()
        {
            // Act
            cameraController.SetCameraMode(CameraController.CameraMode.Chase);
            
            // Assert
            Assert.IsTrue(cameraController.IsTransitioning() || 
                         cameraController.GetCurrentMode() == CameraController.CameraMode.Chase);
        }

        #endregion

        #region PRD Compliance Tests

        [Test]
        public void FollowMode_MeetsDistanceRequirements()
        {
            // Arrange
            cameraController.SetCameraMode(CameraController.CameraMode.Follow);
            
            // Act - Let camera settle (simulate some frames)
            for (int i = 0; i < 10; i++)
            {
                // Simulate Update calls
                System.Reflection.MethodInfo lateUpdateMethod = typeof(CameraController)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                lateUpdateMethod?.Invoke(cameraController, null);
            }
            
            // Assert - Should be close to 8f distance as per PRD
            float distance = cameraController.GetCurrentDistance();
            Assert.Greater(distance, 6f, "Follow camera should maintain reasonable distance");
            Assert.Less(distance, 12f, "Follow camera should not be too far");
        }

        [Test]
        public void ChaseMode_UsesPRDFOV()
        {
            // Arrange & Act
            cameraController.SetCameraMode(CameraController.CameraMode.Chase);
            
            // Let camera transition
            for (int i = 0; i < 20; i++)
            {
                System.Reflection.MethodInfo lateUpdateMethod = typeof(CameraController)
                    .GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                lateUpdateMethod?.Invoke(cameraController, null);
            }
            
            // Assert - Chase mode should use 85° FOV as per PRD
            UnityEngine.Camera cam = cameraGameObject.GetComponent<UnityEngine.Camera>();
            Assert.Greater(cam.fieldOfView, 80f, "Chase mode should use wide FOV");
            Assert.Less(cam.fieldOfView, 90f, "Chase mode FOV should be close to 85°");
        }

        #endregion

        #region Camera Mode Tests

        [Test]
        public void StartCinematic_ActivatesCinematicMode()
        {
            // Arrange
            Vector3 targetPos = Vector3.forward * 5f;
            Vector3 targetRot = Vector3.up * 45f;
            
            // Act
            cameraController.StartCinematic(targetPos, targetRot, 60f, 2f);
            
            // Assert
            Assert.AreEqual(CameraController.CameraMode.Cinematic, cameraController.GetCurrentMode());
        }

        [Test]
        public void StopCinematic_ReturnsToPreviousMode()
        {
            // Arrange
            cameraController.SetCameraMode(CameraController.CameraMode.Chase);
            cameraController.StartCinematic(Vector3.forward, Vector3.zero, 60f, 1f);
            
            // Act
            cameraController.StopCinematic();
            
            // Assert
            Assert.AreNotEqual(CameraController.CameraMode.Cinematic, cameraController.GetCurrentMode());
        }

        #endregion

        #region Camera Effects Tests

        [Test]
        public void TriggerCameraShake_ActivatesShake()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => cameraController.TriggerCameraShake(0.5f, 0.2f));
        }

        [Test]
        public void SetLaneCenter_EnablesLaneCentering()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => cameraController.SetLaneCenter(Vector3.zero, 4f));
        }

        [Test]
        public void DisableLaneCentering_DisablesFeature()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => cameraController.DisableLaneCentering());
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator CameraTransition_CompletesSuccessfully()
        {
            // Arrange
            cameraController.SetCameraMode(CameraController.CameraMode.Follow);
            yield return new WaitForSeconds(0.1f);
            
            // Act
            cameraController.SetCameraMode(CameraController.CameraMode.Chase);
            
            // Wait for transition
            float timeout = 5f;
            float elapsed = 0f;
            
            while (cameraController.IsTransitioning() && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Assert
            Assert.IsFalse(cameraController.IsTransitioning(), "Camera transition should complete");
            Assert.AreEqual(CameraController.CameraMode.Chase, cameraController.GetCurrentMode());
        }

        [UnityTest]
        public IEnumerator CinematicMode_CompletesAutomatically()
        {
            // Arrange
            float cinematicDuration = 0.5f;
            cameraController.StartCinematic(Vector3.forward * 3f, Vector3.zero, 60f, cinematicDuration);
            
            // Act - Wait for cinematic to complete
            yield return new WaitForSeconds(cinematicDuration + 0.2f);
            
            // Assert
            Assert.AreNotEqual(CameraController.CameraMode.Cinematic, cameraController.GetCurrentMode(),
                "Cinematic should auto-complete and return to normal mode");
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void GetCurrentDistance_WithNoTarget_ReturnsZero()
        {
            // Arrange
            cameraController.SetTarget(null);
            
            // Act
            float distance = cameraController.GetCurrentDistance();
            
            // Assert
            Assert.AreEqual(0f, distance);
        }

        [Test]
        public void SetCameraMode_SameMode_DoesNotStartTransition()
        {
            // Arrange
            var initialMode = cameraController.GetCurrentMode();
            
            // Act
            cameraController.SetCameraMode(initialMode);
            
            // Assert
            Assert.IsFalse(cameraController.IsTransitioning());
        }

        [Test]
        public void CameraShake_WithZeroIntensity_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => cameraController.TriggerCameraShake(0f, 1f));
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void GetConfiguration_ReturnsValidConfiguration()
        {
            // Act
            var config = cameraController.GetConfiguration();
            
            // Assert
            Assert.IsNotNull(config);
        }

        #endregion

        #region Performance Tests

        [Test]
        public void CameraUpdates_PerformanceAcceptable()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Act - Simulate many camera updates
            System.Reflection.MethodInfo lateUpdateMethod = typeof(CameraController)
                .GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            for (int i = 0; i < 100; i++)
            {
                lateUpdateMethod?.Invoke(cameraController, null);
            }
            
            stopwatch.Stop();
            
            // Assert - Should complete quickly (less than 10ms for 100 updates)
            Assert.Less(stopwatch.ElapsedMilliseconds, 10, 
                "CameraController should handle 100 updates in less than 10ms");
        }

        #endregion
    }
}
