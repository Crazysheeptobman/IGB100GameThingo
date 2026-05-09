using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string NewSceneName = "TutorialScreen";

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
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        SceneManager.LoadScene("SettingPanel");
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}