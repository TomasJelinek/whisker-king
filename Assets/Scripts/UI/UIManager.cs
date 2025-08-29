using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using WhiskerKing.Core;
using WhiskerKing.Audio;
using WhiskerKing.Level;
using WhiskerKing.Player;

namespace WhiskerKing.UI
{
    /// <summary>
    /// Comprehensive UI Manager for Whisker King
    /// Manages all UI screens, navigation, accessibility, and player progression
    /// Implements responsive design, keyboard navigation, and colorblind support
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public enum UIScreen
        {
            None,
            MainMenu,
            InGameHUD,
            PauseMenu,
            SettingsMenu,
            LevelSelect,
            LevelComplete,
            GameOver,
            SaveLoad,
            Cosmetics,
            TimeTrials,
            Credits
        }

        public enum TransitionType
        {
            Instant,
            Fade,
            SlideLeft,
            SlideRight,
            SlideUp,
            SlideDown,
            Scale,
            Custom
        }

        [System.Serializable]
        public class UIScreenData
        {
            public UIScreen screenType;
            public GameObject screenObject;
            public CanvasGroup canvasGroup;
            public bool isModal = false;
            public bool pausesGame = false;
            public bool requiresEventSystem = true;
            public TransitionType defaultTransition = TransitionType.Fade;
            public float transitionDuration = 0.3f;
            public AudioClip openSound;
            public AudioClip closeSound;
        }

        [System.Serializable]
        public class AccessibilitySettings
        {
            public bool keyboardNavigationEnabled = true;
            public bool colorblindSupport = false;
            public ColorBlindType colorBlindType = ColorBlindType.Normal;
            public float uiScale = 1f;
            public bool highContrastMode = false;
            public bool reducedMotion = false;
            public float navigationSensitivity = 0.2f;
        }

        public enum ColorBlindType
        {
            Normal,
            Protanopia,    // Red-blind
            Deuteranopia,  // Green-blind  
            Tritanopia     // Blue-blind
        }

        [System.Serializable]
        public class ProgressionData
        {
            public int totalFishTreats;
            public int totalYarn;
            public int totalGoldenTokens;
            public float bestLevelTime;
            public int levelsCompleted;
            public int totalPlayTime;
            public Dictionary<string, bool> unlockedCosmetics = new Dictionary<string, bool>();
            public Dictionary<int, MedalType> levelMedals = new Dictionary<int, MedalType>();
        }

        public enum MedalType
        {
            None,
            Bronze,
            Silver,
            Gold
        }

        // Singleton pattern
        private static UIManager instance;
        public static UIManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<UIManager>();
                    if (instance == null)
                    {
                        GameObject uiManagerGO = new GameObject("UIManager");
                        instance = uiManagerGO.AddComponent<UIManager>();
                        DontDestroyOnLoad(uiManagerGO);
                    }
                }
                return instance;
            }
        }

        [Header("UI Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;

        [Header("UI Screens")]
        [SerializeField] private List<UIScreenData> uiScreens = new List<UIScreenData>();

        [Header("Canvas Settings")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;

        [Header("Accessibility")]
        [SerializeField] private AccessibilitySettings accessibility = new AccessibilitySettings();

        [Header("Audio Integration")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip buttonHoverSound;
        [SerializeField] private AudioClip screenTransitionSound;

        [Header("Color Schemes")]
        [SerializeField] private Color normalPrimaryColor = Color.blue;
        [SerializeField] private Color normalSecondaryColor = Color.white;
        [SerializeField] private Color protanopiaColor = Color.yellow;
        [SerializeField] private Color deuteranopiaColor = Color.cyan;
        [SerializeField] private Color tritanopiaColor = Color.magenta;

        // Current UI state
        private UIScreen currentScreen = UIScreen.None;
        private UIScreen previousScreen = UIScreen.None;
        private Stack<UIScreen> screenHistory = new Stack<UIScreen>();
        private Dictionary<UIScreen, UIScreenData> screenLookup = new Dictionary<UIScreen, UIScreenData>();

        // Navigation system
        private EventSystem eventSystem;
        private Selectable currentSelection;
        private List<Selectable> currentSelectables = new List<Selectable>();
        private int currentSelectionIndex = 0;

        // Transition system
        private Coroutine currentTransition;
        private bool isTransitioning = false;

        // Progression tracking
        private ProgressionData playerProgression = new ProgressionData();

        // Configuration cache
        private UIData uiConfig;

        // Component references
        private AudioManager audioManager;
        private LevelManager levelManager;
        private PlayerController playerController;

        // Events
        public System.Action<UIScreen> OnScreenChanged;
        public System.Action<UIScreen, UIScreen> OnScreenTransition;
        public System.Action<AccessibilitySettings> OnAccessibilityChanged;
        public System.Action<ProgressionData> OnProgressionUpdated;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUISystem();
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
            SetupUIComponents();
            LoadProgressionData();
            ApplyAccessibilitySettings();
            
            // Start with main menu
            ShowScreen(UIScreen.MainMenu, TransitionType.Instant);
        }

        private void Update()
        {
            HandleKeyboardNavigation();
            UpdateAccessibility();

            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        #endregion

        #region Initialization

        private void InitializeUISystem()
        {
            // Get required components
            eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
                eventSystem = eventSystemGO.GetComponent<EventSystem>();
            }

            // Initialize screen lookup
            foreach (var screenData in uiScreens)
            {
                screenLookup[screenData.screenType] = screenData;
                
                // Ensure all screens start inactive
                if (screenData.screenObject != null)
                {
                    screenData.screenObject.SetActive(false);
                }
            }

            Debug.Log("UIManager initialized with " + uiScreens.Count + " screens");
        }

        private void LoadConfiguration()
        {
            if (useGameConfiguration && GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                uiConfig = GameConfiguration.Instance.Config.ui;
                ApplyConfiguration();
                Debug.Log("UIManager: Configuration loaded from GameConfig");
            }
            else
            {
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            if (uiConfig == null) return;

            // Apply UI scale settings
            if (canvasScaler != null)
            {
                canvasScaler.referenceResolution = new Vector2(uiConfig.referenceWidth, uiConfig.referenceHeight);
                canvasScaler.matchWidthOrHeight = uiConfig.matchWidthOrHeight;
            }

            // Apply accessibility settings
            accessibility.keyboardNavigationEnabled = uiConfig.accessibility.keyboardNavigation;
            accessibility.colorblindSupport = uiConfig.accessibility.colorblindSupport;
            accessibility.uiScale = uiConfig.accessibility.uiScale;
            accessibility.highContrastMode = uiConfig.accessibility.highContrastMode;
            accessibility.reducedMotion = uiConfig.accessibility.reducedMotion;
        }

        private void UseDefaultConfiguration()
        {
            // Use standard UI configuration
            if (canvasScaler != null)
            {
                canvasScaler.referenceResolution = new Vector2(1920, 1080);
                canvasScaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void SetupUIComponents()
        {
            // Get component references
            audioManager = AudioManager.Instance;
            levelManager = FindObjectOfType<LevelManager>();
            playerController = FindObjectOfType<PlayerController>();

            // Setup canvas if not assigned
            if (mainCanvas == null)
            {
                mainCanvas = GetComponentInChildren<Canvas>();
            }

            if (canvasScaler == null)
            {
                canvasScaler = GetComponentInChildren<CanvasScaler>();
            }

            // Setup button audio for all buttons
            SetupButtonAudio();
        }

        private void SetupButtonAudio()
        {
            Button[] allButtons = FindObjectsOfType<Button>(true);
            foreach (var button in allButtons)
            {
                // Add audio component if not present
                var buttonAudio = button.GetComponent<UIButtonAudio>();
                if (buttonAudio == null)
                {
                    buttonAudio = button.gameObject.AddComponent<UIButtonAudio>();
                }
                
                buttonAudio.Initialize(this);
            }
        }

        #endregion

        #region Screen Management

        /// <summary>
        /// Show a UI screen with optional transition
        /// </summary>
        public void ShowScreen(UIScreen screen, TransitionType transition = TransitionType.Fade)
        {
            if (isTransitioning)
            {
                Debug.LogWarning("Cannot show screen while transitioning");
                return;
            }

            var screenData = GetScreenData(screen);
            if (screenData == null)
            {
                Debug.LogError($"Screen data not found for {screen}");
                return;
            }

            // Store previous screen
            if (currentScreen != UIScreen.None)
            {
                screenHistory.Push(currentScreen);
                previousScreen = currentScreen;
            }

            // Handle game pausing
            if (screenData.pausesGame)
            {
                Time.timeScale = 0f;
            }
            else if (currentScreen != UIScreen.None)
            {
                var currentScreenData = GetScreenData(currentScreen);
                if (currentScreenData != null && currentScreenData.pausesGame && !screenData.pausesGame)
                {
                    Time.timeScale = 1f;
                }
            }

            // Start transition
            if (currentTransition != null)
            {
                StopCoroutine(currentTransition);
            }

            currentTransition = StartCoroutine(TransitionToScreen(screen, transition));
        }

        /// <summary>
        /// Go back to previous screen
        /// </summary>
        public void GoBack()
        {
            if (screenHistory.Count > 0)
            {
                UIScreen previousScreen = screenHistory.Pop();
                ShowScreen(previousScreen);
            }
        }

        /// <summary>
        /// Close current screen
        /// </summary>
        public void CloseCurrentScreen()
        {
            if (currentScreen != UIScreen.None)
            {
                var screenData = GetScreenData(currentScreen);
                if (screenData?.screenObject != null)
                {
                    screenData.screenObject.SetActive(false);
                }
                
                // Unpause game if necessary
                if (screenData != null && screenData.pausesGame)
                {
                    Time.timeScale = 1f;
                }
                
                currentScreen = UIScreen.None;
                OnScreenChanged?.Invoke(UIScreen.None);
            }
        }

        private IEnumerator TransitionToScreen(UIScreen targetScreen, TransitionType transition)
        {
            isTransitioning = true;
            
            var targetScreenData = GetScreenData(targetScreen);
            var currentScreenData = currentScreen != UIScreen.None ? GetScreenData(currentScreen) : null;

            // Play transition sound
            PlayUISound(screenTransitionSound);

            // Hide current screen
            if (currentScreenData != null)
            {
                yield return StartCoroutine(HideScreen(currentScreenData, transition));
                PlayUISound(currentScreenData.closeSound);
            }

            // Show target screen
            if (targetScreenData != null)
            {
                yield return StartCoroutine(ShowScreen(targetScreenData, transition));
                PlayUISound(targetScreenData.openSound);
                
                // Set up navigation
                SetupScreenNavigation(targetScreen);
            }

            // Update current screen
            UIScreen oldScreen = currentScreen;
            currentScreen = targetScreen;

            // Trigger events
            OnScreenTransition?.Invoke(oldScreen, targetScreen);
            OnScreenChanged?.Invoke(targetScreen);

            isTransitioning = false;

            if (debugMode)
            {
                Debug.Log($"Transitioned from {oldScreen} to {targetScreen}");
            }
        }

        private IEnumerator ShowScreen(UIScreenData screenData, TransitionType transition)
        {
            if (screenData.screenObject == null) yield break;

            screenData.screenObject.SetActive(true);

            float duration = screenData.transitionDuration;
            
            switch (transition)
            {
                case TransitionType.Instant:
                    screenData.canvasGroup.alpha = 1f;
                    break;
                    
                case TransitionType.Fade:
                    yield return StartCoroutine(FadeIn(screenData.canvasGroup, duration));
                    break;
                    
                case TransitionType.SlideUp:
                    yield return StartCoroutine(SlideIn(screenData.screenObject.transform, Vector3.up, duration));
                    break;
                    
                case TransitionType.Scale:
                    yield return StartCoroutine(ScaleIn(screenData.screenObject.transform, duration));
                    break;
                    
                default:
                    yield return StartCoroutine(FadeIn(screenData.canvasGroup, duration));
                    break;
            }
        }

        private IEnumerator HideScreen(UIScreenData screenData, TransitionType transition)
        {
            if (screenData.screenObject == null) yield break;

            float duration = screenData.transitionDuration;
            
            switch (transition)
            {
                case TransitionType.Instant:
                    screenData.canvasGroup.alpha = 0f;
                    screenData.screenObject.SetActive(false);
                    break;
                    
                case TransitionType.Fade:
                    yield return StartCoroutine(FadeOut(screenData.canvasGroup, duration));
                    screenData.screenObject.SetActive(false);
                    break;
                    
                case TransitionType.SlideDown:
                    yield return StartCoroutine(SlideOut(screenData.screenObject.transform, Vector3.down, duration));
                    screenData.screenObject.SetActive(false);
                    break;
                    
                case TransitionType.Scale:
                    yield return StartCoroutine(ScaleOut(screenData.screenObject.transform, duration));
                    screenData.screenObject.SetActive(false);
                    break;
                    
                default:
                    yield return StartCoroutine(FadeOut(screenData.canvasGroup, duration));
                    screenData.screenObject.SetActive(false);
                    break;
            }
        }

        #endregion

        #region Transition Animations

        private IEnumerator FadeIn(CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, progress);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOut(CanvasGroup canvasGroup, float duration)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        private IEnumerator SlideIn(Transform transform, Vector3 direction, float duration)
        {
            Vector3 startPosition = transform.localPosition - direction * 1000f;
            Vector3 endPosition = transform.localPosition;
            transform.localPosition = startPosition;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                transform.localPosition = Vector3.Lerp(startPosition, endPosition, EaseOutCubic(progress));
                yield return null;
            }

            transform.localPosition = endPosition;
        }

        private IEnumerator SlideOut(Transform transform, Vector3 direction, float duration)
        {
            Vector3 startPosition = transform.localPosition;
            Vector3 endPosition = startPosition + direction * 1000f;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                transform.localPosition = Vector3.Lerp(startPosition, endPosition, EaseInCubic(progress));
                yield return null;
            }

            transform.localPosition = endPosition;
        }

        private IEnumerator ScaleIn(Transform transform, float duration)
        {
            Vector3 startScale = Vector3.zero;
            Vector3 endScale = Vector3.one;
            transform.localScale = startScale;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                transform.localScale = Vector3.Lerp(startScale, endScale, EaseOutBack(progress));
                yield return null;
            }

            transform.localScale = endScale;
        }

        private IEnumerator ScaleOut(Transform transform, float duration)
        {
            Vector3 startScale = transform.localScale;
            Vector3 endScale = Vector3.zero;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = elapsedTime / duration;
                transform.localScale = Vector3.Lerp(startScale, endScale, EaseInBack(progress));
                yield return null;
            }

            transform.localScale = endScale;
        }

        #endregion

        #region Easing Functions

        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        #endregion

        #region Keyboard Navigation

        private void HandleKeyboardNavigation()
        {
            if (!accessibility.keyboardNavigationEnabled) return;

            // Handle navigation input
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                NavigateNext();
            }
            else if (Input.GetKeyDown(KeyCode.Tab) && Input.GetKey(KeyCode.LeftShift))
            {
                NavigatePrevious();
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateCurrentSelection();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeInput();
            }

            // Handle directional navigation
            HandleDirectionalNavigation();
        }

        private void HandleDirectionalNavigation()
        {
            Vector2 navigationInput = Vector2.zero;
            
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                navigationInput.y = 1f;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                navigationInput.y = -1f;
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                navigationInput.x = -1f;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                navigationInput.x = 1f;

            if (navigationInput.magnitude > accessibility.navigationSensitivity)
            {
                NavigateInDirection(navigationInput);
            }
        }

        private void NavigateNext()
        {
            if (currentSelectables.Count == 0) return;

            currentSelectionIndex = (currentSelectionIndex + 1) % currentSelectables.Count;
            SetCurrentSelection(currentSelectables[currentSelectionIndex]);
        }

        private void NavigatePrevious()
        {
            if (currentSelectables.Count == 0) return;

            currentSelectionIndex = (currentSelectionIndex - 1 + currentSelectables.Count) % currentSelectables.Count;
            SetCurrentSelection(currentSelectables[currentSelectionIndex]);
        }

        private void NavigateInDirection(Vector2 direction)
        {
            if (currentSelection != null)
            {
                Selectable next = currentSelection.FindSelectable(direction);
                if (next != null)
                {
                    SetCurrentSelection(next);
                }
            }
        }

        private void ActivateCurrentSelection()
        {
            if (currentSelection != null)
            {
                PlayUISound(buttonClickSound);
                
                if (currentSelection is Button button)
                {
                    button.onClick.Invoke();
                }
                else if (currentSelection is Toggle toggle)
                {
                    toggle.isOn = !toggle.isOn;
                }
                else if (currentSelection is Slider slider)
                {
                    // Could implement slider keyboard control here
                }
            }
        }

        private void HandleEscapeInput()
        {
            switch (currentScreen)
            {
                case UIScreen.MainMenu:
                    QuitGame();
                    break;
                case UIScreen.InGameHUD:
                    ShowScreen(UIScreen.PauseMenu);
                    break;
                case UIScreen.PauseMenu:
                    ResumeGame();
                    break;
                default:
                    GoBack();
                    break;
            }
        }

        private void SetupScreenNavigation(UIScreen screen)
        {
            var screenData = GetScreenData(screen);
            if (screenData?.screenObject == null) return;

            // Find all selectable elements in the screen
            currentSelectables.Clear();
            var selectables = screenData.screenObject.GetComponentsInChildren<Selectable>();
            
            foreach (var selectable in selectables)
            {
                if (selectable.interactable && selectable.gameObject.activeInHierarchy)
                {
                    currentSelectables.Add(selectable);
                }
            }

            // Set first selectable as current
            if (currentSelectables.Count > 0)
            {
                currentSelectionIndex = 0;
                SetCurrentSelection(currentSelectables[0]);
            }
        }

        private void SetCurrentSelection(Selectable selectable)
        {
            currentSelection = selectable;
            eventSystem.SetSelectedGameObject(selectable.gameObject);
            
            // Update selection index
            currentSelectionIndex = currentSelectables.IndexOf(selectable);
        }

        #endregion

        #region Accessibility

        private void UpdateAccessibility()
        {
            // Update UI scale
            if (mainCanvas != null)
            {
                mainCanvas.transform.localScale = Vector3.one * accessibility.uiScale;
            }
        }

        private void ApplyAccessibilitySettings()
        {
            ApplyColorBlindSupport();
            ApplyHighContrastMode();
            UpdateUIScale();
        }

        private void ApplyColorBlindSupport()
        {
            if (!accessibility.colorblindSupport) return;

            Color primaryColor = accessibility.colorBlindType switch
            {
                ColorBlindType.Protanopia => protanopiaColor,
                ColorBlindType.Deuteranopia => deuteranopiaColor,
                ColorBlindType.Tritanopia => tritanopiaColor,
                _ => normalPrimaryColor
            };

            // Apply color scheme to UI elements
            ApplyColorScheme(primaryColor);
        }

        private void ApplyColorScheme(Color primaryColor)
        {
            // Apply to all buttons and UI elements
            Button[] buttons = FindObjectsOfType<Button>(true);
            foreach (var button in buttons)
            {
                var colors = button.colors;
                colors.normalColor = primaryColor;
                colors.highlightedColor = Color.Lerp(primaryColor, Color.white, 0.2f);
                button.colors = colors;
            }

            // Apply to other UI elements like sliders, toggles, etc.
        }

        private void ApplyHighContrastMode()
        {
            if (!accessibility.highContrastMode) return;

            // Increase contrast for all text elements
            TextMeshProUGUI[] texts = FindObjectsOfType<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                text.fontStyle = FontStyles.Bold;
                text.color = text.color.grayscale > 0.5f ? Color.white : Color.black;
            }
        }

        private void UpdateUIScale()
        {
            if (canvasScaler != null)
            {
                canvasScaler.scaleFactor = accessibility.uiScale;
            }
        }

        /// <summary>
        /// Set accessibility options
        /// </summary>
        public void SetAccessibilityOptions(AccessibilitySettings settings)
        {
            accessibility = settings;
            ApplyAccessibilitySettings();
            OnAccessibilityChanged?.Invoke(accessibility);
        }

        /// <summary>
        /// Toggle colorblind support
        /// </summary>
        public void ToggleColorBlindSupport(ColorBlindType type)
        {
            accessibility.colorblindSupport = true;
            accessibility.colorBlindType = type;
            ApplyColorBlindSupport();
        }

        #endregion

        #region Progression System

        private void LoadProgressionData()
        {
            // Load from PlayerPrefs
            playerProgression.totalFishTreats = PlayerPrefs.GetInt("Progress_FishTreats", 0);
            playerProgression.totalYarn = PlayerPrefs.GetInt("Progress_Yarn", 0);
            playerProgression.totalGoldenTokens = PlayerPrefs.GetInt("Progress_Tokens", 0);
            playerProgression.bestLevelTime = PlayerPrefs.GetFloat("Progress_BestTime", 0f);
            playerProgression.levelsCompleted = PlayerPrefs.GetInt("Progress_Levels", 0);
            playerProgression.totalPlayTime = PlayerPrefs.GetInt("Progress_PlayTime", 0);

            // Load cosmetics and medals (would need more complex serialization)
            LoadCosmeticData();
            LoadMedalData();

            OnProgressionUpdated?.Invoke(playerProgression);
        }

        private void SaveProgressionData()
        {
            PlayerPrefs.SetInt("Progress_FishTreats", playerProgression.totalFishTreats);
            PlayerPrefs.SetInt("Progress_Yarn", playerProgression.totalYarn);
            PlayerPrefs.SetInt("Progress_Tokens", playerProgression.totalGoldenTokens);
            PlayerPrefs.SetFloat("Progress_BestTime", playerProgression.bestLevelTime);
            PlayerPrefs.SetInt("Progress_Levels", playerProgression.levelsCompleted);
            PlayerPrefs.SetInt("Progress_PlayTime", playerProgression.totalPlayTime);

            SaveCosmeticData();
            SaveMedalData();
            
            PlayerPrefs.Save();
        }

        private void LoadCosmeticData()
        {
            // Placeholder - would load unlocked cosmetics from PlayerPrefs
            playerProgression.unlockedCosmetics["default_hat"] = true;
        }

        private void SaveCosmeticData()
        {
            // Placeholder - would save unlocked cosmetics to PlayerPrefs
        }

        private void LoadMedalData()
        {
            // Placeholder - would load level medals from PlayerPrefs
            for (int i = 1; i <= 10; i++)
            {
                string medalKey = $"Medal_Level_{i}";
                int medalValue = PlayerPrefs.GetInt(medalKey, 0);
                playerProgression.levelMedals[i] = (MedalType)medalValue;
            }
        }

        private void SaveMedalData()
        {
            foreach (var medal in playerProgression.levelMedals)
            {
                string medalKey = $"Medal_Level_{medal.Key}";
                PlayerPrefs.SetInt(medalKey, (int)medal.Value);
            }
        }

        /// <summary>
        /// Update player progression
        /// </summary>
        public void UpdateProgression(int fishTreats = 0, int yarn = 0, int tokens = 0)
        {
            playerProgression.totalFishTreats += fishTreats;
            playerProgression.totalYarn += yarn;
            playerProgression.totalGoldenTokens += tokens;

            SaveProgressionData();
            OnProgressionUpdated?.Invoke(playerProgression);
        }

        /// <summary>
        /// Award medal for level completion
        /// </summary>
        public void AwardMedal(int levelNumber, MedalType medal)
        {
            if (!playerProgression.levelMedals.ContainsKey(levelNumber) || 
                playerProgression.levelMedals[levelNumber] < medal)
            {
                playerProgression.levelMedals[levelNumber] = medal;
                SaveProgressionData();
                OnProgressionUpdated?.Invoke(playerProgression);
            }
        }

        /// <summary>
        /// Unlock cosmetic item
        /// </summary>
        public bool UnlockCosmetic(string cosmeticId, int yarnCost)
        {
            if (playerProgression.totalYarn >= yarnCost && 
                !playerProgression.unlockedCosmetics.ContainsKey(cosmeticId))
            {
                playerProgression.totalYarn -= yarnCost;
                playerProgression.unlockedCosmetics[cosmeticId] = true;
                SaveProgressionData();
                OnProgressionUpdated?.Invoke(playerProgression);
                return true;
            }
            return false;
        }

        #endregion

        #region Audio Integration

        /// <summary>
        /// Play UI sound effect
        /// </summary>
        public void PlayUISound(AudioClip clip)
        {
            if (clip != null && audioManager != null)
            {
                audioManager.PlayOneShot(clip, AudioManager.AudioCategory.SFX_UI);
            }
        }

        /// <summary>
        /// Play button click sound
        /// </summary>
        public void PlayButtonClick()
        {
            PlayUISound(buttonClickSound);
        }

        /// <summary>
        /// Play button hover sound
        /// </summary>
        public void PlayButtonHover()
        {
            PlayUISound(buttonHoverSound);
        }

        #endregion

        #region Game Control Methods

        /// <summary>
        /// Start new game
        /// </summary>
        public void StartNewGame()
        {
            if (levelManager != null)
            {
                levelManager.RestartLevel();
            }
            ShowScreen(UIScreen.InGameHUD);
        }

        /// <summary>
        /// Resume game from pause
        /// </summary>
        public void ResumeGame()
        {
            ShowScreen(UIScreen.InGameHUD);
        }

        /// <summary>
        /// Pause current game
        /// </summary>
        public void PauseGame()
        {
            ShowScreen(UIScreen.PauseMenu);
        }

        /// <summary>
        /// Quit to main menu
        /// </summary>
        public void QuitToMainMenu()
        {
            Time.timeScale = 1f;
            ShowScreen(UIScreen.MainMenu);
        }

        /// <summary>
        /// Quit game application
        /// </summary>
        public void QuitGame()
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }

        #endregion

        #region Utility Methods

        private UIScreenData GetScreenData(UIScreen screen)
        {
            return screenLookup.TryGetValue(screen, out UIScreenData data) ? data : null;
        }

        /// <summary>
        /// Get current UI screen
        /// </summary>
        public UIScreen GetCurrentScreen()
        {
            return currentScreen;
        }

        /// <summary>
        /// Check if UI is currently transitioning
        /// </summary>
        public bool IsTransitioning()
        {
            return isTransitioning;
        }

        /// <summary>
        /// Get current player progression
        /// </summary>
        public ProgressionData GetProgressionData()
        {
            return playerProgression;
        }

        /// <summary>
        /// Get accessibility settings
        /// </summary>
        public AccessibilitySettings GetAccessibilitySettings()
        {
            return accessibility;
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            // Update debug information
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(890, 10, 300, 300));
                
                GUILayout.Label("=== UI MANAGER DEBUG ===");
                GUILayout.Label($"Current Screen: {currentScreen}");
                GUILayout.Label($"Previous Screen: {previousScreen}");
                GUILayout.Label($"Transitioning: {isTransitioning}");
                GUILayout.Label($"Screen History: {screenHistory.Count}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== NAVIGATION ===");
                GUILayout.Label($"Current Selection: {(currentSelection?.name ?? "None")}");
                GUILayout.Label($"Selectables: {currentSelectables.Count}");
                GUILayout.Label($"Selection Index: {currentSelectionIndex}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== ACCESSIBILITY ===");
                GUILayout.Label($"Keyboard Nav: {accessibility.keyboardNavigationEnabled}");
                GUILayout.Label($"Colorblind: {accessibility.colorblindSupport}");
                GUILayout.Label($"UI Scale: {accessibility.uiScale:F2}");
                GUILayout.Label($"High Contrast: {accessibility.highContrastMode}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== PROGRESSION ===");
                GUILayout.Label($"Fish Treats: {playerProgression.totalFishTreats}");
                GUILayout.Label($"Yarn: {playerProgression.totalYarn}");
                GUILayout.Label($"Tokens: {playerProgression.totalGoldenTokens}");
                GUILayout.Label($"Levels: {playerProgression.levelsCompleted}");
                
                if (GUILayout.Button("Show Main Menu"))
                {
                    ShowScreen(UIScreen.MainMenu);
                }
                
                if (GUILayout.Button("Show Settings"))
                {
                    ShowScreen(UIScreen.SettingsMenu);
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion

        #region Helper Components

        /// <summary>
        /// UI Button Audio Component
        /// </summary>
        public class UIButtonAudio : MonoBehaviour
        {
            private UIManager uiManager;
            private Button button;

            public void Initialize(UIManager manager)
            {
                uiManager = manager;
                button = GetComponent<Button>();
                
                if (button != null)
                {
                    button.onClick.AddListener(OnClick);
                }

                // Add hover listeners
                var trigger = gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = gameObject.AddComponent<EventTrigger>();
                }

                var entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => { OnHover(); });
                trigger.triggers.Add(entry);
            }

            private void OnClick()
            {
                uiManager?.PlayButtonClick();
            }

            private void OnHover()
            {
                uiManager?.PlayButtonHover();
            }
        }

        #endregion
    }
}
