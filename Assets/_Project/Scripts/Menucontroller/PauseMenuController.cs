using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public GameObject pauseMenuPanel;
    public DetachedFpsLook fpsLook; // drag your camera controller here in inspector

    private bool isPaused = false;

    void Start()
    {
        if (fpsLook == null)
        {
            fpsLook = FindObjectOfType<DetachedFpsLook>();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;

        if (fpsLook != null)
            fpsLook.IsPaused = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;

        if (fpsLook != null)
            fpsLook.IsPaused = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenSettings()
    {
        Time.timeScale = 1f;
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        SceneManager.LoadScene("SettingPanel");
    }
}