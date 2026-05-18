using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private SettingsManager settingsManager;
    [SerializeField] private HighScoreSystem highScoreSystem;
    [SerializeField] private GameObject highScoreUiRoot;
    [SerializeField] private GameObject[] additionalGameplayUiRoots;
    [SerializeField] private bool hideGameplayUiWhilePaused;

    public GameObject pauseMenuPanel;
    public DetachedFpsLook fpsLook;

    public static bool IsGameplayInputBlocked { get; private set; }

    private bool isPaused = false;
    private bool settingsOpenedFromPause;
    private SettingsManager subscribedSettingsManager;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToSettingsManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromSettingsManager();

        if (isPaused)
            IsGameplayInputBlocked = false;
    }

    private void Start()
    {
        SetPauseMenuVisible(false);
        SetGameplayUiVisible(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused && IsSettingsOpen())
            {
                CloseSettingsAndReturnToPauseMenu();
            }
            else if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        IsGameplayInputBlocked = true;

        if (fpsLook != null)
            fpsLook.IsPaused = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SetPauseMenuVisible(!IsSettingsOpen());
        SetGameplayUiVisible(!hideGameplayUiWhilePaused);
    }

    public void OpenSettings()
    {
        if (!isPaused)
            PauseGame();

        settingsOpenedFromPause = true;
        SetPauseMenuVisible(false);
        SetGameplayUiVisible(false);

        if (settingsManager != null)
            settingsManager.OpenSettings();
    }

    public void ResumeGame()
    {
        settingsOpenedFromPause = false;

        if (settingsManager != null && settingsManager.IsOpen)
            settingsManager.CloseSettings();

        SetPauseMenuVisible(false);
        SetGameplayUiVisible(true);

        Time.timeScale = 1f;
        isPaused = false;
        IsGameplayInputBlocked = false;

        if (fpsLook != null)
            fpsLook.IsPaused = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        IsGameplayInputBlocked = false;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        IsGameplayInputBlocked = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CloseSettingsAndReturnToPauseMenu()
    {
        settingsOpenedFromPause = false;

        if (settingsManager != null)
            settingsManager.CloseSettings();

        ShowPauseMenuAfterSettings();
    }

    private void HandleSettingsClosed()
    {
        if (!isPaused || !settingsOpenedFromPause)
            return;

        settingsOpenedFromPause = false;
        ShowPauseMenuAfterSettings();
    }

    private void ShowPauseMenuAfterSettings()
    {
        if (!isPaused)
            return;

        SetPauseMenuVisible(true);
        SetGameplayUiVisible(!hideGameplayUiWhilePaused);
        IsGameplayInputBlocked = true;
    }

    private bool IsSettingsOpen()
    {
        return settingsManager != null && settingsManager.IsOpen;
    }

    private void SetPauseMenuVisible(bool visible)
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(visible);
    }

    private void SetGameplayUiVisible(bool visible)
    {
        if (highScoreSystem != null)
            highScoreSystem.SetUiVisible(visible);

        SetRootVisible(highScoreUiRoot, visible);

        if (additionalGameplayUiRoots == null)
            return;

        for (int i = 0; i < additionalGameplayUiRoots.Length; i++)
            SetRootVisible(additionalGameplayUiRoots[i], visible);
    }

    private static void SetRootVisible(GameObject root, bool visible)
    {
        if (root != null)
            root.SetActive(visible);
    }

    private void ResolveReferences()
    {
        if (settingsManager == null)
            settingsManager = FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);

        if (highScoreSystem == null)
            highScoreSystem = FindFirstObjectByType<HighScoreSystem>(FindObjectsInactive.Include);

        if (fpsLook == null)
            fpsLook = FindFirstObjectByType<DetachedFpsLook>(FindObjectsInactive.Include);
    }

    private void SubscribeToSettingsManager()
    {
        if (settingsManager == null || subscribedSettingsManager == settingsManager)
            return;

        UnsubscribeFromSettingsManager();
        settingsManager.SettingsClosed += HandleSettingsClosed;
        subscribedSettingsManager = settingsManager;
    }

    private void UnsubscribeFromSettingsManager()
    {
        if (subscribedSettingsManager == null)
            return;

        subscribedSettingsManager.SettingsClosed -= HandleSettingsClosed;
        subscribedSettingsManager = null;
    }
}
