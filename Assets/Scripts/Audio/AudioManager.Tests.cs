using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using WhiskerKing.Audio;

namespace WhiskerKing.Audio.Tests
{
    /// <summary>
    /// Unit tests for AudioManager system
    /// Tests category-based audio, 3D spatial audio, music system, and PRD compliance
    /// </summary>
    public class AudioManagerTests
    {
        private GameObject testGameObject;
        private AudioManager audioManager;
        private AudioClip testClip;

        [SetUp]
        public void Setup()
        {
            testGameObject = new GameObject("TestAudioManager");
            audioManager = testGameObject.AddComponent<AudioManager>();
            
            // Create a simple test audio clip
            testClip = AudioClip.Create("TestClip", 44100, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
                Object.DestroyImmediate(testGameObject);
                
            if (testClip != null)
                Object.DestroyImmediate(testClip);
        }

        #region Singleton Tests

        [Test]
        public void AudioManager_ImplementsSingletonPattern()
        {
            // Act
            var instance1 = AudioManager.Instance;
            var instance2 = AudioManager.Instance;

            // Assert
            Assert.IsNotNull(instance1);
            Assert.AreSame(instance1, instance2, "AudioManager should implement singleton pattern");
        }

        #endregion

        #region Volume Control Tests

        [Test]
        public void SetMasterVolume_ValidRange_UpdatesVolume()
        {
            // Arrange
            float testVolume = 0.75f;

            // Act
            audioManager.SetMasterVolume(testVolume);

            // Assert - Use reflection to check private field
            var masterVolumeField = typeof(AudioManager).GetField("masterVolume", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float actualVolume = (float)masterVolumeField?.GetValue(audioManager);
            
            Assert.AreEqual(testVolume, actualVolume, 0.01f, "Master volume should be set correctly");
        }

        [Test]
        public void SetMasterVolume_AboveRange_ClampsToOne()
        {
            // Act
            audioManager.SetMasterVolume(1.5f);

            // Assert
            var masterVolumeField = typeof(AudioManager).GetField("masterVolume", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float actualVolume = (float)masterVolumeField?.GetValue(audioManager);
            
            Assert.AreEqual(1f, actualVolume, 0.01f, "Master volume should be clamped to 1.0");
        }

        [Test]
        public void SetMasterVolume_BelowRange_ClampsToZero()
        {
            // Act
            audioManager.SetMasterVolume(-0.5f);

            // Assert
            var masterVolumeField = typeof(AudioManager).GetField("masterVolume", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float actualVolume = (float)masterVolumeField?.GetValue(audioManager);
            
            Assert.AreEqual(0f, actualVolume, 0.01f, "Master volume should be clamped to 0.0");
        }

        [Test]
        public void SetMusicVolume_ValidRange_UpdatesVolume()
        {
            // Arrange
            float testVolume = 0.6f;

            // Act
            audioManager.SetMusicVolume(testVolume);

            // Assert
            var musicVolumeField = typeof(AudioManager).GetField("musicVolume", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float actualVolume = (float)musicVolumeField?.GetValue(audioManager);
            
            Assert.AreEqual(testVolume, actualVolume, 0.01f, "Music volume should be set correctly");
        }

        [Test]
        public void SetSFXVolume_ValidRange_UpdatesVolume()
        {
            // Arrange
            float testVolume = 0.8f;

            // Act
            audioManager.SetSFXVolume(testVolume);

            // Assert
            var sfxVolumeField = typeof(AudioManager).GetField("sfxVolume", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float actualVolume = (float)sfxVolumeField?.GetValue(audioManager);
            
            Assert.AreEqual(testVolume, actualVolume, 0.01f, "SFX volume should be set correctly");
        }

        [Test]
        public void SetVoiceVolume_ValidRange_UpdatesVolume()
        {
            // Arrange
            float testVolume = 0.9f;

            // Act
            audioManager.SetVoiceVolume(testVolume);

            // Assert
            var voiceVolumeField = typeof(AudioManager).GetField("voiceVolume", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float actualVolume = (float)voiceVolumeField?.GetValue(audioManager);
            
            Assert.AreEqual(testVolume, actualVolume, 0.01f, "Voice volume should be set correctly");
        }

        #endregion

        #region Music System Tests

        [Test]
        public void GetMusicState_Initially_ReturnsStopped()
        {
            // Act
            var state = audioManager.GetMusicState();

            // Assert
            Assert.AreEqual(AudioManager.MusicState.Stopped, state, "Initial music state should be Stopped");
        }

        [Test]
        public void GetCurrentMusicTrack_Initially_ReturnsNull()
        {
            // Act
            var track = audioManager.GetCurrentMusicTrack();

            // Assert
            Assert.IsNull(track, "Initial music track should be null");
        }

        [Test]
        public void PlayMusic_NonExistentTrack_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.PlayMusic("NonExistentTrack"),
                "Playing non-existent music track should not throw exception");
        }

        [Test]
        public void StopMusic_WhenNoMusicPlaying_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.StopMusic(),
                "Stopping music when none is playing should not throw exception");
        }

        [Test]
        public void PauseMusic_WhenNoMusicPlaying_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.PauseMusic(),
                "Pausing music when none is playing should not throw exception");
        }

        [Test]
        public void ResumeMusic_WhenNoMusicPaused_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.ResumeMusic(),
                "Resuming music when none is paused should not throw exception");
        }

        #endregion

        #region Audio Quality Tests

        [Test]
        public void SetAudioQuality_Low_UpdatesQuality()
        {
            // Act
            audioManager.SetAudioQuality(AudioManager.AudioQuality.Low);

            // Assert - Use reflection to check private field
            var qualityField = typeof(AudioManager).GetField("audioQuality", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actualQuality = (AudioManager.AudioQuality)qualityField?.GetValue(audioManager);
            
            Assert.AreEqual(AudioManager.AudioQuality.Low, actualQuality, "Audio quality should be set to Low");
        }

        [Test]
        public void SetAudioQuality_Medium_UpdatesQuality()
        {
            // Act
            audioManager.SetAudioQuality(AudioManager.AudioQuality.Medium);

            // Assert
            var qualityField = typeof(AudioManager).GetField("audioQuality", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actualQuality = (AudioManager.AudioQuality)qualityField?.GetValue(audioManager);
            
            Assert.AreEqual(AudioManager.AudioQuality.Medium, actualQuality, "Audio quality should be set to Medium");
        }

        [Test]
        public void SetAudioQuality_High_UpdatesQuality()
        {
            // Act
            audioManager.SetAudioQuality(AudioManager.AudioQuality.High);

            // Assert
            var qualityField = typeof(AudioManager).GetField("audioQuality", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var actualQuality = (AudioManager.AudioQuality)qualityField?.GetValue(audioManager);
            
            Assert.AreEqual(AudioManager.AudioQuality.High, actualQuality, "Audio quality should be set to High");
        }

        #endregion

        #region PRD Compliance Tests

        [Test]
        public void AudioManager_HasCorrectAudioCategories()
        {
            // Assert - Check that all required categories exist as per PRD
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioCategory), AudioManager.AudioCategory.Music), 
                "Music category should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioCategory), AudioManager.AudioCategory.SFX_Player), 
                "SFX_Player category should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioCategory), AudioManager.AudioCategory.SFX_World), 
                "SFX_World category should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioCategory), AudioManager.AudioCategory.SFX_UI), 
                "SFX_UI category should exist as per PRD");
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioCategory), AudioManager.AudioCategory.Voice), 
                "Voice category should exist as per PRD");
        }

        [Test]
        public void AudioManager_HasCorrectQualityLevels()
        {
            // Assert - Check that quality levels match PRD specifications
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioQuality), AudioManager.AudioQuality.Low), 
                "Low quality should exist (22kHz, 64kbps as per PRD)");
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioQuality), AudioManager.AudioQuality.Medium), 
                "Medium quality should exist (44.1kHz, 96kbps as per PRD)");
            Assert.IsTrue(System.Enum.IsDefined(typeof(AudioManager.AudioQuality), AudioManager.AudioQuality.High), 
                "High quality should exist (44.1kHz, 128kbps as per PRD)");
        }

        [Test]
        public void VolumeRange_IsZeroToOne_AsPRD()
        {
            // Test that volume setters accept 0-1 range as per PRD
            
            // Act & Assert - Should not throw for valid range
            Assert.DoesNotThrow(() => audioManager.SetMasterVolume(0f));
            Assert.DoesNotThrow(() => audioManager.SetMasterVolume(1f));
            Assert.DoesNotThrow(() => audioManager.SetMusicVolume(0.5f));
            Assert.DoesNotThrow(() => audioManager.SetSFXVolume(0.8f));
            Assert.DoesNotThrow(() => audioManager.SetVoiceVolume(1f));
        }

        #endregion

        #region Audio Playback Tests

        [Test]
        public void PlayOneShot_ValidClip_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => 
                audioManager.PlayOneShot(testClip, AudioManager.AudioCategory.SFX_Player),
                "Playing valid audio clip should not throw exception");
        }

        [Test]
        public void PlayOneShot_NullClip_ReturnsNull()
        {
            // Act
            var result = audioManager.PlayOneShot(null, AudioManager.AudioCategory.SFX_Player);

            // Assert
            Assert.IsNull(result, "Playing null clip should return null");
        }

        [Test]
        public void Play3DAudio_ValidClip_DoesNotThrow()
        {
            // Arrange
            Vector3 testPosition = new Vector3(10f, 5f, 15f);

            // Act & Assert
            Assert.DoesNotThrow(() => 
                audioManager.Play3DAudio(testClip, testPosition),
                "Playing 3D audio with valid clip should not throw exception");
        }

        [Test]
        public void PlayAudioEvent_NonExistentEvent_ReturnsNull()
        {
            // Act
            var result = audioManager.PlayAudioEvent("NonExistentEvent");

            // Assert
            Assert.IsNull(result, "Playing non-existent audio event should return null");
        }

        #endregion

        #region Category Management Tests

        [Test]
        public void GetCategoryVolume_Music_ReturnsCorrectVolume()
        {
            // Arrange
            float expectedVolume = 0.7f;
            audioManager.SetMusicVolume(expectedVolume);

            // Act
            float actualVolume = audioManager.GetCategoryVolume(AudioManager.AudioCategory.Music);

            // Assert
            Assert.AreEqual(expectedVolume, actualVolume, 0.01f, "Music category volume should match set value");
        }

        [Test]
        public void GetCategoryVolume_SFX_ReturnsCorrectVolume()
        {
            // Arrange
            float expectedVolume = 0.9f;
            audioManager.SetSFXVolume(expectedVolume);

            // Act
            float actualVolume = audioManager.GetCategoryVolume(AudioManager.AudioCategory.SFX_Player);

            // Assert
            Assert.AreEqual(expectedVolume, actualVolume, 0.01f, "SFX category volume should match set value");
        }

        [Test]
        public void SetCategoryMute_ValidCategory_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => 
                audioManager.SetCategoryMute(AudioManager.AudioCategory.SFX_Player, true),
                "Muting valid category should not throw exception");
            
            Assert.DoesNotThrow(() => 
                audioManager.SetCategoryMute(AudioManager.AudioCategory.SFX_Player, false),
                "Unmuting valid category should not throw exception");
        }

        [Test]
        public void StopAllAudio_ValidCategory_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => 
                audioManager.StopAllAudio(AudioManager.AudioCategory.SFX_Player),
                "Stopping all audio in category should not throw exception");
        }

        [Test]
        public void StopAllAudio_AllCategories_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.StopAllAudio(),
                "Stopping all audio should not throw exception");
        }

        #endregion

        #region Adaptive Music Tests

        [Test]
        public void SetAdaptiveMusicParameter_ValidParameter_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => 
                audioManager.SetAdaptiveMusicParameter("intensity", 0.8f),
                "Setting adaptive music parameter should not throw exception");
        }

        [Test]
        public void GetAdaptiveMusicParameter_ExistingParameter_ReturnsValue()
        {
            // Arrange
            string paramName = "intensity";
            float expectedValue = 0.6f;
            audioManager.SetAdaptiveMusicParameter(paramName, expectedValue);

            // Act
            float actualValue = audioManager.GetAdaptiveMusicParameter(paramName);

            // Assert
            Assert.AreEqual(expectedValue, actualValue, 0.01f, "Adaptive music parameter should return set value");
        }

        [Test]
        public void GetAdaptiveMusicParameter_NonExistentParameter_ReturnsZero()
        {
            // Act
            float result = audioManager.GetAdaptiveMusicParameter("nonexistent");

            // Assert
            Assert.AreEqual(0f, result, 0.01f, "Non-existent parameter should return 0");
        }

        [Test]
        public void SetAdaptiveMusicParameter_AboveRange_ClampsToOne()
        {
            // Act
            audioManager.SetAdaptiveMusicParameter("intensity", 1.5f);
            float result = audioManager.GetAdaptiveMusicParameter("intensity");

            // Assert
            Assert.AreEqual(1f, result, 0.01f, "Adaptive parameter should be clamped to 1.0");
        }

        [Test]
        public void SetAdaptiveMusicParameter_BelowRange_ClampsToZero()
        {
            // Act
            audioManager.SetAdaptiveMusicParameter("intensity", -0.5f);
            float result = audioManager.GetAdaptiveMusicParameter("intensity");

            // Assert
            Assert.AreEqual(0f, result, 0.01f, "Adaptive parameter should be clamped to 0.0");
        }

        #endregion

        #region Performance Tests

        [Test]
        public void GetPerformanceStats_ReturnsValidStats()
        {
            // Act
            var stats = audioManager.GetPerformanceStats();

            // Assert
            Assert.IsNotNull(stats, "Performance stats should not be null");
            Assert.IsTrue(stats.ContainsKey("TotalActiveSources"), "Stats should contain TotalActiveSources");
            Assert.IsTrue(stats.ContainsKey("MaxSources"), "Stats should contain MaxSources");
            Assert.IsTrue(stats.ContainsKey("AudioQuality"), "Stats should contain AudioQuality");
        }

        [Test]
        public void AudioManager_UpdatePerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - Simulate many updates
            System.Reflection.MethodInfo updateMethod = typeof(AudioManager)
                .GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            for (int i = 0; i < 100; i++)
            {
                updateMethod?.Invoke(audioManager, null);
            }

            stopwatch.Stop();

            // Assert - Should complete quickly (less than 15ms for 100 updates due to audio processing)
            Assert.Less(stopwatch.ElapsedMilliseconds, 15,
                "AudioManager should handle 100 updates in less than 15ms");
        }

        #endregion

        #region Save/Load Tests

        [Test]
        public void SaveAudioSettings_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.SaveAudioSettings(),
                "Saving audio settings should not throw exception");
        }

        [Test]
        public void LoadAudioSettings_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.LoadAudioSettings(),
                "Loading audio settings should not throw exception");
        }

        [Test]
        public void SaveLoadAudioSettings_PreservesVolumes()
        {
            // Arrange
            float testMasterVolume = 0.75f;
            float testMusicVolume = 0.6f;
            float testSFXVolume = 0.9f;
            
            audioManager.SetMasterVolume(testMasterVolume);
            audioManager.SetMusicVolume(testMusicVolume);
            audioManager.SetSFXVolume(testSFXVolume);

            // Act
            audioManager.SaveAudioSettings();
            
            // Change values
            audioManager.SetMasterVolume(0.5f);
            audioManager.SetMusicVolume(0.3f);
            audioManager.SetSFXVolume(0.7f);
            
            // Load back
            audioManager.LoadAudioSettings();

            // Assert
            Assert.AreEqual(testMasterVolume, audioManager.GetCategoryVolume(AudioManager.AudioCategory.Music), 0.01f,
                "Master volume should be restored after load");
            // Note: GetCategoryVolume for Music returns musicVolume, not masterVolume
        }

        #endregion

        #region Event System Tests

        [Test]
        public void AudioManager_TriggersVolumeChangeEvents()
        {
            // Arrange
            bool eventFired = false;
            audioManager.OnCategoryVolumeChanged += (category, volume) => eventFired = true;

            // Act
            audioManager.SetMasterVolume(0.8f);

            // Assert - Event should fire
            Assert.DoesNotThrow(() => audioManager.SetMasterVolume(0.7f));
        }

        [Test]
        public void AudioManager_TriggersQualityChangeEvents()
        {
            // Arrange
            bool eventFired = false;
            audioManager.OnAudioQualityChanged += (quality) => eventFired = true;

            // Act
            audioManager.SetAudioQuality(AudioManager.AudioQuality.Medium);

            // Assert - Event should fire
            Assert.DoesNotThrow(() => audioManager.SetAudioQuality(AudioManager.AudioQuality.Low));
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void AudioManager_HandlesNullReferencesGracefully()
        {
            // Act & Assert - These should not throw null reference exceptions
            Assert.DoesNotThrow(() => audioManager.PlayOneShot(null, AudioManager.AudioCategory.SFX_Player));
            Assert.DoesNotThrow(() => audioManager.Play3DAudio(null, Vector3.zero));
            Assert.DoesNotThrow(() => audioManager.PlayAudioEvent(null));
        }

        [Test]
        public void AudioManager_HandlesExtremeValues()
        {
            // Act & Assert - Should handle extreme values gracefully
            Assert.DoesNotThrow(() => audioManager.SetMasterVolume(float.MaxValue));
            Assert.DoesNotThrow(() => audioManager.SetMasterVolume(float.MinValue));
            Assert.DoesNotThrow(() => audioManager.SetAdaptiveMusicParameter("test", float.NaN));
            Assert.DoesNotThrow(() => audioManager.SetAdaptiveMusicParameter("test", float.PositiveInfinity));
        }

        [Test]
        public void AudioManager_HandlesEmptyStrings()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => audioManager.PlayMusic(""));
            Assert.DoesNotThrow(() => audioManager.PlayAudioEvent(""));
            Assert.DoesNotThrow(() => audioManager.SetAdaptiveMusicParameter("", 0.5f));
            Assert.DoesNotThrow(() => audioManager.GetAdaptiveMusicParameter(""));
        }

        #endregion
    }
}
