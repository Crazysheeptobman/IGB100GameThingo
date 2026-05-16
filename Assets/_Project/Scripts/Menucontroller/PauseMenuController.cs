using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private SettingsManager settingsManager;
    public GameObject pauseMenuPanel;
    public DetachedFpsLook fpsLook;

    private bool isPaused = false;

    void Start()
    {
        if (fpsLook == null)
            fpsLook = FindObjectOfType<DetachedFpsLook>();
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

        // Debug: on left click, log what UI element is under the cursor
        if (Input.GetMouseButtonDown(0) && isPaused)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count == 0)
            {
                Debug.Log("PauseManager DEBUG: Click detected but hit NOTHING — " +
                          "no UI element found under cursor. Likely a canvas/EventSystem issue.");
            }
            else
            {
                Debug.Log($"PauseManager DEBUG: Click hit {results.Count} UI element(s):");
                foreach (RaycastResult result in results)
                {
                    Debug.Log($"  → '{result.gameObject.name}' " +
                              $"on Canvas sort order={result.sortingOrder}, " +
                              $"depth={result.depth}, " +
                              $"distance={result.distance:F2}");
                }
            }
        }
    }

    public void PauseGame()
    {
        Debug.Log("PauseManager: PauseGame called");
        pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;

        if (fpsLook != null)
            fpsLook.IsPaused = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OpenSettings()
    {
        Debug.Log("PauseManager: OpenSettings called");
        settingsManager.OpenSettings();
    }

    public void ResumeGame()
    {
        Debug.Log("PauseManager: ResumeGame called");
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
        Debug.Log("PauseManager: ReturnToMainMenu called");
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Debug.Log("PauseManager: QuitGame called");
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}