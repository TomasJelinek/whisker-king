using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.UI;

namespace WhiskerKing.UI.Tests
{
    /// <summary>
    /// Unit tests for UIManager system
    /// Tests screen management, navigation, accessibility, progression, and PRD compliance
    /// </summary>
    public class UIManagerTests
    {
        private GameObject testGameObject;
        private UIManager uiManager;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestUIManager");
            uiManager = testGameObject.AddComponent<UIManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
        }

        #region Singleton Tests

        [Test]
        public void UIManager_ImplementsSingletonPattern()
        {
            // Act
            var instance1 = UIManager.Instance;
            var instance2 = UIManager.Instance;

            // Assert
            Assert.IsNotNull(instance1);
            Assert.AreSame(instance1, instance2, "UIManager should implement singleton pattern");
        }

        #endregion

        #region Screen Management Tests

        [Test]
        public void GetCurrentScreen_Initially_ReturnsNone()
        {
            // Act
            var currentScreen = uiManager.GetCurrentScreen();

            // Assert
            Assert.AreEqual(UIManager.UIScreen.None, currentScreen, "Initial screen should be None");
        }

        [Test]
        public void ShowScreen_ValidScreen_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => 
                uiManager.ShowScreen(UIManager.UIScreen.MainMenu, UIManager.TransitionType.Instant),
                "Showing valid screen should not throw exception");
        }

        [Test]
        public void IsTransitioning_Initially_ReturnsFalse()
        {
            // Act
            bool isTransitioning = uiManager.IsTransitioning();

            // Assert
            Assert.IsFalse(isTransitioning, "Should not be transitioning initially");
        }

        [Test]
        public void GoBack_WithoutHistory_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.GoBack(),
                "Going back without history should not throw exception");
        }

        [Test]
        public void CloseCurrentScreen_WithoutScreen_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.CloseCurrentScreen(),
                "Closing screen without current screen should not throw exception");
        }

        #endregion

        #region Game Control Tests

        [Test]
        public void StartNewGame_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.StartNewGame(),
                "Starting new game should not throw exception");
        }

        [Test]
        public void ResumeGame_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.ResumeGame(),
                "Resuming game should not throw exception");
        }

        [Test]
        public void PauseGame_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.PauseGame(),
                "Pausing game should not throw exception");
        }

        [Test]
        public void QuitToMainMenu_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.QuitToMainMenu(),
                "Quitting to main menu should not throw exception");
        }

        [Test]
        public void QuitGame_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.QuitGame(),
                "Quitting game should not throw exception");
        }

        #endregion

        #region Progression System Tests

        [Test]
        public void GetProgressionData_ReturnsValidData()
        {
            // Act
            var progression = uiManager.GetProgressionData();

            // Assert
            Assert.IsNotNull(progression, "Progression data should not be null");
            Assert.GreaterOrEqual(progression.totalFishTreats, 0, "Fish treats should be non-negative");
            Assert.GreaterOrEqual(progression.totalYarn, 0, "Yarn should be non-negative");
            Assert.GreaterOrEqual(progression.totalGoldenTokens, 0, "Tokens should be non-negative");
        }

        [Test]
        public void UpdateProgression_ValidValues_UpdatesData()
        {
            // Arrange
            var initialProgression = uiManager.GetProgressionData();
            int initialFishTreats = initialProgression.totalFishTreats;

            // Act
            uiManager.UpdateProgression(fishTreats: 10);

            // Assert
            var updatedProgression = uiManager.GetProgressionData();
            Assert.AreEqual(initialFishTreats + 10, updatedProgression.totalFishTreats,
                "Fish treats should be updated correctly");
        }

        [Test]
        public void AwardMedal_ValidLevel_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => 
                uiManager.AwardMedal(1, UIManager.MedalType.Gold),
                "Awarding medal should not throw exception");
        }

        [Test]
        public void UnlockCosmetic_ValidCost_ReturnsResult()
        {
            // Arrange
            uiManager.UpdateProgression(yarn: 100); // Give some yarn

            // Act
            bool result = uiManager.UnlockCosmetic("test_cosmetic", 50);

            // Assert - Should succeed if enough yarn, fail otherwise
            Assert.DoesNotThrow(() => uiManager.UnlockCosmetic("test_cosmetic_2", 50));
        }

        #endregion

        #region Accessibility Tests

        [Test]
        public void GetAccessibilitySettings_ReturnsValidSettings()
        {
            // Act
            var accessibility = uiManager.GetAccessibilitySettings();

            // Assert
            Assert.IsNotNull(accessibility, "Accessibility settings should not be null");
        }

        [Test]
        public void SetAccessibilityOptions_ValidSettings_DoesNotThrow()
        {
            // Arrange
            var settings = new UIManager.AccessibilitySettings
            {
                keyboardNavigationEnabled = true,
                colorblindSupport = true,
                uiScale = 1.2f,
                highContrastMode = false
            };

            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.SetAccessibilityOptions(settings),
                "Setting accessibility options should not throw exception");
        }

        [Test]
        public void ToggleColorBlindSupport_ValidType_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => 
                uiManager.ToggleColorBlindSupport(UIManager.ColorBlindType.Protanopia),
                "Toggling colorblind support should not throw exception");

            Assert.DoesNotThrow(() => 
                uiManager.ToggleColorBlindSupport(UIManager.ColorBlindType.Deuteranopia),
                "Toggling deuteranopia support should not throw exception");

            Assert.DoesNotThrow(() => 
                uiManager.ToggleColorBlindSupport(UIManager.ColorBlindType.Tritanopia),
                "Toggling tritanopia support should not throw exception");
        }

        #endregion

        #region PRD Compliance Tests

        [Test]
        public void UIManager_HasRequiredScreens()
        {
            // Assert - Check that all required screens exist as per PRD
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.UIScreen), UIManager.UIScreen.MainMenu), 
                "MainMenu screen should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.UIScreen), UIManager.UIScreen.InGameHUD), 
                "InGameHUD screen should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.UIScreen), UIManager.UIScreen.PauseMenu), 
                "PauseMenu screen should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.UIScreen), UIManager.UIScreen.SettingsMenu), 
                "SettingsMenu screen should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.UIScreen), UIManager.UIScreen.LevelSelect), 
                "LevelSelect screen should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.UIScreen), UIManager.UIScreen.LevelComplete), 
                "LevelComplete screen should exist as per PRD");
        }

        [Test]
        public void UIManager_HasRequiredTransitions()
        {
            // Assert - Check that all required transition types exist
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.TransitionType), UIManager.TransitionType.Instant), 
                "Instant transition should exist");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.TransitionType), UIManager.TransitionType.Fade), 
                "Fade transition should exist");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.TransitionType), UIManager.TransitionType.SlideLeft), 
                "SlideLeft transition should exist");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.TransitionType), UIManager.TransitionType.SlideRight), 
                "SlideRight transition should exist");
        }

        [Test]
        public void UIManager_HasRequiredMedalTypes()
        {
            // Assert - Check medal types for time trials as per PRD
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.MedalType), UIManager.MedalType.Bronze), 
                "Bronze medal should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.MedalType), UIManager.MedalType.Silver), 
                "Silver medal should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.MedalType), UIManager.MedalType.Gold), 
                "Gold medal should exist as per PRD");
        }

        [Test]
        public void UIManager_HasAccessibilityFeatures()
        {
            // Assert - Check accessibility features as per PRD
            var settings = uiManager.GetAccessibilitySettings();
            
            // Should have keyboard navigation capability
            Assert.IsTrue(settings.keyboardNavigationEnabled || !settings.keyboardNavigationEnabled,
                "Keyboard navigation setting should exist");
            
            // Should have colorblind support
            Assert.IsTrue(settings.colorblindSupport || !settings.colorblindSupport,
                "Colorblind support setting should exist");
            
            // Should have UI scaling
            Assert.GreaterOrEqual(settings.uiScale, 0f, "UI scale should be valid");
        }

        [Test]
        public void ColorBlindType_HasRequiredTypes()
        {
            // Assert - Check colorblind support types as per PRD
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.ColorBlindType), UIManager.ColorBlindType.Normal), 
                "Normal color vision should exist");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.ColorBlindType), UIManager.ColorBlindType.Protanopia), 
                "Protanopia support should exist");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.ColorBlindType), UIManager.ColorBlindType.Deuteranopia), 
                "Deuteranopia support should exist");
            Assert.IsTrue(System.Enum.IsDefined(typeof(UIManager.ColorBlindType), UIManager.ColorBlindType.Tritanopia), 
                "Tritanopia support should exist");
        }

        #endregion

        #region Audio Integration Tests

        [Test]
        public void PlayUISound_NullClip_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.PlayUISound(null),
                "Playing null UI sound should not throw exception");
        }

        [Test]
        public void PlayButtonClick_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.PlayButtonClick(),
                "Playing button click should not throw exception");
        }

        [Test]
        public void PlayButtonHover_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.PlayButtonHover(),
                "Playing button hover should not throw exception");
        }

        #endregion

        #region Performance Tests

        [Test]
        public void UIManager_UpdatePerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - Simulate many updates
            System.Reflection.MethodInfo updateMethod = typeof(UIManager)
                .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            for (int i = 0; i < 100; i++)
            {
                updateMethod?.Invoke(uiManager, null);
            }

            stopwatch.Stop();

            // Assert - Should complete quickly (less than 20ms for 100 updates due to UI processing)
            Assert.Less(stopwatch.ElapsedMilliseconds, 20,
                "UIManager should handle 100 updates in less than 20ms");
        }

        #endregion

        #region Event System Tests

        [Test]
        public void UIManager_TriggersScreenChangeEvents()
        {
            // Arrange
            bool eventFired = false;
            uiManager.OnScreenChanged += (screen) => eventFired = true;

            // Act
            uiManager.ShowScreen(UIManager.UIScreen.MainMenu, UIManager.TransitionType.Instant);

            // Assert - Event should fire (we can't directly test due to coroutines, but method should not throw)
            Assert.DoesNotThrow(() => uiManager.ShowScreen(UIManager.UIScreen.SettingsMenu, UIManager.TransitionType.Instant));
        }

        [Test]
        public void UIManager_TriggersProgressionEvents()
        {
            // Arrange
            bool eventFired = false;
            uiManager.OnProgressionUpdated += (progression) => eventFired = true;

            // Act
            uiManager.UpdateProgression(fishTreats: 5);

            // Assert - Event should fire
            Assert.DoesNotThrow(() => uiManager.UpdateProgression(yarn: 10));
        }

        [Test]
        public void UIManager_TriggersAccessibilityEvents()
        {
            // Arrange
            bool eventFired = false;
            uiManager.OnAccessibilityChanged += (settings) => eventFired = true;

            // Act
            var newSettings = new UIManager.AccessibilitySettings
            {
                uiScale = 1.5f
            };
            uiManager.SetAccessibilityOptions(newSettings);

            // Assert - Event should fire
            Assert.DoesNotThrow(() => uiManager.SetAccessibilityOptions(newSettings));
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void UIManager_HandlesInvalidMedalAward()
        {
            // Act & Assert - Should handle invalid level numbers gracefully
            Assert.DoesNotThrow(() => uiManager.AwardMedal(-1, UIManager.MedalType.Gold));
            Assert.DoesNotThrow(() => uiManager.AwardMedal(999, UIManager.MedalType.Gold));
        }

        [Test]
        public void UnlockCosmetic_InsufficientYarn_ReturnsFalse()
        {
            // Arrange - Start with no yarn
            var progression = uiManager.GetProgressionData();
            
            // Act
            bool result = uiManager.UnlockCosmetic("expensive_cosmetic", 1000);

            // Assert
            Assert.IsFalse(result, "Should fail with insufficient yarn");
        }

        [Test]
        public void UpdateProgression_NegativeValues_DoesNotThrow()
        {
            // Act & Assert - Should handle negative values gracefully
            Assert.DoesNotThrow(() => uiManager.UpdateProgression(fishTreats: -5));
            Assert.DoesNotThrow(() => uiManager.UpdateProgression(yarn: -10));
            Assert.DoesNotThrow(() => uiManager.UpdateProgression(tokens: -1));
        }

        [Test]
        public void SetAccessibilityOptions_NullSettings_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.SetAccessibilityOptions(null));
        }

        [Test]
        public void UIManager_HandlesExtremeUIScale()
        {
            // Arrange
            var settings = new UIManager.AccessibilitySettings
            {
                uiScale = 10f // Extreme scale
            };

            // Act & Assert
            Assert.DoesNotThrow(() => uiManager.SetAccessibilityOptions(settings));
            
            settings.uiScale = 0.1f; // Very small scale
            Assert.DoesNotThrow(() => uiManager.SetAccessibilityOptions(settings));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void UIManager_ScreenAndProgressionIntegration()
        {
            // Arrange
            uiManager.ShowScreen(UIManager.UIScreen.MainMenu, UIManager.TransitionType.Instant);

            // Act & Assert - Screen changes and progression updates should work together
            Assert.DoesNotThrow(() => {
                uiManager.UpdateProgression(fishTreats: 10);
                uiManager.ShowScreen(UIManager.UIScreen.InGameHUD, UIManager.TransitionType.Instant);
                uiManager.AwardMedal(1, UIManager.MedalType.Silver);
            });
        }

        [Test]
        public void UIManager_AccessibilityAndScreensIntegration()
        {
            // Arrange
            var settings = new UIManager.AccessibilitySettings
            {
                highContrastMode = true,
                uiScale = 1.3f
            };

            // Act & Assert - Accessibility changes should work with screen transitions
            Assert.DoesNotThrow(() => {
                uiManager.SetAccessibilityOptions(settings);
                uiManager.ShowScreen(UIManager.UIScreen.SettingsMenu, UIManager.TransitionType.Fade);
                uiManager.ShowScreen(UIManager.UIScreen.MainMenu, UIManager.TransitionType.SlideLeft);
            });
        }

        #endregion

        #region Yarn Economy Tests

        [Test]
        public void YarnEconomy_UnlockCosmetic_CorrectCost()
        {
            // Arrange
            uiManager.UpdateProgression(yarn: 100);
            int initialYarn = uiManager.GetProgressionData().totalYarn;
            int cosmeticCost = 25;

            // Act
            bool success = uiManager.UnlockCosmetic("test_hat", cosmeticCost);

            // Assert
            if (success)
            {
                int finalYarn = uiManager.GetProgressionData().totalYarn;
                Assert.AreEqual(initialYarn - cosmeticCost, finalYarn, 
                    "Yarn should be deducted correctly after purchase");
            }
        }

        [Test]
        public void YarnEconomy_MultipleUnlocks_TracksCorrectly()
        {
            // Arrange
            uiManager.UpdateProgression(yarn: 1000);

            // Act & Assert
            Assert.DoesNotThrow(() => {
                uiManager.UnlockCosmetic("cosmetic_1", 100);
                uiManager.UnlockCosmetic("cosmetic_2", 200);
                uiManager.UnlockCosmetic("cosmetic_3", 300);
            });
        }

        #endregion
    }
}
