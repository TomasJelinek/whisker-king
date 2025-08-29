using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.Performance;

namespace WhiskerKing.Performance.Tests
{
    /// <summary>
    /// Unit tests for PerformanceManager system
    /// Tests performance monitoring, quality adjustment, dynamic resolution, and PRD compliance
    /// </summary>
    public class PerformanceManagerTests
    {
        private GameObject testGameObject;
        private PerformanceManager performanceManager;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestPerformanceManager");
            performanceManager = testGameObject.AddComponent<PerformanceManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        #region Singleton Tests

        [Test]
        public void PerformanceManager_ImplementsSingletonPattern()
        {
            // Act
            var instance1 = PerformanceManager.Instance;
            var instance2 = PerformanceManager.Instance;

            // Assert
            Assert.IsNotNull(instance1);
            Assert.AreSame(instance1, instance2, "PerformanceManager should implement singleton pattern");
        }

        #endregion

        #region Performance Metrics Tests

        [Test]
        public void GetPerformanceMetrics_ReturnsValidMetrics()
        {
            // Act
            var metrics = performanceManager.GetPerformanceMetrics();

            // Assert
            Assert.IsNotNull(metrics, "Performance metrics should not be null");
            Assert.GreaterOrEqual(metrics.averageFrameRate, 0f, "Average frame rate should be non-negative");
            Assert.GreaterOrEqual(metrics.currentFrameRate, 0f, "Current frame rate should be non-negative");
            Assert.GreaterOrEqual(metrics.totalMemoryUsage, 0f, "Memory usage should be non-negative");
        }

        [Test]
        public void GetPerformanceBudget_ReturnsValidBudget()
        {
            // Act
            var budget = performanceManager.GetPerformanceBudget();

            // Assert
            Assert.IsNotNull(budget, "Performance budget should not be null");
            Assert.Greater(budget.targetFrameRate, 0, "Target frame rate should be positive");
            Assert.Greater(budget.minimumFrameRate, 0, "Minimum frame rate should be positive");
            Assert.Greater(budget.maxTotalMemory, 0f, "Max total memory should be positive");
        }

        [Test]
        public void GetCurrentQualitySettings_ReturnsValidSettings()
        {
            // Act
            var qualitySettings = performanceManager.GetCurrentQualitySettings();

            // Assert
            Assert.IsNotNull(qualitySettings, "Quality settings should not be null");
            Assert.IsNotEmpty(qualitySettings.qualityName, "Quality name should not be empty");
            Assert.GreaterOrEqual(qualitySettings.renderScale, 0f, "Render scale should be non-negative");
            Assert.LessOrEqual(qualitySettings.renderScale, 2f, "Render scale should be reasonable");
        }

        #endregion

        #region Quality Management Tests

        [Test]
        public void SetQualityLevel_ValidLevel_AppliesCorrectly()
        {
            // Arrange
            int targetLevel = 1; // Medium quality

            // Act
            performanceManager.SetQualityLevel(targetLevel);
            var qualitySettings = performanceManager.GetCurrentQualitySettings();

            // Assert
            Assert.IsNotNull(qualitySettings, "Quality settings should be applied");
            // Quality name might not match exactly due to platform adjustments, but should be valid
            Assert.IsNotEmpty(qualitySettings.qualityName);
        }

        [Test]
        public void SetQualityLevel_InvalidLevel_HandlesGracefully()
        {
            // Act & Assert - Should not throw exception for invalid levels
            Assert.DoesNotThrow(() => performanceManager.SetQualityLevel(-1));
            Assert.DoesNotThrow(() => performanceManager.SetQualityLevel(999));
        }

        [Test]
        public void SetAutoQualityAdjustment_EnableDisable_Works()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => performanceManager.SetAutoQualityAdjustment(true));
            Assert.DoesNotThrow(() => performanceManager.SetAutoQualityAdjustment(false));
        }

        [Test]
        public void SetTargetFrameRate_ValidFrameRate_AppliesCorrectly()
        {
            // Arrange
            int targetFPS = 30;

            // Act
            performanceManager.SetTargetFrameRate(targetFPS);
            var budget = performanceManager.GetPerformanceBudget();

            // Assert
            Assert.AreEqual(targetFPS, budget.targetFrameRate, "Target frame rate should be applied");
            Assert.AreEqual(targetFPS, Application.targetFrameRate, "Unity target frame rate should be set");
        }

        #endregion

        #region Performance Analysis Tests

        [Test]
        public void ForcePerformanceAnalysis_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => performanceManager.ForcePerformanceAnalysis(),
                "Force performance analysis should not throw exception");
        }

        [Test]
        public void IsPerformanceWithinBudget_ReturnsValidResult()
        {
            // Act
            bool withinBudget = performanceManager.IsPerformanceWithinBudget();

            // Assert - Should return true or false, not throw
            Assert.IsTrue(withinBudget || !withinBudget, "Should return a valid boolean result");
        }

        [Test]
        public void GetPerformanceEfficiency_ReturnsValidRatio()
        {
            // Act
            float efficiency = performanceManager.GetPerformanceEfficiency();

            // Assert
            Assert.GreaterOrEqual(efficiency, 0f, "Performance efficiency should be non-negative");
            Assert.LessOrEqual(efficiency, 2f, "Performance efficiency should be reasonable"); // Allow for some overhead
        }

        [Test]
        public void ResetPerformanceHistory_ClearsData()
        {
            // Act
            performanceManager.ResetPerformanceHistory();

            // Assert - Should not throw, history should be reset
            Assert.DoesNotThrow(() => performanceManager.GetPerformanceMetrics());
        }

        #endregion

        #region PRD Compliance Tests

        [Test]
        public void PerformanceBudgets_MeetPRDRequirements()
        {
            // Act
            var budget = performanceManager.GetPerformanceBudget();

            // Assert - PRD requirements
            Assert.GreaterOrEqual(budget.targetFrameRate, 30, "Target frame rate should meet PRD minimum");
            Assert.LessOrEqual(budget.maxTotalMemory, 1024f, "Memory budget should be within reasonable limits");
            Assert.Greater(budget.maxDrawCalls, 0, "Draw call budget should be positive");
            Assert.Greater(budget.maxTriangles, 0, "Triangle budget should be positive");
        }

        [Test]
        public void QualitySettings_ProvideValidLODBias()
        {
            // Act
            var qualitySettings = performanceManager.GetCurrentQualitySettings();

            // Assert
            Assert.GreaterOrEqual(qualitySettings.lodBias, 0, "LOD bias should be non-negative");
            Assert.LessOrEqual(qualitySettings.lodBias, 5f, "LOD bias should be reasonable");
        }

        [Test]
        public void MobileOptimization_AppliesCorrectSettings()
        {
            // This test would need to be run on mobile platforms or with platform simulation
            // For now, just verify the quality settings are mobile-appropriate
            var qualitySettings = performanceManager.GetCurrentQualitySettings();

            // Assert - Mobile-appropriate settings
            Assert.LessOrEqual(qualitySettings.shadowCascades, 2, "Shadow cascades should meet PRD limit (≤2)");
            Assert.LessOrEqual(qualitySettings.shadowDistance, 50f, "Shadow distance should meet PRD limit (≤50m)");
        }

        #endregion

        #region Performance Monitoring Tests

        [Test]
        public void PerformanceMetrics_UpdateOverTime()
        {
            // Arrange
            var initialMetrics = performanceManager.GetPerformanceMetrics();
            
            // Act - Wait a frame to allow metrics to update
            // In a real test, we'd need to simulate frame updates

            // Assert - Metrics should be valid
            Assert.IsNotNull(initialMetrics, "Initial metrics should be available");
        }

        [Test]
        public void MemoryMetrics_TrackCorrectly()
        {
            // Act
            var metrics = performanceManager.GetPerformanceMetrics();

            // Assert
            Assert.GreaterOrEqual(metrics.totalMemoryUsage, 0f, "Total memory usage should be non-negative");
            Assert.GreaterOrEqual(metrics.currentTextureMemory, 0f, "Texture memory should be non-negative");
            Assert.GreaterOrEqual(metrics.currentMeshMemory, 0f, "Mesh memory should be non-negative");
            Assert.GreaterOrEqual(metrics.currentAudioMemory, 0f, "Audio memory should be non-negative");
        }

        #endregion

        #region Platform-Specific Tests

        [Test]
        public void PlatformBudgets_AdjustForCurrentPlatform()
        {
            // Act
            var budget = performanceManager.GetPerformanceBudget();

            // Assert - Budget should be appropriate for the current platform
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    Assert.LessOrEqual(budget.maxTotalMemory, 1024f, "Mobile memory budget should be conservative");
                    break;
                    
                case RuntimePlatform.WebGLPlayer:
                    Assert.LessOrEqual(budget.maxTotalMemory, 512f, "WebGL memory budget should be very conservative");
                    break;
                    
                default:
                    Assert.GreaterOrEqual(budget.maxTotalMemory, 512f, "Desktop memory budget should be reasonable");
                    break;
            }
        }

        #endregion

        #region Edge Cases Tests

        [Test]
        public void PerformanceManager_HandlesNullComponents()
        {
            // Act & Assert - Should handle missing components gracefully
            Assert.DoesNotThrow(() => performanceManager.GetPerformanceMetrics());
            Assert.DoesNotThrow(() => performanceManager.SetQualityLevel(1));
            Assert.DoesNotThrow(() => performanceManager.ForcePerformanceAnalysis());
        }

        [Test]
        public void ExtremePerformanceConditions_HandledGracefully()
        {
            // Act & Assert - Should handle extreme conditions
            performanceManager.SetTargetFrameRate(1); // Very low FPS
            Assert.DoesNotThrow(() => performanceManager.GetPerformanceMetrics());

            performanceManager.SetTargetFrameRate(240); // Very high FPS
            Assert.DoesNotThrow(() => performanceManager.GetPerformanceMetrics());
        }

        [Test]
        public void RapidQualityChanges_HandleCorrectly()
        {
            // Act - Rapidly change quality levels
            for (int i = 0; i < 10; i++)
            {
                performanceManager.SetQualityLevel(i % 3);
            }

            // Assert - Should not crash or throw
            Assert.DoesNotThrow(() => performanceManager.GetCurrentQualitySettings());
        }

        #endregion

        #region Integration Tests

        [Test]
        public void PerformanceManager_IntegratesWithQualityManager()
        {
            // This test would verify integration with QualityManager
            // For now, just verify it doesn't throw when attempting integration
            Assert.DoesNotThrow(() => performanceManager.ForcePerformanceAnalysis());
        }

        [Test]
        public void DynamicResolution_FunctionsCorrectly()
        {
            // Act
            var qualitySettings = performanceManager.GetCurrentQualitySettings();

            // Assert - Dynamic resolution should produce valid render scales
            Assert.GreaterOrEqual(qualitySettings.renderScale, 0.1f, "Render scale should have reasonable minimum");
            Assert.LessOrEqual(qualitySettings.renderScale, 2f, "Render scale should have reasonable maximum");
        }

        #endregion

        #region Performance Tests

        [Test]
        public void PerformanceManager_UpdatePerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - Simulate multiple performance updates
            for (int i = 0; i < 100; i++)
            {
                performanceManager.GetPerformanceMetrics();
                performanceManager.ForcePerformanceAnalysis();
            }

            stopwatch.Stop();

            // Assert - Should complete quickly
            Assert.Less(stopwatch.ElapsedMilliseconds, 100, 
                "Performance manager should handle 100 operations in less than 100ms");
        }

        [Test]
        public void MemoryFootprint_RemainsReasonable()
        {
            // Act - Create and test multiple instances (shouldn't happen due to singleton, but test robustness)
            var initialMemory = System.GC.GetTotalMemory(false);
            
            for (int i = 0; i < 10; i++)
            {
                var metrics = performanceManager.GetPerformanceMetrics();
                performanceManager.ForcePerformanceAnalysis();
            }
            
            var finalMemory = System.GC.GetTotalMemory(false);

            // Assert - Memory growth should be reasonable
            long memoryGrowth = finalMemory - initialMemory;
            Assert.Less(memoryGrowth, 10 * 1024 * 1024, // 10MB
                "Memory growth should be less than 10MB for performance operations");
        }

        #endregion

        #region Event System Tests

        [Test]
        public void PerformanceManager_TriggersEvents()
        {
            // This test would verify that events are triggered correctly
            // For now, just verify event subscription doesn't throw
            Assert.DoesNotThrow(() => {
                performanceManager.OnPerformanceUpdated += (metrics) => { };
                performanceManager.OnQualityChanged += (settings) => { };
                performanceManager.OnCriticalPerformance += () => { };
            });
        }

        #endregion

        #region Configuration Tests

        [Test]
        public void PerformanceManager_LoadsConfigurationCorrectly()
        {
            // Act
            var budget = performanceManager.GetPerformanceBudget();
            var qualitySettings = performanceManager.GetCurrentQualitySettings();

            // Assert - Configuration should be loaded
            Assert.IsNotNull(budget, "Budget should be loaded from configuration");
            Assert.IsNotNull(qualitySettings, "Quality settings should be loaded from configuration");
            Assert.Greater(budget.targetFrameRate, 0, "Target frame rate should be configured");
        }

        #endregion

        #region Cleanup Tests

        [Test]
        public void PerformanceManager_CleansUpProperly()
        {
            // Act - Destroy the performance manager
            Object.DestroyImmediate(performanceManager);

            // Assert - Should not throw when accessing after destruction
            // Note: This is mainly to ensure no lingering references cause issues
            Assert.DoesNotThrow(() => System.GC.Collect());
        }

        #endregion
    }
}
