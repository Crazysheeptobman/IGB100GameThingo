using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string NewSceneName = "TutorialScreen";
    [SerializeField] private SettingsManager settingsManager;

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }
        public void OpenTutorial()
    {
        SceneManager.LoadScene(NewSceneName);
    }

    public void OpenSettings()
    {
        settingsManager.OpenSettings();
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}