using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WhiskerKing.Core
{
    /// <summary>
    /// Build configuration settings for different target platforms
    /// Based on Whisker King PRD performance targets and specifications
    /// </summary>
    [CreateAssetMenu(fileName = "BuildConfiguration", menuName = "WhiskerKing/Build Configuration")]
    public class BuildConfiguration : ScriptableObject
    {
        [Header("General Settings")]
        public string productName = "Whisker King";
        public string companyName = "Your Studio";
        public string bundleIdentifier = "com.yourstudio.whiskerking";
        public string version = "1.0.0";
        public int bundleVersionCode = 1;

        [Header("Performance Targets")]
        public int targetFrameRate = 60;
        public int minFrameRate = 30;
        public int maxMemoryMB = 512;
        public int maxTextureSizeMB = 256;

        [Header("Platform Specific")]
        public WebGLSettings webGL;
        public AndroidSettings android;
        public iOSSettings iOS;

        [System.Serializable]
        public class WebGLSettings
        {
            [Header("WebGL Configuration")]
            public bool enableExceptionSupport = false;
            public bool enableCodeOptimization = true;
            public WebGLCompressionFormat compressionFormat = WebGLCompressionFormat.Gzip;
            public bool enableMultithreading = true;
            public bool enableWasm = true;
            public int memorySize = 512; // MB
            public bool stripEngineCode = true;

            [Header("WebGL Graphics")]
            public bool enableWebGL2 = true;
            public bool enableWebGL1Fallback = true;
            public bool enableGPUInstancing = true;
            public int textureQuality = 1; // 0=full, 1=half, 2=quarter

            [Header("WebGL Audio")]
            public bool enableAudioCompression = true;
            public WebGLAudioCompressionFormat audioFormat = WebGLAudioCompressionFormat.AAC;
        }

        [System.Serializable]
        public class AndroidSettings
        {
            [Header("Android Configuration")]
            public AndroidArchitecture targetArchitecture = AndroidArchitecture.ARM64;
            public AndroidSdkVersions minSdkVersion = AndroidSdkVersions.AndroidApiLevel21; // Android 5.0
            public AndroidSdkVersions targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33; // Android 13
            public bool enableIL2CPP = true;
            public bool enableOptimization = true;

            [Header("Android Graphics")]
            public bool enableVulkan = true;
            public bool enableOpenGLES3 = true;
            public bool enableMultithreadedRendering = true;
            public int textureCompression = 1; // ASTC

            [Header("Android APK")]
            public bool enableAppBundle = true;
            public bool enableProGuard = true;
            public int compressionMethod = 1; // LZ4HC
            public bool splitApkByAbi = true;

            [Header("Android Performance")]
            public bool enableBurstCompiler = true;
            public bool enableJobsDebugger = false;
            public bool enableDeepProfiling = false;
        }

        [System.Serializable]
        public class iOSSettings
        {
            [Header("iOS Configuration")]
            public iOSSdkVersion sdkVersion = iOSSdkVersion.DeviceSDK;
            public iOSTargetDevice targetDevice = iOSTargetDevice.iPhoneAndiPad;
            public string minimumVersionSupported = "12.0"; // iOS 12+
            public bool enableBitcode = false; // Deprecated by Apple
            public bool enableIL2CPP = true;

            [Header("iOS Graphics")]
            public bool enableMetalAPI = true;
            public bool enableMultithreadedRendering = true;
            public int textureCompression = 0; // ASTC
            public bool enableGPUInstancing = true;

            [Header("iOS Build")]
            public bool enableTestability = false;
            public bool enableSymlinking = true;
            public bool stripEngineCode = true;
            public iOSArchitecture architecture = iOSArchitecture.ARM64;

            [Header("iOS Performance")]
            public bool enableBurstCompiler = true;
            public bool enableManagedStripping = true;
            public string scriptingBackend = "IL2CPP";
        }

        public enum WebGLCompressionFormat
        {
            Disabled = 0,
            Gzip = 1,
            Brotli = 2
        }

        public enum WebGLAudioCompressionFormat
        {
            AAC = 0,
            Vorbis = 1
        }

#if UNITY_EDITOR
        [ContextMenu("Apply WebGL Settings")]
        public void ApplyWebGLSettings()
        {
            PlayerSettings.productName = productName;
            PlayerSettings.companyName = companyName;
            PlayerSettings.bundleVersion = version;
            
            // WebGL specific settings
            PlayerSettings.WebGL.compressionFormat = (UnityEditor.WebGLCompressionFormat)webGL.compressionFormat;
            PlayerSettings.WebGL.memorySize = webGL.memorySize;
            PlayerSettings.WebGL.exceptionSupport = webGL.enableExceptionSupport ? WebGLExceptionSupport.FullWithStacktrace : WebGLExceptionSupport.None;
            PlayerSettings.WebGL.template = "PROJECT:Better2020";
            PlayerSettings.WebGL.threadsSupport = webGL.enableMultithreading;
            PlayerSettings.WebGL.wasmStreaming = webGL.enableWasm;
            
            // Graphics settings for WebGL
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new UnityEngine.Rendering.GraphicsDeviceType[] { 
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2
            });
            
            Debug.Log("Applied WebGL build settings");
        }

        [ContextMenu("Apply Android Settings")]
        public void ApplyAndroidSettings()
        {
            PlayerSettings.productName = productName;
            PlayerSettings.companyName = companyName;
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = bundleVersionCode;
            PlayerSettings.applicationIdentifier = bundleIdentifier;
            
            // Android specific settings
            PlayerSettings.Android.minSdkVersion = android.minSdkVersion;
            PlayerSettings.Android.targetSdkVersion = android.targetSdkVersion;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, android.enableIL2CPP ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
            PlayerSettings.Android.targetArchitectures = (AndroidArchitecture)android.targetArchitecture;
            
            // Graphics settings for Android
            if (android.enableVulkan)
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new UnityEngine.Rendering.GraphicsDeviceType[] { 
                    UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                    UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
                });
            }
            else
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new UnityEngine.Rendering.GraphicsDeviceType[] { 
                    UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
                });
            }
            
            PlayerSettings.MTRendering = android.enableMultithreadedRendering;
            PlayerSettings.Android.buildApkPerCpuArchitecture = android.splitApkByAbi;
            
            Debug.Log("Applied Android build settings");
        }

        [ContextMenu("Apply iOS Settings")]
        public void ApplyiOSSettings()
        {
            PlayerSettings.productName = productName;
            PlayerSettings.companyName = companyName;
            PlayerSettings.bundleVersion = version;
            PlayerSettings.applicationIdentifier = bundleIdentifier;
            
            // iOS specific settings
            PlayerSettings.iOS.sdkVersion = iOS.sdkVersion;
            PlayerSettings.iOS.targetDevice = iOS.targetDevice;
            PlayerSettings.iOS.targetOSVersionString = iOS.minimumVersionSupported;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.buildNumber = bundleVersionCode.ToString();
            
            // Graphics settings for iOS
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new UnityEngine.Rendering.GraphicsDeviceType[] { 
                UnityEngine.Rendering.GraphicsDeviceType.Metal
            });
            
            PlayerSettings.MTRendering = iOS.enableMultithreadedRendering;
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.appleDeveloperTeamID = ""; // Set your team ID
            
            Debug.Log("Applied iOS build settings");
        }

        [ContextMenu("Apply All Settings")]
        public void ApplyAllSettings()
        {
            // Apply common settings
            PlayerSettings.productName = productName;
            PlayerSettings.companyName = companyName;
            PlayerSettings.bundleVersion = version;
            Application.targetFrameRate = targetFrameRate;
            
            Debug.Log("Applied common build settings. Use platform-specific methods for detailed configuration.");
        }
#endif
    }
}
