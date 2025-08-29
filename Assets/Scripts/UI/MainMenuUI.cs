using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WhiskerKing.Core;
using WhiskerKing.Audio;

namespace WhiskerKing.UI
{
    /// <summary>
    /// Main Menu UI Controller for Whisker King
    /// Handles main menu navigation, game start, settings access, and quit functionality
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button levelSelectButton;
        [SerializeField] private Button timeTrialsButton;
        [SerializeField] private Button cosmeticsButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Image logoImage;
        [SerializeField] private CanvasGroup menuCanvasGroup;

        [Header("Animation Settings")]
        [SerializeField] private float logoFloatSpeed = 2f;
        [SerializeField] private float logoFloatAmount = 10f;
        [SerializeField] private AnimationCurve buttonAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Component references
        private UIManager uiManager;
        private AudioManager audioManager;

        // Animation state
        private Vector3 logoStartPosition;
        private bool isAnimating = true;

        private void Awake()
        {
            // Get component references
            uiManager = UIManager.Instance;
            audioManager = AudioManager.Instance;

            // Store initial logo position
            if (logoImage != null)
            {
                logoStartPosition = logoImage.transform.localPosition;
            }
        }

        private void Start()
        {
            SetupButtons();
            SetupUI();
            StartMenuMusic();
        }

        private void Update()
        {
            if (isAnimating)
            {
                AnimateLogo();
            }
        }

        private void SetupButtons()
        {
            // Setup button listeners
            if (startGameButton != null)
                startGameButton.onClick.AddListener(StartGame);
            
            if (levelSelectButton != null)
                levelSelectButton.onClick.AddListener(OpenLevelSelect);
            
            if (timeTrialsButton != null)
                timeTrialsButton.onClick.AddListener(OpenTimeTrials);
            
            if (cosmeticsButton != null)
                cosmeticsButton.onClick.AddListener(OpenCosmetics);
            
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);
            
            if (creditsButton != null)
                creditsButton.onClick.AddListener(ShowCredits);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);

            // Animate buttons in on start
            AnimateButtonsIn();
        }

        private void SetupUI()
        {
            // Set version text
            if (versionText != null)
            {
                versionText.text = $"Version {Application.version}";
            }

            // Set title text
            if (titleText != null)
            {
                titleText.text = "Whisker King";
            }
        }

        private void StartMenuMusic()
        {
            if (audioManager != null)
            {
                audioManager.PlayMusic("MainMenuTheme", true);
            }
        }

        private void AnimateLogo()
        {
            if (logoImage == null) return;

            float yOffset = Mathf.Sin(Time.time * logoFloatSpeed) * logoFloatAmount;
            logoImage.transform.localPosition = logoStartPosition + Vector3.up * yOffset;
        }

        private void AnimateButtonsIn()
        {
            if (menuCanvasGroup == null) return;

            StartCoroutine(AnimateCanvasGroupIn());
        }

        private System.Collections.IEnumerator AnimateCanvasGroupIn()
        {
            float duration = 1f;
            float elapsedTime = 0f;
            
            menuCanvasGroup.alpha = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;
                menuCanvasGroup.alpha = buttonAnimationCurve.Evaluate(progress);
                yield return null;
            }

            menuCanvasGroup.alpha = 1f;
        }

        #region Button Handlers

        private void StartGame()
        {
            if (uiManager != null)
            {
                uiManager.StartNewGame();
            }
        }

        private void OpenLevelSelect()
        {
            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.LevelSelect);
            }
        }

        private void OpenTimeTrials()
        {
            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.TimeTrials);
            }
        }

        private void OpenCosmetics()
        {
            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.Cosmetics);
            }
        }

        private void OpenSettings()
        {
            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.SettingsMenu);
            }
        }

        private void ShowCredits()
        {
            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.Credits);
            }
        }

        private void QuitGame()
        {
            if (uiManager != null)
            {
                uiManager.QuitGame();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Enable/disable menu animations
        /// </summary>
        public void SetAnimationEnabled(bool enabled)
        {
            isAnimating = enabled;
            if (!enabled && logoImage != null)
            {
                logoImage.transform.localPosition = logoStartPosition;
            }
        }

        /// <summary>
        /// Set button interactability
        /// </summary>
        public void SetButtonsInteractable(bool interactable)
        {
            if (startGameButton != null) startGameButton.interactable = interactable;
            if (levelSelectButton != null) levelSelectButton.interactable = interactable;
            if (timeTrialsButton != null) timeTrialsButton.interactable = interactable;
            if (cosmeticsButton != null) cosmeticsButton.interactable = interactable;
            if (settingsButton != null) settingsButton.interactable = interactable;
            if (creditsButton != null) creditsButton.interactable = interactable;
            if (quitButton != null) quitButton.interactable = interactable;
        }

        #endregion
    }
}
