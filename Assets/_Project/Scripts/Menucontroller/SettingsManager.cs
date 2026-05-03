using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Sound Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    
    private string previousSceneName;

    private void Start()
    {
        // Store the scene we came from
        previousSceneName = PlayerPrefs.GetString("PreviousScene", "MainMenu");
        
        // Load saved sound settings
        LoadSoundSettings();
    }

    public void BackToPreviousScene()
    {
        // Save settings before leaving
        SaveSoundSettings();
        SceneManager.LoadScene(previousSceneName);
    }

    public void OnMasterVolumeChanged()
    {
        if (masterVolumeSlider != null)
        {
            AudioListener.volume = masterVolumeSlider.value;
        }
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

    private void OnDestroy()
    {
        SaveSoundSettings();
    }
}