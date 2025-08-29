using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WhiskerKing.Core;

namespace WhiskerKing.Testing
{
    /// <summary>
    /// Comprehensive Test Framework for Whisker King
    /// Provides testing infrastructure, utilities, and automated quality assurance tools
    /// Integrates with Unity Test Framework for unit and integration testing
    /// </summary>
    public static class TestFramework
    {
        public enum TestCategory
        {
            Unit,
            Integration,
            Performance,
            EndToEnd,
            Regression,
            Stress
        }

        public enum TestPriority
        {
            Critical = 0,
            High = 1,
            Medium = 2,
            Low = 3
        }

        [System.Serializable]
        public class TestResult
        {
            public string testName;
            public TestCategory category;
            public TestPriority priority;
            public bool passed;
            public float executionTime;
            public string errorMessage;
            public System.DateTime timestamp;
            public Dictionary<string, object> metrics = new Dictionary<string, object>();
        }

        [System.Serializable]
        public class TestSuite
        {
            public string suiteName;
            public List<TestResult> results = new List<TestResult>();
            public float totalExecutionTime;
            public int passedTests;
            public int failedTests;
            public float passRate => totalTests > 0 ? (float)passedTests / totalTests : 0f;
            public int totalTests => results.Count;
            public System.DateTime startTime;
            public System.DateTime endTime;
        }

        [System.Serializable]
        public class TestConfiguration
        {
            [Header("Test Execution")]
            public bool enablePerformanceTests = true;
            public bool enableIntegrationTests = true;
            public bool enableStressTests = false;
            public int maxTestExecutionTime = 60; // seconds
            public int testTimeoutDuration = 30; // seconds

            [Header("Performance Thresholds")]
            public float maxFrameTime = 16.67f; // 60 FPS
            public int maxMemoryAllocation = 10; // MB
            public float maxLoadTime = 5f; // seconds
            public int maxDrawCalls = 300;
            public int maxTriangles = 50000;

            [Header("Quality Assurance")]
            public bool enableCodeCoverage = true;
            public bool enableAutomatedReporting = true;
            public bool failOnWarnings = false;
            public bool enablePerformanceRegression = true;
            public float performanceRegressionThreshold = 0.1f; // 10%

            [Header("Test Data")]
            public bool useTestData = true;
            public string testDataPath = "Assets/Testing/TestData/";
            public bool cleanupAfterTests = true;
        }

        // Static configuration and state
        private static TestConfiguration configuration = new TestConfiguration();
        private static Dictionary<string, TestSuite> testSuites = new Dictionary<string, TestSuite>();
        private static TestSuite currentSuite;
        private static Stopwatch testStopwatch = new Stopwatch();
        private static List<TestResult> allResults = new List<TestResult>();

        // Performance tracking
        private static float initialMemory;
        private static int initialDrawCalls;
        private static float testStartTime;

        #region Test Framework Setup

        /// <summary>
        /// Initialize the test framework
        /// </summary>
        public static void Initialize(TestConfiguration config = null)
        {
            if (config != null)
                configuration = config;

            testSuites.Clear();
            allResults.Clear();

            UnityEngine.Debug.Log("WhiskerKing Test Framework initialized");
        }

        /// <summary>
        /// Start a new test suite
        /// </summary>
        public static void StartTestSuite(string suiteName)
        {
            currentSuite = new TestSuite
            {
                suiteName = suiteName,
                startTime = System.DateTime.Now
            };

            testSuites[suiteName] = currentSuite;
            UnityEngine.Debug.Log($"Started test suite: {suiteName}");
        }

        /// <summary>
        /// End the current test suite
        /// </summary>
        public static void EndTestSuite()
        {
            if (currentSuite == null) return;

            currentSuite.endTime = System.DateTime.Now;
            currentSuite.totalExecutionTime = (float)(currentSuite.endTime - currentSuite.startTime).TotalSeconds;
            currentSuite.passedTests = currentSuite.results.Count(r => r.passed);
            currentSuite.failedTests = currentSuite.results.Count(r => !r.passed);

            UnityEngine.Debug.Log($"Test suite '{currentSuite.suiteName}' completed: " +
                                 $"{currentSuite.passedTests}/{currentSuite.totalTests} passed " +
                                 $"({currentSuite.passRate:P1}) in {currentSuite.totalExecutionTime:F2}s");

            if (configuration.enableAutomatedReporting)
            {
                GenerateTestReport(currentSuite);
            }

            currentSuite = null;
        }

        #endregion

        #region Test Execution Utilities

        /// <summary>
        /// Start a test with performance tracking
        /// </summary>
        public static void StartTest(string testName, TestCategory category = TestCategory.Unit, 
                                   TestPriority priority = TestPriority.Medium)
        {
            testStopwatch.Restart();
            testStartTime = Time.realtimeSinceStartup;
            
            // Track initial performance metrics
            initialMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin);
            
            UnityEngine.Debug.Log($"Starting test: {testName} [{category}] [{priority}]");
        }

        /// <summary>
        /// End a test and record results
        /// </summary>
        public static TestResult EndTest(string testName, bool passed, string errorMessage = null)
        {
            testStopwatch.Stop();
            
            var result = new TestResult
            {
                testName = testName,
                passed = passed,
                executionTime = testStopwatch.ElapsedMilliseconds / 1000f,
                errorMessage = errorMessage,
                timestamp = System.DateTime.Now
            };

            // Add performance metrics
            float finalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin);
            float memoryDelta = (finalMemory - initialMemory) / (1024f * 1024f); // MB
            
            result.metrics["memoryAllocation"] = memoryDelta;
            result.metrics["executionTimeMs"] = testStopwatch.ElapsedMilliseconds;
            result.metrics["frameTime"] = Time.realtimeSinceStartup - testStartTime;

            // Add to current suite and global results
            currentSuite?.results.Add(result);
            allResults.Add(result);

            string status = passed ? "PASSED" : "FAILED";
            UnityEngine.Debug.Log($"Test {testName}: {status} in {result.executionTime:F3}s");
            
            if (!passed && !string.IsNullOrEmpty(errorMessage))
            {
                UnityEngine.Debug.LogError($"Test failure: {errorMessage}");
            }

            return result;
        }

        /// <summary>
        /// Assert with custom message and context
        /// </summary>
        public static void Assert(bool condition, string message, object context = null)
        {
            if (!condition)
            {
                string contextInfo = context != null ? $" Context: {context}" : "";
                throw new AssertionException($"Assertion failed: {message}{contextInfo}");
            }
        }

        /// <summary>
        /// Assert equality with tolerance for floats
        /// </summary>
        public static void AssertEqual(float expected, float actual, float tolerance = 0.001f, string message = null)
        {
            if (Mathf.Abs(expected - actual) > tolerance)
            {
                string msg = message ?? $"Expected {expected}, but was {actual} (tolerance: {tolerance})";
                throw new AssertionException(msg);
            }
        }

        /// <summary>
        /// Assert Vector3 equality with tolerance
        /// </summary>
        public static void AssertEqual(Vector3 expected, Vector3 actual, float tolerance = 0.001f, string message = null)
        {
            float distance = Vector3.Distance(expected, actual);
            if (distance > tolerance)
            {
                string msg = message ?? $"Expected {expected}, but was {actual} (distance: {distance}, tolerance: {tolerance})";
                throw new AssertionException(msg);
            }
        }

        /// <summary>
        /// Assert performance metrics are within acceptable bounds
        /// </summary>
        public static void AssertPerformance(float executionTime, float memoryAllocation = 0f, string message = null)
        {
            bool performanceOk = true;
            string issues = "";

            if (executionTime > configuration.maxFrameTime)
            {
                performanceOk = false;
                issues += $"Execution time {executionTime:F3}ms exceeds limit {configuration.maxFrameTime:F3}ms. ";
            }

            if (memoryAllocation > configuration.maxMemoryAllocation)
            {
                performanceOk = false;
                issues += $"Memory allocation {memoryAllocation:F2}MB exceeds limit {configuration.maxMemoryAllocation}MB. ";
            }

            if (!performanceOk)
            {
                string msg = message ?? "Performance assertion failed";
                throw new AssertionException($"{msg}: {issues}");
            }
        }

        #endregion

        #region Test Data Management

        /// <summary>
        /// Create test GameObject with required components
        /// </summary>
        public static GameObject CreateTestGameObject(string name, params System.Type[] components)
        {
            GameObject testObject = new GameObject(name);
            
            foreach (var componentType in components)
            {
                testObject.AddComponent(componentType);
            }

            return testObject;
        }

        /// <summary>
        /// Load test configuration from resources
        /// </summary>
        public static T LoadTestData<T>(string resourcePath) where T : ScriptableObject
        {
            T data = Resources.Load<T>(resourcePath);
            if (data == null)
            {
                UnityEngine.Debug.LogWarning($"Test data not found: {resourcePath}");
            }
            return data;
        }

        /// <summary>
        /// Create mock configuration for testing
        /// </summary>
        public static GameConfiguration.Config CreateMockGameConfig()
        {
            return new GameConfiguration.Config
            {
                gameInfo = new GameInfo
                {
                    name = "Whisker King Test",
                    version = "1.0.0-test",
                    buildNumber = 1
                },
                performanceTargets = new PerformanceData
                {
                    frameRate = new FrameRateData
                    {
                        target = 60,
                        minimum = 30,
                        maxFrameTime = 16.67f
                    }
                }
            };
        }

        /// <summary>
        /// Cleanup test objects
        /// </summary>
        public static void CleanupTestObjects()
        {
            if (!configuration.cleanupAfterTests) return;

            GameObject[] testObjects = GameObject.FindGameObjectsWithTag("TestObject");
            foreach (var obj in testObjects)
            {
                Object.DestroyImmediate(obj);
            }

            // Force garbage collection
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Performance Testing

        /// <summary>
        /// Measure execution time of an action
        /// </summary>
        public static float MeasureExecutionTime(System.Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action?.Invoke();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds / 1000f;
        }

        /// <summary>
        /// Measure memory allocation of an action
        /// </summary>
        public static float MeasureMemoryAllocation(System.Action action)
        {
            float initialMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin);
            action?.Invoke();
            float finalMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin);
            
            return (finalMemory - initialMemory) / (1024f * 1024f); // MB
        }

        /// <summary>
        /// Run performance benchmark
        /// </summary>
        public static TestResult RunPerformanceBenchmark(string benchmarkName, System.Action benchmark, 
                                                        int iterations = 100)
        {
            StartTest(benchmarkName, TestCategory.Performance);

            float totalTime = 0f;
            float totalMemory = 0f;
            bool passed = true;
            string errorMessage = null;

            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    float iterationTime = MeasureExecutionTime(benchmark);
                    float iterationMemory = MeasureMemoryAllocation(benchmark);
                    
                    totalTime += iterationTime;
                    totalMemory += iterationMemory;
                }

                float averageTime = totalTime / iterations;
                float averageMemory = totalMemory / iterations;

                // Check performance thresholds
                if (averageTime > configuration.maxFrameTime / 1000f)
                {
                    passed = false;
                    errorMessage = $"Average execution time {averageTime * 1000f:F2}ms exceeds threshold {configuration.maxFrameTime:F2}ms";
                }

                if (averageMemory > configuration.maxMemoryAllocation)
                {
                    passed = false;
                    errorMessage += $" Average memory allocation {averageMemory:F2}MB exceeds threshold {configuration.maxMemoryAllocation}MB";
                }
            }
            catch (System.Exception e)
            {
                passed = false;
                errorMessage = $"Benchmark failed with exception: {e.Message}";
            }

            var result = EndTest(benchmarkName, passed, errorMessage);
            result.metrics["iterations"] = iterations;
            result.metrics["averageTime"] = totalTime / iterations;
            result.metrics["averageMemory"] = totalMemory / iterations;
            result.metrics["totalTime"] = totalTime;
            result.metrics["totalMemory"] = totalMemory;

            return result;
        }

        #endregion

        #region Test Reporting

        /// <summary>
        /// Generate detailed test report
        /// </summary>
        public static void GenerateTestReport(TestSuite suite)
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== WHISKER KING TEST REPORT ===");
            report.AppendLine($"Suite: {suite.suiteName}");
            report.AppendLine($"Executed: {suite.startTime:yyyy-MM-dd HH:mm:ss} - {suite.endTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Duration: {suite.totalExecutionTime:F2} seconds");
            report.AppendLine();
            
            report.AppendLine("=== SUMMARY ===");
            report.AppendLine($"Total Tests: {suite.totalTests}");
            report.AppendLine($"Passed: {suite.passedTests}");
            report.AppendLine($"Failed: {suite.failedTests}");
            report.AppendLine($"Pass Rate: {suite.passRate:P1}");
            report.AppendLine();

            if (suite.failedTests > 0)
            {
                report.AppendLine("=== FAILED TESTS ===");
                foreach (var result in suite.results.Where(r => !r.passed))
                {
                    report.AppendLine($"❌ {result.testName}");
                    report.AppendLine($"   Error: {result.errorMessage}");
                    report.AppendLine($"   Time: {result.executionTime:F3}s");
                    report.AppendLine();
                }
            }

            report.AppendLine("=== PERFORMANCE METRICS ===");
            var perfResults = suite.results.Where(r => r.category == TestCategory.Performance).ToList();
            if (perfResults.Any())
            {
                float avgTime = perfResults.Average(r => r.executionTime);
                float avgMemory = perfResults.Where(r => r.metrics.ContainsKey("memoryAllocation"))
                                           .Average(r => (float)r.metrics["memoryAllocation"]);
                
                report.AppendLine($"Average Test Time: {avgTime:F3}s");
                report.AppendLine($"Average Memory Allocation: {avgMemory:F2}MB");
            }

            string reportText = report.ToString();
            UnityEngine.Debug.Log(reportText);

            // Save to file if possible
            try
            {
                string reportPath = $"{configuration.testDataPath}TestReport_{suite.suiteName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
                System.IO.File.WriteAllText(reportPath, reportText);
                UnityEngine.Debug.Log($"Test report saved to: {reportPath}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"Could not save test report: {e.Message}");
            }
        }

        /// <summary>
        /// Generate overall test summary
        /// </summary>
        public static void GenerateOverallSummary()
        {
            var summary = new System.Text.StringBuilder();
            
            summary.AppendLine("=== WHISKER KING OVERALL TEST SUMMARY ===");
            summary.AppendLine($"Test Suites: {testSuites.Count}");
            summary.AppendLine($"Total Tests: {allResults.Count}");
            summary.AppendLine($"Overall Pass Rate: {(allResults.Count(r => r.passed) / (float)allResults.Count):P1}");
            summary.AppendLine();

            foreach (var suite in testSuites.Values.OrderBy(s => s.startTime))
            {
                summary.AppendLine($"Suite '{suite.suiteName}': {suite.passedTests}/{suite.totalTests} " +
                                 $"({suite.passRate:P1}) in {suite.totalExecutionTime:F2}s");
            }

            UnityEngine.Debug.Log(summary.ToString());
        }

        #endregion

        #region Quality Assurance

        /// <summary>
        /// Run code quality checks
        /// </summary>
        public static TestResult RunCodeQualityChecks()
        {
            StartTest("CodeQualityChecks", TestCategory.Unit, TestPriority.High);
            
            bool passed = true;
            var issues = new List<string>();

            // Check for common code issues
            CheckForEmptyMonoBehaviours(issues);
            CheckForUnusedVariables(issues);
            CheckForPerformanceIssues(issues);
            CheckForMemoryLeaks(issues);

            if (issues.Count > 0)
            {
                passed = !configuration.failOnWarnings;
                string errorMessage = "Code quality issues found:\n" + string.Join("\n", issues);
                return EndTest("CodeQualityChecks", passed, errorMessage);
            }

            return EndTest("CodeQualityChecks", true);
        }

        private static void CheckForEmptyMonoBehaviours(List<string> issues)
        {
            // Placeholder for code analysis
            // In a real implementation, this would analyze the codebase
        }

        private static void CheckForUnusedVariables(List<string> issues)
        {
            // Placeholder for unused variable detection
        }

        private static void CheckForPerformanceIssues(List<string> issues)
        {
            // Check for common Unity performance issues
            if (Object.FindObjectsOfType<MonoBehaviour>().Length > 1000)
            {
                issues.Add("High number of MonoBehaviour instances detected");
            }
        }

        private static void CheckForMemoryLeaks(List<string> issues)
        {
            // Basic memory leak detection
            float currentMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin) / (1024f * 1024f);
            if (currentMemory > 100f) // 100MB threshold
            {
                issues.Add($"High memory usage detected: {currentMemory:F2}MB");
            }
        }

        #endregion

        #region Integration with Unity Test Framework

        /// <summary>
        /// Custom test attribute for Whisker King tests
        /// </summary>
        public class WhiskerKingTestAttribute : TestAttribute
        {
            public TestCategory Category { get; set; } = TestCategory.Unit;
            public TestPriority Priority { get; set; } = TestPriority.Medium;
            public bool RequiresConfiguration { get; set; } = false;
        }

        /// <summary>
        /// Setup method to be called before each test
        /// </summary>
        public static void TestSetup()
        {
            // Initialize test environment
            if (GameConfiguration.Instance == null)
            {
                var configGO = new GameObject("TestGameConfiguration");
                configGO.AddComponent<GameConfiguration>();
            }

            // Clean up any existing test objects
            CleanupTestObjects();
        }

        /// <summary>
        /// Teardown method to be called after each test
        /// </summary>
        public static void TestTeardown()
        {
            CleanupTestObjects();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get test configuration
        /// </summary>
        public static TestConfiguration GetConfiguration()
        {
            return configuration;
        }

        /// <summary>
        /// Set test configuration
        /// </summary>
        public static void SetConfiguration(TestConfiguration config)
        {
            configuration = config;
        }

        /// <summary>
        /// Get all test results
        /// </summary>
        public static List<TestResult> GetAllResults()
        {
            return new List<TestResult>(allResults);
        }

        /// <summary>
        /// Get test suite by name
        /// </summary>
        public static TestSuite GetTestSuite(string suiteName)
        {
            return testSuites.TryGetValue(suiteName, out TestSuite suite) ? suite : null;
        }

        /// <summary>
        /// Clear all test data
        /// </summary>
        public static void ClearAllTestData()
        {
            testSuites.Clear();
            allResults.Clear();
            currentSuite = null;
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown by test framework assertions
    /// </summary>
    public class AssertionException : System.Exception
    {
        public AssertionException(string message) : base(message) { }
        public AssertionException(string message, System.Exception innerException) : base(message, innerException) { }
    }
}
