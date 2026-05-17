using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject settingsPanelRoot;

    [Header("Sound Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Resolution Settings")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private List<Resolution> availableResolutions = new List<Resolution>();
    private const string SfxVolumePrefKey = "SfxVolume";

    private void Awake()
    {
        // Start hidden
        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(false);
    }

    private void Start()
    {
        PopulateResolutionDropdown();
        LoadSoundSettings();
        LoadResolutionSettings();

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(delegate { OnResolutionChanged(); });

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(delegate { OnFullscreenChanged(); });

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(delegate { OnMasterVolumeChanged(); });

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(delegate { OnSfxVolumeChanged(); });
    }

    public void OpenSettings()
    {
        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(true);
    }

    public void CloseSettings()
    {
        SaveSoundSettings();
        SaveResolutionSettings();

        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(false);
    }

    public void OnMasterVolumeChanged()
    {
        if (masterVolumeSlider != null)
        {
            AudioListener.volume = masterVolumeSlider.value;
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        }
    }

    public void OnSfxVolumeChanged()
    {
        if (sfxVolumeSlider != null)
        {
            PlayerPrefs.SetFloat(SfxVolumePrefKey, sfxVolumeSlider.value);
        }
    }

    public void OnResolutionChanged()
    {
        if (resolutionDropdown == null || availableResolutions.Count <= resolutionDropdown.value)
            return;

        Resolution selectedResolution = availableResolutions[resolutionDropdown.value];
        bool fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;
        Screen.fullScreenMode = fullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
        Screen.SetResolution(selectedResolution.width, selectedResolution.height, fullscreen);
    }

    public void OnFullscreenChanged()
    {
        if (fullscreenToggle == null) return;

        Screen.fullScreenMode = fullscreenToggle.isOn 
            ? FullScreenMode.FullScreenWindow 
            : FullScreenMode.Windowed;

        if (resolutionDropdown == null || availableResolutions.Count <= resolutionDropdown.value)
            return;

        Resolution selectedResolution = availableResolutions[resolutionDropdown.value];

        Screen.SetResolution(
            selectedResolution.width,
            selectedResolution.height,
            fullscreenToggle.isOn
        );
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        availableResolutions.Clear();
        resolutionDropdown.ClearOptions();

        Resolution[] unityResolutions = Screen.resolutions;
        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        foreach (Resolution res in unityResolutions)
        {
            if (availableResolutions.Exists(r => r.width == res.width && r.height == res.height))
                continue;

            availableResolutions.Add(res);
            options.Add(res.width + " x " + res.height);

            if (res.width == Screen.currentResolution.width &&
                res.height == Screen.currentResolution.height)
            {
                currentResolutionIndex = availableResolutions.Count - 1;
            }
        }

        if (options.Count == 0)
        {
            Resolution current = Screen.currentResolution;
            availableResolutions.Add(current);
            options.Add(current.width + " x " + current.height);
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = Mathf.Clamp(currentResolutionIndex, 0, options.Count - 1);
        resolutionDropdown.RefreshShownValue();
    }

    private void SaveSoundSettings()
    {
        if (masterVolumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);

        if (sfxVolumeSlider != null)
            PlayerPrefs.SetFloat(SfxVolumePrefKey, sfxVolumeSlider.value);

        PlayerPrefs.Save();
    }

    private void LoadSoundSettings()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 0.5f);
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = savedVolume;
        AudioListener.volume = savedVolume;

        float savedSfxVolume = PlayerPrefs.GetFloat(SfxVolumePrefKey, 1f);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = savedSfxVolume;
    }

    private void SaveResolutionSettings()
    {
        if (resolutionDropdown != null)
            PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown.value);
        if (fullscreenToggle != null)
            PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }
    private void LoadResolutionSettings()
    {
        if (!PlayerPrefs.HasKey("ResolutionIndex"))
        {
            Screen.SetResolution(1920, 1080, true);

            int index = availableResolutions.FindIndex(
                r => r.width == 1920 && r.height == 1080);
            if (index < 0) index = 1;

            if (resolutionDropdown != null)
                resolutionDropdown.value = index;
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = true;

            SaveResolutionSettings();
            return;
        }
        int savedIndex    = PlayerPrefs.GetInt("ResolutionIndex", 1);
        int savedFullscreen = PlayerPrefs.GetInt("Fullscreen", 1);

        if (resolutionDropdown != null)
            resolutionDropdown.value = Mathf.Clamp(savedIndex,
                0, availableResolutions.Count - 1);
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = savedFullscreen == 1;

        OnFullscreenChanged();
    }

    private void OnDestroy()
    {
        SaveSoundSettings();
        SaveResolutionSettings();
    }

}