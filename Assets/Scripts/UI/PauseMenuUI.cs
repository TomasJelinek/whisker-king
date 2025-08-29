using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WhiskerKing.Core;
using WhiskerKing.Audio;
using WhiskerKing.Level;

namespace WhiskerKing.UI
{
    /// <summary>
    /// Pause Menu UI Controller for Whisker King
    /// Handles game pause functionality, settings access, and level navigation
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Pause Menu Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartLevelButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button levelSelectButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI pauseTitle;
        [SerializeField] private TextMeshProUGUI levelNameText;
        [SerializeField] private TextMeshProUGUI currentTimeText;
        [SerializeField] private TextMeshProUGUI collectiblesText;
        [SerializeField] private CanvasGroup menuCanvasGroup;

        [Header("Progress Display")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image medalPreview;

        [Header("Audio Settings")]
        [SerializeField] private AudioClip pauseSound;
        [SerializeField] private AudioClip resumeSound;

        // Component references
        private UIManager uiManager;
        private AudioManager audioManager;
        private LevelManager levelManager;

        // Pause state
        private bool isPaused = false;
        private float timeWhenPaused = 0f;

        private void Awake()
        {
            // Get component references
            uiManager = UIManager.Instance;
            audioManager = AudioManager.Instance;
            levelManager = FindObjectOfType<LevelManager>();
        }

        private void Start()
        {
            SetupButtons();
            UpdatePauseMenuInfo();
        }

        private void OnEnable()
        {
            OnPauseMenuOpened();
        }

        private void OnDisable()
        {
            OnPauseMenuClosed();
        }

        private void SetupButtons()
        {
            // Setup button listeners
            if (resumeButton != null)
                resumeButton.onClick.AddListener(ResumeGame);
            
            if (restartLevelButton != null)
                restartLevelButton.onClick.AddListener(RestartLevel);
            
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);
            
            if (levelSelectButton != null)
                levelSelectButton.onClick.AddListener(OpenLevelSelect);
            
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);

            // Set resume button as default selection
            if (resumeButton != null)
            {
                resumeButton.Select();
            }
        }

        private void UpdatePauseMenuInfo()
        {
            if (levelManager == null) return;

            var currentLevel = levelManager.GetCurrentLevel();
            var progress = levelManager.GetProgress();

            // Update level name
            if (levelNameText != null)
            {
                levelNameText.text = currentLevel.levelName ?? "Current Level";
            }

            // Update current time
            if (currentTimeText != null)
            {
                currentTimeText.text = $"Time: {FormatTime(progress.elapsedTime)}";
                timeWhenPaused = progress.elapsedTime;
            }

            // Update collectibles info
            if (collectiblesText != null)
            {
                collectiblesText.text = $"Fish Treats: {progress.collectedFishTreats}\n" +
                                       $"Yarn: {progress.collectedYarn}\n" +
                                       $"Tokens: {progress.collectedTokens}";
            }

            // Update progress bar
            if (progressBar != null)
            {
                progressBar.value = progress.completionPercentage / 100f;
            }

            if (progressText != null)
            {
                progressText.text = $"Progress: {progress.completionPercentage:F0}%";
            }

            // Update medal preview based on current time
            UpdateMedalPreview(progress.elapsedTime);
        }

        private void UpdateMedalPreview(float currentTime)
        {
            if (medalPreview == null || levelManager == null) return;

            var currentLevel = levelManager.GetCurrentLevel();
            if (currentLevel.targetTime <= 0) return;

            // Calculate medal thresholds (example values)
            float goldThreshold = currentLevel.targetTime * 0.8f;   // 80% of target time
            float silverThreshold = currentLevel.targetTime * 1.0f; // Target time
            float bronzeThreshold = currentLevel.targetTime * 1.2f; // 120% of target time

            UIManager.MedalType currentMedal = UIManager.MedalType.None;

            if (currentTime <= goldThreshold)
                currentMedal = UIManager.MedalType.Gold;
            else if (currentTime <= silverThreshold)
                currentMedal = UIManager.MedalType.Silver;
            else if (currentTime <= bronzeThreshold)
                currentMedal = UIManager.MedalType.Bronze;

            // Update medal display color/sprite based on type
            switch (currentMedal)
            {
                case UIManager.MedalType.Gold:
                    medalPreview.color = Color.yellow;
                    break;
                case UIManager.MedalType.Silver:
                    medalPreview.color = Color.gray;
                    break;
                case UIManager.MedalType.Bronze:
                    medalPreview.color = new Color(0.8f, 0.5f, 0.2f); // Bronze color
                    break;
                default:
                    medalPreview.color = Color.clear;
                    break;
            }
        }

        private void OnPauseMenuOpened()
        {
            isPaused = true;
            
            // Play pause sound
            if (audioManager != null && pauseSound != null)
            {
                audioManager.PlayOneShot(pauseSound, AudioManager.AudioCategory.SFX_UI);
            }

            // Pause background music
            if (audioManager != null)
            {
                audioManager.PauseMusic();
            }

            // Ensure time is paused
            Time.timeScale = 0f;

            // Update pause menu information
            UpdatePauseMenuInfo();
        }

        private void OnPauseMenuClosed()
        {
            isPaused = false;
            
            // Play resume sound
            if (audioManager != null && resumeSound != null)
            {
                audioManager.PlayOneShot(resumeSound, AudioManager.AudioCategory.SFX_UI);
            }

            // Resume background music
            if (audioManager != null)
            {
                audioManager.ResumeMusic();
            }

            // Resume time
            Time.timeScale = 1f;
        }

        #region Button Handlers

        private void ResumeGame()
        {
            if (uiManager != null)
            {
                uiManager.ResumeGame();
            }
        }

        private void RestartLevel()
        {
            if (levelManager != null)
            {
                levelManager.RestartLevel();
            }

            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.InGameHUD, UIManager.TransitionType.Instant);
            }
        }

        private void OpenSettings()
        {
            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.SettingsMenu);
            }
        }

        private void OpenLevelSelect()
        {
            if (uiManager != null)
            {
                uiManager.ShowScreen(UIManager.UIScreen.LevelSelect);
            }
        }

        private void GoToMainMenu()
        {
            // Unpause time before going to main menu
            Time.timeScale = 1f;
            
            if (uiManager != null)
            {
                uiManager.QuitToMainMenu();
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

        #region Utility Methods

        private string FormatTime(float timeInSeconds)
        {
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggle pause state
        /// </summary>
        public void TogglePause()
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                if (uiManager != null)
                {
                    uiManager.PauseGame();
                }
            }
        }

        /// <summary>
        /// Check if game is currently paused
        /// </summary>
        public bool IsPaused()
        {
            return isPaused;
        }

        /// <summary>
        /// Force refresh pause menu information
        /// </summary>
        public void RefreshPauseMenu()
        {
            UpdatePauseMenuInfo();
        }

        /// <summary>
        /// Set button interactability
        /// </summary>
        public void SetButtonsInteractable(bool interactable)
        {
            if (resumeButton != null) resumeButton.interactable = interactable;
            if (restartLevelButton != null) restartLevelButton.interactable = interactable;
            if (settingsButton != null) settingsButton.interactable = interactable;
            if (levelSelectButton != null) levelSelectButton.interactable = interactable;
            if (mainMenuButton != null) mainMenuButton.interactable = interactable;
            if (quitButton != null) quitButton.interactable = interactable;
        }

        /// <summary>
        /// Get time when game was paused
        /// </summary>
        public float GetPauseTime()
        {
            return timeWhenPaused;
        }

        #endregion
    }
}
