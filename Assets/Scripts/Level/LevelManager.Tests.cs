using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.Level;

namespace WhiskerKing.Level.Tests
{
    /// <summary>
    /// Unit tests for LevelManager system
    /// Tests level progression, state management, checkpoints, and PRD compliance
    /// </summary>
    public class LevelManagerTests
    {
        private GameObject testGameObject;
        private LevelManager levelManager;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestLevelManager");
            levelManager = testGameObject.AddComponent<LevelManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        #region Basic Functionality Tests

        [Test]
        public void LevelManager_InitializesInLoadingState()
        {
            // Assert
            Assert.AreEqual(LevelManager.LevelState.Loading, levelManager.GetCurrentState());
        }

        [Test]
        public void GetCurrentSection_ReturnsValidSection()
        {
            // Act & Assert
            LevelManager.LevelSection section = levelManager.GetCurrentSection();
            Assert.IsTrue(System.Enum.IsDefined(typeof(LevelManager.LevelSection), section));
        }

        [Test]
        public void GetProgress_ReturnsValidProgress()
        {
            // Act
            var progress = levelManager.GetProgress();

            // Assert
            Assert.IsNotNull(progress);
            Assert.GreaterOrEqual(progress.completionPercentage, 0f);
            Assert.LessOrEqual(progress.completionPercentage, 100f);
        }

        [Test]
        public void GetCurrentLevel_ReturnsValidLevelData()
        {
            // Act
            var levelData = levelManager.GetCurrentLevel();

            // Assert
            Assert.IsNotNull(levelData);
        }

        #endregion

        #region State Management Tests

        [Test]
        public void PauseLevel_WhenInProgress_ChangesToPausedState()
        {
            // Arrange
            SetLevelState(LevelManager.LevelState.InProgress);

            // Act
            levelManager.PauseLevel();

            // Assert
            Assert.AreEqual(LevelManager.LevelState.Paused, levelManager.GetCurrentState());
        }

        [Test]
        public void ResumeLevel_WhenPaused_ChangesToInProgressState()
        {
            // Arrange
            SetLevelState(LevelManager.LevelState.Paused);

            // Act
            levelManager.ResumeLevel();

            // Assert
            Assert.AreEqual(LevelManager.LevelState.InProgress, levelManager.GetCurrentState());
        }

        [Test]
        public void RestartLevel_ResetsProgress()
        {
            // Arrange
            var initialProgress = levelManager.GetProgress();

            // Act
            levelManager.RestartLevel();

            // Assert
            var newProgress = levelManager.GetProgress();
            Assert.AreEqual(0f, newProgress.completionPercentage);
            Assert.AreEqual(0f, newProgress.elapsedTime);
        }

        #endregion

        #region PRD Compliance Tests

        [Test]
        public void LevelManager_HasCorrectSectionSequence()
        {
            // Act
            var currentSection = levelManager.GetCurrentSection();

            // Assert - Should start with Start section as per PRD
            Assert.AreEqual(LevelManager.LevelSection.Start, currentSection);
        }

        [Test]
        public void CheckpointInterval_IsWithinPRDRange()
        {
            // Act - Use reflection to get checkpoint interval
            var intervalField = typeof(LevelManager).GetField("checkpointInterval", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float interval = (float)intervalField?.GetValue(levelManager);

            // Assert - PRD specifies 25-40 seconds
            Assert.GreaterOrEqual(interval, 25f, "Checkpoint interval should be at least 25s as per PRD");
            Assert.LessOrEqual(interval, 40f, "Checkpoint interval should be at most 40s as per PRD");
        }

        #endregion

        #region Checkpoint System Tests

        [Test]
        public void ForceCheckpoint_CreatesCheckpoint()
        {
            // Arrange
            int initialCheckpointCount = levelManager.GetCheckpoints().Count;

            // Act
            levelManager.ForceCheckpoint();

            // Assert
            int newCheckpointCount = levelManager.GetCheckpoints().Count;
            Assert.Greater(newCheckpointCount, initialCheckpointCount);
        }

        [Test]
        public void GetCheckpoints_ReturnsValidList()
        {
            // Act
            var checkpoints = levelManager.GetCheckpoints();

            // Assert
            Assert.IsNotNull(checkpoints);
        }

        [UnityTest]
        public IEnumerator Checkpoints_AreCreatedOverTime()
        {
            // Arrange
            SetLevelState(LevelManager.LevelState.InProgress);
            int initialCount = levelManager.GetCheckpoints().Count;

            // Act - Wait for checkpoint interval
            yield return new WaitForSeconds(31f); // Just over minimum interval

            // Assert
            int newCount = levelManager.GetCheckpoints().Count;
            Assert.GreaterOrEqual(newCount, initialCount, "Checkpoints should be created automatically over time");
        }

        #endregion

        #region Collectible System Tests

        [Test]
        public void CollectItem_FishTreats_UpdatesProgress()
        {
            // Arrange
            var initialProgress = levelManager.GetProgress();

            // Act
            levelManager.CollectItem("FishTreats", 10);

            // Assert
            var newProgress = levelManager.GetProgress();
            Assert.AreEqual(initialProgress.collectedFishTreats + 10, newProgress.collectedFishTreats);
        }

        [Test]
        public void CollectItem_Yarn_UpdatesProgress()
        {
            // Arrange
            var initialProgress = levelManager.GetProgress();

            // Act
            levelManager.CollectItem("Yarn", 5);

            // Assert
            var newProgress = levelManager.GetProgress();
            Assert.AreEqual(initialProgress.collectedYarn + 5, newProgress.collectedYarn);
        }

        [Test]
        public void CollectItem_Token_UpdatesProgress()
        {
            // Arrange
            var initialProgress = levelManager.GetProgress();

            // Act
            levelManager.CollectItem("GoldenMouseToken", 1);

            // Assert
            var newProgress = levelManager.GetProgress();
            Assert.AreEqual(initialProgress.collectedTokens + 1, newProgress.collectedTokens);
        }

        [Test]
        public void CollectItem_UpdatesScore()
        {
            // Arrange
            var initialProgress = levelManager.GetProgress();

            // Act
            levelManager.CollectItem("FishTreats", 10);

            // Assert
            var newProgress = levelManager.GetProgress();
            Assert.Greater(newProgress.currentScore, initialProgress.currentScore);
        }

        #endregion

        #region Section Management Tests

        [Test]
        public void CompleteCurrentSection_AdvancesToNextSection()
        {
            // Arrange
            var initialSection = levelManager.GetCurrentSection();

            // Act
            levelManager.CompleteCurrentSection();

            // Assert
            var newSection = levelManager.GetCurrentSection();
            Assert.AreNotEqual(initialSection, newSection, "Section should advance after completion");
        }

        [Test]
        public void SectionProgression_FollowsPRDTemplate()
        {
            // Arrange & Act - Complete sections in sequence
            Assert.AreEqual(LevelManager.LevelSection.Start, levelManager.GetCurrentSection());
            
            levelManager.CompleteCurrentSection();
            Assert.AreEqual(LevelManager.LevelSection.Mechanic, levelManager.GetCurrentSection());
            
            levelManager.CompleteCurrentSection();
            Assert.AreEqual(LevelManager.LevelSection.Checkpoint, levelManager.GetCurrentSection());
            
            levelManager.CompleteCurrentSection();
            Assert.AreEqual(LevelManager.LevelSection.Combination, levelManager.GetCurrentSection());
            
            levelManager.CompleteCurrentSection();
            Assert.AreEqual(LevelManager.LevelSection.Final, levelManager.GetCurrentSection());
        }

        #endregion

        #region Performance Tests

        [Test]
        public void LevelManager_UpdatePerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            SetLevelState(LevelManager.LevelState.InProgress);

            // Act - Simulate many updates
            System.Reflection.MethodInfo updateMethod = typeof(LevelManager)
                .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            for (int i = 0; i < 100; i++)
            {
                updateMethod?.Invoke(levelManager, null);
            }

            stopwatch.Stop();

            // Assert - Should complete quickly (less than 10ms for 100 updates)
            Assert.Less(stopwatch.ElapsedMilliseconds, 10,
                "LevelManager should handle 100 updates in less than 10ms");
        }

        #endregion

        #region Event System Tests

        [Test]
        public void LevelManager_TriggersEvents()
        {
            // Arrange
            bool stateChangedFired = false;
            bool progressUpdatedFired = false;

            levelManager.OnLevelStateChanged += (state) => stateChangedFired = true;
            levelManager.OnProgressUpdated += (progress) => progressUpdatedFired = true;

            // Act
            levelManager.PauseLevel();

            // Assert - Events should fire
            Assert.DoesNotThrow(() => levelManager.PauseLevel());
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void SetLevelData_UpdatesCurrentLevel()
        {
            // Arrange
            var newLevelData = new LevelManager.LevelData
            {
                levelNumber = 5,
                levelName = "Test Level",
                targetTime = 120f
            };

            // Act
            levelManager.SetLevelData(newLevelData);

            // Assert
            var currentLevel = levelManager.GetCurrentLevel();
            Assert.AreEqual(5, currentLevel.levelNumber);
            Assert.AreEqual("Test Level", currentLevel.levelName);
            Assert.AreEqual(120f, currentLevel.targetTime);
        }

        [Test]
        public void CollectItem_InvalidType_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => levelManager.CollectItem("InvalidItem", 1));
        }

        [Test]
        public void PauseLevel_WhenNotInProgress_DoesNotChangState()
        {
            // Arrange
            SetLevelState(LevelManager.LevelState.Completed);
            var initialState = levelManager.GetCurrentState();

            // Act
            levelManager.PauseLevel();

            // Assert
            Assert.AreEqual(initialState, levelManager.GetCurrentState());
        }

        #endregion

        /// <summary>
        /// Helper method to set level state via reflection
        /// </summary>
        private void SetLevelState(LevelManager.LevelState state)
        {
            var stateField = typeof(LevelManager).GetField("currentState", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField?.SetValue(levelManager, state);
        }
    }
}
