using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class DeathScreenController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deathScreenPanel;
    [SerializeField] private TextMeshProUGUI respawnCountdownText;
    [SerializeField] private TextMeshProUGUI deathMessageText;

    [Header("Settings")]
    [SerializeField, Min(0f)] private float displayDuration = 2f;
    [SerializeField] private bool pauseGameOnDeath = true;

    [Header("Fade")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool useFadeIn = true;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.3f;

    private static DeathScreenController instance;
    private bool isShowingDeathScreen;

    public static bool IsDead => instance != null && instance.isShowingDeathScreen;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(false);

        if (useFadeIn && canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (respawnCountdownText != null)
            respawnCountdownText.gameObject.SetActive(false);
    }

    public static void ShowDeathScreen()
    {
        if (instance != null && !instance.isShowingDeathScreen)
            instance.StartCoroutine(instance.DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        isShowingDeathScreen = true;

        HighScoreSystem scoreSystem = FindFirstObjectByType<HighScoreSystem>();

        bool gotNewHighScore = false;

        if (scoreSystem != null)
        {
            gotNewHighScore = scoreSystem.TrySetNewBestScore();
        }

        if (deathMessageText != null)
        {
            deathMessageText.text = gotNewHighScore
                ? "Well done! You got a new high score!"
                : "Good try!";
        }

        if (pauseGameOnDeath)
            Time.timeScale = 0f;

        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(true);

        if (useFadeIn && canvasGroup != null)
            yield return StartCoroutine(FadeIn());

        float countdownDuration = displayDuration - (useFadeIn ? fadeInDuration : 0f);

        if (respawnCountdownText != null)
        {
            respawnCountdownText.gameObject.SetActive(true);
            yield return StartCoroutine(RunCountdown(countdownDuration));
            respawnCountdownText.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSecondsRealtime(countdownDuration);
        }

        if (deathScreenPanel != null)
            deathScreenPanel.SetActive(false);

        if (pauseGameOnDeath)
            Time.timeScale = 1f;

        RestartScene();

        isShowingDeathScreen = false;
    }

    private IEnumerator RunCountdown(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float remaining = duration - elapsed;
            int displaySeconds = Mathf.FloorToInt(remaining);

            respawnCountdownText.text = $"Respawning in {displaySeconds}";

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        respawnCountdownText.text = "Respawning in 0";
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null)
            yield break;

        float elapsed = 0f;
        canvasGroup.alpha = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private void RestartScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private void OnDestroy()
    {
        if (instance == this && pauseGameOnDeath)
            Time.timeScale = 1f;
    }
}