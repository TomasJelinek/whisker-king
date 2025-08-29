using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using WhiskerKing.Core;
using WhiskerKing.Performance;

namespace WhiskerKing.Testing
{
    /// <summary>
    /// Quality Assurance System for Whisker King
    /// Provides automated quality checks, code analysis, and regression testing
    /// Integrates with CI/CD pipelines and generates quality reports
    /// </summary>
    public class QualityAssuranceSystem : MonoBehaviour
    {
        [System.Serializable]
        public class QualityConfiguration
        {
            [Header("Quality Checks")]
            public bool enableCodeQualityChecks = true;
            public bool enablePerformanceRegression = true;
            public bool enableMemoryLeakDetection = true;
            public bool enableAssetValidation = true;
            public bool enableUIConsistencyChecks = true;

            [Header("Quality Thresholds")]
            public float maxCodeComplexity = 10f;
            public int maxMethodLines = 50;
            public float maxMemoryGrowthMB = 50f;
            public float maxLoadTimeSeconds = 10f;
            public int maxTextureSize = 2048;
            public float minCodeCoverage = 0.8f; // 80%

            [Header("Regression Testing")]
            public bool enableRegressionTests = true;
            public float performanceRegressionThreshold = 0.1f; // 10%
            public int regressionTestSampleSize = 100;
            public bool saveRegressionBaselines = true;

            [Header("Reporting")]
            public bool generateQualityReport = true;
            public bool exportMetrics = true;
            public string reportOutputPath = "QualityReports/";
            public bool sendNotifications = false;
        }

        [System.Serializable]
        public class QualityMetrics
        {
            [Header("Code Quality")]
            public int totalLinesOfCode;
            public int numberOfClasses;
            public int numberOfMethods;
            public float averageMethodComplexity;
            public float codeCoverage;
            public int codeSmells;
            public int criticalIssues;

            [Header("Performance Quality")]
            public float averageFrameRate;
            public float averageLoadTime;
            public float memoryUsage;
            public int drawCallCount;
            public int triangleCount;
            public float renderTime;

            [Header("Asset Quality")]
            public int totalAssets;
            public int oversizedAssets;
            public int uncompressedTextures;
            public int missingReferences;
            public long totalAssetSize;

            [Header("UI Quality")]
            public int uiElements;
            public int accessibilityViolations;
            public int inconsistentStyles;
            public int missingLocalizations;

            [Header("Test Quality")]
            public int totalTests;
            public int passingTests;
            public int failingTests;
            public float testCoverage;
            public float testExecutionTime;
        }

        [System.Serializable]
        public class QualityIssue
        {
            public enum Severity { Low, Medium, High, Critical }
            public enum Category { Code, Performance, Asset, UI, Test, Security }

            public string issueId;
            public string title;
            public string description;
            public Severity severity;
            public Category category;
            public string filePath;
            public int lineNumber;
            public string recommendation;
            public System.DateTime detectedTime;
        }

        [System.Serializable]
        public class RegressionBaseline
        {
            public string testName;
            public float averageExecutionTime;
            public float memoryUsage;
            public float frameRate;
            public int drawCalls;
            public System.DateTime baselineDate;
            public string version;
        }

        // Singleton pattern
        private static QualityAssuranceSystem instance;
        public static QualityAssuranceSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<QualityAssuranceSystem>();
                    if (instance == null)
                    {
                        GameObject qaSystemGO = new GameObject("QualityAssuranceSystem");
                        instance = qaSystemGO.AddComponent<QualityAssuranceSystem>();
                        DontDestroyOnLoad(qaSystemGO);
                    }
                }
                return instance;
            }
        }

        [Header("QA Configuration")]
        [SerializeField] private QualityConfiguration configuration = new QualityConfiguration();
        [SerializeField] private bool debugMode = true;

        // Quality tracking
        private QualityMetrics currentMetrics = new QualityMetrics();
        private List<QualityIssue> detectedIssues = new List<QualityIssue>();
        private Dictionary<string, RegressionBaseline> regressionBaselines = new Dictionary<string, RegressionBaseline>();
        
        // Quality history
        private List<QualityMetrics> qualityHistory = new List<QualityMetrics>();
        private const int maxHistorySize = 100;

        // Component references
        private PerformanceManager performanceManager;
        private TestFramework.TestConfiguration testConfig;

        // Events
        public System.Action<QualityMetrics> OnQualityAssessmentCompleted;
        public System.Action<QualityIssue> OnQualityIssueDetected;
        public System.Action<float> OnQualityScoreUpdated;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeQASystem();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            LoadConfiguration();
            LoadRegressionBaselines();
            
            // Start periodic quality assessments
            InvokeRepeating(nameof(PerformPeriodicQualityCheck), 60f, 300f); // Every 5 minutes
        }

        #endregion

        #region Initialization

        private void InitializeQASystem()
        {
            // Get component references
            performanceManager = PerformanceManager.Instance;

            // Create output directory
            string outputPath = Path.Combine(Application.persistentDataPath, configuration.reportOutputPath);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            Debug.Log("Quality Assurance System initialized");
        }

        private void LoadConfiguration()
        {
            if (GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                // Load QA configuration from game config if available
                Debug.Log("QA System: Configuration loaded from GameConfig");
            }
            else
            {
                Debug.Log("QA System: Using default configuration");
            }
        }

        private void LoadRegressionBaselines()
        {
            try
            {
                string baselinePath = Path.Combine(Application.persistentDataPath, "regression_baselines.json");
                if (File.Exists(baselinePath))
                {
                    string json = File.ReadAllText(baselinePath);
                    var baselines = JsonUtility.FromJson<SerializableDictionary<string, RegressionBaseline>>(json);
                    if (baselines != null)
                    {
                        regressionBaselines = baselines.ToDictionary();
                        Debug.Log($"Loaded {regressionBaselines.Count} regression baselines");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not load regression baselines: {e.Message}");
            }
        }

        #endregion

        #region Quality Assessment

        /// <summary>
        /// Perform comprehensive quality assessment
        /// </summary>
        public IEnumerator PerformQualityAssessment()
        {
            Debug.Log("Starting comprehensive quality assessment...");
            
            // Reset current metrics
            currentMetrics = new QualityMetrics();
            detectedIssues.Clear();

            // Run all quality checks
            if (configuration.enableCodeQualityChecks)
            {
                yield return StartCoroutine(PerformCodeQualityCheck());
            }

            if (configuration.enablePerformanceRegression)
            {
                yield return StartCoroutine(PerformPerformanceCheck());
            }

            if (configuration.enableMemoryLeakDetection)
            {
                yield return StartCoroutine(PerformMemoryLeakCheck());
            }

            if (configuration.enableAssetValidation)
            {
                yield return StartCoroutine(PerformAssetValidation());
            }

            if (configuration.enableUIConsistencyChecks)
            {
                yield return StartCoroutine(PerformUIConsistencyCheck());
            }

            // Calculate overall quality score
            float qualityScore = CalculateQualityScore();
            OnQualityScoreUpdated?.Invoke(qualityScore);

            // Store metrics in history
            qualityHistory.Add(currentMetrics);
            if (qualityHistory.Count > maxHistorySize)
            {
                qualityHistory.RemoveAt(0);
            }

            // Generate report
            if (configuration.generateQualityReport)
            {
                GenerateQualityReport();
            }

            OnQualityAssessmentCompleted?.Invoke(currentMetrics);
            Debug.Log($"Quality assessment completed. Score: {qualityScore:F2}");
        }

        private IEnumerator PerformCodeQualityCheck()
        {
            Debug.Log("Performing code quality check...");

            // Analyze code structure
            AnalyzeCodeStructure();
            
            // Check for code smells
            DetectCodeSmells();
            
            // Validate coding standards
            ValidateCodingStandards();

            yield return null;
        }

        private void AnalyzeCodeStructure()
        {
            try
            {
                // Find all C# scripts in the project
                string[] scriptFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
                
                currentMetrics.totalLinesOfCode = 0;
                currentMetrics.numberOfClasses = 0;
                currentMetrics.numberOfMethods = 0;
                
                List<float> methodComplexities = new List<float>();

                foreach (string file in scriptFiles)
                {
                    if (file.Contains("Library") || file.Contains("Temp")) continue;

                    string content = File.ReadAllText(file);
                    string[] lines = content.Split('\n');
                    
                    // Count lines of code (excluding empty lines and comments)
                    int loc = lines.Count(line => !string.IsNullOrWhiteSpace(line) && 
                                                 !line.Trim().StartsWith("//") && 
                                                 !line.Trim().StartsWith("*"));
                    currentMetrics.totalLinesOfCode += loc;

                    // Rough estimation of classes and methods
                    currentMetrics.numberOfClasses += CountOccurrences(content, "class ");
                    currentMetrics.numberOfClasses += CountOccurrences(content, "struct ");
                    
                    int methods = CountOccurrences(content, "public ") + 
                                 CountOccurrences(content, "private ") + 
                                 CountOccurrences(content, "protected ");
                    currentMetrics.numberOfMethods += methods;

                    // Check method length (simplified)
                    CheckMethodLength(file, content);
                }

                if (methodComplexities.Count > 0)
                {
                    currentMetrics.averageMethodComplexity = methodComplexities.Average();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Code structure analysis failed: {e.Message}");
            }
        }

        private void DetectCodeSmells()
        {
            currentMetrics.codeSmells = 0;
            
            // Simplified code smell detection
            string[] scriptFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            
            foreach (string file in scriptFiles)
            {
                if (file.Contains("Library") || file.Contains("Temp")) continue;

                try
                {
                    string content = File.ReadAllText(file);
                    
                    // Check for long methods
                    if (HasLongMethods(content))
                    {
                        AddQualityIssue(new QualityIssue
                        {
                            issueId = System.Guid.NewGuid().ToString(),
                            title = "Long Method Detected",
                            description = "Method exceeds recommended length",
                            severity = QualityIssue.Severity.Medium,
                            category = QualityIssue.Category.Code,
                            filePath = file,
                            recommendation = "Consider breaking down into smaller methods",
                            detectedTime = System.DateTime.Now
                        });
                        currentMetrics.codeSmells++;
                    }

                    // Check for duplicate code patterns (simplified)
                    if (HasDuplicateCode(content))
                    {
                        AddQualityIssue(new QualityIssue
                        {
                            issueId = System.Guid.NewGuid().ToString(),
                            title = "Code Duplication Detected",
                            description = "Potential code duplication found",
                            severity = QualityIssue.Severity.Low,
                            category = QualityIssue.Category.Code,
                            filePath = file,
                            recommendation = "Consider extracting common functionality",
                            detectedTime = System.DateTime.Now
                        });
                        currentMetrics.codeSmells++;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Code smell detection failed for {file}: {e.Message}");
                }
            }
        }

        private void ValidateCodingStandards()
        {
            // Check naming conventions, formatting, etc.
            // This is a simplified implementation
            currentMetrics.criticalIssues = detectedIssues.Count(i => i.severity == QualityIssue.Severity.Critical);
        }

        private IEnumerator PerformPerformanceCheck()
        {
            Debug.Log("Performing performance quality check...");

            if (performanceManager != null)
            {
                var metrics = performanceManager.GetPerformanceMetrics();
                currentMetrics.averageFrameRate = metrics.averageFrameRate;
                currentMetrics.memoryUsage = metrics.totalMemoryUsage;
                currentMetrics.drawCallCount = metrics.currentDrawCalls;
                currentMetrics.triangleCount = metrics.currentTriangles;

                // Check performance thresholds
                var budget = performanceManager.GetPerformanceBudget();
                
                if (metrics.averageFrameRate < budget.minimumFrameRate)
                {
                    AddQualityIssue(new QualityIssue
                    {
                        issueId = System.Guid.NewGuid().ToString(),
                        title = "Low Frame Rate",
                        description = $"Average FPS ({metrics.averageFrameRate:F1}) below minimum ({budget.minimumFrameRate})",
                        severity = QualityIssue.Severity.High,
                        category = QualityIssue.Category.Performance,
                        recommendation = "Optimize rendering or reduce quality settings",
                        detectedTime = System.DateTime.Now
                    });
                }

                if (metrics.totalMemoryUsage > budget.maxTotalMemory * 0.9f)
                {
                    AddQualityIssue(new QualityIssue
                    {
                        issueId = System.Guid.NewGuid().ToString(),
                        title = "High Memory Usage",
                        description = $"Memory usage ({metrics.totalMemoryUsage:F1}MB) near budget limit ({budget.maxTotalMemory:F1}MB)",
                        severity = QualityIssue.Severity.Medium,
                        category = QualityIssue.Category.Performance,
                        recommendation = "Review memory allocation and implement cleanup",
                        detectedTime = System.DateTime.Now
                    });
                }
            }

            yield return null;
        }

        private IEnumerator PerformMemoryLeakCheck()
        {
            Debug.Log("Performing memory leak detection...");

            float initialMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin);
            
            // Force garbage collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            yield return new WaitForSecondsRealtime(1f);
            
            float afterGCMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemory(UnityEngine.Profiling.Profiler.Area.RenderingPlugin);
            float memoryDelta = (afterGCMemory - initialMemory) / (1024f * 1024f); // MB

            if (memoryDelta > configuration.maxMemoryGrowthMB)
            {
                AddQualityIssue(new QualityIssue
                {
                    issueId = System.Guid.NewGuid().ToString(),
                    title = "Potential Memory Leak",
                    description = $"Memory grew by {memoryDelta:F2}MB after GC",
                    severity = QualityIssue.Severity.High,
                    category = QualityIssue.Category.Performance,
                    recommendation = "Review object lifecycle and disposal patterns",
                    detectedTime = System.DateTime.Now
                });
            }
        }

        private IEnumerator PerformAssetValidation()
        {
            Debug.Log("Performing asset validation...");

            // Find all assets in the project
            string[] assetGuids = UnityEditor.AssetDatabase.FindAssets("", new[] { "Assets" });
            
            currentMetrics.totalAssets = assetGuids.Length;
            currentMetrics.oversizedAssets = 0;
            currentMetrics.uncompressedTextures = 0;
            currentMetrics.missingReferences = 0;

            foreach (string guid in assetGuids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                
                // Check texture assets
                if (assetPath.EndsWith(".png") || assetPath.EndsWith(".jpg") || assetPath.EndsWith(".tga"))
                {
                    var textureImporter = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
                    if (textureImporter != null)
                    {
                        if (textureImporter.maxTextureSize > configuration.maxTextureSize)
                        {
                            currentMetrics.oversizedAssets++;
                            AddQualityIssue(new QualityIssue
                            {
                                issueId = System.Guid.NewGuid().ToString(),
                                title = "Oversized Texture",
                                description = $"Texture size ({textureImporter.maxTextureSize}) exceeds limit",
                                severity = QualityIssue.Severity.Medium,
                                category = QualityIssue.Category.Asset,
                                filePath = assetPath,
                                recommendation = "Reduce texture size or use compression",
                                detectedTime = System.DateTime.Now
                            });
                        }

                        if (textureImporter.textureCompression == UnityEditor.TextureImporterCompression.Uncompressed)
                        {
                            currentMetrics.uncompressedTextures++;
                        }
                    }
                }

                // Yield periodically to avoid blocking
                if (System.Array.IndexOf(assetGuids, guid) % 100 == 0)
                {
                    yield return null;
                }
            }
        }

        private IEnumerator PerformUIConsistencyCheck()
        {
            Debug.Log("Performing UI consistency check...");

            // Find all UI elements
            var canvases = FindObjectsOfType<Canvas>(true);
            currentMetrics.uiElements = 0;
            currentMetrics.accessibilityViolations = 0;

            foreach (var canvas in canvases)
            {
                var uiElements = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                currentMetrics.uiElements += uiElements.Length;

                // Check for accessibility issues
                foreach (var element in uiElements)
                {
                    // Check for missing alt text, color contrast, etc.
                    if (element is UnityEngine.UI.Image image && image.sprite == null)
                    {
                        currentMetrics.accessibilityViolations++;
                    }
                }

                yield return null;
            }
        }

        #endregion

        #region Regression Testing

        /// <summary>
        /// Run regression tests against baseline
        /// </summary>
        public IEnumerator RunRegressionTests()
        {
            if (!configuration.enableRegressionTests)
                yield break;

            Debug.Log("Running regression tests...");

            var testResults = new Dictionary<string, float>();

            // Run performance regression tests
            yield return StartCoroutine(RunPerformanceRegressionTest("PlayerMovement", testResults));
            yield return StartCoroutine(RunPerformanceRegressionTest("AudioSystem", testResults));
            yield return StartCoroutine(RunPerformanceRegressionTest("UINavigation", testResults));

            // Compare against baselines
            foreach (var result in testResults)
            {
                CheckRegressionThreshold(result.Key, result.Value);
            }
        }

        private IEnumerator RunPerformanceRegressionTest(string testName, Dictionary<string, float> results)
        {
            // Run simplified performance test
            float startTime = Time.realtimeSinceStartup;
            
            // Simulate test execution
            for (int i = 0; i < configuration.regressionTestSampleSize; i++)
            {
                // Simulate work
                yield return null;
            }
            
            float executionTime = Time.realtimeSinceStartup - startTime;
            results[testName] = executionTime;
        }

        private void CheckRegressionThreshold(string testName, float currentValue)
        {
            if (regressionBaselines.ContainsKey(testName))
            {
                var baseline = regressionBaselines[testName];
                float regressionRatio = (currentValue - baseline.averageExecutionTime) / baseline.averageExecutionTime;
                
                if (regressionRatio > configuration.performanceRegressionThreshold)
                {
                    AddQualityIssue(new QualityIssue
                    {
                        issueId = System.Guid.NewGuid().ToString(),
                        title = "Performance Regression Detected",
                        description = $"Test '{testName}' is {regressionRatio:P1} slower than baseline",
                        severity = QualityIssue.Severity.High,
                        category = QualityIssue.Category.Performance,
                        recommendation = "Investigate recent changes that may have impacted performance",
                        detectedTime = System.DateTime.Now
                    });
                }
            }
            else
            {
                // Create new baseline
                regressionBaselines[testName] = new RegressionBaseline
                {
                    testName = testName,
                    averageExecutionTime = currentValue,
                    baselineDate = System.DateTime.Now,
                    version = Application.version
                };

                if (configuration.saveRegressionBaselines)
                {
                    SaveRegressionBaselines();
                }
            }
        }

        private void SaveRegressionBaselines()
        {
            try
            {
                string baselinePath = Path.Combine(Application.persistentDataPath, "regression_baselines.json");
                var serializableBaselines = new SerializableDictionary<string, RegressionBaseline>(regressionBaselines);
                string json = JsonUtility.ToJson(serializableBaselines, true);
                File.WriteAllText(baselinePath, json);
                Debug.Log("Regression baselines saved");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Could not save regression baselines: {e.Message}");
            }
        }

        #endregion

        #region Quality Scoring

        private float CalculateQualityScore()
        {
            float score = 100f; // Start with perfect score

            // Deduct points for issues
            foreach (var issue in detectedIssues)
            {
                switch (issue.severity)
                {
                    case QualityIssue.Severity.Critical:
                        score -= 20f;
                        break;
                    case QualityIssue.Severity.High:
                        score -= 10f;
                        break;
                    case QualityIssue.Severity.Medium:
                        score -= 5f;
                        break;
                    case QualityIssue.Severity.Low:
                        score -= 1f;
                        break;
                }
            }

            // Performance penalty
            if (performanceManager != null)
            {
                var metrics = performanceManager.GetPerformanceMetrics();
                var budget = performanceManager.GetPerformanceBudget();
                
                float performanceRatio = metrics.averageFrameRate / budget.targetFrameRate;
                if (performanceRatio < 0.9f)
                {
                    score -= (0.9f - performanceRatio) * 50f;
                }
            }

            return Mathf.Max(0f, score);
        }

        #endregion

        #region Reporting

        private void GenerateQualityReport()
        {
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== WHISKER KING QUALITY ASSURANCE REPORT ===");
            report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Version: {Application.version}");
            report.AppendLine();

            report.AppendLine("=== QUALITY SCORE ===");
            float qualityScore = CalculateQualityScore();
            report.AppendLine($"Overall Quality Score: {qualityScore:F1}/100");
            
            string qualityGrade = qualityScore >= 90f ? "EXCELLENT" :
                                 qualityScore >= 80f ? "GOOD" :
                                 qualityScore >= 70f ? "ACCEPTABLE" : "NEEDS IMPROVEMENT";
            report.AppendLine($"Quality Grade: {qualityGrade}");
            report.AppendLine();

            report.AppendLine("=== CODE QUALITY METRICS ===");
            report.AppendLine($"Lines of Code: {currentMetrics.totalLinesOfCode:N0}");
            report.AppendLine($"Classes: {currentMetrics.numberOfClasses}");
            report.AppendLine($"Methods: {currentMetrics.numberOfMethods}");
            report.AppendLine($"Average Method Complexity: {currentMetrics.averageMethodComplexity:F2}");
            report.AppendLine($"Code Smells: {currentMetrics.codeSmells}");
            report.AppendLine($"Critical Issues: {currentMetrics.criticalIssues}");
            report.AppendLine();

            report.AppendLine("=== PERFORMANCE METRICS ===");
            report.AppendLine($"Average Frame Rate: {currentMetrics.averageFrameRate:F1} FPS");
            report.AppendLine($"Memory Usage: {currentMetrics.memoryUsage:F1} MB");
            report.AppendLine($"Draw Calls: {currentMetrics.drawCallCount}");
            report.AppendLine($"Triangles: {currentMetrics.triangleCount:N0}");
            report.AppendLine();

            report.AppendLine("=== ASSET METRICS ===");
            report.AppendLine($"Total Assets: {currentMetrics.totalAssets}");
            report.AppendLine($"Oversized Assets: {currentMetrics.oversizedAssets}");
            report.AppendLine($"Uncompressed Textures: {currentMetrics.uncompressedTextures}");
            report.AppendLine($"Missing References: {currentMetrics.missingReferences}");
            report.AppendLine();

            if (detectedIssues.Count > 0)
            {
                report.AppendLine("=== DETECTED ISSUES ===");
                var groupedIssues = detectedIssues.GroupBy(i => i.severity).OrderBy(g => g.Key);
                
                foreach (var group in groupedIssues)
                {
                    report.AppendLine($"{group.Key} Issues ({group.Count()}):");
                    foreach (var issue in group.Take(10)) // Limit to top 10 per severity
                    {
                        report.AppendLine($"  • {issue.title}: {issue.description}");
                        if (!string.IsNullOrEmpty(issue.filePath))
                        {
                            report.AppendLine($"    File: {Path.GetFileName(issue.filePath)}");
                        }
                        if (!string.IsNullOrEmpty(issue.recommendation))
                        {
                            report.AppendLine($"    Recommendation: {issue.recommendation}");
                        }
                        report.AppendLine();
                    }
                }
            }

            string reportText = report.ToString();
            Debug.Log(reportText);

            // Save to file
            try
            {
                string fileName = $"QualityReport_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(Application.persistentDataPath, configuration.reportOutputPath, fileName);
                File.WriteAllText(filePath, reportText);
                Debug.Log($"Quality report saved to: {filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Could not save quality report: {e.Message}");
            }
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Trigger quality assessment manually
        /// </summary>
        public void TriggerQualityAssessment()
        {
            StartCoroutine(PerformQualityAssessment());
        }

        /// <summary>
        /// Get current quality metrics
        /// </summary>
        public QualityMetrics GetCurrentMetrics()
        {
            return currentMetrics;
        }

        /// <summary>
        /// Get all detected issues
        /// </summary>
        public List<QualityIssue> GetDetectedIssues()
        {
            return new List<QualityIssue>(detectedIssues);
        }

        /// <summary>
        /// Get quality score
        /// </summary>
        public float GetQualityScore()
        {
            return CalculateQualityScore();
        }

        /// <summary>
        /// Add custom quality issue
        /// </summary>
        public void AddQualityIssue(QualityIssue issue)
        {
            detectedIssues.Add(issue);
            OnQualityIssueDetected?.Invoke(issue);
        }

        /// <summary>
        /// Clear all issues
        /// </summary>
        public void ClearAllIssues()
        {
            detectedIssues.Clear();
        }

        /// <summary>
        /// Get configuration
        /// </summary>
        public QualityConfiguration GetConfiguration()
        {
            return configuration;
        }

        #endregion

        #region Helper Methods

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        private void CheckMethodLength(string filePath, string content)
        {
            string[] lines = content.Split('\n');
            bool inMethod = false;
            int methodLineCount = 0;
            int methodStartLine = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                
                if (line.Contains("public ") || line.Contains("private ") || line.Contains("protected "))
                {
                    if (line.Contains("(") && line.Contains(")") && (line.Contains("{") || i + 1 < lines.Length && lines[i + 1].Trim().StartsWith("{")))
                    {
                        inMethod = true;
                        methodLineCount = 0;
                        methodStartLine = i + 1;
                    }
                }
                else if (inMethod && line == "}")
                {
                    inMethod = false;
                    if (methodLineCount > configuration.maxMethodLines)
                    {
                        AddQualityIssue(new QualityIssue
                        {
                            issueId = System.Guid.NewGuid().ToString(),
                            title = "Long Method",
                            description = $"Method has {methodLineCount} lines (max: {configuration.maxMethodLines})",
                            severity = QualityIssue.Severity.Medium,
                            category = QualityIssue.Category.Code,
                            filePath = filePath,
                            lineNumber = methodStartLine,
                            recommendation = "Consider breaking method into smaller functions",
                            detectedTime = System.DateTime.Now
                        });
                    }
                }
                else if (inMethod && !string.IsNullOrWhiteSpace(line))
                {
                    methodLineCount++;
                }
            }
        }

        private bool HasLongMethods(string content)
        {
            // Simplified check for long methods
            return content.Contains("public ") && content.Length > 5000; // Rough heuristic
        }

        private bool HasDuplicateCode(string content)
        {
            // Simplified duplicate code detection
            return false; // Placeholder
        }

        private void PerformPeriodicQualityCheck()
        {
            if (configuration.enableCodeQualityChecks)
            {
                StartCoroutine(PerformQualityAssessment());
            }
        }

        #endregion

        #region Debug Interface

        private void OnGUI()
        {
            if (!debugMode) return;

            GUILayout.BeginArea(new Rect(10, 450, 350, 300));
            
            GUILayout.Label("=== QUALITY ASSURANCE SYSTEM ===");
            GUILayout.Label($"Quality Score: {GetQualityScore():F1}/100");
            GUILayout.Label($"Issues Detected: {detectedIssues.Count}");
            
            var criticalIssues = detectedIssues.Count(i => i.severity == QualityIssue.Severity.Critical);
            var highIssues = detectedIssues.Count(i => i.severity == QualityIssue.Severity.High);
            
            GUILayout.Label($"Critical: {criticalIssues}, High: {highIssues}");
            
            GUILayout.Space(10);
            GUILayout.Label("=== METRICS ===");
            GUILayout.Label($"Lines of Code: {currentMetrics.totalLinesOfCode:N0}");
            GUILayout.Label($"Classes: {currentMetrics.numberOfClasses}");
            GUILayout.Label($"Methods: {currentMetrics.numberOfMethods}");
            GUILayout.Label($"Code Smells: {currentMetrics.codeSmells}");
            
            GUILayout.Space(10);
            GUILayout.Label("=== CONTROLS ===");
            
            if (GUILayout.Button("Run Quality Assessment"))
            {
                TriggerQualityAssessment();
            }
            
            if (GUILayout.Button("Run Regression Tests"))
            {
                StartCoroutine(RunRegressionTests());
            }
            
            if (GUILayout.Button("Clear Issues"))
            {
                ClearAllIssues();
            }
            
            if (GUILayout.Button("Generate Report"))
            {
                GenerateQualityReport();
            }
            
            GUILayout.EndArea();
        }

        #endregion

        #region Serializable Dictionary Helper

        [System.Serializable]
        private class SerializableDictionary<TKey, TValue>
        {
            public List<TKey> keys = new List<TKey>();
            public List<TValue> values = new List<TValue>();

            public SerializableDictionary() { }

            public SerializableDictionary(Dictionary<TKey, TValue> dictionary)
            {
                foreach (var kvp in dictionary)
                {
                    keys.Add(kvp.Key);
                    values.Add(kvp.Value);
                }
            }

            public Dictionary<TKey, TValue> ToDictionary()
            {
                var dictionary = new Dictionary<TKey, TValue>();
                for (int i = 0; i < Mathf.Min(keys.Count, values.Count); i++)
                {
                    dictionary[keys[i]] = values[i];
                }
                return dictionary;
            }
        }

        #endregion
    }
}
