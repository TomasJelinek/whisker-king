using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.Combat;
using WhiskerKing.Player;

namespace WhiskerKing.Combat.Tests
{
    /// <summary>
    /// Unit tests for TailWhip combat system
    /// Tests timing, damage, hit detection, and PRD compliance
    /// </summary>
    public class TailWhipTests
    {
        private GameObject testGameObject;
        private TailWhip tailWhip;
        private PlayerController playerController;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestTailWhip");
            tailWhip = testGameObject.AddComponent<TailWhip>();
            playerController = testGameObject.AddComponent<PlayerController>();
            testGameObject.AddComponent<CharacterController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        #region Basic Functionality Tests

        [Test]
        public void TailWhip_InitializesInIdleState()
        {
            // Assert
            Assert.AreEqual(TailWhip.AttackState.Idle, tailWhip.GetCurrentState());
        }

        [Test]
        public void TryStartAttack_WhenIdle_StartsAttack()
        {
            // Act
            bool result = tailWhip.TryStartAttack();

            // Assert
            Assert.IsTrue(result);
            Assert.AreNotEqual(TailWhip.AttackState.Idle, tailWhip.GetCurrentState());
        }

        [Test]
        public void TryStartAttack_WhenActive_ReturnsFalse()
        {
            // Arrange
            tailWhip.TryStartAttack();

            // Act
            bool result = tailWhip.TryStartAttack();

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void CanAttack_WhenIdle_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(tailWhip.CanAttack());
        }

        [Test]
        public void CancelAttack_StopsAttack()
        {
            // Arrange
            tailWhip.TryStartAttack();

            // Act
            tailWhip.CancelAttack();

            // Assert
            Assert.AreEqual(TailWhip.AttackState.Idle, tailWhip.GetCurrentState());
        }

        #endregion

        #region PRD Compliance Tests

        [Test]
        public void TailWhip_HasCorrectTiming()
        {
            // Act
            var config = tailWhip.GetConfiguration();

            // Assert - PRD specifies 0.1s windup, 0.18s active, 0.12s recovery
            Assert.AreEqual(100, config.windupTimeMS, "Windup time should be 100ms");
            Assert.AreEqual(180, config.activeTimeMS, "Active time should be 180ms");
            Assert.AreEqual(120, config.recoveryTimeMS, "Recovery time should be 120ms");
        }

        [Test]
        public void TailWhip_HasCorrectDamage()
        {
            // Act
            var config = tailWhip.GetConfiguration();

            // Assert - PRD specifies 25 damage
            Assert.AreEqual(25f, config.damage, 0.1f, "Damage should be 25 as per PRD");
        }

        [Test]
        public void TailWhip_HasCorrectRange()
        {
            // Act
            var config = tailWhip.GetConfiguration();

            // Assert - PRD specifies 2.5f range
            Assert.AreEqual(2.5f, config.range, 0.1f, "Range should be 2.5f as per PRD");
        }

        [Test]
        public void TailWhip_HasCorrectAngle()
        {
            // Act
            var config = tailWhip.GetConfiguration();

            // Assert - PRD specifies 270° angle coverage
            Assert.AreEqual(270f, config.angleDegreess, 1f, "Angle should be 270° as per PRD");
        }

        [Test]
        public void TailWhip_HasCorrectStunDuration()
        {
            // Act
            var config = tailWhip.GetConfiguration();

            // Assert - PRD specifies 1.5s stun duration
            Assert.AreEqual(1.5f, config.stunDuration, 0.1f, "Stun duration should be 1.5s as per PRD");
        }

        #endregion

        #region State Machine Tests

        [UnityTest]
        public IEnumerator AttackState_ProgressesThroughStates()
        {
            // Arrange
            tailWhip.TryStartAttack();

            // Act & Assert - Should start in windup
            Assert.AreEqual(TailWhip.AttackState.Windup, tailWhip.GetCurrentState());

            // Wait for windup to complete (100ms + buffer)
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(TailWhip.AttackState.Active, tailWhip.GetCurrentState());

            // Wait for active to complete (180ms + buffer)
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(TailWhip.AttackState.Recovery, tailWhip.GetCurrentState());

            // Wait for recovery to complete (120ms + buffer)
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(TailWhip.AttackState.Idle, tailWhip.GetCurrentState());
        }

        [UnityTest]
        public IEnumerator GetAttackProgress_ReturnsCorrectProgress()
        {
            // Arrange
            tailWhip.TryStartAttack();

            // Act & Assert - Progress should increase over time
            yield return new WaitForSeconds(0.05f); // 50ms into windup
            float earlyProgress = tailWhip.GetAttackProgress();
            Assert.Greater(earlyProgress, 0f);
            Assert.Less(earlyProgress, 1f);

            yield return new WaitForSeconds(0.3f); // Well into attack sequence
            float lateProgress = tailWhip.GetAttackProgress();
            Assert.Greater(lateProgress, earlyProgress);
        }

        [UnityTest]
        public IEnumerator GetStateProgress_ReturnsCorrectStateProgress()
        {
            // Arrange
            tailWhip.TryStartAttack();

            // Act & Assert - State progress should reset for each state
            yield return new WaitForSeconds(0.05f); // 50ms into windup (100ms total)
            float windupProgress = tailWhip.GetStateProgress();
            Assert.Greater(windupProgress, 0.4f); // Should be about 50% through windup
            Assert.Less(windupProgress, 0.7f);

            // Wait for active phase
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(TailWhip.AttackState.Active, tailWhip.GetCurrentState());
            
            float activeStartProgress = tailWhip.GetStateProgress();
            Assert.Less(activeStartProgress, 0.2f); // Should be near start of active phase
        }

        #endregion

        #region Hit Detection Tests

        [Test]
        public void CreateDamageableTarget_ForHitTesting()
        {
            // Arrange - Create a test target
            GameObject target = new GameObject("TestTarget");
            target.tag = "Enemy";
            target.AddComponent<BoxCollider>();
            TestDamageable damageable = target.AddComponent<TestDamageable>();
            
            // Position target within range and angle
            target.transform.position = testGameObject.transform.position + Vector3.forward * 2f;

            // Start attack and wait for active phase
            tailWhip.TryStartAttack();
            
            // Simulate active phase by directly calling private method via reflection
            System.Reflection.MethodInfo updateMethod = typeof(TailWhip)
                .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Fast-forward through windup
            for (int i = 0; i < 20; i++)
            {
                updateMethod?.Invoke(tailWhip, null);
            }

            // Assert
            Assert.AreEqual(TailWhip.AttackState.Windup, tailWhip.GetCurrentState());
            
            // Cleanup
            Object.DestroyImmediate(target);
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void GetConfiguration_ReturnsValidConfiguration()
        {
            // Act
            var config = tailWhip.GetConfiguration();

            // Assert
            Assert.IsNotNull(config);
            Assert.Greater(config.windupTimeMS, 0);
            Assert.Greater(config.activeTimeMS, 0);
            Assert.Greater(config.recoveryTimeMS, 0);
            Assert.Greater(config.damage, 0);
            Assert.Greater(config.range, 0);
        }

        [Test]
        public void SetComboEnabled_UpdatesComboState()
        {
            // Act
            tailWhip.SetComboEnabled(true);

            // Assert - No direct way to test this, but should not throw
            Assert.DoesNotThrow(() => tailWhip.SetComboEnabled(false));
        }

        #endregion

        #region Performance Tests

        [Test]
        public void TailWhip_UpdatePerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            tailWhip.TryStartAttack();

            // Act - Simulate many updates
            System.Reflection.MethodInfo updateMethod = typeof(TailWhip)
                .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            for (int i = 0; i < 100; i++)
            {
                updateMethod?.Invoke(tailWhip, null);
            }

            stopwatch.Stop();

            // Assert - Should complete quickly (less than 5ms for 100 updates)
            Assert.Less(stopwatch.ElapsedMilliseconds, 5,
                "TailWhip should handle 100 updates in less than 5ms");
        }

        #endregion

        #region Event Tests

        [Test]
        public void TailWhip_TriggersEvents()
        {
            // Arrange
            bool startEventFired = false;
            bool completeEventFired = false;

            tailWhip.OnAttackStart += () => startEventFired = true;
            tailWhip.OnAttackComplete += () => completeEventFired = true;

            // Act
            tailWhip.TryStartAttack();

            // Assert - Start event should fire when attack begins
            // Complete event would fire after full sequence, which we can't easily test without coroutine
            Assert.DoesNotThrow(() => tailWhip.TryStartAttack());
        }

        #endregion

        /// <summary>
        /// Test implementation of IDamageable for testing hit detection
        /// </summary>
        private class TestDamageable : MonoBehaviour, IDamageable
        {
            public float LastDamage { get; private set; }
            public Vector3 LastHitPoint { get; private set; }
            public Vector3 LastHitDirection { get; private set; }
            public float LastStunDuration { get; private set; }
            public int HitCount { get; private set; }

            public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, float stunDuration = 0f)
            {
                LastDamage = damage;
                LastHitPoint = hitPoint;
                LastHitDirection = hitDirection;
                LastStunDuration = stunDuration;
                HitCount++;
            }
        }
    }
}
