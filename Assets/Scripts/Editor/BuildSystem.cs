#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Collections.Generic;

namespace WhiskerKing.Editor
{
    /// <summary>
    /// Automated build system for Whisker King
    /// Handles builds for WebGL, Android, and iOS with platform-specific optimizations
    /// </summary>
    public static class BuildSystem
    {
        private const string BUILD_PATH = "Builds";
        private const string WEBGL_PATH = BUILD_PATH + "/WebGL";
        private const string ANDROID_PATH = BUILD_PATH + "/Android";
        private const string IOS_PATH = BUILD_PATH + "/iOS";

        // Scene paths for builds
        private static readonly string[] SCENES = new string[]
        {
            "Assets/Scenes/MainMenu/MainMenu.unity",
            "Assets/Scenes/Levels/Level_Seaside_01.unity",
            // Add more scenes as they are created
        };

        #region Menu Items

        [MenuItem("WhiskerKing/Build/WebGL Development", false, 1)]
        public static void BuildWebGLDevelopment()
        {
            BuildWebGL(BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        [MenuItem("WhiskerKing/Build/WebGL Release", false, 2)]
        public static void BuildWebGLRelease()
        {
            BuildWebGL(BuildOptions.None);
        }

        [MenuItem("WhiskerKing/Build/Android Development", false, 11)]
        public static void BuildAndroidDevelopment()
        {
            BuildAndroid(BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        [MenuItem("WhiskerKing/Build/Android Release", false, 12)]
        public static void BuildAndroidRelease()
        {
            BuildAndroid(BuildOptions.None);
        }

        [MenuItem("WhiskerKing/Build/iOS Development", false, 21)]
        public static void BuildiOSDevelopment()
        {
            BuildiOS(BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        [MenuItem("WhiskerKing/Build/iOS Release", false, 22)]
        public static void BuildiOSRelease()
        {
            BuildiOS(BuildOptions.None);
        }

        [MenuItem("WhiskerKing/Build/All Platforms Development", false, 31)]
        public static void BuildAllDevelopment()
        {
            BuildWebGL(BuildOptions.Development);
            BuildAndroid(BuildOptions.Development);
            BuildiOS(BuildOptions.Development);
        }

        [MenuItem("WhiskerKing/Build/Clean Build Folder", false, 41)]
        public static void CleanBuildFolder()
        {
            if (Directory.Exists(BUILD_PATH))
            {
                Directory.Delete(BUILD_PATH, true);
                Debug.Log("Build folder cleaned");
            }
        }

        #endregion

        #region WebGL Build

        public static void BuildWebGL(BuildOptions options)
        {
            Debug.Log("Starting WebGL build...");

            // Apply WebGL-specific settings
            ConfigureWebGLSettings();

            // Create build directory
            Directory.CreateDirectory(WEBGL_PATH);

            // Build settings
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = GetValidScenes(),
                locationPathName = WEBGL_PATH,
                target = BuildTarget.WebGL,
                options = options
            };

            // Execute build
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            HandleBuildResult(report, "WebGL");
        }

        private static void ConfigureWebGLSettings()
        {
            // WebGL specific optimizations
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.threadsSupport = true;
            PlayerSettings.WebGL.wasmStreaming = true;

            // Graphics settings
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new UnityEngine.Rendering.GraphicsDeviceType[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2
            });

            // Performance settings
            PlayerSettings.runInBackground = false;
            PlayerSettings.displayResolutionDialog = ResolutionDialogSetting.HiddenByDefault;

            Debug.Log("Applied WebGL configuration");
        }

        #endregion

        #region Android Build

        public static void BuildAndroid(BuildOptions options)
        {
            Debug.Log("Starting Android build...");

            // Apply Android-specific settings
            ConfigureAndroidSettings();

            // Create build directory
            Directory.CreateDirectory(ANDROID_PATH);

            string fileName = options.HasFlag(BuildOptions.Development) ? "WhiskerKing_Dev.apk" : "WhiskerKing.apk";
            string buildPath = Path.Combine(ANDROID_PATH, fileName);

            // Build settings
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = GetValidScenes(),
                locationPathName = buildPath,
                target = BuildTarget.Android,
                options = options
            };

            // Execute build
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            HandleBuildResult(report, "Android");
        }

        private static void ConfigureAndroidSettings()
        {
            // Android specific optimizations
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel21; // Android 5.0
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33; // Android 13
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // Graphics settings
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new UnityEngine.Rendering.GraphicsDeviceType[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
            });

            PlayerSettings.MTRendering = true;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false; // Single APK for all architectures

            // Bundle settings
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.useAPKExpansionFiles = false;

            Debug.Log("Applied Android configuration");
        }

        #endregion

        #region iOS Build

        public static void BuildiOS(BuildOptions options)
        {
            Debug.Log("Starting iOS build...");

            // Apply iOS-specific settings
            ConfigureiOSSettings();

            // Create build directory
            Directory.CreateDirectory(IOS_PATH);

            // Build settings
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = GetValidScenes(),
                locationPathName = IOS_PATH,
                target = BuildTarget.iOS,
                options = options
            };

            // Execute build
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            HandleBuildResult(report, "iOS");
        }

        private static void ConfigureiOSSettings()
        {
            // iOS specific optimizations
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "12.0";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);

            // Graphics settings
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new UnityEngine.Rendering.GraphicsDeviceType[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Metal
            });

            PlayerSettings.MTRendering = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;

            // Performance settings
            PlayerSettings.iOS.scriptCallOptimization = ScriptCallOptimizationLevel.FastButNoExceptions;

            Debug.Log("Applied iOS configuration");
        }

        #endregion

        #region Utility Methods

        private static string[] GetValidScenes()
        {
            List<string> validScenes = new List<string>();

            foreach (string scenePath in SCENES)
            {
                if (File.Exists(scenePath))
                {
                    validScenes.Add(scenePath);
                }
                else
                {
                    Debug.LogWarning($"Scene not found: {scenePath}");
                }
            }

            if (validScenes.Count == 0)
            {
                Debug.LogError("No valid scenes found for build!");
                // Fallback to current scene
                validScenes.Add(EditorApplication.currentScene);
            }

            return validScenes.ToArray();
        }

        private static void HandleBuildResult(BuildReport report, string platform)
        {
            BuildSummary summary = report.summary;

            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    Debug.Log($"{platform} build succeeded!");
                    Debug.Log($"Build size: {GetBuildSizeString(summary.totalSize)}");
                    Debug.Log($"Build time: {summary.totalTime}");
                    Debug.Log($"Output path: {summary.outputPath}");
                    break;

                case BuildResult.Failed:
                    Debug.LogError($"{platform} build failed!");
                    Debug.LogError($"Errors: {summary.totalErrors}");
                    break;

                case BuildResult.Cancelled:
                    Debug.LogWarning($"{platform} build was cancelled");
                    break;

                case BuildResult.Unknown:
                    Debug.LogWarning($"{platform} build result unknown");
                    break;
            }

            // Log detailed build info
            if (summary.totalErrors > 0)
            {
                Debug.LogError($"Build completed with {summary.totalErrors} error(s) and {summary.totalWarnings} warning(s)");
            }
            else if (summary.totalWarnings > 0)
            {
                Debug.LogWarning($"Build completed with {summary.totalWarnings} warning(s)");
            }
        }

        private static string GetBuildSizeString(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region Build Validation

        [MenuItem("WhiskerKing/Validate/Check Build Settings", false, 51)]
        public static void ValidateBuildSettings()
        {
            Debug.Log("=== Build Settings Validation ===");

            // Check scenes
            ValidateScenes();

            // Check player settings
            ValidatePlayerSettings();

            // Check quality settings
            ValidateQualitySettings();

            Debug.Log("=== Validation Complete ===");
        }

        private static void ValidateScenes()
        {
            Debug.Log("Validating scenes...");
            
            foreach (string scenePath in SCENES)
            {
                if (File.Exists(scenePath))
                {
                    Debug.Log($"✓ Scene found: {scenePath}");
                }
                else
                {
                    Debug.LogWarning($"✗ Scene missing: {scenePath}");
                }
            }
        }

        private static void ValidatePlayerSettings()
        {
            Debug.Log("Validating player settings...");

            if (string.IsNullOrEmpty(PlayerSettings.productName))
                Debug.LogWarning("✗ Product name is empty");
            else
                Debug.Log($"✓ Product name: {PlayerSettings.productName}");

            if (string.IsNullOrEmpty(PlayerSettings.companyName))
                Debug.LogWarning("✗ Company name is empty");
            else
                Debug.Log($"✓ Company name: {PlayerSettings.companyName}");

            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion))
                Debug.LogWarning("✗ Bundle version is empty");
            else
                Debug.Log($"✓ Bundle version: {PlayerSettings.bundleVersion}");
        }

        private static void ValidateQualitySettings()
        {
            Debug.Log("Validating quality settings...");

            int qualityLevels = QualitySettings.names.Length;
            Debug.Log($"✓ Quality levels configured: {qualityLevels}");

            if (qualityLevels < 3)
                Debug.LogWarning("✗ Recommend at least 3 quality levels (Low/Medium/High)");
        }

        #endregion
    }
}
#endif
