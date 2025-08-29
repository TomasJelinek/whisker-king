using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

namespace WhiskerKing.Core
{
    public class QualityManager : MonoBehaviour
    {
        [Header("URP Assets")]
        [SerializeField] private UniversalRenderPipelineAsset lowQualityAsset;
        [SerializeField] private UniversalRenderPipelineAsset mediumQualityAsset;
        [SerializeField] private UniversalRenderPipelineAsset highQualityAsset;

        [Header("Performance Monitoring")]
        [SerializeField] private float targetFrameRate = 60f;
        [SerializeField] private float frameRateCheckInterval = 2f;
        [SerializeField] private int frameRateCheckSamples = 30;
        [SerializeField] private bool enableAutoQualityAdjustment = true;

        [Header("Quality Thresholds")]
        [SerializeField] private float lowQualityThreshold = 45f;
        [SerializeField] private float mediumQualityThreshold = 55f;
        [SerializeField] private float highQualityThreshold = 65f;

        private float[] frameTimeBuffer;
        private int frameTimeIndex;
        private float lastQualityCheckTime;
        private QualityLevel currentQualityLevel;

        public enum QualityLevel
        {
            Low = 0,
            Medium = 1,
            High = 2
        }

        private void Start()
        {
            InitializeQualityManager();
            SetInitialQuality();
        }

        private void Update()
        {
            if (enableAutoQualityAdjustment)
            {
                MonitorPerformance();
            }
        }

        private void InitializeQualityManager()
        {
            frameTimeBuffer = new float[frameRateCheckSamples];
            frameTimeIndex = 0;
            lastQualityCheckTime = Time.time;

            // Set target frame rate
            Application.targetFrameRate = Mathf.RoundToInt(targetFrameRate);
        }

        private void SetInitialQuality()
        {
            // Determine initial quality based on device capabilities
            QualityLevel initialQuality = DetermineDeviceQuality();
            SetQuality(initialQuality);
        }

        private QualityLevel DetermineDeviceQuality()
        {
            // Check device specifications based on PRD requirements
            int memorySize = SystemInfo.systemMemorySize;
            string gpuName = SystemInfo.graphicsDeviceName.ToLower();
            int processorCount = SystemInfo.processorCount;
            
            // Log device specifications for debugging
            Debug.Log($"Device Detection - Memory: {memorySize}MB, GPU: {SystemInfo.graphicsDeviceName}, CPU Cores: {processorCount}");

            // High-end device criteria (Snapdragon 8 Gen 1+, A14 Bionic+)
            if (memorySize >= 4096 && processorCount >= 6)
            {
                // Android high-end GPUs
                if (gpuName.Contains("adreno") && (gpuName.Contains("730") || gpuName.Contains("740") || 
                    gpuName.Contains("650") || gpuName.Contains("660") || gpuName.Contains("685")))
                    return QualityLevel.High;
                    
                // Mali high-end GPUs  
                if (gpuName.Contains("mali") && (gpuName.Contains("g78") || gpuName.Contains("g710") || gpuName.Contains("g715")))
                    return QualityLevel.High;
                    
                // Apple high-end processors
                if (gpuName.Contains("apple") && (gpuName.Contains("a14") || gpuName.Contains("a15") || 
                    gpuName.Contains("a16") || gpuName.Contains("a17") || gpuName.Contains("m1") || gpuName.Contains("m2")))
                    return QualityLevel.High;
                    
                // Desktop/high-end mobile GPUs
                if (gpuName.Contains("geforce") || gpuName.Contains("radeon") || gpuName.Contains("intel arc"))
                    return QualityLevel.High;
            }

            // Medium device criteria (Snapdragon 855+, A12 Bionic+)
            if (memorySize >= 3072 && processorCount >= 4)
            {
                // Android mid-range GPUs
                if (gpuName.Contains("adreno") && (gpuName.Contains("630") || gpuName.Contains("640") || 
                    gpuName.Contains("530") || gpuName.Contains("540")))
                    return QualityLevel.Medium;
                    
                // Mali mid-range GPUs
                if (gpuName.Contains("mali") && (gpuName.Contains("g72") || gpuName.Contains("g76")))
                    return QualityLevel.Medium;
                    
                // Apple mid-range processors
                if (gpuName.Contains("apple") && (gpuName.Contains("a12") || gpuName.Contains("a13")))
                    return QualityLevel.Medium;
                    
                // Integrated graphics that can handle medium settings
                if (gpuName.Contains("intel") && (gpuName.Contains("iris") || gpuName.Contains("uhd")))
                    return QualityLevel.Medium;
            }

            // Low-end device criteria (Minimum spec: Snapdragon 660+, A10 Fusion+)
            if (memorySize >= 2048 && processorCount >= 4)
            {
                // Android minimum spec GPUs
                if (gpuName.Contains("adreno") && (gpuName.Contains("510") || gpuName.Contains("512") || 
                    gpuName.Contains("506") || gpuName.Contains("508")))
                    return QualityLevel.Low;
                    
                // Mali minimum spec GPUs
                if (gpuName.Contains("mali") && (gpuName.Contains("g51") || gpuName.Contains("g71")))
                    return QualityLevel.Low;
                    
                // Apple minimum spec processors
                if (gpuName.Contains("apple") && (gpuName.Contains("a10") || gpuName.Contains("a11")))
                    return QualityLevel.Low;
            }

            // Default to lowest quality for unsupported devices
            Debug.LogWarning($"Device below minimum specifications. Using Low Quality. " +
                           $"Memory: {memorySize}MB, Cores: {processorCount}");
            return QualityLevel.Low;
        }

        public void SetQuality(QualityLevel quality)
        {
            currentQualityLevel = quality;
            UniversalRenderPipelineAsset targetAsset = null;

            switch (quality)
            {
                case QualityLevel.Low:
                    targetAsset = lowQualityAsset;
                    QualitySettings.SetQualityLevel(0, true);
                    break;
                case QualityLevel.Medium:
                    targetAsset = mediumQualityAsset;
                    QualitySettings.SetQualityLevel(1, true);
                    break;
                case QualityLevel.High:
                    targetAsset = highQualityAsset;
                    QualitySettings.SetQualityLevel(2, true);
                    break;
            }

            if (targetAsset != null)
            {
                GraphicsSettings.renderPipelineAsset = targetAsset;
                QualitySettings.renderPipeline = targetAsset;
            }

            ApplyQualitySettings(quality);
            Debug.Log($"Quality set to: {quality}");
        }

        private void ApplyQualitySettings(QualityLevel quality)
        {
            switch (quality)
            {
                case QualityLevel.Low:
                    // Low quality settings
                    Application.targetFrameRate = 30;
                    Screen.SetResolution((int)(Screen.currentResolution.width * 0.8f), 
                                       (int)(Screen.currentResolution.height * 0.8f), 
                                       Screen.fullScreen);
                    break;

                case QualityLevel.Medium:
                    // Medium quality settings
                    Application.targetFrameRate = 60;
                    Screen.SetResolution(Screen.currentResolution.width, 
                                       Screen.currentResolution.height, 
                                       Screen.fullScreen);
                    break;

                case QualityLevel.High:
                    // High quality settings
                    Application.targetFrameRate = 60;
                    Screen.SetResolution(Screen.currentResolution.width, 
                                       Screen.currentResolution.height, 
                                       Screen.fullScreen);
                    break;
            }
        }

        private void MonitorPerformance()
        {
            // Record frame time
            frameTimeBuffer[frameTimeIndex] = Time.deltaTime;
            frameTimeIndex = (frameTimeIndex + 1) % frameRateCheckSamples;

            // Check if it's time to evaluate quality
            if (Time.time - lastQualityCheckTime >= frameRateCheckInterval)
            {
                float averageFrameTime = CalculateAverageFrameTime();
                float averageFrameRate = 1f / averageFrameTime;

                AdjustQualityBasedOnPerformance(averageFrameRate);
                lastQualityCheckTime = Time.time;
            }
        }

        private float CalculateAverageFrameTime()
        {
            float sum = 0f;
            for (int i = 0; i < frameTimeBuffer.Length; i++)
            {
                sum += frameTimeBuffer[i];
            }
            return sum / frameTimeBuffer.Length;
        }

        private void AdjustQualityBasedOnPerformance(float currentFrameRate)
        {
            QualityLevel newQuality = currentQualityLevel;

            // Downgrade quality if performance is poor
            if (currentFrameRate < lowQualityThreshold && currentQualityLevel > QualityLevel.Low)
            {
                newQuality = QualityLevel.Low;
            }
            else if (currentFrameRate < mediumQualityThreshold && currentQualityLevel > QualityLevel.Medium)
            {
                newQuality = QualityLevel.Medium;
            }
            // Upgrade quality if performance is good
            else if (currentFrameRate > highQualityThreshold && currentQualityLevel < QualityLevel.High)
            {
                newQuality = QualityLevel.High;
            }
            else if (currentFrameRate > mediumQualityThreshold && currentQualityLevel < QualityLevel.Medium)
            {
                newQuality = QualityLevel.Medium;
            }

            // Apply quality change if needed
            if (newQuality != currentQualityLevel)
            {
                SetQuality(newQuality);
                Debug.Log($"Auto-adjusted quality from {currentQualityLevel} to {newQuality} (FPS: {currentFrameRate:F1})");
            }
        }

        public QualityLevel GetCurrentQuality()
        {
            return currentQualityLevel;
        }

        public void SetAutoQualityAdjustment(bool enabled)
        {
            enableAutoQualityAdjustment = enabled;
        }

        // Public methods for manual quality control
        public void SetLowQuality() => SetQuality(QualityLevel.Low);
        public void SetMediumQuality() => SetQuality(QualityLevel.Medium);
        public void SetHighQuality() => SetQuality(QualityLevel.High);
    }
}
