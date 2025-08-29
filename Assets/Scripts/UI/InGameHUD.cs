using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using WhiskerKing.Core;
using WhiskerKing.Level;
using WhiskerKing.Player;

namespace WhiskerKing.UI
{
    /// <summary>
    /// In-Game HUD Controller for Whisker King
    /// Displays collectible counters, time, progress, and player status
    /// Updates in real-time during gameplay
    /// </summary>
    public class InGameHUD : MonoBehaviour
    {
        [Header("Collectible Display")]
        [SerializeField] private TextMeshProUGUI fishTreatsText;
        [SerializeField] private TextMeshProUGUI yarnText;
        [SerializeField] private TextMeshProUGUI tokensText;
        [SerializeField] private Image fishTreatsIcon;
        [SerializeField] private Image yarnIcon;
        [SerializeField] private Image tokensIcon;

        [Header("Timer Display")]
        [SerializeField] private TextMeshProUGUI currentTimeText;
        [SerializeField] private TextMeshProUGUI bestTimeText;
        [SerializeField] private Image timerBackground;

        [Header("Progress Display")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI sectionText;

        [Header("Health/Status Display")]
        [SerializeField] private Slider healthBar;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Image[] lifeIcons;

        [Header("Medal Preview")]
        [SerializeField] private Image medalIcon;
        [SerializeField] private Sprite bronzeMedal;
        [SerializeField] private Sprite silverMedal;
        [SerializeField] private Sprite goldMedal;

        [Header("Animation Settings")]
        [SerializeField] private float collectibleAnimationDuration = 0.5f;
        [SerializeField] private AnimationCurve collectibleAnimationCurve = AnimationCurve.EaseOutBack(0, 0, 1, 1);
        [SerializeField] private Color collectibleHighlightColor = Color.yellow;

        [Header("Warning Settings")]
        [SerializeField] private float lowTimeWarning = 30f;
        [SerializeField] private Color warningColor = Color.red;
        [SerializeField] private float warningFlashSpeed = 2f;

        // Component references
        private UIManager uiManager;
        private LevelManager levelManager;
        private PlayerController playerController;

        // Current values
        private int currentFishTreats = 0;
        private int currentYarn = 0;
        private int currentTokens = 0;
        private float currentTime = 0f;
        private float bestTime = 0f;
        private float currentProgress = 0f;

        // Animation state
        private Coroutine collectibleAnimation;
        private bool isWarningFlashing = false;

        // Medal thresholds (would be loaded from configuration)
        private float goldTimeThreshold = 120f;   // 2 minutes for gold
        private float silverTimeThreshold = 180f; // 3 minutes for silver
        private float bronzeTimeThreshold = 300f; // 5 minutes for bronze

        private void Awake()
        {
            // Get component references
            uiManager = UIManager.Instance;
            levelManager = FindObjectOfType<LevelManager>();
            playerController = FindObjectOfType<PlayerController>();
        }

        private void Start()
        {
            InitializeHUD();
            SubscribeToEvents();
        }

        private void Update()
        {
            UpdateTimeDisplay();
            UpdateProgressDisplay();
            UpdateMedalPreview();
            UpdateWarningStates();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void InitializeHUD()
        {
            // Initialize all displays
            UpdateCollectibleDisplay();
            UpdateProgressDisplay();
            UpdateHealthDisplay();
            UpdateMedalPreview();

            // Load best time for current level
            if (levelManager != null)
            {
                var levelData = levelManager.GetCurrentLevel();
                bestTime = levelData.bestTime;
                UpdateBestTimeDisplay();
            }
        }

        private void SubscribeToEvents()
        {
            // Subscribe to level manager events
            if (levelManager != null)
            {
                levelManager.OnProgressUpdated += OnProgressUpdated;
                levelManager.OnLevelCompleted += OnLevelCompleted;
            }

            // Subscribe to UI manager events for progression updates
            if (uiManager != null)
            {
                uiManager.OnProgressionUpdated += OnProgressionUpdated;
            }
        }

        private void UnsubscribeFromEvents()
        {
            // Unsubscribe from events
            if (levelManager != null)
            {
                levelManager.OnProgressUpdated -= OnProgressUpdated;
                levelManager.OnLevelCompleted -= OnLevelCompleted;
            }

            if (uiManager != null)
            {
                uiManager.OnProgressionUpdated -= OnProgressionUpdated;
            }
        }

        #region Display Updates

        private void UpdateCollectibleDisplay()
        {
            // Update Fish Treats
            if (fishTreatsText != null)
            {
                fishTreatsText.text = currentFishTreats.ToString();
            }

            // Update Yarn
            if (yarnText != null)
            {
                yarnText.text = currentYarn.ToString();
            }

            // Update Tokens
            if (tokensText != null)
            {
                tokensText.text = currentTokens.ToString();
            }
        }

        private void UpdateTimeDisplay()
        {
            if (levelManager == null) return;

            var progress = levelManager.GetProgress();
            currentTime = progress.elapsedTime;

            // Update current time display
            if (currentTimeText != null)
            {
                currentTimeText.text = FormatTime(currentTime);
            }

            // Check for low time warning
            if (levelManager.GetCurrentLevel().targetTime > 0)
            {
                float remainingTime = levelManager.GetCurrentLevel().targetTime - currentTime;
                if (remainingTime <= lowTimeWarning && remainingTime > 0 && !isWarningFlashing)
                {
                    StartTimeWarning();
                }
                else if (remainingTime > lowTimeWarning && isWarningFlashing)
                {
                    StopTimeWarning();
                }
            }
        }

        private void UpdateBestTimeDisplay()
        {
            if (bestTimeText != null)
            {
                if (bestTime > 0)
                {
                    bestTimeText.text = $"Best: {FormatTime(bestTime)}";
                }
                else
                {
                    bestTimeText.text = "Best: --:--";
                }
            }
        }

        private void UpdateProgressDisplay()
        {
            if (levelManager == null) return;

            var progress = levelManager.GetProgress();
            currentProgress = progress.completionPercentage;

            // Update progress bar
            if (progressBar != null)
            {
                progressBar.value = currentProgress / 100f;
            }

            // Update progress text
            if (progressText != null)
            {
                progressText.text = $"{currentProgress:F0}%";
            }

            // Update section text
            if (sectionText != null)
            {
                sectionText.text = progress.currentSection.ToString();
            }
        }

        private void UpdateHealthDisplay()
        {
            // Placeholder for health display
            // Would integrate with player health system when implemented
            if (healthBar != null)
            {
                healthBar.value = 1f; // Full health placeholder
            }

            if (healthText != null)
            {
                healthText.text = "100/100";
            }

            // Update life icons
            for (int i = 0; i < lifeIcons.Length; i++)
            {
                if (lifeIcons[i] != null)
                {
                    lifeIcons[i].enabled = true; // Show all lives placeholder
                }
            }
        }

        private void UpdateMedalPreview()
        {
            if (medalIcon == null) return;

            UIManager.MedalType currentMedal = GetCurrentMedalType();
            
            switch (currentMedal)
            {
                case UIManager.MedalType.Gold:
                    medalIcon.sprite = goldMedal;
                    medalIcon.color = Color.white;
                    break;
                case UIManager.MedalType.Silver:
                    medalIcon.sprite = silverMedal;
                    medalIcon.color = Color.white;
                    break;
                case UIManager.MedalType.Bronze:
                    medalIcon.sprite = bronzeMedal;
                    medalIcon.color = Color.white;
                    break;
                default:
                    medalIcon.color = Color.clear;
                    break;
            }
        }

        private UIManager.MedalType GetCurrentMedalType()
        {
            if (currentTime <= goldTimeThreshold)
                return UIManager.MedalType.Gold;
            else if (currentTime <= silverTimeThreshold)
                return UIManager.MedalType.Silver;
            else if (currentTime <= bronzeTimeThreshold)
                return UIManager.MedalType.Bronze;
            else
                return UIManager.MedalType.None;
        }

        private void UpdateWarningStates()
        {
            // Handle warning flash animation
            if (isWarningFlashing && currentTimeText != null)
            {
                float flash = Mathf.Sin(Time.time * warningFlashSpeed);
                Color flashColor = Color.Lerp(Color.white, warningColor, (flash + 1f) * 0.5f);
                currentTimeText.color = flashColor;
            }
        }

        #endregion

        #region Event Handlers

        private void OnProgressUpdated(LevelManager.LevelProgress progress)
        {
            // Update collectibles from level progress
            currentFishTreats = progress.collectedFishTreats;
            currentYarn = progress.collectedYarn;
            currentTokens = progress.collectedTokens;

            UpdateCollectibleDisplay();
        }

        private void OnProgressionUpdated(UIManager.ProgressionData progression)
        {
            // Update total progression data if needed for HUD display
        }

        private void OnLevelCompleted(LevelManager.LevelData levelData)
        {
            // Handle level completion
            StopTimeWarning();
        }

        #endregion

        #region Collectible Animation

        /// <summary>
        /// Animate collectible counter when items are collected
        /// </summary>
        public void AnimateCollectibleGain(string collectibleType, int amount)
        {
            switch (collectibleType.ToLower())
            {
                case "fishtreat":
                case "fishtreats":
                    AnimateCollectibleIcon(fishTreatsIcon, fishTreatsText);
                    break;
                case "yarn":
                    AnimateCollectibleIcon(yarnIcon, yarnText);
                    break;
                case "token":
                case "goldenmousetoken":
                    AnimateCollectibleIcon(tokensIcon, tokensText);
                    break;
            }
        }

        private void AnimateCollectibleIcon(Image icon, TextMeshProUGUI text)
        {
            if (icon == null && text == null) return;

            if (collectibleAnimation != null)
            {
                StopCoroutine(collectibleAnimation);
            }

            collectibleAnimation = StartCoroutine(CollectibleAnimationCoroutine(icon, text));
        }

        private IEnumerator CollectibleAnimationCoroutine(Image icon, TextMeshProUGUI text)
        {
            float elapsedTime = 0f;
            Vector3 originalScale = Vector3.one;
            Color originalColor = Color.white;

            if (icon != null)
            {
                originalScale = icon.transform.localScale;
                originalColor = icon.color;
            }
            else if (text != null)
            {
                originalScale = text.transform.localScale;
                originalColor = text.color;
            }

            while (elapsedTime < collectibleAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / collectibleAnimationDuration;
                float animationValue = collectibleAnimationCurve.Evaluate(progress);

                // Scale animation
                float scale = Mathf.Lerp(1f, 1.3f, animationValue);
                Vector3 newScale = originalScale * scale;

                // Color animation
                Color highlightColor = Color.Lerp(originalColor, collectibleHighlightColor, animationValue);

                // Apply to icon
                if (icon != null)
                {
                    icon.transform.localScale = newScale;
                    icon.color = highlightColor;
                }

                // Apply to text
                if (text != null)
                {
                    text.transform.localScale = newScale;
                    text.color = highlightColor;
                }

                yield return null;
            }

            // Reset to original state
            if (icon != null)
            {
                icon.transform.localScale = originalScale;
                icon.color = originalColor;
            }

            if (text != null)
            {
                text.transform.localScale = originalScale;
                text.color = originalColor;
            }
        }

        #endregion

        #region Warning System

        private void StartTimeWarning()
        {
            isWarningFlashing = true;
        }

        private void StopTimeWarning()
        {
            isWarningFlashing = false;
            if (currentTimeText != null)
            {
                currentTimeText.color = Color.white;
            }
        }

        #endregion

        #region Utility Methods

        private string FormatTime(float timeInSeconds)
        {
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            int milliseconds = Mathf.FloorToInt((timeInSeconds % 1f) * 100f);

            return $"{minutes:00}:{seconds:00}.{milliseconds:00}";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Force update all HUD elements
        /// </summary>
        public void RefreshHUD()
        {
            UpdateCollectibleDisplay();
            UpdateProgressDisplay();
            UpdateHealthDisplay();
            UpdateBestTimeDisplay();
            UpdateMedalPreview();
        }

        /// <summary>
        /// Set HUD visibility
        /// </summary>
        public void SetHUDVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// Update medal time thresholds
        /// </summary>
        public void SetMedalThresholds(float gold, float silver, float bronze)
        {
            goldTimeThreshold = gold;
            silverTimeThreshold = silver;
            bronzeTimeThreshold = bronze;
        }

        /// <summary>
        /// Manually update collectible counts (for testing or external updates)
        /// </summary>
        public void UpdateCollectibles(int fishTreats, int yarn, int tokens)
        {
            currentFishTreats = fishTreats;
            currentYarn = yarn;
            currentTokens = tokens;
            UpdateCollectibleDisplay();
        }

        #endregion
    }
}
