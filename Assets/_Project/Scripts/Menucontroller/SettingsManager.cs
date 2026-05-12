using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{
    [Header("Sound Settings")]
    [SerializeField] private Slider masterVolumeSlider;

    [Header("Resolution Settings")]
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    private string previousSceneName;
    private List<Resolution> availableResolutions = new List<Resolution>();

    private void Start()
    {
        // Store the scene we came from
        previousSceneName = PlayerPrefs.GetString("PreviousScene", "MainMenu");

        PopulateResolutionDropdown();
        LoadSoundSettings();
        LoadResolutionSettings();

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(delegate { OnResolutionChanged(); });
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.AddListener(delegate { OnFullscreenChanged(); });
        }
    }

    public void BackToPreviousScene()
    {
        SaveSoundSettings();
        SaveResolutionSettings();
        SceneManager.LoadScene(previousSceneName);
    }

    public void OnMasterVolumeChanged()
    {
        if (masterVolumeSlider != null)
        {
            AudioListener.volume = masterVolumeSlider.value;
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
        if (fullscreenToggle != null && resolutionDropdown != null && availableResolutions.Count > resolutionDropdown.value)
        {
            bool fullscreen = fullscreenToggle.isOn;
            Resolution selectedResolution = availableResolutions[resolutionDropdown.value];
            Screen.SetResolution(selectedResolution.width, selectedResolution.height, fullscreen);
        }
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null)
        {
            return;
        }

        availableResolutions.Clear();
        resolutionDropdown.ClearOptions();

        Resolution[] resolutions = Screen.resolutions;
        List<string> options = new List<string>();

        int currentResolutionIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution res = resolutions[i];
            string option = res.width + " x " + res.height;
            options.Add(option);
            availableResolutions.Add(res);

            if (res.width == Screen.currentResolution.width && res.height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
    }

    private void SaveSoundSettings()
    {
        if (masterVolumeSlider != null)
        {
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        }
        PlayerPrefs.Save();
    }

    private void LoadSoundSettings()
    {
        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = savedVolume;
            AudioListener.volume = savedVolume;
        }
    }

    private void SaveResolutionSettings()
    {
        if (resolutionDropdown != null)
        {
            PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown.value);
        }
        if (fullscreenToggle != null)
        {
            PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    private void LoadResolutionSettings()
    {
        int savedResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 0);
        int savedFullscreen = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0);

        if (resolutionDropdown != null)
        {
            resolutionDropdown.value = Mathf.Clamp(savedResolutionIndex, 0, availableResolutions.Count - 1);
        }
        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = savedFullscreen == 1;
        }
        OnResolutionChanged();
    }

    private void OnDestroy()
    {
        SaveSoundSettings();
        SaveResolutionSettings();
    }
}