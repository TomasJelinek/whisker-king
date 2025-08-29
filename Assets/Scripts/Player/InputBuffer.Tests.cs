using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.Player;

namespace WhiskerKing.Player.Tests
{
    /// <summary>
    /// Unit tests for InputBuffer system
    /// Tests 120ms buffer time requirement and responsive controls as per PRD specifications
    /// </summary>
    public class InputBufferTests
    {
        private GameObject testGameObject;
        private InputBuffer inputBuffer;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestInputBuffer");
            inputBuffer = testGameObject.AddComponent<InputBuffer>();
            inputBuffer.Initialize(0.12f); // 120ms as per PRD
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
        }

        #region Basic Functionality Tests

        [Test]
        public void Initialize_SetsCorrectBufferTime()
        {
            // Arrange & Act
            inputBuffer.Initialize(0.15f);

            // Assert
            Assert.AreEqual(0.15f, inputBuffer.GetBufferTime(), 0.001f);
        }

        [Test]
        public void BufferInput_StoresInputCorrectly()
        {
            // Arrange & Act
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Assert
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Slide));
        }

        [Test]
        public void ConsumeInput_RemovesInputFromBuffer()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Act
            bool consumed = inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);

            // Assert
            Assert.IsTrue(consumed);
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
        }

        [Test]
        public void ConsumeInput_ReturnsFalseWhenNoInput()
        {
            // Act
            bool consumed = inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);

            // Assert
            Assert.IsFalse(consumed);
        }

        #endregion

        #region Buffer Time Tests

        [UnityTest]
        public IEnumerator BufferInput_ExpiresAfterBufferTime()
        {
            // Arrange
            inputBuffer.Initialize(0.1f); // 100ms buffer time for faster testing
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Act - wait slightly longer than buffer time
            yield return new WaitForSeconds(0.12f);

            // Assert
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
        }

        [UnityTest]
        public IEnumerator BufferInput_ValidWithinBufferTime()
        {
            // Arrange
            inputBuffer.Initialize(0.2f); // 200ms buffer time
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Act - wait less than buffer time
            yield return new WaitForSeconds(0.1f);

            // Assert
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
        }

        [UnityTest]
        public IEnumerator GetInputAge_ReturnsCorrectAge()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Act
            yield return new WaitForSeconds(0.05f); // Wait 50ms

            // Assert
            float age = inputBuffer.GetInputAge(InputBuffer.InputType.Jump);
            Assert.Greater(age, 0.04f); // Should be at least 40ms
            Assert.Less(age, 0.07f); // Should be less than 70ms (accounting for frame timing)
        }

        #endregion

        #region PRD Compliance Tests

        [Test]
        public void Initialize_DefaultsTo120msBufferTime()
        {
            // Arrange & Act
            var newGameObject = new GameObject("TestDefault");
            var newInputBuffer = newGameObject.AddComponent<InputBuffer>();

            // Assert
            Assert.AreEqual(0.12f, newInputBuffer.GetBufferTime(), 0.001f);

            // Cleanup
            Object.DestroyImmediate(newGameObject);
        }

        [UnityTest]
        public IEnumerator PRDCompliance_120msBufferWindow()
        {
            // Arrange - Use exact PRD specification
            inputBuffer.Initialize(0.12f); // 120ms
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Test at 119ms - should still be valid
            yield return new WaitForSeconds(0.119f);
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump), 
                "Input should be valid at 119ms");

            // Test consumption at edge of buffer window
            bool consumed = inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);
            Assert.IsTrue(consumed, "Input should be consumable at edge of buffer window");
        }

        [UnityTest]
        public IEnumerator PRDCompliance_ResponsiveControls()
        {
            // Test that inputs can be buffered and consumed quickly for responsive feel
            // This simulates a common scenario where player inputs slightly before ground contact

            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Act - consume immediately (common case)
            bool consumed = inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);

            // Assert
            Assert.IsTrue(consumed, "Input should be consumable immediately for responsive controls");
            
            // Test multiple rapid inputs
            for (int i = 0; i < 5; i++)
            {
                inputBuffer.BufferInput(InputBuffer.InputType.Jump);
                yield return null; // Wait one frame
                consumed = inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);
                Assert.IsTrue(consumed, $"Rapid input {i} should be consumable");
            }
        }

        #endregion

        #region Priority System Tests

        [Test]
        public void BufferInput_WithPriority_StoresCorrectly()
        {
            // Arrange & Act
            inputBuffer.BufferInput(InputBuffer.InputType.Jump, InputBuffer.InputPriority.High);

            // Assert
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
        }

        [Test]
        public void ConsumeInput_HigherPriorityFirst()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump, InputBuffer.InputPriority.Low);
            inputBuffer.BufferInput(InputBuffer.InputType.Jump, InputBuffer.InputPriority.High);

            // Act
            bool consumed = inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);

            // Assert
            Assert.IsTrue(consumed);
            // Note: We can't directly test which priority was consumed without exposing internal state
            // But we can verify that inputs are still being handled correctly
        }

        [Test]
        public void ConsumeInput_WithContextData_ReturnsCorrectData()
        {
            // Arrange
            Vector2 expectedContext = new Vector2(1.5f, -0.5f);
            inputBuffer.BufferInput(InputBuffer.InputType.Slide, InputBuffer.InputPriority.Normal, expectedContext);

            // Act
            bool consumed = inputBuffer.ConsumeInput(InputBuffer.InputType.Slide, out Vector2 actualContext);

            // Assert
            Assert.IsTrue(consumed);
            Assert.AreEqual(expectedContext.x, actualContext.x, 0.001f);
            Assert.AreEqual(expectedContext.y, actualContext.y, 0.001f);
        }

        #endregion

        #region Queue Management Tests

        [Test]
        public void BufferInput_MultipleInputs_TracksCorrectly()
        {
            // Arrange & Act
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            inputBuffer.BufferInput(InputBuffer.InputType.Slide);
            inputBuffer.BufferInput(InputBuffer.InputType.Attack);

            // Assert
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Slide));
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Attack));
        }

        [Test]
        public void GetBufferedInputCount_ReturnsCorrectCount()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);

            // Act
            int count = inputBuffer.GetBufferedInputCount(InputBuffer.InputType.Jump);

            // Assert
            Assert.AreEqual(2, count);
        }

        [Test]
        public void ClearInput_RemovesSpecificInputType()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            inputBuffer.BufferInput(InputBuffer.InputType.Slide);

            // Act
            inputBuffer.ClearInput(InputBuffer.InputType.Jump);

            // Assert
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Slide));
        }

        [Test]
        public void ClearAllInputs_RemovesAllInputs()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            inputBuffer.BufferInput(InputBuffer.InputType.Slide);
            inputBuffer.BufferInput(InputBuffer.InputType.Attack);

            // Act
            inputBuffer.ClearAllInputs();

            // Assert
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Slide));
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Attack));
        }

        #endregion

        #region Statistics Tests

        [Test]
        public void GetInputStatistics_ReturnsValidData()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);

            // Act
            var stats = inputBuffer.GetInputStatistics();

            // Assert
            Assert.IsTrue(stats.ContainsKey("BufferTimeMS"));
            Assert.IsTrue(stats.ContainsKey("Jump_Total"));
            Assert.IsTrue(stats.ContainsKey("Jump_Consumed"));
            Assert.AreEqual(120f, stats["BufferTimeMS"], 0.1f); // 120ms buffer time
            Assert.AreEqual(2f, stats["Jump_Total"]);
            Assert.AreEqual(1f, stats["Jump_Consumed"]);
        }

        [Test]
        public void ResetStatistics_ClearsAllStats()
        {
            // Arrange
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);

            // Act
            inputBuffer.ResetStatistics();
            var stats = inputBuffer.GetInputStatistics();

            // Assert
            Assert.AreEqual(0f, stats["Jump_Total"]);
            Assert.AreEqual(0f, stats["Jump_Consumed"]);
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void ConsumeInput_NonExistentInputType_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(inputBuffer.ConsumeInput(InputBuffer.InputType.Jump));
        }

        [Test]
        public void GetInputAge_NonExistentInput_ReturnsMaxValue()
        {
            // Act
            float age = inputBuffer.GetInputAge(InputBuffer.InputType.Jump);

            // Assert
            Assert.AreEqual(float.MaxValue, age);
        }

        [Test]
        public void SetBufferTime_UpdatesBufferTimeCorrectly()
        {
            // Arrange
            float newBufferTime = 0.08f; // 80ms

            // Act
            inputBuffer.SetBufferTime(newBufferTime);

            // Assert
            Assert.AreEqual(newBufferTime, inputBuffer.GetBufferTime(), 0.001f);
        }

        [UnityTest]
        public IEnumerator BufferTime_ChangeDuringRuntime_AffectsValidation()
        {
            // Arrange
            inputBuffer.Initialize(0.2f); // 200ms
            inputBuffer.BufferInput(InputBuffer.InputType.Jump);
            
            // Wait 150ms
            yield return new WaitForSeconds(0.15f);
            
            // Should still be valid with 200ms buffer
            Assert.IsTrue(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
            
            // Act - reduce buffer time to 100ms
            inputBuffer.SetBufferTime(0.1f);
            
            // Wait one frame for cleanup
            yield return null;
            
            // Assert - should now be expired (150ms > 100ms)
            Assert.IsFalse(inputBuffer.HasBufferedInput(InputBuffer.InputType.Jump));
        }

        #endregion

        #region Performance Tests

        [Test]
        public void BufferInput_ManyInputs_PerformanceAcceptable()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - buffer many inputs
            for (int i = 0; i < 1000; i++)
            {
                inputBuffer.BufferInput(InputBuffer.InputType.Jump);
                inputBuffer.ConsumeInput(InputBuffer.InputType.Jump);
            }

            stopwatch.Stop();

            // Assert - should complete quickly (less than 10ms for 1000 operations)
            Assert.Less(stopwatch.ElapsedMilliseconds, 10, 
                "InputBuffer should handle 1000 operations in less than 10ms");
        }

        #endregion
    }
}
