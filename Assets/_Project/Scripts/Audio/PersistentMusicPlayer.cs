using UnityEngine;
using UnityEngine.SceneManagement; // NEW

[DisallowMultipleComponent]
public class PersistentMusicPlayer : MonoBehaviour
{
    private static PersistentMusicPlayer instance;

    [SerializeField] private AudioClip musicClip;
    [SerializeField, Tooltip("Optional Resources path used if no music clip is assigned, for example SFX/Music.")]
    private string resourcesFallbackPath = "SFX/Music";
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
    [SerializeField] private bool playOnAwake = true;
    [SerializeField] private bool switchToDifferentSceneClip = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu"; 
    [SerializeField] private bool destroyOnMainMenu = true; 

    private AudioSource musicSource;

    private void Awake()
    {
        ResolveFallbackClip();

        if (instance != null && instance != this)
        {
            instance.ApplyDuplicateSettings(this);
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSource();

        if (playOnAwake)
        {
            PlayMusic();
        }

        // NEW: Subscribe to scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // NEW: Clean up event subscription
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // NEW: Handle scene changes
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainMenuSceneName)
        {
            if (destroyOnMainMenu)
            {
                if (musicSource != null)
                {
                    musicSource.Stop();
                }
                Destroy(gameObject);
            }
            else
            {
                if (musicSource != null && musicSource.isPlaying)
                {
                    musicSource.Stop();
                }
            }
        }
    }

    private void OnValidate()
    {
        if (musicSource != null)
        {
            musicSource.volume = volume;
        }
    }

    public void PlayMusic()
    {
        ResolveFallbackClip();

        if (musicClip == null)
        {
            return;
        }

        EnsureAudioSource();

        if (musicSource.clip == musicClip && musicSource.isPlaying)
        {
            musicSource.volume = volume;
            return;
        }

        musicSource.clip = musicClip;
        musicSource.volume = volume;
        musicSource.loop = true;
        musicSource.pitch = 1f;
        musicSource.Play();
    }

    private void ApplyDuplicateSettings(PersistentMusicPlayer duplicate)
    {
        if (duplicate == null)
        {
            return;
        }

        volume = duplicate.volume;
        EnsureAudioSource();
        musicSource.volume = volume;

        bool hasDifferentClip = duplicate.musicClip != null && duplicate.musicClip != musicClip;
        if (hasDifferentClip && switchToDifferentSceneClip)
        {
            musicClip = duplicate.musicClip;
            PlayMusic();
        }
        else if (musicClip == null && duplicate.musicClip != null)
        {
            musicClip = duplicate.musicClip;
            PlayMusic();
        }
        else if (playOnAwake && musicClip != null && !musicSource.isPlaying)
        {
            PlayMusic();
        }
    }

    private void EnsureAudioSource()
    {
        if (musicSource == null)
        {
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.dopplerLevel = 0f;
        musicSource.volume = volume;
    }

    private void ResolveFallbackClip()
    {
        if (musicClip != null || string.IsNullOrEmpty(resourcesFallbackPath))
        {
            return;
        }

        musicClip = Resources.Load<AudioClip>(resourcesFallbackPath);
    }
}