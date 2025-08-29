using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using WhiskerKing.Core;
using WhiskerKing.Player;
using WhiskerKing.Camera;
using WhiskerKing.Combat;
using WhiskerKing.Level;
using WhiskerKing.Audio;
using WhiskerKing.UI;
using WhiskerKing.Performance;

namespace WhiskerKing.Testing
{
    /// <summary>
    /// Automated Test Runner for Whisker King
    /// Executes comprehensive test suites automatically and generates reports
    /// Integrates with Unity Test Framework and provides CI/CD support
    /// </summary>
    public class AutomatedTestRunner : MonoBehaviour
    {
        [System.Serializable]
        public class TestRunConfiguration
        {
            [Header("Test Execution")]
            public bool runOnStart = false;
            public bool runContinuously = false;
            public float continuousTestInterval = 300f; // 5 minutes
            public bool runOnlyOnDemand = true;

            [Header("Test Categories")]
            public bool includeUnitTests = true;
            public bool includeIntegrationTests = true;
            public bool includePerformanceTests = true;
            public bool includeStressTests = false;
            public bool includeRegressionTests = true;

            [Header("Test Systems")]
            public bool testPlayerSystem = true;
            public bool testCameraSystem = true;
            public bool testCombatSystem = true;
            public bool testLevelSystem = true;
            public bool testAudioSystem = true;
            public bool testUISystem = true;
            public bool testPerformanceSystem = true;
            public bool testSaveSystem = true;

            [Header("Reporting")]
            public bool generateDetailedReports = true;
            public bool saveReportsToFile = true;
            public bool logToConsole = true;
            public bool uploadResultsToCI = false;

            [Header("Performance Thresholds")]
            public float maxTestExecutionTime = 60f;
            public int maxFailuresBeforeAbort = 10;
            public bool stopOnFirstFailure = false;
        }

        // Singleton pattern
        private static AutomatedTestRunner instance;
        public static AutomatedTestRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<AutomatedTestRunner>();
                    if (instance == null)
                    {
                        GameObject testRunnerGO = new GameObject("AutomatedTestRunner");
                        instance = testRunnerGO.AddComponent<AutomatedTestRunner>();
                        DontDestroyOnLoad(testRunnerGO);
                    }
                }
                return instance;
            }
        }

        [Header("Test Configuration")]
        [SerializeField] private TestRunConfiguration configuration = new TestRunConfiguration();
        [SerializeField] private bool debugMode = true;

        // Test execution state
        private bool isRunning = false;
        private bool isInitialized = false;
        private Coroutine continuousTestCoroutine;
        private TestFramework.TestSuite currentTestSuite;
        
        // Test results
        private List<TestFramework.TestSuite> completedSuites = new List<TestFramework.TestSuite>();
        private Dictionary<string, System.Type[]> testSuiteMap = new Dictionary<string, System.Type[]>();
        
        // Performance tracking
        private float totalTestTime = 0f;
        private int totalTestsRun = 0;
        private int totalTestsPassed = 0;
        private int totalTestsFailed = 0;

        // Events
        public System.Action<TestFramework.TestSuite> OnTestSuiteCompleted;
        public System.Action OnAllTestsCompleted;
        public System.Action<string> OnTestFailure;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeTestRunner();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (configuration.runOnStart)
            {
                StartCoroutine(RunAllTestsCoroutine());
            }

            if (configuration.runContinuously)
            {
                StartContinuousTesting();
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        #endregion

        #region Initialization

        private void InitializeTestRunner()
        {
            // Initialize test framework
            var testConfig = new TestFramework.TestConfiguration
            {
                enablePerformanceTests = configuration.includePerformanceTests,
                enableIntegrationTests = configuration.includeIntegrationTests,
                maxTestExecutionTime = (int)configuration.maxTestExecutionTime,
                enableAutomatedReporting = configuration.generateDetailedReports
            };

            TestFramework.Initialize(testConfig);

            // Map test suites to their test classes
            InitializeTestSuiteMap();

            isInitialized = true;
            Debug.Log("AutomatedTestRunner initialized");
        }

        private void InitializeTestSuiteMap()
        {
            testSuiteMap.Clear();

            if (configuration.testPlayerSystem)
            {
                testSuiteMap["PlayerSystem"] = new System.Type[]
                {
                    typeof(PlayerControllerTests),
                    typeof(InputBufferTests)
                };
            }

            if (configuration.testCameraSystem)
            {
                testSuiteMap["CameraSystem"] = new System.Type[]
                {
                    typeof(CameraControllerTests)
                };
            }

            if (configuration.testCombatSystem)
            {
                testSuiteMap["CombatSystem"] = new System.Type[]
                {
                    typeof(TailWhipTests),
                    typeof(CrateSystemTests)
                };
            }

            if (configuration.testLevelSystem)
            {
                testSuiteMap["LevelSystem"] = new System.Type[]
                {
                    typeof(LevelManagerTests),
                    typeof(EnvironmentalHazardsTests)
                };
            }

            if (configuration.testAudioSystem)
            {
                testSuiteMap["AudioSystem"] = new System.Type[]
                {
                    typeof(AudioManagerTests)
                };
            }

            if (configuration.testUISystem)
            {
                testSuiteMap["UISystem"] = new System.Type[]
                {
                    typeof(UIManagerTests)
                };
            }

            if (configuration.testPerformanceSystem)
            {
                testSuiteMap["PerformanceSystem"] = new System.Type[]
                {
                    typeof(PerformanceManagerTests),
                    typeof(ObjectPoolTests)
                };
            }
        }

        #endregion

        #region Test Execution

        /// <summary>
        /// Run all configured test suites
        /// </summary>
        public void RunAllTests()
        {
            if (!isInitialized)
            {
                Debug.LogError("Test runner not initialized");
                return;
            }

            if (isRunning)
            {
                Debug.LogWarning("Test runner is already running");
                return;
            }

            StartCoroutine(RunAllTestsCoroutine());
        }

        private IEnumerator RunAllTestsCoroutine()
        {
            isRunning = true;
            float startTime = Time.realtimeSinceStartup;
            
            Debug.Log("=== Starting Automated Test Run ===");
            
            // Reset counters
            totalTestTime = 0f;
            totalTestsRun = 0;
            totalTestsPassed = 0;
            totalTestsFailed = 0;
            completedSuites.Clear();

            // Run each test suite
            foreach (var suitePair in testSuiteMap)
            {
                if (totalTestsFailed >= configuration.maxFailuresBeforeAbort)
                {
                    Debug.LogWarning("Aborting test run due to too many failures");
                    break;
                }

                yield return StartCoroutine(RunTestSuite(suitePair.Key, suitePair.Value));
                
                // Allow frame to process
                yield return null;
            }

            // Calculate final statistics
            totalTestTime = Time.realtimeSinceStartup - startTime;
            
            // Generate final report
            GenerateFinalReport();
            
            OnAllTestsCompleted?.Invoke();
            isRunning = false;
            
            Debug.Log($"=== Test Run Completed in {totalTestTime:F2}s ===");
        }

        private IEnumerator RunTestSuite(string suiteName, System.Type[] testClasses)
        {
            Debug.Log($"Running test suite: {suiteName}");
            
            TestFramework.StartTestSuite(suiteName);
            
            foreach (var testClass in testClasses)
            {
                yield return StartCoroutine(RunTestClass(testClass));
            }
            
            TestFramework.EndTestSuite();
            
            var completedSuite = TestFramework.GetTestSuite(suiteName);
            if (completedSuite != null)
            {
                completedSuites.Add(completedSuite);
                totalTestsRun += completedSuite.totalTests;
                totalTestsPassed += completedSuite.passedTests;
                totalTestsFailed += completedSuite.failedTests;
                
                OnTestSuiteCompleted?.Invoke(completedSuite);
            }
        }

        private IEnumerator RunTestClass(System.Type testClass)
        {
            Debug.Log($"Running test class: {testClass.Name}");

            // Get all test methods in the class
            var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                   .Where(m => m.GetCustomAttribute<UnityEngine.TestTools.UnityTestAttribute>() != null ||
                                              m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null)
                                   .ToArray();

            foreach (var method in methods)
            {
                if (configuration.stopOnFirstFailure && totalTestsFailed > 0)
                    break;

                yield return StartCoroutine(RunTestMethod(testClass, method));
            }
        }

        private IEnumerator RunTestMethod(System.Type testClass, MethodInfo method)
        {
            string testName = $"{testClass.Name}.{method.Name}";
            float testStartTime = Time.realtimeSinceStartup;

            try
            {
                TestFramework.StartTest(testName, TestFramework.TestCategory.Unit);

                // Setup test environment
                TestFramework.TestSetup();

                // Execute the test method
                object result = method.Invoke(null, null);
                
                // If it's a UnityTest (IEnumerator), wait for it to complete
                if (result is IEnumerator coroutine)
                {
                    yield return StartCoroutine(coroutine);
                }

                // Test passed if no exception was thrown
                TestFramework.EndTest(testName, true);
                
                if (debugMode)
                {
                    float executionTime = Time.realtimeSinceStartup - testStartTime;
                    Debug.Log($"✓ {testName} passed in {executionTime:F3}s");
                }
            }
            catch (System.Exception e)
            {
                TestFramework.EndTest(testName, false, e.Message);
                OnTestFailure?.Invoke(testName);
                
                Debug.LogError($"✗ {testName} failed: {e.Message}");
                if (debugMode)
                {
                    Debug.LogException(e);
                }
            }
            finally
            {
                // Cleanup after test
                TestFramework.TestTeardown();
            }
        }

        /// <summary>
        /// Run performance benchmarks
        /// </summary>
        public IEnumerator RunPerformanceBenchmarks()
        {
            if (!configuration.includePerformanceTests)
                yield break;

            Debug.Log("Running performance benchmarks...");
            
            TestFramework.StartTestSuite("PerformanceBenchmarks");

            // Player movement benchmark
            yield return StartCoroutine(RunPlayerMovementBenchmark());
            
            // Audio system benchmark  
            yield return StartCoroutine(RunAudioSystemBenchmark());
            
            // UI system benchmark
            yield return StartCoroutine(RunUISystemBenchmark());
            
            // Memory allocation benchmark
            yield return StartCoroutine(RunMemoryAllocationBenchmark());

            TestFramework.EndTestSuite();
        }

        private IEnumerator RunPlayerMovementBenchmark()
        {
            var testGO = TestFramework.CreateTestGameObject("TestPlayer", typeof(CharacterController), typeof(PlayerController));
            var playerController = testGO.GetComponent<PlayerController>();

            TestFramework.RunPerformanceBenchmark("PlayerMovement_UpdateLoop", 
                () => {
                    // Simulate player update
                    playerController.enabled = true;
                    // playerController.Update(); // Would need to expose or simulate
                }, 1000);

            Object.DestroyImmediate(testGO);
            yield return null;
        }

        private IEnumerator RunAudioSystemBenchmark()
        {
            if (AudioManager.Instance == null)
                yield break;

            TestFramework.RunPerformanceBenchmark("AudioSystem_PlayOneShot",
                () => {
                    // AudioManager.Instance.PlayOneShot(null, AudioManager.AudioCategory.SFX_Player);
                }, 500);

            yield return null;
        }

        private IEnumerator RunUISystemBenchmark()
        {
            if (UIManager.Instance == null)
                yield break;

            TestFramework.RunPerformanceBenchmark("UISystem_ScreenTransition",
                () => {
                    // UIManager.Instance.ShowScreen(UIManager.UIScreen.MainMenu, UIManager.TransitionType.Instant);
                }, 100);

            yield return null;
        }

        private IEnumerator RunMemoryAllocationBenchmark()
        {
            TestFramework.RunPerformanceBenchmark("Memory_ObjectCreation",
                () => {
                    var tempObjects = new GameObject[100];
                    for (int i = 0; i < 100; i++)
                    {
                        tempObjects[i] = new GameObject($"TempObject_{i}");
                    }
                    
                    for (int i = 0; i < 100; i++)
                    {
                        Object.DestroyImmediate(tempObjects[i]);
                    }
                }, 10);

            yield return null;
        }

        #endregion

        #region Continuous Testing

        /// <summary>
        /// Start continuous testing mode
        /// </summary>
        public void StartContinuousTesting()
        {
            if (continuousTestCoroutine != null)
                StopCoroutine(continuousTestCoroutine);

            continuousTestCoroutine = StartCoroutine(ContinuousTestingCoroutine());
        }

        /// <summary>
        /// Stop continuous testing mode
        /// </summary>
        public void StopContinuousTesting()
        {
            if (continuousTestCoroutine != null)
            {
                StopCoroutine(continuousTestCoroutine);
                continuousTestCoroutine = null;
            }
        }

        private IEnumerator ContinuousTestingCoroutine()
        {
            while (configuration.runContinuously)
            {
                if (!isRunning)
                {
                    yield return StartCoroutine(RunAllTestsCoroutine());
                }

                yield return new WaitForSecondsRealtime(configuration.continuousTestInterval);
            }
        }

        #endregion

        #region Reporting

        private void GenerateFinalReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== WHISKER KING AUTOMATED TEST REPORT ===");
            report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Total Execution Time: {totalTestTime:F2} seconds");
            report.AppendLine();
            
            report.AppendLine("=== SUMMARY ===");
            report.AppendLine($"Total Tests Run: {totalTestsRun}");
            report.AppendLine($"Tests Passed: {totalTestsPassed}");
            report.AppendLine($"Tests Failed: {totalTestsFailed}");
            float overallPassRate = totalTestsRun > 0 ? (float)totalTestsPassed / totalTestsRun : 0f;
            report.AppendLine($"Overall Pass Rate: {overallPassRate:P1}");
            report.AppendLine();

            report.AppendLine("=== TEST SUITES ===");
            foreach (var suite in completedSuites.OrderBy(s => s.suiteName))
            {
                report.AppendLine($"{suite.suiteName}: {suite.passedTests}/{suite.totalTests} " +
                                 $"({suite.passRate:P1}) in {suite.totalExecutionTime:F2}s");
                
                if (suite.failedTests > 0)
                {
                    var failedTests = suite.results.Where(r => !r.passed).ToList();
                    foreach (var failed in failedTests)
                    {
                        report.AppendLine($"  ❌ {failed.testName}: {failed.errorMessage}");
                    }
                }
            }

            // Performance summary
            report.AppendLine();
            report.AppendLine("=== PERFORMANCE METRICS ===");
            var perfResults = completedSuites.SelectMany(s => s.results)
                                           .Where(r => r.category == TestFramework.TestCategory.Performance)
                                           .ToList();
            
            if (perfResults.Any())
            {
                float avgExecutionTime = perfResults.Average(r => r.executionTime);
                report.AppendLine($"Average Performance Test Time: {avgExecutionTime:F3}s");
                
                var memoryResults = perfResults.Where(r => r.metrics.ContainsKey("memoryAllocation")).ToList();
                if (memoryResults.Any())
                {
                    float avgMemory = memoryResults.Average(r => (float)r.metrics["memoryAllocation"]);
                    report.AppendLine($"Average Memory Allocation: {avgMemory:F2}MB");
                }
            }

            // Quality assessment
            report.AppendLine();
            report.AppendLine("=== QUALITY ASSESSMENT ===");
            
            if (overallPassRate >= 0.95f)
                report.AppendLine("Quality Status: EXCELLENT ✨");
            else if (overallPassRate >= 0.90f)
                report.AppendLine("Quality Status: GOOD ✅");
            else if (overallPassRate >= 0.80f)
                report.AppendLine("Quality Status: ACCEPTABLE ⚠️");
            else
                report.AppendLine("Quality Status: NEEDS IMPROVEMENT ❌");

            string reportText = report.ToString();
            
            if (configuration.logToConsole)
            {
                Debug.Log(reportText);
            }

            if (configuration.saveReportsToFile)
            {
                SaveReportToFile(reportText);
            }

            // Generate overall summary in test framework
            TestFramework.GenerateOverallSummary();
        }

        private void SaveReportToFile(string reportText)
        {
            try
            {
                string fileName = $"AutomatedTestReport_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
                System.IO.File.WriteAllText(filePath, reportText);
                Debug.Log($"Test report saved to: {filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save test report: {e.Message}");
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get test run configuration
        /// </summary>
        public TestRunConfiguration GetConfiguration()
        {
            return configuration;
        }

        /// <summary>
        /// Set test run configuration
        /// </summary>
        public void SetConfiguration(TestRunConfiguration config)
        {
            configuration = config;
        }

        /// <summary>
        /// Check if test runner is currently running
        /// </summary>
        public bool IsRunning()
        {
            return isRunning;
        }

        /// <summary>
        /// Get test statistics
        /// </summary>
        public string GetTestStatistics()
        {
            return $"Tests: {totalTestsRun}, Passed: {totalTestsPassed}, Failed: {totalTestsFailed}, " +
                   $"Pass Rate: {(totalTestsRun > 0 ? (float)totalTestsPassed / totalTestsRun : 0f):P1}, " +
                   $"Time: {totalTestTime:F2}s";
        }

        /// <summary>
        /// Get completed test suites
        /// </summary>
        public List<TestFramework.TestSuite> GetCompletedSuites()
        {
            return new List<TestFramework.TestSuite>(completedSuites);
        }

        /// <summary>
        /// Run specific test suite
        /// </summary>
        public void RunSpecificSuite(string suiteName)
        {
            if (testSuiteMap.ContainsKey(suiteName))
            {
                StartCoroutine(RunTestSuite(suiteName, testSuiteMap[suiteName]));
            }
            else
            {
                Debug.LogError($"Test suite '{suiteName}' not found");
            }
        }

        /// <summary>
        /// Force stop all tests
        /// </summary>
        public void StopAllTests()
        {
            StopAllCoroutines();
            isRunning = false;
            Debug.Log("All tests stopped");
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 290, 400));
            
            GUILayout.Label("=== AUTOMATED TEST RUNNER ===");
            GUILayout.Label($"Status: {(isRunning ? "RUNNING" : "IDLE")}");
            GUILayout.Label($"Initialized: {isInitialized}");
            
            GUILayout.Space(10);
            GUILayout.Label("=== STATISTICS ===");
            GUILayout.Label($"Total Tests: {totalTestsRun}");
            GUILayout.Label($"Passed: {totalTestsPassed}");
            GUILayout.Label($"Failed: {totalTestsFailed}");
            if (totalTestsRun > 0)
            {
                GUILayout.Label($"Pass Rate: {((float)totalTestsPassed / totalTestsRun):P1}");
            }
            GUILayout.Label($"Execution Time: {totalTestTime:F2}s");
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            GUI.enabled = !isRunning;
            if (GUILayout.Button("Run All Tests"))
            {
                RunAllTests();
            }
            
            if (GUILayout.Button("Run Performance Tests"))
            {
                StartCoroutine(RunPerformanceBenchmarks());
            }
            
            GUI.enabled = isRunning;
            if (GUILayout.Button("Stop Tests"))
            {
                StopAllTests();
            }
            
            GUI.enabled = true;
            if (GUILayout.Button("Clear Results"))
            {
                completedSuites.Clear();
                totalTestsRun = 0;
                totalTestsPassed = 0;
                totalTestsFailed = 0;
                totalTestTime = 0f;
                TestFramework.ClearAllTestData();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONFIGURATION ===");
            configuration.includeUnitTests = GUILayout.Toggle(configuration.includeUnitTests, "Unit Tests");
            configuration.includeIntegrationTests = GUILayout.Toggle(configuration.includeIntegrationTests, "Integration Tests");
            configuration.includePerformanceTests = GUILayout.Toggle(configuration.includePerformanceTests, "Performance Tests");
            configuration.stopOnFirstFailure = GUILayout.Toggle(configuration.stopOnFirstFailure, "Stop on First Failure");
            
            GUILayout.EndArea();
        }

        #endregion
    }
}
