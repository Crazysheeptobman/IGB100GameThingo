using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class DeathScreenController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deathScreenPanel;
    
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
        // Singleton pattern to ensure only one death screen exists
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        
        // Make sure death screen is hidden at start
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }
        
        // Set up canvas group if we want fade animation
        if (useFadeIn && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
    
    /// <summary>
    /// Call this method when the player dies
    /// </summary>
    public static void ShowDeathScreen()
    {
        Debug.Log("ShowDeathScreen called. Instance = " + instance);

        if (instance != null && !instance.isShowingDeathScreen)
        {
            instance.StartCoroutine(instance.DeathSequence());
        }
    }
    
    private IEnumerator DeathSequence()
    {
        isShowingDeathScreen = true;
        
        // Pause the game
        if (pauseGameOnDeath)
        {
            Time.timeScale = 0f;
        }
        
        // Show the death screen panel
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(true);
        }
        
        // Fade in animation (if enabled)
        if (useFadeIn && canvasGroup != null)
        {
            yield return StartCoroutine(FadeIn());
        }
        
        // Wait for the display duration (using unscaled time since game is paused)
        yield return new WaitForSecondsRealtime(displayDuration);
        
        // Hide the death screen
        if (deathScreenPanel != null)
        {
            deathScreenPanel.SetActive(false);
        }
        
        // Unpause the game
        if (pauseGameOnDeath)
        {
            Time.timeScale = 1f;
        }
        
        // Restart the scene
        RestartScene();
        
        isShowingDeathScreen = false;
    }
    
    private IEnumerator FadeIn()
    {
        if (canvasGroup == null)
        {
            yield break;
        }
        
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
        // Make sure we unpause the game if this object is destroyed
        if (instance == this && pauseGameOnDeath)
        {
            Time.timeScale = 1f;
        }
    }
}