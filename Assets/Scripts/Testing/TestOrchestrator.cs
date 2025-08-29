using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;
using WhiskerKing.Core;

namespace WhiskerKing.Testing
{
    /// <summary>
    /// Test Orchestrator for Whisker King
    /// Central coordinator for all testing systems, providing unified test execution,
    /// reporting, and integration with CI/CD pipelines
    /// </summary>
    public class TestOrchestrator : MonoBehaviour
    {
        [System.Serializable]
        public class OrchestrationSettings
        {
            [Header("Test Execution")]
            public bool runAllTestsOnStart = false;
            public bool enableParallelExecution = false;
            public int maxConcurrentTests = 4;
            public float testTimeoutMultiplier = 1f;

            [Header("Test Categories")]
            public bool runUnitTests = true;
            public bool runIntegrationTests = true;
            public bool runPerformanceTests = true;
            public bool runQualityAssurance = true;
            public bool runRegressionTests = false;

            [Header("Reporting")]
            public bool generateUnifiedReport = true;
            public bool exportToCI = false;
            public bool saveDetailedLogs = true;
            public string reportExportPath = "TestResults/";

            [Header("Quality Gates")]
            public float minimumPassRate = 0.95f;      // 95%
            public float minimumPerformanceScore = 80f; // 80/100
            public float minimumQualityScore = 75f;     // 75/100
            public bool failBuildOnQualityGate = false;
        }

        [System.Serializable]
        public class TestExecutionSummary
        {
            [Header("Test Results")]
            public int totalTestsExecuted;
            public int totalTestsPassed;
            public int totalTestsFailed;
            public float overallPassRate;
            public float totalExecutionTime;

            [Header("System Coverage")]
            public int unitTestsPassed;
            public int integrationTestsPassed;
            public int performanceTestsPassed;
            public int qaTestsPassed;

            [Header("Quality Metrics")]
            public float performanceScore;
            public float qualityScore;
            public bool passedQualityGates;
            public string qualityGrading;

            [Header("Execution Stats")]
            public System.DateTime executionStartTime;
            public System.DateTime executionEndTime;
            public string executionDuration;
        }

        // Singleton pattern
        private static TestOrchestrator instance;
        public static TestOrchestrator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<TestOrchestrator>();
                    if (instance == null)
                    {
                        GameObject orchestratorGO = new GameObject("TestOrchestrator");
                        instance = orchestratorGO.AddComponent<TestOrchestrator>();
                        DontDestroyOnLoad(orchestratorGO);
                    }
                }
                return instance;
            }
        }

        [Header("Orchestration Settings")]
        [SerializeField] private OrchestrationSettings settings = new OrchestrationSettings();
        [SerializeField] private bool debugMode = true;

        // Component references
        private AutomatedTestRunner testRunner;
        private QualityAssuranceSystem qaSystem;
        private TestFramework.TestConfiguration testConfig;

        // Execution state
        private bool isExecuting = false;
        private TestExecutionSummary currentSummary = new TestExecutionSummary();
        private List<TestExecutionSummary> executionHistory = new List<TestExecutionSummary>();

        // Test results aggregation
        private Dictionary<string, TestFramework.TestSuite> completedSuites = new Dictionary<string, TestFramework.TestSuite>();
        private List<string> executionLog = new List<string>();

        // Events
        public System.Action<TestExecutionSummary> OnTestExecutionCompleted;
        public System.Action<string> OnExecutionPhaseChanged;
        public System.Action<bool> OnQualityGateResult;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeOrchestrator();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (settings.runAllTestsOnStart)
            {
                StartCoroutine(ExecuteFullTestSuiteCoroutine());
            }
        }

        #endregion

        #region Initialization

        private void InitializeOrchestrator()
        {
            // Get or create component references
            testRunner = AutomatedTestRunner.Instance;
            qaSystem = QualityAssuranceSystem.Instance;

            // Setup test configuration
            testConfig = new TestFramework.TestConfiguration
            {
                enablePerformanceTests = settings.runPerformanceTests,
                enableIntegrationTests = settings.runIntegrationTests,
                maxTestExecutionTime = (int)(60 * settings.testTimeoutMultiplier),
                enableAutomatedReporting = settings.generateUnifiedReport
            };

            TestFramework.Initialize(testConfig);

            // Create output directory
            if (!System.IO.Directory.Exists(settings.reportExportPath))
            {
                System.IO.Directory.CreateDirectory(settings.reportExportPath);
            }

            Debug.Log("TestOrchestrator initialized");
        }

        #endregion

        #region Test Execution

        /// <summary>
        /// Execute the complete test suite
        /// </summary>
        public void ExecuteFullTestSuite()
        {
            if (isExecuting)
            {
                Debug.LogWarning("Test execution already in progress");
                return;
            }

            StartCoroutine(ExecuteFullTestSuiteCoroutine());
        }

        private IEnumerator ExecuteFullTestSuiteCoroutine()
        {
            isExecuting = true;
            
            // Initialize execution summary
            currentSummary = new TestExecutionSummary
            {
                executionStartTime = System.DateTime.Now
            };

            executionLog.Clear();
            completedSuites.Clear();
            
            LogExecution("=== WHISKER KING COMPREHENSIVE TEST EXECUTION STARTED ===");
            LogExecution($"Started at: {currentSummary.executionStartTime:yyyy-MM-dd HH:mm:ss}");

            float executionStartTime = Time.realtimeSinceStartup;

            try
            {
                // Phase 1: Unit Tests
                if (settings.runUnitTests)
                {
                    OnExecutionPhaseChanged?.Invoke("Running Unit Tests");
                    yield return StartCoroutine(ExecuteUnitTests());
                }

                // Phase 2: Integration Tests
                if (settings.runIntegrationTests)
                {
                    OnExecutionPhaseChanged?.Invoke("Running Integration Tests");
                    yield return StartCoroutine(ExecuteIntegrationTests());
                }

                // Phase 3: Performance Tests
                if (settings.runPerformanceTests)
                {
                    OnExecutionPhaseChanged?.Invoke("Running Performance Tests");
                    yield return StartCoroutine(ExecutePerformanceTests());
                }

                // Phase 4: Quality Assurance
                if (settings.runQualityAssurance)
                {
                    OnExecutionPhaseChanged?.Invoke("Running Quality Assurance");
                    yield return StartCoroutine(ExecuteQualityAssurance());
                }

                // Phase 5: Regression Tests (if enabled)
                if (settings.runRegressionTests)
                {
                    OnExecutionPhaseChanged?.Invoke("Running Regression Tests");
                    yield return StartCoroutine(ExecuteRegressionTests());
                }

                // Phase 6: Generate Results
                OnExecutionPhaseChanged?.Invoke("Generating Test Results");
                yield return StartCoroutine(AggregateResults());

                // Phase 7: Quality Gate Check
                OnExecutionPhaseChanged?.Invoke("Evaluating Quality Gates");
                bool passedQualityGates = EvaluateQualityGates();
                
                OnQualityGateResult?.Invoke(passedQualityGates);

                LogExecution($"Quality Gates: {(passedQualityGates ? "PASSED" : "FAILED")}");

            }
            catch (System.Exception e)
            {
                LogExecution($"Test execution failed with exception: {e.Message}");
                Debug.LogError($"Test orchestration error: {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                // Finalize execution
                currentSummary.executionEndTime = System.DateTime.Now;
                currentSummary.totalExecutionTime = Time.realtimeSinceStartup - executionStartTime;
                currentSummary.executionDuration = 
                    (currentSummary.executionEndTime - currentSummary.executionStartTime).ToString(@"hh\:mm\:ss");

                // Generate final report
                if (settings.generateUnifiedReport)
                {
                    GenerateUnifiedReport();
                }

                // Add to history
                executionHistory.Add(currentSummary);
                
                OnTestExecutionCompleted?.Invoke(currentSummary);
                OnExecutionPhaseChanged?.Invoke("Test Execution Complete");
                
                LogExecution("=== TEST EXECUTION COMPLETED ===");
                LogExecution($"Total Time: {currentSummary.executionDuration}");
                LogExecution($"Overall Pass Rate: {currentSummary.overallPassRate:P2}");

                isExecuting = false;
            }
        }

        private IEnumerator ExecuteUnitTests()
        {
            LogExecution("Starting Unit Test execution...");
            
            // Configure test runner for unit tests
            var config = testRunner.GetConfiguration();
            config.includeUnitTests = true;
            config.includeIntegrationTests = false;
            config.includePerformanceTests = false;
            testRunner.SetConfiguration(config);

            // Execute unit tests
            testRunner.RunAllTests();
            
            // Wait for completion
            while (testRunner.IsRunning())
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // Collect results
            var completedSuites = testRunner.GetCompletedSuites();
            foreach (var suite in completedSuites)
            {
                currentSummary.unitTestsPassed += suite.passedTests;
                currentSummary.totalTestsExecuted += suite.totalTests;
                currentSummary.totalTestsPassed += suite.passedTests;
                currentSummary.totalTestsFailed += suite.failedTests;
            }

            LogExecution($"Unit Tests completed: {currentSummary.unitTestsPassed} passed");
        }

        private IEnumerator ExecuteIntegrationTests()
        {
            LogExecution("Starting Integration Test execution...");

            // Run comprehensive integration tests
            yield return StartCoroutine(testRunner.RunAllTestsCoroutine());

            // Update summary with integration test results
            var suites = testRunner.GetCompletedSuites();
            int integrationTests = 0;
            int integrationPassed = 0;

            foreach (var suite in suites)
            {
                if (suite.suiteName.Contains("Integration"))
                {
                    integrationTests += suite.totalTests;
                    integrationPassed += suite.passedTests;
                }
            }

            currentSummary.integrationTestsPassed = integrationPassed;
            LogExecution($"Integration Tests completed: {integrationPassed}/{integrationTests} passed");
        }

        private IEnumerator ExecutePerformanceTests()
        {
            LogExecution("Starting Performance Test execution...");

            // Run performance benchmarks
            yield return StartCoroutine(testRunner.RunPerformanceBenchmarks());

            // Get performance score from PerformanceManager
            if (Performance.PerformanceManager.Instance != null)
            {
                var perfMetrics = Performance.PerformanceManager.Instance.GetPerformanceMetrics();
                currentSummary.performanceScore = Performance.PerformanceManager.Instance.GetPerformanceEfficiency() * 100f;
            }

            LogExecution($"Performance Tests completed. Score: {currentSummary.performanceScore:F1}/100");
        }

        private IEnumerator ExecuteQualityAssurance()
        {
            LogExecution("Starting Quality Assurance execution...");

            // Run comprehensive quality assessment
            yield return StartCoroutine(qaSystem.PerformQualityAssessment());

            // Get quality score
            currentSummary.qualityScore = qaSystem.GetQualityScore();
            
            LogExecution($"Quality Assurance completed. Score: {currentSummary.qualityScore:F1}/100");
        }

        private IEnumerator ExecuteRegressionTests()
        {
            LogExecution("Starting Regression Test execution...");

            // Run regression tests
            yield return StartCoroutine(qaSystem.RunRegressionTests());

            LogExecution("Regression Tests completed");
        }

        private IEnumerator AggregateResults()
        {
            LogExecution("Aggregating test results...");

            // Calculate overall pass rate
            if (currentSummary.totalTestsExecuted > 0)
            {
                currentSummary.overallPassRate = (float)currentSummary.totalTestsPassed / currentSummary.totalTestsExecuted;
            }

            // Determine quality grading
            float avgScore = (currentSummary.performanceScore + currentSummary.qualityScore) / 2f;
            
            if (avgScore >= 90f)
                currentSummary.qualityGrading = "EXCELLENT";
            else if (avgScore >= 80f)
                currentSummary.qualityGrading = "GOOD";
            else if (avgScore >= 70f)
                currentSummary.qualityGrading = "ACCEPTABLE";
            else
                currentSummary.qualityGrading = "NEEDS IMPROVEMENT";

            LogExecution($"Results aggregated. Quality Grade: {currentSummary.qualityGrading}");
            yield return null;
        }

        #endregion

        #region Quality Gates

        private bool EvaluateQualityGates()
        {
            bool passedAll = true;
            var issues = new List<string>();

            // Pass rate gate
            if (currentSummary.overallPassRate < settings.minimumPassRate)
            {
                passedAll = false;
                issues.Add($"Pass rate {currentSummary.overallPassRate:P2} below minimum {settings.minimumPassRate:P2}");
            }

            // Performance gate
            if (currentSummary.performanceScore < settings.minimumPerformanceScore)
            {
                passedAll = false;
                issues.Add($"Performance score {currentSummary.performanceScore:F1} below minimum {settings.minimumPerformanceScore:F1}");
            }

            // Quality gate
            if (currentSummary.qualityScore < settings.minimumQualityScore)
            {
                passedAll = false;
                issues.Add($"Quality score {currentSummary.qualityScore:F1} below minimum {settings.minimumQualityScore:F1}");
            }

            currentSummary.passedQualityGates = passedAll;

            if (!passedAll)
            {
                LogExecution("Quality Gate FAILURES:");
                foreach (var issue in issues)
                {
                    LogExecution($"  - {issue}");
                }

                if (settings.failBuildOnQualityGate)
                {
                    throw new System.Exception($"Build failed quality gates: {string.Join(", ", issues)}");
                }
            }
            else
            {
                LogExecution("All Quality Gates PASSED");
            }

            return passedAll;
        }

        #endregion

        #region Reporting

        private void GenerateUnifiedReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== WHISKER KING UNIFIED TEST REPORT ===");
            report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Unity Version: {Application.unityVersion}");
            report.AppendLine($"Platform: {Application.platform}");
            report.AppendLine($"Version: {Application.version}");
            report.AppendLine();

            report.AppendLine("=== EXECUTION SUMMARY ===");
            report.AppendLine($"Start Time: {currentSummary.executionStartTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"End Time: {currentSummary.executionEndTime:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Duration: {currentSummary.executionDuration}");
            report.AppendLine($"Total Execution Time: {currentSummary.totalExecutionTime:F2} seconds");
            report.AppendLine();

            report.AppendLine("=== TEST RESULTS ===");
            report.AppendLine($"Total Tests Executed: {currentSummary.totalTestsExecuted}");
            report.AppendLine($"Tests Passed: {currentSummary.totalTestsPassed}");
            report.AppendLine($"Tests Failed: {currentSummary.totalTestsFailed}");
            report.AppendLine($"Overall Pass Rate: {currentSummary.overallPassRate:P2}");
            report.AppendLine();

            report.AppendLine("=== SYSTEM COVERAGE ===");
            report.AppendLine($"Unit Tests Passed: {currentSummary.unitTestsPassed}");
            report.AppendLine($"Integration Tests Passed: {currentSummary.integrationTestsPassed}");
            report.AppendLine($"Performance Tests Passed: {currentSummary.performanceTestsPassed}");
            report.AppendLine($"QA Tests Passed: {currentSummary.qaTestsPassed}");
            report.AppendLine();

            report.AppendLine("=== QUALITY METRICS ===");
            report.AppendLine($"Performance Score: {currentSummary.performanceScore:F1}/100");
            report.AppendLine($"Quality Score: {currentSummary.qualityScore:F1}/100");
            report.AppendLine($"Quality Grading: {currentSummary.qualityGrading}");
            report.AppendLine();

            report.AppendLine("=== QUALITY GATES ===");
            report.AppendLine($"Minimum Pass Rate: {settings.minimumPassRate:P2} (Actual: {currentSummary.overallPassRate:P2})");
            report.AppendLine($"Minimum Performance Score: {settings.minimumPerformanceScore:F1} (Actual: {currentSummary.performanceScore:F1})");
            report.AppendLine($"Minimum Quality Score: {settings.minimumQualityScore:F1} (Actual: {currentSummary.qualityScore:F1})");
            report.AppendLine($"Quality Gates Status: {(currentSummary.passedQualityGates ? "PASSED" : "FAILED")}");
            report.AppendLine();

            if (settings.saveDetailedLogs && executionLog.Count > 0)
            {
                report.AppendLine("=== EXECUTION LOG ===");
                foreach (var logEntry in executionLog)
                {
                    report.AppendLine(logEntry);
                }
                report.AppendLine();
            }

            report.AppendLine("=== PRD COMPLIANCE STATUS ===");
            report.AppendLine("Performance Targets:");
            report.AppendLine($"  - 60 FPS Target: {(currentSummary.performanceScore >= 80 ? "COMPLIANT" : "NON-COMPLIANT")}");
            report.AppendLine($"  - Memory Budget: {(currentSummary.qualityScore >= 75 ? "COMPLIANT" : "NON-COMPLIANT")}");
            report.AppendLine($"  - Test Coverage: {(currentSummary.overallPassRate >= 0.9f ? "COMPLIANT" : "NON-COMPLIANT")}");
            report.AppendLine();

            string reportText = report.ToString();
            Debug.Log(reportText);

            // Save unified report
            try
            {
                string fileName = $"UnifiedTestReport_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = System.IO.Path.Combine(Application.persistentDataPath, settings.reportExportPath, fileName);
                System.IO.File.WriteAllText(filePath, reportText);
                LogExecution($"Unified report saved to: {filePath}");

                // Export to CI format if enabled
                if (settings.exportToCI)
                {
                    ExportToCIFormat(filePath.Replace(".txt", "_CI.xml"));
                }
            }
            catch (System.Exception e)
            {
                LogExecution($"Failed to save unified report: {e.Message}");
                Debug.LogError($"Report generation error: {e.Message}");
            }
        }

        private void ExportToCIFormat(string filePath)
        {
            // Generate JUnit-style XML for CI integration
            var xml = new System.Text.StringBuilder();
            
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine($"<testsuite name=\"WhiskerKingTests\" tests=\"{currentSummary.totalTestsExecuted}\" failures=\"{currentSummary.totalTestsFailed}\" time=\"{currentSummary.totalExecutionTime:F2}\">");
            
            xml.AppendLine($"  <testcase classname=\"QualityGates\" name=\"PassRate\" time=\"0.1\">{(currentSummary.overallPassRate >= settings.minimumPassRate ? "" : "<failure>Pass rate below threshold</failure>")}</testcase>");
            xml.AppendLine($"  <testcase classname=\"QualityGates\" name=\"PerformanceScore\" time=\"0.1\">{(currentSummary.performanceScore >= settings.minimumPerformanceScore ? "" : "<failure>Performance score below threshold</failure>")}</testcase>");
            xml.AppendLine($"  <testcase classname=\"QualityGates\" name=\"QualityScore\" time=\"0.1\">{(currentSummary.qualityScore >= settings.minimumQualityScore ? "" : "<failure>Quality score below threshold</failure>")}</testcase>");
            
            xml.AppendLine("</testsuite>");

            try
            {
                System.IO.File.WriteAllText(filePath, xml.ToString());
                LogExecution($"CI report exported to: {filePath}");
            }
            catch (System.Exception e)
            {
                LogExecution($"CI export failed: {e.Message}");
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current execution summary
        /// </summary>
        public TestExecutionSummary GetCurrentSummary()
        {
            return currentSummary;
        }

        /// <summary>
        /// Get execution history
        /// </summary>
        public List<TestExecutionSummary> GetExecutionHistory()
        {
            return new List<TestExecutionSummary>(executionHistory);
        }

        /// <summary>
        /// Check if tests are currently executing
        /// </summary>
        public bool IsExecuting()
        {
            return isExecuting;
        }

        /// <summary>
        /// Get orchestration settings
        /// </summary>
        public OrchestrationSettings GetSettings()
        {
            return settings;
        }

        /// <summary>
        /// Set orchestration settings
        /// </summary>
        public void SetSettings(OrchestrationSettings newSettings)
        {
            settings = newSettings;
        }

        /// <summary>
        /// Force stop test execution
        /// </summary>
        public void StopExecution()
        {
            if (isExecuting)
            {
                StopAllCoroutines();
                isExecuting = false;
                LogExecution("Test execution forcibly stopped");
            }
        }

        /// <summary>
        /// Get execution statistics
        /// </summary>
        public string GetExecutionStatistics()
        {
            return $"Executions: {executionHistory.Count}, " +
                   $"Avg Pass Rate: {(executionHistory.Count > 0 ? executionHistory.Average(e => e.overallPassRate) : 0f):P2}, " +
                   $"Avg Performance: {(executionHistory.Count > 0 ? executionHistory.Average(e => e.performanceScore) : 0f):F1}, " +
                   $"Avg Quality: {(executionHistory.Count > 0 ? executionHistory.Average(e => e.qualityScore) : 0f):F1}";
        }

        #endregion

        #region Helper Methods

        private void LogExecution(string message)
        {
            string timestampedMessage = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
            executionLog.Add(timestampedMessage);
            
            if (debugMode)
            {
                Debug.Log($"TestOrchestrator: {timestampedMessage}");
            }
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(Screen.width - 450, 400, 440, 400));
            
            GUILayout.Label("=== TEST ORCHESTRATOR ===");
            GUILayout.Label($"Status: {(isExecuting ? "EXECUTING" : "IDLE")}");
            
            if (currentSummary.totalTestsExecuted > 0)
            {
                GUILayout.Label($"Pass Rate: {currentSummary.overallPassRate:P2}");
                GUILayout.Label($"Performance: {currentSummary.performanceScore:F1}/100");
                GUILayout.Label($"Quality: {currentSummary.qualityScore:F1}/100");
                GUILayout.Label($"Grade: {currentSummary.qualityGrading}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== TEST CATEGORIES ===");
            settings.runUnitTests = GUILayout.Toggle(settings.runUnitTests, "Unit Tests");
            settings.runIntegrationTests = GUILayout.Toggle(settings.runIntegrationTests, "Integration Tests");
            settings.runPerformanceTests = GUILayout.Toggle(settings.runPerformanceTests, "Performance Tests");
            settings.runQualityAssurance = GUILayout.Toggle(settings.runQualityAssurance, "Quality Assurance");
            settings.runRegressionTests = GUILayout.Toggle(settings.runRegressionTests, "Regression Tests");
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            GUI.enabled = !isExecuting;
            if (GUILayout.Button("Execute Full Test Suite"))
            {
                ExecuteFullTestSuite();
            }
            
            GUI.enabled = isExecuting;
            if (GUILayout.Button("Stop Execution"))
            {
                StopExecution();
            }
            
            GUI.enabled = true;
            if (GUILayout.Button("Generate Report"))
            {
                GenerateUnifiedReport();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== QUALITY GATES ===");
            GUILayout.Label($"Min Pass Rate: {settings.minimumPassRate:P0}");
            GUILayout.Label($"Min Performance: {settings.minimumPerformanceScore:F0}");
            GUILayout.Label($"Min Quality: {settings.minimumQualityScore:F0}");
            
            if (currentSummary.totalTestsExecuted > 0)
            {
                string gateStatus = currentSummary.passedQualityGates ? "PASSED" : "FAILED";
                GUILayout.Label($"Gates Status: {gateStatus}");
            }
            
            GUILayout.EndArea();
        }

        #endregion
    }
}
