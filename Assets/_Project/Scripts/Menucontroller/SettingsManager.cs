using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject settingsPanelRoot;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Sound Settings")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Mouse Settings")]
    [SerializeField] private Slider mouseSensitivitySlider;

    [Header("Display Settings")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    public static float MouseSensitivity { get; private set; } = 0.5f;
    public bool IsOpen { get; private set; }

    public event Action SettingsOpened;
    public event Action SettingsClosed;

    private List<Resolution> availableResolutions = new List<Resolution>();

    private void Awake()
    {
        SetSettingsVisible(false, notify: false);
    }

    private void Start()
    {
        PopulateResolutionDropdown();

        LoadSoundSettings();
        LoadMouseSettings();
        LoadDisplayUISettings();

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.onValueChanged.AddListener(delegate { OnMusicVolumeChanged(); });
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.onValueChanged.AddListener(delegate { OnSFXVolumeChanged(); });
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 0f;
            mouseSensitivitySlider.maxValue = 1f;
            mouseSensitivitySlider.onValueChanged.AddListener(delegate { OnMouseSensitivityChanged(); });
        }

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(delegate { OnResolutionChanged(); });

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(delegate { OnFullscreenChanged(); });
    }

    public void OpenSettings()
    {
        SetSettingsVisible(true, notify: true);
    }

    public void CloseSettings()
    {
        SetSettingsVisible(false, notify: true);
    }

    private void SetSettingsVisible(bool visible, bool notify)
    {
        if (settingsPanelRoot != null)
            settingsPanelRoot.SetActive(visible);

        if (IsOpen == visible)
            return;

        IsOpen = visible;

        if (!notify)
            return;

        if (visible)
            SettingsOpened?.Invoke();
        else
            SettingsClosed?.Invoke();
    }

    private float SliderToDecibels(float sliderValue)
    {
        sliderValue = Mathf.Clamp(sliderValue, 0.0001f, 1f);
        return Mathf.Log10(sliderValue) * 20f;
    }

    public void OnMusicVolumeChanged()
    {
        if (musicVolumeSlider == null || audioMixer == null) return;

        audioMixer.SetFloat("MusicVolume", SliderToDecibels(musicVolumeSlider.value));
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        PlayerPrefs.Save();
    }

    public void OnSFXVolumeChanged()
    {
        if (sfxVolumeSlider == null || audioMixer == null) return;

        audioMixer.SetFloat("SFXVolume", SliderToDecibels(sfxVolumeSlider.value));
        PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider.value);
        PlayerPrefs.Save();
    }

    private void LoadSoundSettings()
    {
        if (audioMixer == null) return;

        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 1f);

        if (musicVolumeSlider != null)
            musicVolumeSlider.value = savedMusic;

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = savedSFX;

        audioMixer.SetFloat("MusicVolume", SliderToDecibels(savedMusic));
        audioMixer.SetFloat("SFXVolume", SliderToDecibels(savedSFX));
    }

    public void OnMouseSensitivityChanged()
    {
        if (mouseSensitivitySlider == null) return;

        MouseSensitivity = mouseSensitivitySlider.value;
        PlayerPrefs.SetFloat("MouseSensitivity", MouseSensitivity);
        PlayerPrefs.Save();
    }

    private void LoadMouseSettings()
    {
        float savedSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 0.5f);

        MouseSensitivity = savedSensitivity;

        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.value = savedSensitivity;
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        availableResolutions.Clear();
        resolutionDropdown.ClearOptions();

        List<(int width, int height)> targetResolutions = new List<(int, int)>
        {
            (2880, 1800),
            (2560, 1440),
            (1920, 1080),
            (1280, 720)
        };

        List<string> options = new List<string>();

        foreach (var target in targetResolutions)
        {
            Resolution res = new Resolution
            {
                width = target.width,
                height = target.height
            };

            availableResolutions.Add(res);
            options.Add($"{res.width} x {res.height}");
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.RefreshShownValue();
    }

    private void LoadDisplayUISettings()
    {
        int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", 2);
        int savedFullscreen = PlayerPrefs.GetInt("Fullscreen", 1);

        if (resolutionDropdown != null)
        {
            resolutionDropdown.value = Mathf.Clamp(savedIndex, 0, availableResolutions.Count - 1);
            resolutionDropdown.RefreshShownValue();
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = savedFullscreen == 1;
        }
    }

    public void OnResolutionChanged()
    {
        if (resolutionDropdown == null) return;
        if (resolutionDropdown.value >= availableResolutions.Count) return;

        Resolution selectedResolution = availableResolutions[resolutionDropdown.value];

        bool isFullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;

        Screen.SetResolution(selectedResolution.width, selectedResolution.height, isFullscreen);

        PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown.value);
        PlayerPrefs.Save();
    }

    public void OnFullscreenChanged()
    {
        if (fullscreenToggle == null) return;

        Screen.fullScreenMode = fullscreenToggle.isOn
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();
    }
}