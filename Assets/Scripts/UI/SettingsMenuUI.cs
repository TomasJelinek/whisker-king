using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WhiskerKing.Core;
using WhiskerKing.Audio;

namespace WhiskerKing.UI
{
    /// <summary>
    /// Settings Menu UI Controller for Whisker King
    /// Handles audio, video, control, and accessibility settings
    /// </summary>
    public class SettingsMenuUI : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        
        [Header("Tab Buttons")]
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button videoTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private Button accessibilityTabButton;

        [Header("Settings Panels")]
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject videoPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject accessibilityPanel;

        [Header("Audio Settings")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider voiceVolumeSlider;
        [SerializeField] private TextMeshProUGUI masterVolumeText;
        [SerializeField] private TextMeshProUGUI musicVolumeText;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;
        [SerializeField] private TextMeshProUGUI voiceVolumeText;
        [SerializeField] private Toggle muteMasterToggle;
        [SerializeField] private TMP_Dropdown audioQualityDropdown;

        [Header("Video Settings")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private Slider fpsLimitSlider;
        [SerializeField] private TextMeshProUGUI fpsLimitText;
        [SerializeField] private Toggle motionBlurToggle;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private TextMeshProUGUI brightnessText;

        [Header("Control Settings")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private TextMeshProUGUI mouseSensitivityText;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private TMP_Dropdown controlSchemeDropdown;
        [SerializeField] private Button keyBindingsButton;

        [Header("Accessibility Settings")]
        [SerializeField] private Toggle keyboardNavigationToggle;
        [SerializeField] private TMP_Dropdown colorBlindDropdown;
        [SerializeField] private Toggle highContrastToggle;
        [SerializeField] private Toggle reducedMotionToggle;
        [SerializeField] private Slider uiScaleSlider;
        [SerializeField] private TextMeshProUGUI uiScaleText;
        [SerializeField] private Slider navigationSpeedSlider;
        [SerializeField] private TextMeshProUGUI navigationSpeedText;

        [Header("Tab Colors")]
        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = Color.gray;

        // Component references
        private UIManager uiManager;
        private AudioManager audioManager;

        // Settings state
        private SettingsTab currentTab = SettingsTab.Audio;
        private bool hasUnsavedChanges = false;

        // Cached settings
        private float cachedMasterVolume;
        private float cachedMusicVolume;
        private float cachedSFXVolume;
        private float cachedVoiceVolume;
        private AudioManager.AudioQuality cachedAudioQuality;
        private UIManager.AccessibilitySettings cachedAccessibility;

        public enum SettingsTab
        {
            Audio,
            Video,
            Controls,
            Accessibility
        }

        private void Awake()
        {
            // Get component references
            uiManager = UIManager.Instance;
            audioManager = AudioManager.Instance;
        }

        private void Start()
        {
            SetupButtons();
            SetupSliders();
            SetupDropdowns();
            LoadCurrentSettings();
            ShowTab(SettingsTab.Audio);
        }

        private void SetupButtons()
        {
            // Navigation buttons
            if (backButton != null)
                backButton.onClick.AddListener(GoBack);
            
            if (applyButton != null)
                applyButton.onClick.AddListener(ApplySettings);
            
            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefaults);

            // Tab buttons
            if (audioTabButton != null)
                audioTabButton.onClick.AddListener(() => ShowTab(SettingsTab.Audio));
            
            if (videoTabButton != null)
                videoTabButton.onClick.AddListener(() => ShowTab(SettingsTab.Video));
            
            if (controlsTabButton != null)
                controlsTabButton.onClick.AddListener(() => ShowTab(SettingsTab.Controls));
            
            if (accessibilityTabButton != null)
                accessibilityTabButton.onClick.AddListener(() => ShowTab(SettingsTab.Accessibility));

            // Other buttons
            if (keyBindingsButton != null)
                keyBindingsButton.onClick.AddListener(OpenKeyBindings);
        }

        private void SetupSliders()
        {
            // Audio sliders
            SetupVolumeSlider(masterVolumeSlider, masterVolumeText, OnMasterVolumeChanged);
            SetupVolumeSlider(musicVolumeSlider, musicVolumeText, OnMusicVolumeChanged);
            SetupVolumeSlider(sfxVolumeSlider, sfxVolumeText, OnSFXVolumeChanged);
            SetupVolumeSlider(voiceVolumeSlider, voiceVolumeText, OnVoiceVolumeChanged);

            // Video sliders
            if (fpsLimitSlider != null)
            {
                fpsLimitSlider.onValueChanged.AddListener(OnFPSLimitChanged);
            }

            if (brightnessSlider != null)
            {
                brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
            }

            // Control sliders
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            }

            // Accessibility sliders
            if (uiScaleSlider != null)
            {
                uiScaleSlider.onValueChanged.AddListener(OnUIScaleChanged);
            }

            if (navigationSpeedSlider != null)
            {
                navigationSpeedSlider.onValueChanged.AddListener(OnNavigationSpeedChanged);
            }
        }

        private void SetupVolumeSlider(Slider slider, TextMeshProUGUI text, UnityEngine.Events.UnityAction<float> callback)
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.onValueChanged.AddListener(callback);
                slider.onValueChanged.AddListener((value) => UpdateVolumeText(text, value));
            }
        }

        private void SetupDropdowns()
        {
            // Audio quality dropdown
            if (audioQualityDropdown != null)
            {
                audioQualityDropdown.options.Clear();
                audioQualityDropdown.options.Add(new TMP_Dropdown.OptionData("Low"));
                audioQualityDropdown.options.Add(new TMP_Dropdown.OptionData("Medium"));
                audioQualityDropdown.options.Add(new TMP_Dropdown.OptionData("High"));
                audioQualityDropdown.onValueChanged.AddListener(OnAudioQualityChanged);
            }

            // Resolution dropdown
            if (resolutionDropdown != null)
            {
                resolutionDropdown.options.Clear();
                Resolution[] resolutions = Screen.resolutions;
                foreach (var resolution in resolutions)
                {
                    resolutionDropdown.options.Add(new TMP_Dropdown.OptionData($"{resolution.width} x {resolution.height}"));
                }
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            // Quality preset dropdown
            if (qualityDropdown != null)
            {
                qualityDropdown.options.Clear();
                string[] qualityNames = QualitySettings.names;
                foreach (string name in qualityNames)
                {
                    qualityDropdown.options.Add(new TMP_Dropdown.OptionData(name));
                }
                qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
            }

            // Control scheme dropdown
            if (controlSchemeDropdown != null)
            {
                controlSchemeDropdown.options.Clear();
                controlSchemeDropdown.options.Add(new TMP_Dropdown.OptionData("Keyboard & Mouse"));
                controlSchemeDropdown.options.Add(new TMP_Dropdown.OptionData("Controller"));
                controlSchemeDropdown.onValueChanged.AddListener(OnControlSchemeChanged);
            }

            // Colorblind support dropdown
            if (colorBlindDropdown != null)
            {
                colorBlindDropdown.options.Clear();
                colorBlindDropdown.options.Add(new TMP_Dropdown.OptionData("Normal"));
                colorBlindDropdown.options.Add(new TMP_Dropdown.OptionData("Protanopia"));
                colorBlindDropdown.options.Add(new TMP_Dropdown.OptionData("Deuteranopia"));
                colorBlindDropdown.options.Add(new TMP_Dropdown.OptionData("Tritanopia"));
                colorBlindDropdown.onValueChanged.AddListener(OnColorBlindChanged);
            }
        }

        private void LoadCurrentSettings()
        {
            LoadAudioSettings();
            LoadVideoSettings();
            LoadControlSettings();
            LoadAccessibilitySettings();
        }

        private void LoadAudioSettings()
        {
            if (audioManager == null) return;

            // Load current volumes
            cachedMasterVolume = audioManager.GetCategoryVolume(AudioManager.AudioCategory.Music); // Using music as master proxy
            cachedMusicVolume = audioManager.GetCategoryVolume(AudioManager.AudioCategory.Music);
            cachedSFXVolume = audioManager.GetCategoryVolume(AudioManager.AudioCategory.SFX_Player);
            cachedVoiceVolume = audioManager.GetCategoryVolume(AudioManager.AudioCategory.Voice);

            // Set slider values
            if (masterVolumeSlider != null) masterVolumeSlider.value = cachedMasterVolume;
            if (musicVolumeSlider != null) musicVolumeSlider.value = cachedMusicVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = cachedSFXVolume;
            if (voiceVolumeSlider != null) voiceVolumeSlider.value = cachedVoiceVolume;

            // Update text displays
            UpdateVolumeText(masterVolumeText, cachedMasterVolume);
            UpdateVolumeText(musicVolumeText, cachedMusicVolume);
            UpdateVolumeText(sfxVolumeText, cachedSFXVolume);
            UpdateVolumeText(voiceVolumeText, cachedVoiceVolume);

            // Set audio quality dropdown
            // This would require getting current quality from audio manager
            // For now, default to high
            if (audioQualityDropdown != null)
                audioQualityDropdown.value = 2; // High quality
        }

        private void LoadVideoSettings()
        {
            // Load video settings from Unity settings
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = Screen.fullScreen;

            if (vsyncToggle != null)
                vsyncToggle.isOn = QualitySettings.vSyncCount > 0;

            if (qualityDropdown != null)
                qualityDropdown.value = QualitySettings.GetQualityLevel();

            // Set default values for other settings
            if (fpsLimitSlider != null)
            {
                fpsLimitSlider.value = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
                UpdateFPSText(fpsLimitSlider.value);
            }

            if (brightnessSlider != null)
            {
                brightnessSlider.value = 1f; // Default brightness
                UpdateBrightnessText(1f);
            }
        }

        private void LoadControlSettings()
        {
            // Load control settings (placeholder values)
            if (mouseSensitivitySlider != null)
            {
                mouseSensitivitySlider.value = 1f; // Default sensitivity
                UpdateMouseSensitivityText(1f);
            }

            if (invertYToggle != null)
                invertYToggle.isOn = false; // Default not inverted

            if (controlSchemeDropdown != null)
                controlSchemeDropdown.value = 0; // Default keyboard & mouse
        }

        private void LoadAccessibilitySettings()
        {
            if (uiManager == null) return;

            cachedAccessibility = uiManager.GetAccessibilitySettings();

            // Set accessibility controls
            if (keyboardNavigationToggle != null)
                keyboardNavigationToggle.isOn = cachedAccessibility.keyboardNavigationEnabled;

            if (highContrastToggle != null)
                highContrastToggle.isOn = cachedAccessibility.highContrastMode;

            if (reducedMotionToggle != null)
                reducedMotionToggle.isOn = cachedAccessibility.reducedMotion;

            if (uiScaleSlider != null)
            {
                uiScaleSlider.value = cachedAccessibility.uiScale;
                UpdateUIScaleText(cachedAccessibility.uiScale);
            }

            if (navigationSpeedSlider != null)
            {
                navigationSpeedSlider.value = cachedAccessibility.navigationSensitivity;
                UpdateNavigationSpeedText(cachedAccessibility.navigationSensitivity);
            }

            if (colorBlindDropdown != null)
                colorBlindDropdown.value = (int)cachedAccessibility.colorBlindType;
        }

        #region Tab Management

        private void ShowTab(SettingsTab tab)
        {
            currentTab = tab;

            // Hide all panels
            if (audioPanel != null) audioPanel.SetActive(false);
            if (videoPanel != null) videoPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (accessibilityPanel != null) accessibilityPanel.SetActive(false);

            // Update tab button colors
            UpdateTabColors();

            // Show selected panel
            switch (tab)
            {
                case SettingsTab.Audio:
                    if (audioPanel != null) audioPanel.SetActive(true);
                    break;
                case SettingsTab.Video:
                    if (videoPanel != null) videoPanel.SetActive(true);
                    break;
                case SettingsTab.Controls:
                    if (controlsPanel != null) controlsPanel.SetActive(true);
                    break;
                case SettingsTab.Accessibility:
                    if (accessibilityPanel != null) accessibilityPanel.SetActive(true);
                    break;
            }
        }

        private void UpdateTabColors()
        {
            // Update tab button colors based on current selection
            SetTabButtonColor(audioTabButton, currentTab == SettingsTab.Audio);
            SetTabButtonColor(videoTabButton, currentTab == SettingsTab.Video);
            SetTabButtonColor(controlsTabButton, currentTab == SettingsTab.Controls);
            SetTabButtonColor(accessibilityTabButton, currentTab == SettingsTab.Accessibility);
        }

        private void SetTabButtonColor(Button button, bool isActive)
        {
            if (button == null) return;

            ColorBlock colors = button.colors;
            colors.normalColor = isActive ? activeTabColor : inactiveTabColor;
            button.colors = colors;
        }

        #endregion

        #region Audio Settings Handlers

        private void OnMasterVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetMasterVolume(value);
                hasUnsavedChanges = true;
            }
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetMusicVolume(value);
                hasUnsavedChanges = true;
            }
        }

        private void OnSFXVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetSFXVolume(value);
                hasUnsavedChanges = true;
            }
        }

        private void OnVoiceVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetVoiceVolume(value);
                hasUnsavedChanges = true;
            }
        }

        private void OnAudioQualityChanged(int index)
        {
            if (audioManager != null)
            {
                AudioManager.AudioQuality quality = (AudioManager.AudioQuality)index;
                audioManager.SetAudioQuality(quality);
                hasUnsavedChanges = true;
            }
        }

        private void UpdateVolumeText(TextMeshProUGUI text, float value)
        {
            if (text != null)
            {
                text.text = $"{(value * 100):F0}%";
            }
        }

        #endregion

        #region Video Settings Handlers

        private void OnResolutionChanged(int index)
        {
            Resolution[] resolutions = Screen.resolutions;
            if (index >= 0 && index < resolutions.Length)
            {
                Resolution selectedResolution = resolutions[index];
                Screen.SetResolution(selectedResolution.width, selectedResolution.height, Screen.fullScreen);
                hasUnsavedChanges = true;
            }
        }

        private void OnQualityChanged(int index)
        {
            QualitySettings.SetQualityLevel(index);
            hasUnsavedChanges = true;
        }

        private void OnFPSLimitChanged(float value)
        {
            Application.targetFrameRate = (int)value;
            UpdateFPSText(value);
            hasUnsavedChanges = true;
        }

        private void OnBrightnessChanged(float value)
        {
            // Apply brightness setting (would need to be implemented in graphics)
            UpdateBrightnessText(value);
            hasUnsavedChanges = true;
        }

        private void UpdateFPSText(float value)
        {
            if (fpsLimitText != null)
            {
                fpsLimitText.text = value <= 0 ? "Unlimited" : $"{value:F0} FPS";
            }
        }

        private void UpdateBrightnessText(float value)
        {
            if (brightnessText != null)
            {
                brightnessText.text = $"{(value * 100):F0}%";
            }
        }

        #endregion

        #region Control Settings Handlers

        private void OnMouseSensitivityChanged(float value)
        {
            UpdateMouseSensitivityText(value);
            hasUnsavedChanges = true;
        }

        private void OnControlSchemeChanged(int index)
        {
            hasUnsavedChanges = true;
        }

        private void UpdateMouseSensitivityText(float value)
        {
            if (mouseSensitivityText != null)
            {
                mouseSensitivityText.text = $"{value:F2}";
            }
        }

        private void OpenKeyBindings()
        {
            // Open key bindings screen (would be implemented separately)
            Debug.Log("Open Key Bindings");
        }

        #endregion

        #region Accessibility Settings Handlers

        private void OnUIScaleChanged(float value)
        {
            if (uiManager != null)
            {
                var accessibility = uiManager.GetAccessibilitySettings();
                accessibility.uiScale = value;
                uiManager.SetAccessibilityOptions(accessibility);
                UpdateUIScaleText(value);
                hasUnsavedChanges = true;
            }
        }

        private void OnNavigationSpeedChanged(float value)
        {
            if (uiManager != null)
            {
                var accessibility = uiManager.GetAccessibilitySettings();
                accessibility.navigationSensitivity = value;
                uiManager.SetAccessibilityOptions(accessibility);
                UpdateNavigationSpeedText(value);
                hasUnsavedChanges = true;
            }
        }

        private void OnColorBlindChanged(int index)
        {
            if (uiManager != null)
            {
                UIManager.ColorBlindType colorBlindType = (UIManager.ColorBlindType)index;
                uiManager.ToggleColorBlindSupport(colorBlindType);
                hasUnsavedChanges = true;
            }
        }

        private void UpdateUIScaleText(float value)
        {
            if (uiScaleText != null)
            {
                uiScaleText.text = $"{(value * 100):F0}%";
            }
        }

        private void UpdateNavigationSpeedText(float value)
        {
            if (navigationSpeedText != null)
            {
                navigationSpeedText.text = $"{value:F2}";
            }
        }

        #endregion

        #region Button Handlers

        private void GoBack()
        {
            if (hasUnsavedChanges)
            {
                // Could show confirmation dialog
                Debug.Log("Unsaved changes detected");
            }

            if (uiManager != null)
            {
                uiManager.GoBack();
            }
        }

        private void ApplySettings()
        {
            // Save all settings
            SaveAllSettings();
            hasUnsavedChanges = false;
            
            // Show confirmation or close menu
            Debug.Log("Settings applied");
        }

        private void ResetToDefaults()
        {
            // Reset all settings to default values
            ResetAudioSettings();
            ResetVideoSettings();
            ResetControlSettings();
            ResetAccessibilitySettings();
            
            hasUnsavedChanges = true;
        }

        #endregion

        #region Save/Reset Methods

        private void SaveAllSettings()
        {
            // Save audio settings
            if (audioManager != null)
            {
                audioManager.SaveAudioSettings();
            }

            // Save other settings (video, controls, accessibility)
            // This would typically be saved to PlayerPrefs or a settings file
        }

        private void ResetAudioSettings()
        {
            if (masterVolumeSlider != null) masterVolumeSlider.value = 1f;
            if (musicVolumeSlider != null) musicVolumeSlider.value = 0.8f;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = 1f;
            if (voiceVolumeSlider != null) voiceVolumeSlider.value = 1f;
            if (audioQualityDropdown != null) audioQualityDropdown.value = 2; // High
        }

        private void ResetVideoSettings()
        {
            if (fpsLimitSlider != null) fpsLimitSlider.value = 60f;
            if (brightnessSlider != null) brightnessSlider.value = 1f;
            if (fullscreenToggle != null) fullscreenToggle.isOn = true;
            if (vsyncToggle != null) vsyncToggle.isOn = true;
        }

        private void ResetControlSettings()
        {
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = 1f;
            if (invertYToggle != null) invertYToggle.isOn = false;
            if (controlSchemeDropdown != null) controlSchemeDropdown.value = 0;
        }

        private void ResetAccessibilitySettings()
        {
            if (keyboardNavigationToggle != null) keyboardNavigationToggle.isOn = true;
            if (highContrastToggle != null) highContrastToggle.isOn = false;
            if (reducedMotionToggle != null) reducedMotionToggle.isOn = false;
            if (uiScaleSlider != null) uiScaleSlider.value = 1f;
            if (navigationSpeedSlider != null) navigationSpeedSlider.value = 0.2f;
            if (colorBlindDropdown != null) colorBlindDropdown.value = 0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check if there are unsaved changes
        /// </summary>
        public bool HasUnsavedChanges()
        {
            return hasUnsavedChanges;
        }

        /// <summary>
        /// Force refresh settings from current values
        /// </summary>
        public void RefreshSettings()
        {
            LoadCurrentSettings();
            hasUnsavedChanges = false;
        }

        #endregion
    }
}
