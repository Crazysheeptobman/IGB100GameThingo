using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject settingsPanelRoot;

    [Header("Sound Settings")]
    [SerializeField] private Slider masterVolumeSlider;

    [Header("Resolution Settings")]
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private List<Resolution> availableResolutions = new List<Resolution>();

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

    public void OnResolutionChanged()
    {
        if (resolutionDropdown != null && availableResolutions.Count > resolutionDropdown.value)
        {
            Resolution selectedResolution = availableResolutions[resolutionDropdown.value];
            bool fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;
            Screen.SetResolution(selectedResolution.width, selectedResolution.height, fullscreen);
        }
    }

    public void OnFullscreenChanged()
    {
        if (fullscreenToggle == null) return;

        Screen.fullScreenMode = fullscreenToggle.isOn 
            ? FullScreenMode.FullScreenWindow 
            : FullScreenMode.Windowed;

        if (resolutionDropdown != null &&
            availableResolutions.Count > resolutionDropdown.value)
        {
            Resolution selectedResolution = availableResolutions[resolutionDropdown.value];

            Screen.SetResolution(
                selectedResolution.width,
                selectedResolution.height,
                fullscreenToggle.isOn
            );
        }
    }

        private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        availableResolutions.Clear();
        resolutionDropdown.ClearOptions();

        Resolution[] resolutions = Screen.resolutions;
        List<string> options = new List<string>();
        HashSet<string> addedResolutions = new HashSet<string>();

        int currentResolutionIndex = 0;

        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution res = resolutions[i];
            string option = res.width + " x " + res.height;

            if (addedResolutions.Contains(option))
                continue;

            addedResolutions.Add(option);
            options.Add(option);
            availableResolutions.Add(res);

            if (res.width == Screen.currentResolution.width &&
                res.height == Screen.currentResolution.height)
            {
                currentResolutionIndex = availableResolutions.Count - 1;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void SaveSoundSettings()
    {
        if (masterVolumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        PlayerPrefs.Save();
    }

    private void LoadSoundSettings()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = savedVolume;
        }
        AudioListener.volume = savedVolume;
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
        int savedResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
        int savedFullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0);

        if (resolutionDropdown != null)
            resolutionDropdown.value = Mathf.Clamp(savedResolutionIndex,
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