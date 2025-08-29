using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WhiskerKing.Core;
using WhiskerKing.Player;
using WhiskerKing.Level;

namespace WhiskerKing.Audio
{
    /// <summary>
    /// Comprehensive Audio Manager for Whisker King
    /// Implements category-based audio system, 3D spatial audio, dynamic music, and performance optimization
    /// Based on PRD specifications: OGG Vorbis for WebGL, MP3 for mobile, 44.1kHz 16-bit, 128kbps music, 96kbps SFX
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public enum AudioCategory
        {
            Music,
            SFX_Player,
            SFX_World,
            SFX_UI,
            Voice,
            Ambient
        }

        public enum MusicState
        {
            Stopped,
            Playing,
            Fading,
            Paused,
            CrossFading
        }

        public enum AudioQuality
        {
            Low,      // 22kHz, 64kbps
            Medium,   // 44.1kHz, 96kbps
            High      // 44.1kHz, 128kbps
        }

        [System.Serializable]
        public class AudioCategorySettings
        {
            public AudioCategory category;
            public AudioMixerGroup mixerGroup;
            public float volume = 1f;
            public float pitch = 1f;
            public bool muted = false;
            public bool is3D = false;
            public AnimationCurve volumeRolloff = AnimationCurve.Linear(0, 1, 100, 0);
        }

        [System.Serializable]
        public class MusicLayer
        {
            public string layerName;
            public AudioClip clip;
            public float volume = 1f;
            public bool loop = true;
            public bool isAdaptive = false;
            public float fadeInTime = 1f;
            public float fadeOutTime = 1f;
        }

        [System.Serializable]
        public class AudioEvent
        {
            public string eventName;
            public AudioCategory category;
            public List<AudioClip> clips = new List<AudioClip>();
            public Vector2 volumeRange = new Vector2(0.8f, 1f);
            public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
            public float cooldown = 0.1f;
            public int maxConcurrent = 3;
            public bool randomizeClip = true;
            public bool is3D = true;
            public float maxDistance = 50f;
            public float minDistance = 1f;
        }

        // Singleton pattern
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<AudioManager>();
                    if (instance == null)
                    {
                        GameObject audioManagerGO = new GameObject("AudioManager");
                        instance = audioManagerGO.AddComponent<AudioManager>();
                        DontDestroyOnLoad(audioManagerGO);
                    }
                }
                return instance;
            }
        }

        [Header("Audio Configuration")]
        [SerializeField] private bool useGameConfiguration = true;
        [SerializeField] private bool debugMode = false;

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("Category Settings")]
        [SerializeField] private List<AudioCategorySettings> categorySettings = new List<AudioCategorySettings>();

        [Header("Music System")]
        [SerializeField] private List<MusicLayer> musicLayers = new List<MusicLayer>();
        [SerializeField] private float musicCrossFadeTime = 2f;
        [SerializeField] private bool enableAdaptiveMusic = true;

        [Header("Audio Events")]
        [SerializeField] private List<AudioEvent> audioEvents = new List<AudioEvent>();

        [Header("3D Audio Settings")]
        [SerializeField] private float dopplerLevel = 1f;
        [SerializeField] private float rolloffScale = 1f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

        [Header("Performance Settings")]
        [SerializeField] private int maxAudioSources = 32;
        [SerializeField] private int poolSize = 16;
        [SerializeField] private AudioQuality audioQuality = AudioQuality.High;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float voiceVolume = 1f;

        // Audio source pools
        private Dictionary<AudioCategory, Queue<AudioSource>> audioSourcePools = new Dictionary<AudioCategory, Queue<AudioSource>>();
        private Dictionary<AudioCategory, List<AudioSource>> activeAudioSources = new Dictionary<AudioCategory, List<AudioSource>>();

        // Music system
        private AudioSource musicSource;
        private AudioSource musicCrossFadeSource;
        private MusicState currentMusicState = MusicState.Stopped;
        private string currentMusicTrack;
        private Coroutine musicFadeCoroutine;
        private Dictionary<string, float> adaptiveMusicParameters = new Dictionary<string, float>();

        // Audio event system
        private Dictionary<string, float> lastAudioEventTimes = new Dictionary<string, float>();
        private Dictionary<string, int> concurrentAudioEvents = new Dictionary<string, int>();

        // 3D audio tracking
        private Transform playerTransform;
        private Transform listenerTransform;
        private List<AudioSource> spatialAudioSources = new List<AudioSource>();

        // Configuration cache
        private AudioData audioConfig;

        // Performance tracking
        private int totalAudioSourcesUsed;
        private float audioMemoryUsage;
        private Dictionary<AudioCategory, int> categoryPlayCounts = new Dictionary<AudioCategory, int>();

        // Events
        public System.Action<string> OnMusicTrackChanged;
        public System.Action<AudioCategory, float> OnCategoryVolumeChanged;
        public System.Action<string, Vector3> OnAudioEventPlayed;
        public System.Action<AudioQuality> OnAudioQualityChanged;

        #region Unity Lifecycle

        private void Awake()
        {
            // Ensure singleton
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioSystem();
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
            SetupAudioComponents();
            InitializeAudioPools();
            ConfigureAudioSettings();
        }

        private void Update()
        {
            UpdateSpatialAudio();
            UpdateAdaptiveMusic();
            CleanupFinishedAudioSources();

            if (debugMode)
            {
                UpdateDebugInfo();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (debugMode)
            {
                DrawAudioGizmos();
            }
        }

        #endregion

        #region Initialization

        private void InitializeAudioSystem()
        {
            // Initialize category tracking
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
            {
                activeAudioSources[category] = new List<AudioSource>();
                categoryPlayCounts[category] = 0;
            }

            // Initialize audio event tracking
            foreach (var audioEvent in audioEvents)
            {
                lastAudioEventTimes[audioEvent.eventName] = 0f;
                concurrentAudioEvents[audioEvent.eventName] = 0;
            }

            // Initialize adaptive music parameters
            adaptiveMusicParameters["intensity"] = 0.5f;
            adaptiveMusicParameters["tension"] = 0f;
            adaptiveMusicParameters["exploration"] = 1f;
            adaptiveMusicParameters["combat"] = 0f;

            Debug.Log("AudioManager initialized");
        }

        private void LoadConfiguration()
        {
            if (useGameConfiguration && GameConfiguration.Instance != null && GameConfiguration.Instance.Config != null)
            {
                audioConfig = GameConfiguration.Instance.Config.audio;
                ApplyConfiguration();
                Debug.Log("AudioManager: Configuration loaded from GameConfig");
            }
            else
            {
                UseDefaultConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            if (audioConfig == null) return;

            // Apply volume settings
            masterVolume = audioConfig.masterVolume;
            musicVolume = audioConfig.musicVolume;
            sfxVolume = audioConfig.sfxVolume;
            voiceVolume = audioConfig.voiceVolume;

            // Apply quality settings
            audioQuality = audioConfig.quality switch
            {
                "Low" => AudioQuality.Low,
                "Medium" => AudioQuality.Medium,
                "High" => AudioQuality.High,
                _ => AudioQuality.High
            };

            // Apply 3D audio settings
            dopplerLevel = audioConfig.dopplerLevel;
            rolloffScale = audioConfig.rolloffScale;

            // Apply performance settings
            maxAudioSources = audioConfig.maxAudioSources;
            poolSize = audioConfig.poolSize;

            // Apply mixer settings
            if (audioMixer != null)
            {
                audioMixer.SetFloat("MasterVolume", Mathf.Log10(masterVolume) * 20);
                audioMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolume) * 20);
                audioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVolume) * 20);
                audioMixer.SetFloat("VoiceVolume", Mathf.Log10(voiceVolume) * 20);
            }
        }

        private void UseDefaultConfiguration()
        {
            // Use PRD-compliant defaults
            masterVolume = 1f;
            musicVolume = 0.8f;
            sfxVolume = 1f;
            voiceVolume = 1f;
            audioQuality = AudioQuality.High;
            maxAudioSources = 32;
            poolSize = 16;
        }

        private void SetupAudioComponents()
        {
            // Create dedicated music sources
            GameObject musicSourceGO = new GameObject("MusicSource");
            musicSourceGO.transform.SetParent(transform);
            musicSource = musicSourceGO.AddComponent<AudioSource>();
            ConfigureMusicSource(musicSource);

            GameObject crossFadeSourceGO = new GameObject("CrossFadeMusicSource");
            crossFadeSourceGO.transform.SetParent(transform);
            musicCrossFadeSource = crossFadeSourceGO.AddComponent<AudioSource>();
            ConfigureMusicSource(musicCrossFadeSource);

            // Find player and listener transforms
            var player = FindObjectOfType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }

            listenerTransform = FindObjectOfType<AudioListener>()?.transform ?? Camera.main?.transform;
        }

        private void ConfigureMusicSource(AudioSource source)
        {
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D audio for music
            source.volume = musicVolume;
            
            if (audioMixer != null)
            {
                var musicGroup = categorySettings.FirstOrDefault(c => c.category == AudioCategory.Music)?.mixerGroup;
                if (musicGroup != null)
                {
                    source.outputAudioMixerGroup = musicGroup;
                }
            }
        }

        #endregion

        #region Audio Pool Management

        private void InitializeAudioPools()
        {
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
            {
                audioSourcePools[category] = new Queue<AudioSource>();
                
                // Create initial pool of audio sources
                for (int i = 0; i < poolSize; i++)
                {
                    CreatePooledAudioSource(category);
                }
            }
        }

        private AudioSource CreatePooledAudioSource(AudioCategory category)
        {
            GameObject sourceGO = new GameObject($"AudioSource_{category}");
            sourceGO.transform.SetParent(transform);
            
            AudioSource source = sourceGO.AddComponent<AudioSource>();
            ConfigureAudioSource(source, category);
            
            sourceGO.SetActive(false);
            audioSourcePools[category].Enqueue(source);
            
            return source;
        }

        private void ConfigureAudioSource(AudioSource source, AudioCategory category)
        {
            var settings = categorySettings.FirstOrDefault(c => c.category == category);
            if (settings != null)
            {
                source.outputAudioMixerGroup = settings.mixerGroup;
                source.volume = settings.volume;
                source.pitch = settings.pitch;
                source.mute = settings.muted;
                
                if (settings.is3D)
                {
                    source.spatialBlend = 1f;
                    source.rolloffMode = rolloffMode;
                    source.dopplerLevel = dopplerLevel;
                    source.maxDistance = 50f;
                    source.minDistance = 1f;
                }
                else
                {
                    source.spatialBlend = 0f;
                }
            }

            source.playOnAwake = false;
        }

        private AudioSource GetPooledAudioSource(AudioCategory category)
        {
            if (audioSourcePools[category].Count > 0)
            {
                AudioSource source = audioSourcePools[category].Dequeue();
                source.gameObject.SetActive(true);
                activeAudioSources[category].Add(source);
                totalAudioSourcesUsed++;
                return source;
            }
            else if (totalAudioSourcesUsed < maxAudioSources)
            {
                // Create new source if under limit
                AudioSource newSource = CreatePooledAudioSource(category);
                newSource.gameObject.SetActive(true);
                activeAudioSources[category].Add(newSource);
                totalAudioSourcesUsed++;
                return newSource;
            }
            else
            {
                // Find oldest source to reuse
                AudioSource oldestSource = FindOldestAudioSource(category);
                if (oldestSource != null)
                {
                    oldestSource.Stop();
                    return oldestSource;
                }
            }

            return null;
        }

        private AudioSource FindOldestAudioSource(AudioCategory category)
        {
            var activeSources = activeAudioSources[category];
            return activeSources.FirstOrDefault(source => !source.isPlaying);
        }

        private void ReturnAudioSourceToPool(AudioSource source, AudioCategory category)
        {
            if (source == null) return;

            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
            
            activeAudioSources[category].Remove(source);
            audioSourcePools[category].Enqueue(source);
            totalAudioSourcesUsed = Mathf.Max(0, totalAudioSourcesUsed - 1);
        }

        #endregion

        #region Audio Playback

        /// <summary>
        /// Play audio event by name
        /// </summary>
        public AudioSource PlayAudioEvent(string eventName, Vector3? position = null, Transform parent = null)
        {
            var audioEvent = audioEvents.FirstOrDefault(e => e.eventName == eventName);
            if (audioEvent == null)
            {
                if (debugMode) Debug.LogWarning($"Audio event '{eventName}' not found");
                return null;
            }

            // Check cooldown
            if (Time.time - lastAudioEventTimes[eventName] < audioEvent.cooldown)
            {
                return null;
            }

            // Check concurrent limit
            if (concurrentAudioEvents[eventName] >= audioEvent.maxConcurrent)
            {
                return null;
            }

            // Get audio source
            AudioSource source = GetPooledAudioSource(audioEvent.category);
            if (source == null) return null;

            // Select audio clip
            AudioClip clip = audioEvent.randomizeClip && audioEvent.clips.Count > 1
                ? audioEvent.clips[Random.Range(0, audioEvent.clips.Count)]
                : audioEvent.clips.FirstOrDefault();

            if (clip == null)
            {
                ReturnAudioSourceToPool(source, audioEvent.category);
                return null;
            }

            // Configure source
            source.clip = clip;
            source.volume = Random.Range(audioEvent.volumeRange.x, audioEvent.volumeRange.y);
            source.pitch = Random.Range(audioEvent.pitchRange.x, audioEvent.pitchRange.y);

            // Set position for 3D audio
            if (audioEvent.is3D && position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f;
                source.maxDistance = audioEvent.maxDistance;
                source.minDistance = audioEvent.minDistance;
                
                if (parent != null)
                {
                    source.transform.SetParent(parent);
                }
                
                spatialAudioSources.Add(source);
            }
            else
            {
                source.spatialBlend = 0f;
                source.transform.SetParent(transform);
            }

            // Play audio
            source.Play();

            // Update tracking
            lastAudioEventTimes[eventName] = Time.time;
            concurrentAudioEvents[eventName]++;
            categoryPlayCounts[audioEvent.category]++;

            // Schedule cleanup
            StartCoroutine(CleanupAudioSource(source, audioEvent, clip.length));

            // Trigger events
            OnAudioEventPlayed?.Invoke(eventName, position ?? Vector3.zero);

            if (debugMode)
            {
                Debug.Log($"Played audio event: {eventName} at {position}");
            }

            return source;
        }

        /// <summary>
        /// Play one-shot audio clip
        /// </summary>
        public AudioSource PlayOneShot(AudioClip clip, AudioCategory category, float volume = 1f, 
                                       Vector3? position = null, Transform parent = null)
        {
            if (clip == null) return null;

            AudioSource source = GetPooledAudioSource(category);
            if (source == null) return null;

            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f;

            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 1f;
                
                if (parent != null)
                {
                    source.transform.SetParent(parent);
                }
                
                spatialAudioSources.Add(source);
            }
            else
            {
                source.spatialBlend = 0f;
                source.transform.SetParent(transform);
            }

            source.Play();
            categoryPlayCounts[category]++;

            StartCoroutine(CleanupAudioSource(source, null, clip.length));

            return source;
        }

        private IEnumerator CleanupAudioSource(AudioSource source, AudioEvent audioEvent, float delay)
        {
            yield return new WaitForSeconds(delay + 0.1f);

            if (source != null)
            {
                // Update concurrent tracking
                if (audioEvent != null)
                {
                    concurrentAudioEvents[audioEvent.eventName] = 
                        Mathf.Max(0, concurrentAudioEvents[audioEvent.eventName] - 1);
                }

                // Remove from spatial tracking
                spatialAudioSources.Remove(source);

                // Return to appropriate category pool
                AudioCategory category = audioEvent?.category ?? AudioCategory.SFX_World;
                ReturnAudioSourceToPool(source, category);
            }
        }

        #endregion

        #region Music System

        /// <summary>
        /// Play music track with optional cross-fade
        /// </summary>
        public void PlayMusic(string trackName, bool crossFade = true)
        {
            var musicLayer = musicLayers.FirstOrDefault(m => m.layerName == trackName);
            if (musicLayer == null)
            {
                if (debugMode) Debug.LogWarning($"Music track '{trackName}' not found");
                return;
            }

            if (currentMusicTrack == trackName && currentMusicState == MusicState.Playing)
            {
                return; // Already playing this track
            }

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            if (crossFade && musicSource.isPlaying)
            {
                musicFadeCoroutine = StartCoroutine(CrossFadeMusic(musicLayer));
            }
            else
            {
                musicFadeCoroutine = StartCoroutine(PlayMusicImmediate(musicLayer));
            }
        }

        private IEnumerator CrossFadeMusic(MusicLayer newLayer)
        {
            currentMusicState = MusicState.CrossFading;
            
            // Setup cross-fade source
            musicCrossFadeSource.clip = newLayer.clip;
            musicCrossFadeSource.volume = 0f;
            musicCrossFadeSource.loop = newLayer.loop;
            musicCrossFadeSource.Play();

            float fadeTime = musicCrossFadeTime;
            float elapsedTime = 0f;
            float startVolume = musicSource.volume;

            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeTime;

                musicSource.volume = Mathf.Lerp(startVolume, 0f, progress);
                musicCrossFadeSource.volume = Mathf.Lerp(0f, newLayer.volume * musicVolume, progress);

                yield return null;
            }

            // Switch sources
            musicSource.Stop();
            AudioSource temp = musicSource;
            musicSource = musicCrossFadeSource;
            musicCrossFadeSource = temp;

            currentMusicTrack = newLayer.layerName;
            currentMusicState = MusicState.Playing;
            
            OnMusicTrackChanged?.Invoke(currentMusicTrack);

            if (debugMode)
            {
                Debug.Log($"Cross-faded to music track: {newLayer.layerName}");
            }
        }

        private IEnumerator PlayMusicImmediate(MusicLayer layer)
        {
            currentMusicState = MusicState.Fading;
            
            musicSource.clip = layer.clip;
            musicSource.volume = 0f;
            musicSource.loop = layer.loop;
            musicSource.Play();

            float fadeTime = layer.fadeInTime;
            float elapsedTime = 0f;

            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeTime;

                musicSource.volume = Mathf.Lerp(0f, layer.volume * musicVolume, progress);
                yield return null;
            }

            currentMusicTrack = layer.layerName;
            currentMusicState = MusicState.Playing;
            
            OnMusicTrackChanged?.Invoke(currentMusicTrack);

            if (debugMode)
            {
                Debug.Log($"Playing music track: {layer.layerName}");
            }
        }

        /// <summary>
        /// Stop music with fade out
        /// </summary>
        public void StopMusic(float fadeTime = -1f)
        {
            if (fadeTime < 0) fadeTime = musicCrossFadeTime;

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            musicFadeCoroutine = StartCoroutine(FadeOutMusic(fadeTime));
        }

        private IEnumerator FadeOutMusic(float fadeTime)
        {
            currentMusicState = MusicState.Fading;
            
            float startVolume = musicSource.volume;
            float elapsedTime = 0f;

            while (elapsedTime < fadeTime && musicSource.volume > 0f)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeTime;

                musicSource.volume = Mathf.Lerp(startVolume, 0f, progress);
                yield return null;
            }

            musicSource.Stop();
            currentMusicState = MusicState.Stopped;
            currentMusicTrack = null;

            if (debugMode)
            {
                Debug.Log("Music stopped");
            }
        }

        /// <summary>
        /// Pause/Resume music
        /// </summary>
        public void PauseMusic()
        {
            if (currentMusicState == MusicState.Playing)
            {
                musicSource.Pause();
                currentMusicState = MusicState.Paused;
            }
        }

        public void ResumeMusic()
        {
            if (currentMusicState == MusicState.Paused)
            {
                musicSource.UnPause();
                currentMusicState = MusicState.Playing;
            }
        }

        #endregion

        #region Adaptive Music System

        private void UpdateAdaptiveMusic()
        {
            if (!enableAdaptiveMusic || currentMusicState != MusicState.Playing) return;

            // Update adaptive parameters based on game state
            UpdateAdaptiveParameters();

            // Apply adaptive effects to music
            ApplyAdaptiveEffects();
        }

        private void UpdateAdaptiveParameters()
        {
            // Get game state from various systems
            var levelManager = LevelManager.Instance;
            if (levelManager != null)
            {
                var progress = levelManager.GetProgress();
                var currentSection = levelManager.GetCurrentSection();

                // Adjust intensity based on level section
                switch (currentSection)
                {
                    case LevelManager.LevelSection.Start:
                        adaptiveMusicParameters["intensity"] = 0.3f;
                        break;
                    case LevelManager.LevelSection.Mechanic:
                        adaptiveMusicParameters["intensity"] = 0.5f;
                        break;
                    case LevelManager.LevelSection.Combination:
                        adaptiveMusicParameters["intensity"] = 0.7f;
                        break;
                    case LevelManager.LevelSection.Final:
                        adaptiveMusicParameters["intensity"] = 0.9f;
                        break;
                }

                // Adjust exploration parameter
                adaptiveMusicParameters["exploration"] = Mathf.Clamp01(1f - (progress.completionPercentage / 100f));
            }

            // Detect combat situations (placeholder - would integrate with combat system)
            bool inCombat = DetectCombatSituation();
            adaptiveMusicParameters["combat"] = inCombat ? 1f : 0f;
            adaptiveMusicParameters["tension"] = inCombat ? 0.8f : 0.2f;
        }

        private bool DetectCombatSituation()
        {
            // This would integrate with the combat system to detect active combat
            // For now, return false as placeholder
            return false;
        }

        private void ApplyAdaptiveEffects()
        {
            if (audioMixer == null) return;

            // Apply intensity parameter to music volume and filters
            float intensity = adaptiveMusicParameters["intensity"];
            float targetVolume = Mathf.Lerp(0.6f, 1f, intensity) * musicVolume;
            musicSource.volume = Mathf.Lerp(musicSource.volume, targetVolume, Time.deltaTime * 2f);

            // Apply tension parameter to pitch and reverb
            float tension = adaptiveMusicParameters["tension"];
            float targetPitch = Mathf.Lerp(0.98f, 1.02f, tension);
            musicSource.pitch = Mathf.Lerp(musicSource.pitch, targetPitch, Time.deltaTime);

            // Apply parameters to mixer (if groups exist)
            audioMixer.SetFloat("MusicIntensity", intensity);
            audioMixer.SetFloat("MusicTension", tension);
            audioMixer.SetFloat("CombatFilter", adaptiveMusicParameters["combat"]);
        }

        #endregion

        #region 3D Spatial Audio

        private void UpdateSpatialAudio()
        {
            if (listenerTransform == null) return;

            // Update 3D audio settings
            AudioListener.dopplerLevel = dopplerLevel;
            
            // Update spatial audio sources
            for (int i = spatialAudioSources.Count - 1; i >= 0; i--)
            {
                var source = spatialAudioSources[i];
                if (source == null || !source.isPlaying)
                {
                    spatialAudioSources.RemoveAt(i);
                    continue;
                }

                // Apply distance-based volume adjustments
                UpdateSpatialAudioSource(source);
            }
        }

        private void UpdateSpatialAudioSource(AudioSource source)
        {
            if (listenerTransform == null || source == null) return;

            float distance = Vector3.Distance(source.transform.position, listenerTransform.position);
            
            // Apply custom rolloff curve if available
            var categorySettings = this.categorySettings.FirstOrDefault(c => c.is3D);
            if (categorySettings != null && categorySettings.volumeRolloff != null)
            {
                float normalizedDistance = Mathf.Clamp01(distance / source.maxDistance);
                float rolloffMultiplier = categorySettings.volumeRolloff.Evaluate(normalizedDistance);
                
                // This would be applied in conjunction with Unity's built-in 3D audio
                // For fine-tuned control over spatial audio falloff
            }
        }

        /// <summary>
        /// Play 3D positioned audio
        /// </summary>
        public AudioSource Play3DAudio(AudioClip clip, Vector3 position, AudioCategory category = AudioCategory.SFX_World,
                                      float volume = 1f, float minDistance = 1f, float maxDistance = 50f,
                                      Transform parent = null)
        {
            if (clip == null) return null;

            AudioSource source = GetPooledAudioSource(category);
            if (source == null) return null;

            // Configure for 3D audio
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.rolloffMode = rolloffMode;
            source.dopplerLevel = dopplerLevel;

            // Set position
            source.transform.position = position;
            if (parent != null)
            {
                source.transform.SetParent(parent);
            }

            source.Play();
            spatialAudioSources.Add(source);
            categoryPlayCounts[category]++;

            StartCoroutine(CleanupAudioSource(source, null, clip.length));

            return source;
        }

        #endregion

        #region Volume Controls

        /// <summary>
        /// Set master volume (0-1 range)
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            
            if (audioMixer != null)
            {
                float dbValue = masterVolume > 0 ? Mathf.Log10(masterVolume) * 20 : -80f;
                audioMixer.SetFloat("MasterVolume", dbValue);
            }

            OnCategoryVolumeChanged?.Invoke(AudioCategory.Music, masterVolume);
            
            if (debugMode)
            {
                Debug.Log($"Master volume set to: {masterVolume:F2}");
            }
        }

        /// <summary>
        /// Set music volume (0-1 range)
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            
            if (audioMixer != null)
            {
                float dbValue = musicVolume > 0 ? Mathf.Log10(musicVolume) * 20 : -80f;
                audioMixer.SetFloat("MusicVolume", dbValue);
            }

            // Update music sources
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }
            if (musicCrossFadeSource != null)
            {
                musicCrossFadeSource.volume = musicVolume;
            }

            OnCategoryVolumeChanged?.Invoke(AudioCategory.Music, musicVolume);
            
            if (debugMode)
            {
                Debug.Log($"Music volume set to: {musicVolume:F2}");
            }
        }

        /// <summary>
        /// Set SFX volume (0-1 range)
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            
            if (audioMixer != null)
            {
                float dbValue = sfxVolume > 0 ? Mathf.Log10(sfxVolume) * 20 : -80f;
                audioMixer.SetFloat("SFXVolume", dbValue);
            }

            OnCategoryVolumeChanged?.Invoke(AudioCategory.SFX_Player, sfxVolume);
            
            if (debugMode)
            {
                Debug.Log($"SFX volume set to: {sfxVolume:F2}");
            }
        }

        /// <summary>
        /// Set voice volume (0-1 range)
        /// </summary>
        public void SetVoiceVolume(float volume)
        {
            voiceVolume = Mathf.Clamp01(volume);
            
            if (audioMixer != null)
            {
                float dbValue = voiceVolume > 0 ? Mathf.Log10(voiceVolume) * 20 : -80f;
                audioMixer.SetFloat("VoiceVolume", dbValue);
            }

            OnCategoryVolumeChanged?.Invoke(AudioCategory.Voice, voiceVolume);
            
            if (debugMode)
            {
                Debug.Log($"Voice volume set to: {voiceVolume:F2}");
            }
        }

        /// <summary>
        /// Mute/unmute category
        /// </summary>
        public void SetCategoryMute(AudioCategory category, bool muted)
        {
            var settings = categorySettings.FirstOrDefault(c => c.category == category);
            if (settings != null)
            {
                settings.muted = muted;
                
                // Apply to all active sources in category
                foreach (var source in activeAudioSources[category])
                {
                    source.mute = muted;
                }
            }
        }

        #endregion

        #region Audio Quality Management

        /// <summary>
        /// Set audio quality level
        /// </summary>
        public void SetAudioQuality(AudioQuality quality)
        {
            audioQuality = quality;
            ApplyAudioQualitySettings();
            OnAudioQualityChanged?.Invoke(quality);
        }

        private void ApplyAudioQualitySettings()
        {
            // Configure Unity's audio settings based on quality level
            var audioConfiguration = AudioSettings.GetConfiguration();
            
            switch (audioQuality)
            {
                case AudioQuality.Low:
                    audioConfiguration.sampleRate = 22050; // 22kHz
                    AudioSettings.Reset(audioConfiguration);
                    break;
                    
                case AudioQuality.Medium:
                    audioConfiguration.sampleRate = 44100; // 44.1kHz
                    AudioSettings.Reset(audioConfiguration);
                    break;
                    
                case AudioQuality.High:
                    audioConfiguration.sampleRate = 44100; // 44.1kHz
                    AudioSettings.Reset(audioConfiguration);
                    break;
            }

            if (debugMode)
            {
                Debug.Log($"Audio quality set to: {audioQuality} ({audioConfiguration.sampleRate}Hz)");
            }
        }

        #endregion

        #region Cleanup and Performance

        private void CleanupFinishedAudioSources()
        {
            foreach (var categoryList in activeAudioSources.Values)
            {
                for (int i = categoryList.Count - 1; i >= 0; i--)
                {
                    var source = categoryList[i];
                    if (source == null || (!source.isPlaying && !source.gameObject.activeInHierarchy))
                    {
                        categoryList.RemoveAt(i);
                        totalAudioSourcesUsed = Mathf.Max(0, totalAudioSourcesUsed - 1);
                    }
                }
            }

            // Cleanup spatial audio list
            spatialAudioSources.RemoveAll(source => source == null || !source.isPlaying);
        }

        /// <summary>
        /// Stop all audio in category
        /// </summary>
        public void StopAllAudio(AudioCategory category)
        {
            foreach (var source in activeAudioSources[category])
            {
                if (source != null)
                {
                    source.Stop();
                    ReturnAudioSourceToPool(source, category);
                }
            }
            activeAudioSources[category].Clear();
        }

        /// <summary>
        /// Stop all audio
        /// </summary>
        public void StopAllAudio()
        {
            foreach (AudioCategory category in System.Enum.GetValues(typeof(AudioCategory)))
            {
                StopAllAudio(category);
            }
            
            // Stop music
            StopMusic(0.5f);
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// Get current music state
        /// </summary>
        public MusicState GetMusicState()
        {
            return currentMusicState;
        }

        /// <summary>
        /// Get current music track
        /// </summary>
        public string GetCurrentMusicTrack()
        {
            return currentMusicTrack;
        }

        /// <summary>
        /// Get volume for category
        /// </summary>
        public float GetCategoryVolume(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Music => musicVolume,
                AudioCategory.SFX_Player => sfxVolume,
                AudioCategory.SFX_World => sfxVolume,
                AudioCategory.SFX_UI => sfxVolume,
                AudioCategory.Voice => voiceVolume,
                _ => 1f
            };
        }

        /// <summary>
        /// Get audio performance stats
        /// </summary>
        public Dictionary<string, object> GetPerformanceStats()
        {
            return new Dictionary<string, object>
            {
                ["TotalActiveSources"] = totalAudioSourcesUsed,
                ["MaxSources"] = maxAudioSources,
                ["PoolSize"] = poolSize,
                ["SpatialSources"] = spatialAudioSources.Count,
                ["CategoryPlayCounts"] = new Dictionary<AudioCategory, int>(categoryPlayCounts),
                ["AudioMemoryUsage"] = audioMemoryUsage,
                ["AudioQuality"] = audioQuality.ToString()
            };
        }

        /// <summary>
        /// Set adaptive music parameter
        /// </summary>
        public void SetAdaptiveMusicParameter(string parameterName, float value)
        {
            adaptiveMusicParameters[parameterName] = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Get adaptive music parameter
        /// </summary>
        public float GetAdaptiveMusicParameter(string parameterName)
        {
            return adaptiveMusicParameters.TryGetValue(parameterName, out float value) ? value : 0f;
        }

        #endregion

        #region Save/Load Settings

        /// <summary>
        /// Save audio settings to PlayerPrefs
        /// </summary>
        public void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("Audio_MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("Audio_MusicVolume", musicVolume);
            PlayerPrefs.SetFloat("Audio_SFXVolume", sfxVolume);
            PlayerPrefs.SetFloat("Audio_VoiceVolume", voiceVolume);
            PlayerPrefs.SetString("Audio_Quality", audioQuality.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load audio settings from PlayerPrefs
        /// </summary>
        public void LoadAudioSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("Audio_MasterVolume", masterVolume);
            musicVolume = PlayerPrefs.GetFloat("Audio_MusicVolume", musicVolume);
            sfxVolume = PlayerPrefs.GetFloat("Audio_SFXVolume", sfxVolume);
            voiceVolume = PlayerPrefs.GetFloat("Audio_VoiceVolume", voiceVolume);
            
            string qualityString = PlayerPrefs.GetString("Audio_Quality", audioQuality.ToString());
            if (System.Enum.TryParse(qualityString, out AudioQuality loadedQuality))
            {
                audioQuality = loadedQuality;
            }

            // Apply loaded settings
            SetMasterVolume(masterVolume);
            SetMusicVolume(musicVolume);
            SetSFXVolume(sfxVolume);
            SetVoiceVolume(voiceVolume);
            SetAudioQuality(audioQuality);
        }

        #endregion

        #region Debug

        private void UpdateDebugInfo()
        {
            // Calculate memory usage estimate
            audioMemoryUsage = totalAudioSourcesUsed * 0.1f; // Rough estimate
        }

        private void DrawAudioGizmos()
        {
            // Draw spatial audio sources
            Gizmos.color = Color.cyan;
            foreach (var source in spatialAudioSources)
            {
                if (source != null)
                {
                    Gizmos.DrawWireSphere(source.transform.position, source.minDistance);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireSphere(source.transform.position, source.maxDistance);
                    Gizmos.color = Color.cyan;
                }
            }

            // Draw listener position
            if (listenerTransform != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(listenerTransform.position, Vector3.one * 2f);
            }
        }

        private void OnGUI()
        {
            if (debugMode)
            {
                GUILayout.BeginArea(new Rect(580, 10, 300, 400));
                
                GUILayout.Label("=== AUDIO MANAGER DEBUG ===");
                GUILayout.Label($"Music: {currentMusicTrack ?? "None"} ({currentMusicState})");
                GUILayout.Label($"Active Sources: {totalAudioSourcesUsed}/{maxAudioSources}");
                GUILayout.Label($"Spatial Sources: {spatialAudioSources.Count}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== VOLUMES ===");
                GUILayout.Label($"Master: {masterVolume:F2}");
                GUILayout.Label($"Music: {musicVolume:F2}");
                GUILayout.Label($"SFX: {sfxVolume:F2}");
                GUILayout.Label($"Voice: {voiceVolume:F2}");
                
                GUILayout.Space(5);
                GUILayout.Label("=== ADAPTIVE MUSIC ===");
                foreach (var param in adaptiveMusicParameters)
                {
                    GUILayout.Label($"{param.Key}: {param.Value:F2}");
                }
                
                GUILayout.Space(5);
                GUILayout.Label("=== CATEGORY PLAY COUNTS ===");
                foreach (var count in categoryPlayCounts)
                {
                    GUILayout.Label($"{count.Key}: {count.Value}");
                }
                
                GUILayout.Space(5);
                GUILayout.Label("=== PERFORMANCE ===");
                GUILayout.Label($"Memory: {audioMemoryUsage:F1}MB (est)");
                GUILayout.Label($"Quality: {audioQuality}");
                
                if (GUILayout.Button("Stop All Audio"))
                {
                    StopAllAudio();
                }
                
                if (GUILayout.Button("Save Settings"))
                {
                    SaveAudioSettings();
                }
                
                GUILayout.EndArea();
            }
        }

        #endregion
    }
}
