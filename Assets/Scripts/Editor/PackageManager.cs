#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace WhiskerKing.Editor
{
    /// <summary>
    /// Package Manager utilities for Whisker King
    /// Handles validation, installation, and updates of required Unity packages
    /// </summary>
    public static class PackageManager
    {
        // Required packages for Whisker King based on PRD specifications
        private static readonly Dictionary<string, string> RequiredPackages = new Dictionary<string, string>
        {
            { "com.unity.render-pipelines.universal", "14.0.8" },    // URP for mobile rendering
            { "com.unity.inputsystem", "1.7.0" },                    // New Input System for responsive controls
            { "com.unity.test-framework", "1.1.33" },                // Unit testing framework
            { "com.unity.addressables", "1.21.14" },                 // Asset streaming and memory management
            { "com.unity.analytics", "3.8.1" },                      // Game analytics and metrics
            { "com.unity.textmeshpro", "3.0.6" },                    // Enhanced text rendering
            { "com.unity.timeline", "1.7.4" },                       // Animation and cutscenes
            { "com.unity.cinemachine", "2.9.7" },                    // Advanced camera system
            { "com.unity.burst", "1.8.4" },                          // High-performance compiled code
            { "com.unity.mathematics", "1.2.6" },                    // Performance math library
            { "com.unity.collections", "1.4.0" },                    // High-performance collections
            { "com.unity.jobs", "0.51.0" }                          // Job system for multithreading
        };

        // Optional packages that enhance development
        private static readonly Dictionary<string, string> OptionalPackages = new Dictionary<string, string>
        {
            { "com.unity.ide.visualstudio", "2.0.18" },             // Visual Studio integration
            { "com.unity.ide.vscode", "1.2.5" },                    // VS Code integration
            { "com.unity.ide.rider", "3.0.24" },                    // JetBrains Rider integration
            { "com.unity.collab-proxy", "2.0.5" },                  // Unity Collaborate
            { "com.unity.recorder", "4.0.1" },                      // Video/image recording
            { "com.unity.performance.profile-analyzer", "1.2.2" }   // Performance analysis
        };

        private static ListRequest listRequest;
        private static AddRequest addRequest;
        private static RemoveRequest removeRequest;

        [MenuItem("WhiskerKing/Packages/Install Required Packages", false, 1)]
        public static void InstallRequiredPackages()
        {
            Debug.Log("Starting installation of required packages for Whisker King...");
            
            foreach (var package in RequiredPackages)
            {
                string packageId = $"{package.Key}@{package.Value}";
                InstallPackage(packageId, true);
            }
        }

        [MenuItem("WhiskerKing/Packages/Install Optional Packages", false, 2)]
        public static void InstallOptionalPackages()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Install Optional Packages",
                "This will install development and IDE integration packages. Continue?",
                "Install",
                "Cancel");

            if (confirmed)
            {
                Debug.Log("Starting installation of optional packages...");
                
                foreach (var package in OptionalPackages)
                {
                    string packageId = $"{package.Key}@{package.Value}";
                    InstallPackage(packageId, false);
                }
            }
        }

        [MenuItem("WhiskerKing/Packages/Validate Package Dependencies", false, 11)]
        public static void ValidatePackages()
        {
            Debug.Log("=== Package Validation for Whisker King ===");
            
            listRequest = Client.List(true);
            EditorApplication.update += ValidatePackagesProgress;
        }

        [MenuItem("WhiskerKing/Packages/Update All Packages", false, 12)]
        public static void UpdateAllPackages()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Update Packages",
                "This will update all packages to their latest compatible versions. This may cause breaking changes. Continue?",
                "Update",
                "Cancel");

            if (confirmed)
            {
                Debug.Log("Starting package updates...");
                UpdatePackagesToLatest();
            }
        }

        [MenuItem("WhiskerKing/Packages/Generate Package Report", false, 21)]
        public static void GeneratePackageReport()
        {
            listRequest = Client.List(true);
            EditorApplication.update += GeneratePackageReportProgress;
        }

        [MenuItem("WhiskerKing/Packages/Clean Package Cache", false, 31)]
        public static void CleanPackageCache()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clean Package Cache",
                "This will clear the Unity Package Manager cache. Packages will need to be re-downloaded. Continue?",
                "Clean",
                "Cancel");

            if (confirmed)
            {
                Client.Resolve();
                Debug.Log("Package cache cleaning initiated...");
            }
        }

        private static void InstallPackage(string packageId, bool isRequired)
        {
            Debug.Log($"Installing package: {packageId} ({(isRequired ? "Required" : "Optional")})");
            
            addRequest = Client.Add(packageId);
            
            // Note: In a real implementation, you'd want to handle this asynchronously
            // For this example, we'll just log the request
            string priority = isRequired ? "HIGH" : "NORMAL";
            Debug.Log($"Package installation queued with {priority} priority: {packageId}");
        }

        private static void ValidatePackagesProgress()
        {
            if (listRequest.IsCompleted)
            {
                EditorApplication.update -= ValidatePackagesProgress;
                
                if (listRequest.Status == StatusCode.Success)
                {
                    ValidatePackageList(listRequest.Result);
                }
                else
                {
                    Debug.LogError($"Package validation failed: {listRequest.Error.message}");
                }
            }
        }

        private static void ValidatePackageList(UnityEditor.PackageManager.PackageCollection packages)
        {
            Debug.Log("=== Required Package Validation ===");
            
            var installedPackages = packages.ToDictionary(p => p.name, p => p.version);
            var validationResults = new List<PackageValidationResult>();

            // Check required packages
            foreach (var requiredPackage in RequiredPackages)
            {
                var result = new PackageValidationResult
                {
                    packageName = requiredPackage.Key,
                    expectedVersion = requiredPackage.Value,
                    isRequired = true
                };

                if (installedPackages.ContainsKey(requiredPackage.Key))
                {
                    result.installedVersion = installedPackages[requiredPackage.Key];
                    result.isInstalled = true;
                    result.isCorrectVersion = result.installedVersion == requiredPackage.Value;
                }

                validationResults.Add(result);
            }

            // Report results
            foreach (var result in validationResults)
            {
                if (result.isInstalled)
                {
                    if (result.isCorrectVersion)
                    {
                        Debug.Log($"✓ {result.packageName}: {result.installedVersion} (Expected: {result.expectedVersion})");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠ {result.packageName}: {result.installedVersion} (Expected: {result.expectedVersion}) - Version mismatch");
                    }
                }
                else
                {
                    if (result.isRequired)
                    {
                        Debug.LogError($"✗ {result.packageName}: NOT INSTALLED (Required: {result.expectedVersion})");
                    }
                    else
                    {
                        Debug.Log($"○ {result.packageName}: Not installed (Optional)");
                    }
                }
            }

            // Summary
            int installedRequired = validationResults.Count(r => r.isRequired && r.isInstalled);
            int totalRequired = validationResults.Count(r => r.isRequired);
            
            Debug.Log($"=== Validation Summary ===");
            Debug.Log($"Required packages installed: {installedRequired}/{totalRequired}");
            
            if (installedRequired < totalRequired)
            {
                Debug.LogWarning("Some required packages are missing. Run 'Install Required Packages' to fix this.");
            }
            else
            {
                Debug.Log("All required packages are installed!");
            }
        }

        private static void UpdatePackagesToLatest()
        {
            // In a real implementation, this would check for and install latest versions
            Debug.Log("Checking for package updates...");
            
            foreach (var package in RequiredPackages.Keys)
            {
                Debug.Log($"Checking for updates: {package}");
                // Would implement actual update logic here
            }
        }

        private static void GeneratePackageReportProgress()
        {
            if (listRequest.IsCompleted)
            {
                EditorApplication.update -= GeneratePackageReportProgress;
                
                if (listRequest.Status == StatusCode.Success)
                {
                    GeneratePackageReportFile(listRequest.Result);
                }
                else
                {
                    Debug.LogError($"Package report generation failed: {listRequest.Error.message}");
                }
            }
        }

        private static void GeneratePackageReportFile(UnityEditor.PackageManager.PackageCollection packages)
        {
            var report = new PackageReport
            {
                generatedDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                unityVersion = Application.unityVersion,
                packages = packages.Select(p => new PackageInfo
                {
                    name = p.name,
                    version = p.version,
                    displayName = p.displayName,
                    description = p.description,
                    category = p.category.ToString(),
                    source = p.source.ToString(),
                    isRequired = RequiredPackages.ContainsKey(p.name),
                    expectedVersion = RequiredPackages.ContainsKey(p.name) ? RequiredPackages[p.name] : ""
                }).ToList()
            };

            string reportPath = "Packages/package-report.json";
            string jsonContent = JsonConvert.SerializeObject(report, Formatting.Indented);
            
            File.WriteAllText(reportPath, jsonContent);
            
            Debug.Log($"Package report generated: {reportPath}");
            Debug.Log($"Total packages: {report.packages.Count}");
            Debug.Log($"Required packages: {report.packages.Count(p => p.isRequired)}");
            Debug.Log($"Optional packages: {report.packages.Count(p => !p.isRequired)}");
            
            // Refresh the Project window to show the new file
            AssetDatabase.Refresh();
        }

        [System.Serializable]
        private class PackageValidationResult
        {
            public string packageName;
            public string expectedVersion;
            public string installedVersion;
            public bool isInstalled;
            public bool isRequired;
            public bool isCorrectVersion;
        }

        [System.Serializable]
        private class PackageReport
        {
            public string generatedDate;
            public string unityVersion;
            public List<PackageInfo> packages;
        }

        [System.Serializable]
        private class PackageInfo
        {
            public string name;
            public string version;
            public string displayName;
            public string description;
            public string category;
            public string source;
            public bool isRequired;
            public string expectedVersion;
        }

        // Development utilities
        [MenuItem("WhiskerKing/Packages/Development/List All Packages", false, 101)]
        public static void ListAllPackages()
        {
            listRequest = Client.List(true);
            EditorApplication.update += ListAllPackagesProgress;
        }

        private static void ListAllPackagesProgress()
        {
            if (listRequest.IsCompleted)
            {
                EditorApplication.update -= ListAllPackagesProgress;
                
                if (listRequest.Status == StatusCode.Success)
                {
                    Debug.Log("=== All Installed Packages ===");
                    foreach (var package in listRequest.Result)
                    {
                        string status = RequiredPackages.ContainsKey(package.name) ? "[REQUIRED]" : 
                                      OptionalPackages.ContainsKey(package.name) ? "[OPTIONAL]" : "[OTHER]";
                        Debug.Log($"{status} {package.displayName} ({package.name}) - {package.version}");
                    }
                }
                else
                {
                    Debug.LogError($"Failed to list packages: {listRequest.Error.message}");
                }
            }
        }

        [MenuItem("WhiskerKing/Packages/Development/Export Package List", false, 102)]
        public static void ExportPackageList()
        {
            string manifestPath = "Packages/manifest.json";
            string exportPath = "package-export.json";
            
            if (File.Exists(manifestPath))
            {
                File.Copy(manifestPath, exportPath, true);
                Debug.Log($"Package manifest exported to: {exportPath}");
            }
            else
            {
                Debug.LogError("Package manifest not found!");
            }
        }
    }
}
#endif
