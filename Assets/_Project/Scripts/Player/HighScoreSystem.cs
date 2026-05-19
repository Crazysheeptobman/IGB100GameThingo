using TMPro;
using UnityEngine;

public class HighScoreSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("UI Text")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text combinedStatsText;

    [Header("UI Formats")]
    [SerializeField] private string scoreFormat = "Score: {0}";
    [SerializeField] private string bestScoreFormat = "Best Score: {0}";
    [SerializeField] private string coinsFormat = "Coins: {0}";
    [SerializeField] private string timeFormat = "Time: {0}";
    [SerializeField] private string combinedStatsFormat =
        "Score: {0}\nBest Score: {1}\nCoins: {2}\nTime: {3}";

    private float startZ;
    private float highestZ;
    private float elapsedTime;

    private int bonusScore;
    private int coins;
    private static int bestScoreThisSession = 0;

    public float HighestZ => highestZ;
    public int Coins => coins;
    public static int BestScoreThisSession => bestScoreThisSession;

    public int Score =>
        Mathf.Max(0, Mathf.FloorToInt(highestZ - startZ)) + bonusScore;

    private void Awake()
    {
        if (player == null)
            player = transform;

        startZ = highestZ = player.position.z;
        RefreshUi();
    }

    private void Update()
    {
        if (player == null)
            return;

        elapsedTime += Time.deltaTime;

        float z = player.position.z;

        if (z > highestZ)
            highestZ = z;

        RefreshUi();
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        int ms = Mathf.FloorToInt((seconds * 100f) % 100f);

        return $"{m:00}:{s:00}.{ms:00}";
    }

    private void RefreshUi()
    {
        string formattedTime = FormatTime(elapsedTime);

        SetText(scoreText, scoreFormat, Score);
        SetText(bestScoreText, bestScoreFormat, bestScoreThisSession);
        SetText(coinsText, coinsFormat, coins);
        SetText(timeText, timeFormat, formattedTime);

        if (combinedStatsText != null)
        {
            combinedStatsText.text = string.Format(
                combinedStatsFormat,
                Score,
                bestScoreThisSession,
                coins,
                formattedTime
            );
        }
    }

    private static void SetText(TMP_Text text, string format, object value)
    {
        if (text == null)
            return;

        text.text = string.Format(format, value);
    }

    public void ResetScore()
    {
        if (player == null)
            return;

        startZ = highestZ = player.position.z;
        elapsedTime = 0f;
        bonusScore = 0;
        coins = 0;
        RefreshUi();
    }

    public void AddPoints(int points)
    {
        bonusScore += Mathf.Max(0, points);
        RefreshUi();
    }

    public void AddCoin(int amount)
    {
        coins += Mathf.Max(0, amount);
        RefreshUi();
    }

    public void SetUiVisible(bool visible)
    {
        SetTextVisible(scoreText, visible);
        SetTextVisible(bestScoreText, visible);
        SetTextVisible(coinsText, visible);
        SetTextVisible(timeText, visible);
        SetTextVisible(combinedStatsText, visible);
    }

    private static void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text != null)
            text.gameObject.SetActive(visible);
    }

    public bool TrySetNewBestScore()
    {
        if (Score > bestScoreThisSession)
        {
            bestScoreThisSession = Score;
            RefreshUi();
            return true;
        }

        return false;
    }
}