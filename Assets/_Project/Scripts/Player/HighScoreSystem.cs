using UnityEngine;

public class HighScoreSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("UI")]
    [SerializeField] private bool showScoreOnGui = true;


    [SerializeField] private Vector2 baseGuiPosition = new Vector2(20f, 20f);
    [SerializeField] private Vector2 baseGuiSize = new Vector2(420f, 180f);
    [SerializeField] private int baseFontSize = 40;

    private const float REFERENCE_WIDTH = 1920f;
    private const float REFERENCE_HEIGHT = 1080f;

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
    }

    private void Update()
    {
        if (player == null)
            return;

        elapsedTime += Time.deltaTime;

        float z = player.position.z;

        if (z > highestZ)
            highestZ = z;
    }

    private void OnGUI()
    {
        if (!showScoreOnGui)
            return;

        float widthScale = Screen.width / REFERENCE_WIDTH;
        float heightScale = Screen.height / REFERENCE_HEIGHT;

        float scale = Mathf.Min(widthScale, heightScale);

        Rect scaledRect = new Rect(
            baseGuiPosition.x * widthScale,
            baseGuiPosition.y * heightScale,
            baseGuiSize.x * widthScale,
            baseGuiSize.y * heightScale
        );

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = Mathf.RoundToInt(baseFontSize * scale);
        style.alignment = TextAnchor.UpperLeft;

        GUI.Box(
            scaledRect,
            $"Score: {Score}\n" +
            $"Best Score: {bestScoreThisSession}\n" +
            $"Coins: {coins}\n" +
            $"Time: {FormatTime(elapsedTime)}",
            style
        );
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        int ms = Mathf.FloorToInt((seconds * 100f) % 100f);

        return $"{m:00}:{s:00}.{ms:00}";
    }

    public void ResetScore()
    {
        if (player == null)
            return;

        startZ = highestZ = player.position.z;
        elapsedTime = 0f;
        bonusScore = 0;
        coins = 0;
    }

    public void AddPoints(int points)
    {
        bonusScore += Mathf.Max(0, points);
    }

    public void AddCoin(int amount)
    {
        coins += Mathf.Max(0, amount);
    }

    public bool TrySetNewBestScore()
    {
        if (Score > bestScoreThisSession)
        {
            bestScoreThisSession = Score;
            return true;
        }

        return false;
    }
}